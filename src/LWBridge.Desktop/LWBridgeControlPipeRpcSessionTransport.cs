using System.Buffers.Binary;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace LWBridge.Desktop;

// LWB-R7-120: authenticated-session command/result transport. This layer uses
// the recovered queue/byte/time boundaries, but remains isolated from normal
// application composition until its Windows proof is complete.
internal sealed class LWBridgeControlPipeRpcSessionTransport : IAsyncDisposable
{
    private readonly LWBridgeControlPipeAcceptedSession session;
    private readonly LWBridgeControlPipeCallRegistry calls;
    private readonly Channel<OutboundFrame> outbound =
        Channel.CreateUnbounded<OutboundFrame>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
    private readonly Channel<InboundFrame> inbound =
        Channel.CreateUnbounded<InboundFrame>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });
    private readonly object outboundGate = new();
    private readonly object inboundGate = new();
    private readonly object lifecycleGate = new();
    private readonly CancellationTokenSource lifetime = new();

    private Task? runTask;
    private int outboundQueuedItems;
    private long outboundQueuedBytes;
    private int inboundQueuedItems;
    private long inboundQueuedBytes;
    private int outboundFramesWritten;
    private int inboundFramesRead;
    private int resultsCorrelated;
    private int unknownResults;
    private int heartbeatsObserved;
    private bool disposed;

    internal LWBridgeControlPipeRpcSessionTransport(
        LWBridgeControlPipeAcceptedSession session,
        LWBridgeControlPipeCallRegistry calls)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(calls);
        _ = session.Connection;
        this.session = session;
        this.calls = calls;
    }

    public int OutboundFramesWritten =>
        Volatile.Read(ref outboundFramesWritten);

    public int InboundFramesRead =>
        Volatile.Read(ref inboundFramesRead);

    public int ResultsCorrelated =>
        Volatile.Read(ref resultsCorrelated);

    public int UnknownResults =>
        Volatile.Read(ref unknownResults);

    public int HeartbeatsObserved =>
        Volatile.Read(ref heartbeatsObserved);

    internal Task StartAsync()
    {
        lock (lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return runTask ??= RunAsync(lifetime.Token);
        }
    }

    internal async Task<JsonElement?> CallLuaAsync(
        string functionName,
        JsonElement args,
        long timestamp,
        long createdAt,
        TimeSpan? resultTimeout = null,
        string? timeoutMessage = null)
    {
        Task? running;
        lock (lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            running = runTask;
        }

        if (running is null || running.IsCompleted)
        {
            throw new InvalidOperationException(
                "Authenticated bridge RPC session is not running.");
        }

        LWBridgeAuthenticatedConnection connection = session.Connection;
        LWBridgePendingCall pending = calls.BeginCall(
            connection.ProfileId,
            connection.InstanceId,
            functionName,
            args,
            createdAt,
            resultTimeout,
            timeoutMessage);

        byte[] payload = LWBridgeControlPipeProtocol.EncodeCallCommand(
            connection.ProfileId,
            connection.InstanceId,
            pending.Id,
            functionName,
            args,
            timestamp,
            createdAt);
        byte[] frame = LWBridgeControlPipeProtocol.EncodeFrame(payload);

        if (!TryReserveOutbound(frame.Length))
        {
            var error = new InvalidOperationException(
                "named pipe outbound queue or byte budget is full");
            _ = calls.TryFailPending(pending.Id, error);
            throw error;
        }

        if (!outbound.Writer.TryWrite(
                new OutboundFrame(pending.Id, frame)))
        {
            ReleaseOutbound(frame.Length);
            var error = new InvalidOperationException(
                "named pipe outbound queue is closed");
            _ = calls.TryFailPending(pending.Id, error);
            throw error;
        }

        return await pending.Completion.ConfigureAwait(false);
    }

    internal async Task StopAsync()
    {
        Task? running;
        lock (lifecycleGate)
        {
            if (disposed)
                return;
            lifetime.Cancel();
            outbound.Writer.TryComplete();
            inbound.Writer.TryComplete();
            running = runTask;
        }

        if (running is not null)
        {
            try
            {
                await running.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (lifetime.IsCancellationRequested)
            {
            }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        Stream stream = session.Stream;

        Task writer = WriterLoopAsync(stream, cancellationToken);
        Task reader = ReaderLoopAsync(stream, cancellationToken);
        Task consumer = InboundLoopAsync(cancellationToken);

        try
        {
            Task completed =
                await Task.WhenAny(writer, reader, consumer)
                    .ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
                await completed.ConfigureAwait(false);
        }
        finally
        {
            lifetime.Cancel();
            outbound.Writer.TryComplete();
            inbound.Writer.TryComplete();

            await ObserveSecondaryTerminationAsync(writer).ConfigureAwait(false);
            await ObserveSecondaryTerminationAsync(reader).ConfigureAwait(false);
            await ObserveSecondaryTerminationAsync(consumer).ConfigureAwait(false);
        }
    }

    private async Task WriterLoopAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        await foreach (
            OutboundFrame item in outbound.Reader.ReadAllAsync(
                cancellationToken).ConfigureAwait(false))
        {
            try
            {
                using var writeTimeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                writeTimeout.CancelAfter(
                    LWBridgeControlPipeTransportLimits.WriteTimeout);
                await stream.WriteAsync(
                    item.Frame,
                    writeTimeout.Token).ConfigureAwait(false);
                await stream.FlushAsync(
                    writeTimeout.Token).ConfigureAwait(false);
                Interlocked.Increment(ref outboundFramesWritten);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                _ = calls.TryFailPending(
                    item.CommandId,
                    new TimeoutException(
                        "named pipe write exceeded the recovered 10-second timeout"));
                throw;
            }
            catch (Exception error)
            {
                _ = calls.TryFailPending(item.CommandId, error);
                throw;
            }
            finally
            {
                ReleaseOutbound(item.Frame.Length);
            }
        }
    }

    private async Task ReaderLoopAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            InboundFrame? frame =
                await TryReadFrameAsync(
                    stream,
                    cancellationToken).ConfigureAwait(false);
            if (frame is null)
                return;

            Interlocked.Increment(ref inboundFramesRead);
            await ReserveInboundAsync(
                frame.FrameBytes,
                cancellationToken).ConfigureAwait(false);

            try
            {
                await inbound.Writer.WriteAsync(
                    frame,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                ReleaseInbound(frame.FrameBytes);
                throw;
            }
        }
    }

    private async Task InboundLoopAsync(
        CancellationToken cancellationToken)
    {
        await foreach (
            InboundFrame frame in inbound.Reader.ReadAllAsync(
                cancellationToken).ConfigureAwait(false))
        {
            try
            {
                LWBridgeEnvelope envelope =
                    LWBridgeControlPipeProtocol.ParseEnvelope(
                        frame.Payload);
                string type = envelope.Type;
                if (string.Equals(
                        type,
                        LWBridgeControlPipeProtocol.ResultType,
                        StringComparison.Ordinal))
                {
                    LWBridgeCallResult result =
                        LWBridgeControlPipeProtocol.ParseCallResult(
                            frame.Payload);
                    LWBridgeAuthenticatedConnection connection =
                        session.Connection;
                    if (calls.TryCompleteResult(
                            connection.ProfileId,
                            connection.InstanceId,
                            result))
                    {
                        Interlocked.Increment(ref resultsCorrelated);
                    }
                    else
                    {
                        Interlocked.Increment(ref unknownResults);
                    }
                }
                else if (string.Equals(
                             type,
                             "heartbeat",
                             StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref heartbeatsObserved);
                }
                else
                {
                    throw new InvalidDataException(
                        "LWBridge inbound authenticated message type is unsupported.");
                }
            }
            finally
            {
                ReleaseInbound(frame.FrameBytes);
            }
        }
    }

    private async Task<InboundFrame?> TryReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var idleTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        idleTimeout.CancelAfter(
            LWBridgeControlPipeTransportLimits.IdleActivityTimeout);

        byte[] prefix =
            new byte[LWBridgeControlPipeProtocol.FramePrefixLength];
        int prefixRead = 0;
        while (prefixRead < prefix.Length)
        {
            int read = await stream.ReadAsync(
                prefix.AsMemory(prefixRead),
                idleTimeout.Token).ConfigureAwait(false);
            if (read == 0)
            {
                if (prefixRead == 0)
                    return null;
                throw new EndOfStreamException(
                    "named pipe closed during frame prefix");
            }

            prefixRead += read;
        }

        uint payloadLength =
            BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        if (payloadLength is 0 or >
            LWBridgeControlPipeProtocol.MaxFramePayloadLength)
        {
            throw new InvalidDataException(
                "LWBridge control-pipe frame length is outside the recovered range.");
        }

        byte[] payload =
            new byte[checked((int)payloadLength)];
        int payloadRead = 0;
        while (payloadRead < payload.Length)
        {
            int read = await stream.ReadAsync(
                payload.AsMemory(payloadRead),
                idleTimeout.Token).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    "named pipe closed during frame payload");
            }

            payloadRead += read;
        }

        return new InboundFrame(
            payload,
            checked(
                LWBridgeControlPipeProtocol.FramePrefixLength +
                payload.Length));
    }

    private bool TryReserveOutbound(int frameBytes)
    {
        lock (outboundGate)
        {
            if (!LWBridgeControlPipeTransportLimits.CanEnqueueOutboundItem(
                    outboundQueuedItems) ||
                !LWBridgeControlPipeTransportLimits.CanReserveOutboundBytes(
                    outboundQueuedBytes,
                    frameBytes))
            {
                return false;
            }

            outboundQueuedItems++;
            outboundQueuedBytes += frameBytes;
            return true;
        }
    }

    private void ReleaseOutbound(int frameBytes)
    {
        lock (outboundGate)
        {
            outboundQueuedItems--;
            outboundQueuedBytes -= frameBytes;
        }
    }

    private async Task ReserveInboundAsync(
        int frameBytes,
        CancellationToken cancellationToken)
    {
        if (frameBytes >
            LWBridgeControlPipeTransportLimits.InboundQueueByteBudget)
        {
            throw new InvalidDataException(
                "named pipe inbound frame exceeds recovered byte budget");
        }

        using var backpressure =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        backpressure.CancelAfter(
            LWBridgeControlPipeTransportLimits
                .InboundBackpressureTimeout);

        while (true)
        {
            backpressure.Token.ThrowIfCancellationRequested();

            lock (inboundGate)
            {
                if (LWBridgeControlPipeTransportLimits.CanEnqueueInbound(
                        inboundQueuedItems,
                        inboundQueuedBytes,
                        frameBytes))
                {
                    inboundQueuedItems++;
                    inboundQueuedBytes += frameBytes;
                    return;
                }
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(1),
                backpressure.Token).ConfigureAwait(false);
        }
    }

    private void ReleaseInbound(int frameBytes)
    {
        lock (inboundGate)
        {
            inboundQueuedItems--;
            inboundQueuedBytes -= frameBytes;
        }
    }

    private static async Task ObserveSecondaryTerminationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // RunAsync awaits and propagates the first completed task before
            // entering cleanup. Remaining reader/writer/consumer termination
            // is secondary to that primary outcome and must not mask it.
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (lifecycleGate)
        {
            if (disposed)
                return;
        }

        await StopAsync().ConfigureAwait(false);
        lock (lifecycleGate)
            disposed = true;
        lifetime.Dispose();
    }

    private sealed record OutboundFrame(
        string CommandId,
        byte[] Frame);

    private sealed record InboundFrame(
        byte[] Payload,
        int FrameBytes);
}
