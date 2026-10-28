using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record NormalUiResourceProofExpected(
    int ServerId,
    string RecordKey,
    int PointIndex,
    int X,
    int Y,
    int? Level,
    long UpdatedAt,
    string ResultPath,
    string ResultSha256,
    string? ProbeVersion,
    string? ProfileId,
    string? LaunchSessionId,
    int? GamePid,
    string? DeclaredSourceCaptureSha256);

internal sealed record NormalUiResourceProofTableRow(
    bool IsEmpty,
    IReadOnlyList<string> Cells);

internal sealed record NormalUiResourceProofTableSnapshot(
    bool Busy,
    IReadOnlyList<NormalUiResourceProofTableRow> Rows);

internal sealed record NormalUiResourceProofSearchObservation(
    long Sequence,
    string RequestId,
    JsonElement Payload,
    JsonElement Result);

internal sealed record NormalUiResourceProofMatch(
    JsonElement QueryRow,
    string RowText,
    IReadOnlyList<string> Cells);

internal static class NormalUiResourceProofContract
{
    // PM13-02: this script only snapshots the rendered table. Correlation to a
    // specific acquisition/query is enforced in C# below, not inferred from text.
    internal const string ResourceTableSnapshotScript = """
        (() => {
          const table = document.querySelector('.map-table--resource');
          if (!table) return null;
          return {
            busy: table.getAttribute('aria-busy') === 'true',
            rows: [...table.querySelectorAll('tbody tr')].map(row => ({
              isEmpty: !!row.querySelector('td.map-empty'),
              cells: [...row.querySelectorAll('td')].map((cell, index) => (index === 0 ? (cell.querySelector('.map-coordinate-button span:not(.map-coordinate-icon)')?.textContent || cell.innerText || '') : (cell.innerText || '')).trim())
            }))
          };
        })()
        """;

    internal static NormalUiResourceProofMatch RequireRenderedRow(
        NormalUiResourceProofExpected expected,
        JsonElement queryRow,
        NormalUiResourceProofTableSnapshot snapshot,
        string expectedUpdatedText)
    {
        if (snapshot.Busy)
            throw new InvalidDataException("Normal-window resource table is still loading.");
        if (string.IsNullOrWhiteSpace(expectedUpdatedText))
            throw new InvalidDataException("Normal-window proof could not derive the expected rendered update time.");

        string coordinate = $"{expected.X},{expected.Y}";
        string level = expected.Level?.ToString(CultureInfo.InvariantCulture) ?? "-";
        NormalUiResourceProofTableRow? matched = null;
        foreach (NormalUiResourceProofTableRow row in snapshot.Rows)
        {
            // PM13 originally observed five columns. The recovered/current Resource
            // table now includes a resource-amount column before status, making the
            // settled row six columns; MAP-014 adds terrain as a seventh.
            // Accept these exact layouts and correlate
            // the timestamp from the last cell so the proof remains version-tolerant
            // without weakening coordinate/level/content checks.
            if (row.IsEmpty || row.Cells.Count is not (5 or 6 or 7)) continue;
            int updatedAtIndex = row.Cells.Count - 1;
            if (row.Cells[0] != coordinate || row.Cells[2] != level || row.Cells[updatedAtIndex] != expectedUpdatedText) continue;
            int statusIndex = row.Cells.Count == 7 ? 5 : 3;
            if (string.IsNullOrWhiteSpace(row.Cells[1]) || string.IsNullOrWhiteSpace(row.Cells[statusIndex])) continue;
            if (row.Cells.Count == 7 && (string.IsNullOrWhiteSpace(row.Cells[3]) || string.IsNullOrWhiteSpace(row.Cells[4]))) continue;
            if (row.Cells.Count == 6 && string.IsNullOrWhiteSpace(row.Cells[4])) continue;
            if (matched is not null)
                throw new InvalidDataException("Normal-window resource table rendered duplicate rows for the expected acquisition.");
            matched = row;
        }

        if (matched is null)
            throw new InvalidDataException("Normal-window resource table did not render the exact queried acquisition row.");

        return new NormalUiResourceProofMatch(
            queryRow.Clone(),
            string.Join('\t', matched.Cells),
            matched.Cells.ToArray());
    }

    private static string? TryGetString(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
