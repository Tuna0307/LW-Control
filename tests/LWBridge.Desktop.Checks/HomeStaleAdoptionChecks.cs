using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class HomeStaleAdoptionChecks
{
    private const string Profile = "stale-adoption-owner";
    private const string OldSession = "retired-session";
    private const string OldNonce = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Created = "2026-10-13T00:00:00.0000000Z";

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "home-stale-adoption-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string exe = Path.Combine(root, "Game", "LastWar.exe");
            byte[] valid = MakeRecord(exe);
            foreach (string mode in new[] { "absent", "exited", "reused" })
            {
                var api = new ProbeApi(mode);
                Require(Allowed(valid, exe, api), mode + " exact retired identity is replaceable");
                api.AssertQueryOnly();
            }
            foreach (string mode in new[] { "live", "equal-offset", "denied", "unknown-creation", "invalid-creation", "throws" })
            {
                var api = new ProbeApi(mode);
                Require(!Allowed(valid, exe, api), mode + " must retain previous identity");
                api.AssertQueryOnly();
            }
            Require(!Allowed(Encoding.UTF8.GetBytes("{}"), exe, new("absent")), "malformed record retained");
            Require(!Allowed(MakeRecord(exe, profile: "foreign"), exe, new("absent")), "foreign profile retained");
            Require(!Allowed(MakeRecord(exe + ".other"), exe, new("absent")), "different installation retained");
            Require(!OverviewStaleAdoptionOwnership.CanReplaceRetiredRecord(valid, Profile, exe, OldSession, new ProbeApi("absent")),
                "same session cannot be admitted through retired-owner exception, regardless of nonce");
            JsonObject corruptObject = JsonNode.Parse(valid)!.AsObject();
            corruptObject["encryptedPipeToken"] = Convert.ToBase64String(new byte[128]);
            byte[] corrupted = JsonSerializer.SerializeToUtf8Bytes(corruptObject);
            Require(!Allowed(corrupted, exe, new("absent")), "invalid DPAPI token retained");
            ExclusiveTransaction(root, exe, valid);
            await Lifecycle(root, "dead-record", "absent", false);
            await Lifecycle(root, "presentation-heartbeat-grace", "absent", false,
                exercisePresentationGrace: true);
            // FEATURE-RECOVERY-018 R2: a normal FormClosed-driven lifecycle
            // Close must invoke exact-owner Stop and restore the game package.
            await Lifecycle(root, "form-close-restores", "absent", false, closeAfterRunning: true);
            await Lifecycle(root, "live-record", "live", true);
            await Lifecycle(root, "denied-record", "denied", true);
            await Lifecycle(root, "malformed-record", "absent", true, malformed: true);
            await Lifecycle(root, "same-session-wrong-nonce", "absent", true, conflictingNonce: true);
            Console.WriteLine("HOME_STALE_ADOPTION_OWNERSHIP_OK actual Start/commit/rollback; exclusive stale identity retirement; game launches=0");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static byte[] MakeRecord(string exe, string profile = Profile) => OverviewAdoptionRecord.Serialize(new(
        profile, OldSession, OldNonce, 48123, exe, Created, OverviewLifecycleService.BridgeVersion,
        new string('A', 43), 1_790_000_000_000, true));

    private static bool Allowed(byte[] bytes, string exe, ProbeApi api) =>
        OverviewStaleAdoptionOwnership.CanReplaceRetiredRecord(bytes, Profile, exe, "new-session", api);

    private static void ExclusiveTransaction(string root, string exe, byte[] original)
    {
        string path = Path.Combine(root, "transaction.json");
        string replacement = Path.Combine(root, "foreign.json");
        File.WriteAllBytes(path, original);
        File.WriteAllText(replacement, "foreign successor");
        byte[] successor = MakeRecord(exe, "replacement-profile");
        // Reproduce the old production predicate: another session always fails,
        // even with a fully valid record whose old process is proven absent.
        Require(!OverviewRuntimeFileOwnership.TryWrite(path, successor, _ => false) &&
                File.ReadAllBytes(path).SequenceEqual(original), "legacy foreign-session predicate reproduces commit failure");
        bool replacementDenied = false;
        Require(OverviewRuntimeFileOwnership.TryWrite(path, successor,
            bytes => Allowed(bytes, exe, new("absent")), (_, _) =>
            {
                try { File.Move(replacement, path, overwrite: true); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // MoveFileEx replacement of an exclusively opened Windows
                    // destination may report ERROR_ACCESS_DENIED rather than a
                    // sharing IOException. Both must preserve both file bytes.
                    replacementDenied = true;
                }
            }), "retired snapshot accepted through actual exclusive write");
        Require(replacementDenied && File.ReadAllBytes(path).SequenceEqual(successor) && File.Exists(replacement),
            "locked pathname cannot be replaced by a racing foreign writer");
        foreach (string mode in new[] { "live", "denied", "unknown-creation" })
        {
            File.WriteAllBytes(path, original);
            Require(!OverviewRuntimeFileOwnership.TryWrite(path, successor, b => Allowed(b, exe, new(mode))) &&
                    File.ReadAllBytes(path).SequenceEqual(original), mode + " record remains byte-identical");
        }
    }

    private static async Task Lifecycle(string parent, string name, string processMode, bool expectRollback,
        bool malformed = false, bool conflictingNonce = false, bool closeAfterRunning = false,
        bool exercisePresentationGrace = false)
    {
        string root = Path.Combine(parent, name);
        string exe = Path.Combine(root, "Game", "LastWar.exe");
        string runtime = Path.Combine(root, "runtime");
        Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
        Directory.CreateDirectory(runtime);
        File.WriteAllBytes(exe, []);
        string adoption = Path.Combine(runtime, "adoption.json");
        byte[] oldBytes = malformed ? Encoding.UTF8.GetBytes("{malformed") : MakeRecord(exe);
        File.WriteAllBytes(adoption, oldBytes);
        string? session = null, nonce = null;
        byte[]? report = null;
        int starts = 0, stops = 0;
        bool alive = false;
        bool heartbeatReadable = true;
        bool heartbeatReady = true;
        bool heartbeatOwnerMatches = true;
        bool routeConnected = true;
        long testNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        DateTimeOffset HookNow() => exercisePresentationGrace
            ? DateTimeOffset.FromUnixTimeMilliseconds(testNow)
            : DateTimeOffset.UtcNow;
        var api = new ProbeApi(processMode);
        var hooks = new OverviewLifecycleTestHooks
        {
            UtcNow = HookNow,
            OwnedProcessApi = api,
            SelectedGamePids = _ => [],
            ProcessMatches = (pid, path, creation) => alive && pid == 48125 && path == exe && creation == Created,
            AuthenticatedRouteConnected = _ => routeConnected,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            WriteLease = (_, _, _) => { },
            ReadAllBytes = path => path.EndsWith("game-reported.txt", StringComparison.OrdinalIgnoreCase) && report is not null ? report
                : path.EndsWith("heartbeat.json", StringComparison.OrdinalIgnoreCase)
                    ? heartbeatReadable
                        ? JsonSerializer.SerializeToUtf8Bytes(new
                        {
                            schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion, profileId = Profile,
                            sessionId = heartbeatOwnerMatches ? session : "foreign-heartbeat-owner",
                            challenge = nonce, gamePid = 48125, ready = heartbeatReady,
                            messageVisible = true, messageText = OverviewLifecycleService.ReadyMessage,
                            updatedAt = HookNow().ToUnixTimeSeconds()
                        })
                        : throw new IOException("inert heartbeat publication unavailable")
                    : File.ReadAllBytes(path),
            RunHelperAsync = (inv, _) =>
            {
                if (inv.Operation == "stop")
                {
                    stops++; alive = false;
                    return Task.FromResult(JsonSerializer.SerializeToElement(new
                    {
                        mode = "overview_exact_pid_close_restore", bridgeVersion = OverviewLifecycleService.BridgeVersion,
                        profileId = Profile, sessionId = inv.SessionId, gamePid = inv.GamePid,
                        gamePath = exe, gameStartedAtUtc = Created, gameRunning = false, installedFilesChanged = false,
                        close = new { accepted = true, processExited = true, alreadyExited = false },
                        restore = new { restored = true }
                    }));
                }
                starts++; alive = true; session = inv.SessionId; nonce = inv.Challenge;
                if (conflictingNonce)
                {
                    oldBytes = OverviewAdoptionRecord.Serialize(new(Profile, session!, new string('f', 64),
                        48123, exe, Created, OverviewLifecycleService.BridgeVersion, new string('A', 43),
                        1_790_000_000_000, true));
                    File.WriteAllBytes(adoption, oldBytes);
                }
                report = Encoding.UTF8.GetBytes($"schema=1\nsessionId={session}\nchallenge={nonce}\ndeadlineMilliseconds={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 90000}\ngamePid=48125\n");
                long unix = HookNow().ToUnixTimeSeconds();
                return Task.FromResult(JsonSerializer.SerializeToElement(new
                {
                    mode = "overview_install_launch_ready_deferred_restore", bridgeVersion = OverviewLifecycleService.BridgeVersion,
                    profileId = Profile, sessionId = session,
                    challengeSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nonce!))).ToLowerInvariant(),
                    gamePid = 48125, launcherPid = 48126, gamePath = exe, gameStartedAtUtc = Created,
                    gameRunning = true, installedFilesChanged = true,
                    restore = new { restored = false, deferred = true, stage = "active_ready_deferred_restore" },
                    ready = new
                    {
                        schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion, profileId = Profile,
                        sessionId = session, challenge = nonce, gamePid = 48125, ready = true, messageVisible = true,
                        messageText = OverviewLifecycleService.ReadyMessage, readyAt = unix, updatedAt = unix
                    }
                }));
            }
        };
        using var host = new LWBridgeControlPipeHostState(pipePath: @"\\.\pipe\stale-adoption-" + Guid.NewGuid().ToString("N"));
        using var service = new OverviewLifecycleService(Profile, root,
            helperPath: Path.Combine(root, "inert-helper.py"), requireCurrentClientEvidence: false,
            testHooks: hooks, startRecoveryMonitor: false, bridgeHostState: host,
            enableBridgeControlPipeLaunchBinding: true, runtimeRoot: runtime,
            applicationDataRoot: root, evidenceRoot: Path.Combine(root, "evidence"), backupRoot: Path.Combine(root, "backups"));
        string? failure = null;
        try { await service.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }), CancellationToken.None); }
        catch (BridgeCommandException error) { failure = error.Code; }
        if (expectRollback)
            Require(failure == "RECOVERY_RECORD_COMMIT_FAILED" && starts == 1 && stops == 1 && !alive &&
                    File.ReadAllBytes(adoption).SequenceEqual(oldBytes), name + " protects old bytes and exact-rolls back new helper");
        else
        {
            Require(failure is null && starts == 1 && stops == 0 && alive && service.IsReady &&
                    OverviewAdoptionRecord.TryDeserialize(File.ReadAllBytes(adoption), out var stored) && stored is not null &&
                    stored.InstanceId == session && stored.InstanceId != OldSession, "dead record allows genuinely ready producer to persist successor");
            if (exercisePresentationGrace)
            {
                var backend = new LWBridgeBackend(
                    config: new LocalConfigStore(
                        persistent: false,
                        initialValue: new LWBridgeLocalConfig { ProfileId = Profile }),
                    overviewLifecycle: service,
                    bridgeHostState: host,
                    applicationDataRoot: root);
                async Task<JsonElement> StatusAsync() => JsonSerializer.SerializeToElement(
                    await backend.InvokeAsync(
                        "get_status",
                        JsonSerializer.SerializeToElement(new { profileId = Profile }),
                        CancellationToken.None),
                    JsonOptions.Default);

                Require(service.IsPresentationReady,
                    "fresh exact heartbeat is presentation-ready");
                JsonElement freshStatus = await StatusAsync();
                Require(freshStatus.GetProperty("xluaOnline").GetBoolean() &&
                        freshStatus.GetProperty("presentationOnline").GetBoolean(),
                    "fresh exact heartbeat is strict-command and presentation online");
                heartbeatReadable = false;
                testNow += 6_000;
                Require(!service.IsReady && service.IsPresentationReady,
                    "strict command readiness fails closed while bounded presentation grace survives heartbeat publication loss");
                JsonElement publicationGap = await StatusAsync();
                Require(!publicationGap.GetProperty("xluaOnline").GetBoolean() &&
                        publicationGap.GetProperty("presentationOnline").GetBoolean(),
                    "publication gap is presentation-only and never upgrades strict xlua command readiness");
                JsonElement profileGap = JsonSerializer.SerializeToElement(
                    service.CreateProfileInstanceStatus(), JsonOptions.Default);
                Require(profileGap.GetProperty("connectionState").GetString() == "reconnecting" &&
                        !profileGap.GetProperty("commandReady").GetBoolean() &&
                        profileGap.GetProperty("presentationReady").GetBoolean() &&
                        profileGap.GetProperty("lastHeartbeatAt").ValueKind == JsonValueKind.Number,
                    "15-second profile presentation stays corroborated while the 5-second command gate is degraded");
                try
                {
                    await backend.InvokeAsync(
                        "set_automation",
                        JsonSerializer.SerializeToElement(new
                        {
                            profileId = Profile,
                            name = "presentation-grace-probe",
                            enabled = true,
                        }),
                        CancellationToken.None);
                    throw new InvalidDataException("Presentation grace admitted a game-side command.");
                }
                catch (BridgeCommandException error) when (error.Code == "GAME_DISCONNECTED")
                {
                    // Presentation grace is intentionally display-only.
                }
                routeConnected = false;
                Require(!service.IsPresentationReady,
                    "presentation grace never hides authenticated route loss");
                routeConnected = true;
                Require(service.IsPresentationReady,
                    "presentation grace resumes only after authenticated route corroboration");
                alive = false;
                Require(!service.IsPresentationReady,
                    "presentation grace never hides exact process loss");
                alive = true;
                testNow += 10_000;
                Require(!service.IsPresentationReady,
                    "presentation grace expires after fifteen seconds without a validated heartbeat");
                heartbeatReadable = true;
                Require(service.IsReady && service.IsPresentationReady,
                    "new exact heartbeat restores strict and presentation readiness");
                heartbeatReady = false;
                Require(!service.IsReady && !service.IsPresentationReady,
                    "readable exact heartbeat that explicitly reports not-ready defeats remembered presentation grace immediately");
                heartbeatReady = true;
                Require(service.IsReady && service.IsPresentationReady,
                    "explicit ready heartbeat restores both readiness layers");

                foreach (bool observeThroughStrictGate in new[] { false, true })
                {
                    foreach (bool wrongOwner in new[] { false, true })
                    {
                        string observation = observeThroughStrictGate ? "strict gate" : "presentation gate";
                        string negative = wrongOwner ? "wrong owner" : "explicit not-ready";
                        heartbeatReady = wrongOwner;
                        heartbeatOwnerMatches = !wrongOwner;
                        Require(!(observeThroughStrictGate ? service.IsReady : service.IsPresentationReady),
                            $"{observation} rejects readable {negative} heartbeat");
                        heartbeatReadable = false;
                        Require(!service.IsReady && !service.IsPresentationReady,
                            $"{observation} {negative} evidence retires cached ready before subsequent read loss");
                        heartbeatReadable = true;
                        heartbeatReady = true;
                        heartbeatOwnerMatches = true;
                        Require(service.IsReady && service.IsPresentationReady,
                            $"fresh valid heartbeat recovers after {observation} {negative} and read loss");
                    }
                }
            }
            if (closeAfterRunning)
                service.Close();
            else
                await service.InvokeAsync("profile_instance_stop", JsonSerializer.SerializeToElement(new { }), CancellationToken.None);
            Require(stops == 1 && !alive && !File.Exists(adoption), "normal exact Stop removes only new adoption record");
        }
        api.AssertQueryOnly();
    }

    private sealed class ProbeApi(string mode) : IOwnedProcessApi
    {
        private readonly List<uint> accesses = [];
        internal void AssertQueryOnly() => Require(accesses.All(a => a ==
            (OwnedProcessTermination.ProcessQueryLimitedInformation | OwnedProcessTermination.Synchronize)), "retirement requests query/synchronize only");
        public IntPtr Open(int pid, uint access)
        {
            accesses.Add(access);
            if (mode == "throws") throw new UnauthorizedAccessException();
            return mode is "absent" or "denied" ? IntPtr.Zero : new IntPtr(1);
        }
        public bool PidExists(int pid) => mode != "absent";
        public bool WaitExited(IntPtr handle, int milliseconds) => mode == "exited";
        public string? CreationUtc(IntPtr handle) => mode switch
        {
            "reused" => "2026-10-14T00:00:00.0000000Z",
            "equal-offset" => "2026-10-13T00:00:00+00:00",
            "unknown-creation" => null,
            "invalid-creation" => "unreadable",
            _ => Created
        };
        public string? ImagePath(IntPtr handle) => throw new InvalidOperationException("retirement must not rely on image mismatch");
        public bool Terminate(IntPtr handle, uint exitCode) => throw new InvalidOperationException("retirement never terminates");
        public void Close(IntPtr handle) { }
        public int LastErrorCode(int fallback) => fallback;
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("HOME_STALE_ADOPTION: " + message);
    }
}
