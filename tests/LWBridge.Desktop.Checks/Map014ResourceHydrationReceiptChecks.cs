using System.Text.Json;
using LWBridge.Desktop;

internal static class Map014ResourceHydrationReceiptChecks
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-resource-receipts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            JsonElement first = JsonSerializer.SerializeToElement(new
            {
                requestId = "reply1", error = "native_partial", targetCount = 1, readyCount = 0,
                details = Array.Empty<object>(),
            });
            string path = CurrentClientMapBlockSource.RetainFirstResourceHydrationReply(root, "run1", "reply1", first);
            string preserved = File.ReadAllText(path);
            CurrentClientMapBlockSource.RetainFirstResourceHydrationReply(root, "run1", "reply2",
                JsonSerializer.SerializeToElement(new { requestId = "reply2", error = "state_unavailable" }));
            Require(File.ReadAllText(path) == preserved, "later state-unavailable overwrote the first native reply");

            foreach ((string run, string request, JsonElement reply) in new[]
            {
                ("../escape", "reply1", first),
                ("foreign", "replyOther", first),
                ("safe", "../request", first),
                ("bad/run", "reply1", first),
            })
            {
                try { CurrentClientMapBlockSource.RetainFirstResourceHydrationReply(root, run, request, reply); }
                catch (InvalidDataException) { continue; }
                throw new Exception("Resource receipt accepted unsafe or foreign identity.");
            }
            Require(!Directory.Exists(Path.Combine(root, "resource-hydration-receipts", "foreign")),
                "foreign reply created retained-source artifacts");

            var resource = new FirstLivePreparedResource(
                new FirstLiveResultImport(2212, "1", 1, 0, 0, 1, 1000, "controlled", "inert", null, null, "{}"),
                new MapStoredRecord("resource", 2212, "1", 1, null, null, null, 1, null, null, null, null, 1000,
                    JsonSerializer.Serialize(new
                    {
                        rebuildGatherOccupancyKnown = true, rebuildGatherOccupied = false,
                        resourceConfigId = 123, resourceNameKey = "native-name", resourceMaxAmount = 100,
                    })));
            var resources = new Dictionary<string, FirstLivePreparedResource> { ["1"] = resource };
            var requestOwner = new MapScanExecutionRequest("run1", 2212, 0, 1000, 1000, ["resource"], 8);
            var failed = CurrentClientMapBlockSource.ResourceScanDetailObservation.Failed("native_partial");
            bool rejectionRecorded = false;
            try { CurrentClientMapBlockSource.RequireFullWorldResourceHydration(root, requestOwner, resources, failed); }
            catch (MapScanSourceValidationException error)
            {
                rejectionRecorded = true;
                string retained = (string)error.Data["resourceHydrationFailureReceipt"]!;
                using JsonDocument diagnostic = JsonDocument.Parse(File.ReadAllText(retained));
                JsonElement value = diagnostic.RootElement;
                Require(value.GetProperty("nativeError").GetString() == "native_partial" &&
                    value.GetProperty("nativeTargetCount").GetInt32() == 1 &&
                    value.GetProperty("nativeReadyCount").GetInt32() == 0 &&
                    value.GetProperty("nativeDetailCount").GetInt32() == 0 &&
                    value.GetProperty("currentIdleCount").GetInt32() == 1 &&
                    value.GetProperty("rawReceiptLocator").GetString() == path,
                    "first full-world failure lost native counters/source locator");
            }
            string failurePath = Path.Combine(root, "resource-hydration-receipts", "run1", "first-failure.json");
            Require(rejectionRecorded && File.Exists(failurePath), "failed hydration did not terminalize or retain its first rejection");
            string firstFailure = File.ReadAllText(failurePath);
            try { CurrentClientMapBlockSource.RequireFullWorldResourceHydration(root, requestOwner, resources,
                    CurrentClientMapBlockSource.ResourceScanDetailObservation.Failed("second error")); }
            catch (MapScanSourceValidationException) { }
            Require(File.ReadAllText(failurePath) == firstFailure, "later failure overwrote the first exception");

            var timedOut = new TimeoutException("original correlated request timed out");
            bool timeoutRejected = false;
            try { CurrentClientMapBlockSource.RequireFullWorldResourceHydration(root,
                    requestOwner with { RunId = "timeout" }, resources,
                    CurrentClientMapBlockSource.ResourceScanDetailObservation.Failed(timedOut.Message), timedOut); }
            catch (MapScanSourceValidationException error)
            {
                timeoutRejected = true;
                Require(ReferenceEquals(error.InnerException, timedOut), "original timeout cause was replaced");
                using JsonDocument diagnostic = JsonDocument.Parse(File.ReadAllText(
                    (string)error.Data["resourceHydrationFailureReceipt"]!));
                Require(diagnostic.RootElement.GetProperty("originalMessage").GetString() == timedOut.Message &&
                    diagnostic.RootElement.GetProperty("rawReceiptLocator").ValueKind == JsonValueKind.Null,
                    "timeout fabricated a native receipt or lost its original message");
            }
            Require(timeoutRejected, "full-world hydration accepted an original timeout");

            var valid = new CurrentClientMapBlockSource.ResourceScanDetailObservation(1, 1, 0, 0, 1,
                new Dictionary<string, CurrentClientMapBlockSource.ResourceScanDetail>
                    { ["1"] = new(true, 80, 100, 123, 100, "native-name") }, null);
            CurrentClientMapBlockSource.RequireFullWorldResourceHydration(root,
                requestOwner with { RunId = "valid" }, resources, valid);
            Require(!Directory.Exists(Path.Combine(root, "resource-hydration-receipts", "valid")),
                "valid hydration was classified as a failure");
            // Actual artifact I/O failure cannot replace the native rejection.
            string blockedRoot = Path.Combine(root, "file-not-directory");
            File.WriteAllText(blockedRoot, "inert blocker");
            CurrentClientMapBlockSource.RequireFullWorldResourceHydration(blockedRoot, requestOwner, resources, valid);
            bool ioFailureRejected = false;
            try { CurrentClientMapBlockSource.RequireFullWorldResourceHydration(blockedRoot, requestOwner, resources, failed, timedOut); }
            catch (MapScanSourceValidationException error)
            {
                ioFailureRejected = true;
                Require(ReferenceEquals(error.InnerException, timedOut) && !error.Data.Contains("resourceHydrationFailureReceipt"),
                    "artifact IO replaced the native failure or invented a saved receipt");
            }
            Require(ioFailureRejected, "unwritable diagnostic root made failed hydration successful");
            string corruptFolder = Path.Combine(root, "resource-hydration-receipts", "corrupt");
            Directory.CreateDirectory(corruptFolder);
            string corrupt = Path.Combine(corruptFolder, "first-reply.json");
            File.WriteAllText(corrupt, "{truncated");
            bool corruptRejected = false;
            try { CurrentClientMapBlockSource.RequireFullWorldResourceHydration(root,
                    requestOwner with { RunId = "corrupt" }, resources, failed, timedOut); }
            catch (MapScanSourceValidationException error)
            {
                corruptRejected = true;
                Require(ReferenceEquals(error.InnerException, timedOut) && !error.Data.Contains("resourceHydrationFailureReceipt"),
                    "corrupted diagnostic replaced the original cause or fabricated a failure locator");
            }
            Require(corruptRejected && File.ReadAllText(corrupt) == "{truncated",
                "corrupted native evidence was overwritten or accepted");
            Require(!Directory.EnumerateFiles(root, ".receipt-*.tmp", SearchOption.AllDirectories).Any(),
                "completed diagnostic writes left owned temporary files");
            Console.WriteLine("MAP014_RESOURCE_HYDRATION_RECEIPTS_OK native=false firstReply=true firstFailure=true timeout=true");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new Exception("Resource hydration receipt: " + message);
    }
}
