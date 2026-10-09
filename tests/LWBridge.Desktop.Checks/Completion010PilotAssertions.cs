namespace LWBridge.Desktop.Checks;

// Shared with the live runner; this is a pure proof gate, not a producer or game stub.
internal static class Completion010PilotAssertions
{
    internal static readonly string[] CityHeaders =
    [
        "Server", "X", "Y", "Player", "UID", "UUID",
        "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At"
    ];

    internal sealed record RunProof(
        string Kind, string? DurableStatus, bool TimedOut, int Total,
        int Page1Count, int Page2Count, int SummaryCount, int MissTotal,
        int? FilteredTotal, bool FilterRequired,
        IReadOnlyList<string> Page1Keys, IReadOnlyList<string> Page2Keys,
        int ReopenedTotal, int ExportCount, int WorkbookRows,
        IReadOnlyList<string>? WorkbookHeaders, bool WorkbookContentsValid);

    internal sealed record FinalProof(
        bool PreflightRootAndBackupsVerified, bool OwnedSessionStarted,
        bool OwnedHomeStopSucceeded, bool ConfirmedExactProcessExit,
        bool OriginalHashesRestored, bool RecoveryJournalAbsent,
        bool IsolatedRootRemoved, bool NoTaskOwnedGameProcesses,
        bool AllRequiredRunsPositive, bool? RequestedPositiveStopVerified);

    internal static string TerminalClass(string? durableStatus, bool timedOut) =>
        timedOut ? "timed_out" : durableStatus switch
        {
            "completed" => "completed",
            "cancelled" => "cancelled",
            "failed" => "failed",
            _ => "incomplete"
        };

    internal static IReadOnlyList<string> RunErrors(RunProof run)
    {
        var errors = new List<string>();
        if (run.Kind is not ("city" or "resource")) errors.Add("invalid kind");
        if (TerminalClass(run.DurableStatus, run.TimedOut) != "completed")
            errors.Add("durable run is not completed");
        if (run.Total <= 0) errors.Add("positive published rows required");
        if (run.SummaryCount != run.Total) errors.Add("summary disagrees with query");
        if (run.Page1Count != Math.Min(50, Math.Max(0, run.Total)) ||
            run.Page2Count != Math.Min(50, Math.Max(0, run.Total - 50)))
            errors.Add("pagination sizes disagree with total");
        if (run.Page1Keys.Count != run.Page1Count || run.Page2Keys.Count != run.Page2Count ||
            run.Page1Keys.Concat(run.Page2Keys).Distinct(StringComparer.Ordinal).Count() !=
            run.Page1Count + run.Page2Count)
            errors.Add("missing or duplicate page identities");
        if (run.MissTotal != 0) errors.Add("negative keyword query returned rows");
        if (run.FilterRequired && (run.FilteredTotal is null || run.FilteredTotal <= 0 ||
                                   run.FilteredTotal > run.Total))
            errors.Add("positive filter witness missing or out of bounds");
        if (run.ReopenedTotal != run.Total) errors.Add("SQLite reopen count mismatch");
        if (run.Kind == "city" &&
            (run.ExportCount != run.Total || run.WorkbookRows != run.Total + 1 ||
             run.WorkbookHeaders is null ||
             !run.WorkbookHeaders.SequenceEqual(CityHeaders, StringComparer.Ordinal) ||
             !run.WorkbookContentsValid))
            errors.Add("City workbook headers, row count or contents mismatch");
        return errors;
    }

    internal static IReadOnlyList<string> FinalErrors(FinalProof state)
    {
        var errors = new List<string>();
        if (!state.PreflightRootAndBackupsVerified) errors.Add("preflight root/backup mismatch");
        if (!state.OwnedSessionStarted) errors.Add("no exact owned Home session");
        if (!state.AllRequiredRunsPositive) errors.Add("required City and Resource positive witnesses missing");
        if (!state.OwnedHomeStopSucceeded) errors.Add("exact owned Home Stop failed");
        if (!state.ConfirmedExactProcessExit) errors.Add("captured exact game process still present or unverified");
        if (!state.OriginalHashesRestored) errors.Add("installed triplet not restored");
        if (!state.RecoveryJournalAbsent) errors.Add("recovery journal still pending");
        if (!state.IsolatedRootRemoved) errors.Add("isolated root not removed");
        if (!state.NoTaskOwnedGameProcesses) errors.Add("task-owned game process remains");
        if (state.RequestedPositiveStopVerified == false) errors.Add("requested positive-stage Stop unproven");
        return errors;
    }

    internal static void RunInverseChecks()
    {
        var valid = new RunProof("city", "completed", false, 2, 2, 0, 2, 0,
            1, true, ["key1", "key2"], [], 2, 2, 3, CityHeaders, true);
        if (RunErrors(valid).Count != 0)
            throw new InvalidOperationException("valid pilot run proof unexpectedly rejected");
        var cases = new Dictionary<string, RunProof>
        {
            ["cancelled"] = valid with { DurableStatus = "cancelled" },
            ["failed"] = valid with { DurableStatus = "failed" },
            ["empty"] = valid with { Total = 0, SummaryCount = 0, Page1Count = 0, Page1Keys = [],
                ReopenedTotal = 0, ExportCount = 0, WorkbookRows = 1 },
            ["timed_out"] = valid with { TimedOut = true },
            ["wrong_summary"] = valid with { SummaryCount = 1 },
            ["duplicate_pages"] = valid with { Total = 51, SummaryCount = 51, Page1Count = 50,
                Page2Count = 1, Page1Keys = Enumerable.Repeat("duplicated", 50).ToArray(),
                Page2Keys = ["duplicated"], ReopenedTotal = 51, ExportCount = 51, WorkbookRows = 52 },
            ["bad_export"] = valid with { WorkbookHeaders = ["bad"] },
            ["bad_contents"] = valid with { WorkbookContentsValid = false },
            ["reopen_mismatch"] = valid with { ReopenedTotal = 1 },
            ["filter_missing"] = valid with { FilteredTotal = null },
            ["negative_query"] = valid with { MissTotal = 1 },
        };
        foreach (var (key, bad) in cases)
            if (RunErrors(bad).Count == 0)
                throw new InvalidOperationException("pilot inverse accepted " + key);
        var good = new FinalProof(true, true, true, true, true, true, true, true, true, null);
        if (FinalErrors(good).Count != 0) throw new InvalidOperationException("valid final proof rejected");
        var finalCases = new Dictionary<string, FinalProof>
        {
            ["preflight_mismatch"] = good with { PreflightRootAndBackupsVerified = false },
            ["failed_stop"] = good with { OwnedHomeStopSucceeded = false },
            ["failed_exit"] = good with { ConfirmedExactProcessExit = false },
            ["failed_restoration"] = good with { OriginalHashesRestored = false },
            ["journal_left"] = good with { RecoveryJournalAbsent = false },
            ["root_left"] = good with { IsolatedRootRemoved = false },
            ["no_acquisition"] = good with { AllRequiredRunsPositive = false },
            ["positive_stop_unproven"] = good with { RequestedPositiveStopVerified = false },
        };
        foreach (var (key, bad) in finalCases)
            if (FinalErrors(bad).Count == 0)
                throw new InvalidOperationException("pilot final inverse accepted " + key);
        Console.WriteLine($"COMPLETION010 pilot proof: {cases.Count + finalCases.Count + 2}/" +
            $"{cases.Count + finalCases.Count + 2} positive/inverse gates passed; real launches=0");
    }
}
