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

    internal static JsonElement RequireCorrelatedSearchRow(
        NormalUiResourceProofExpected expected,
        NormalUiResourceProofSearchObservation observation)
    {
        if (string.IsNullOrWhiteSpace(observation.RequestId))
            throw new InvalidDataException("Normal-window proof did not capture the Resource Search native request id.");
        if (expected.ProfileId is { Length: > 0 } expectedProfile &&
            (!observation.Payload.TryGetProperty("profileId", out JsonElement profileValue) ||
             profileValue.ValueKind != JsonValueKind.String ||
             !string.Equals(profileValue.GetString(), expectedProfile, StringComparison.Ordinal)))
            throw new InvalidDataException("Normal-window Resource Search request did not match the acquisition profile identity.");
        MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(observation.Payload);
        if (query.Kind != "resource" || query.ServerId != expected.ServerId || query.Page != 1)
            throw new InvalidDataException("Normal-window proof did not observe the expected page-1 resource Search query.");

        JsonElement result = observation.Result;
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("rows", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array ||
            !result.TryGetProperty("total", out JsonElement totalValue) || !totalValue.TryGetInt32(out int total) || total < 1)
        {
            throw new InvalidDataException("Normal-window resource Search returned no correlated rows.");
        }

        JsonElement? matched = null;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (!MatchesQueryRow(row, expected)) continue;
            if (matched.HasValue)
                throw new InvalidDataException("Normal-window resource Search returned duplicate rows for the expected acquisition identity.");
            matched = row.Clone();
        }
        return matched ?? throw new InvalidDataException(
            "Normal-window resource Search did not return the exact newly acquired resource row.");
    }

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
            if (row.IsEmpty || row.Cells.Count != 5) continue;
            if (row.Cells[0] != coordinate || row.Cells[2] != level || row.Cells[4] != expectedUpdatedText) continue;
            if (string.IsNullOrWhiteSpace(row.Cells[1]) || string.IsNullOrWhiteSpace(row.Cells[3])) continue;
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

    private static bool MatchesQueryRow(JsonElement row, NormalUiResourceProofExpected expected)
    {
        if (row.ValueKind != JsonValueKind.Object) return false;
        return TryGetInt(row, "serverId") == expected.ServerId &&
            string.Equals(TryGetString(row, "recordKey"), expected.RecordKey, StringComparison.Ordinal) &&
            TryGetInt(row, "pointIndex") == expected.PointIndex &&
            TryGetInt(row, "x") == expected.X &&
            TryGetInt(row, "y") == expected.Y &&
            TryGetNullableInt(row, "level") == expected.Level &&
            TryGetLong(row, "updatedAt") == expected.UpdatedAt;
    }

    private static int? TryGetInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : null;

    private static int? TryGetNullableInt(JsonElement row, string name) =>
        !row.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null
            ? null
            : value.TryGetInt32(out int parsed) ? parsed : int.MinValue;

    private static long? TryGetLong(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed) ? parsed : null;

    private static string? TryGetString(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
