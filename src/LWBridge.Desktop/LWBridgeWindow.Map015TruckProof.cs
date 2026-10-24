using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;

namespace LWBridge.Desktop;

internal sealed partial class LWBridgeWindow
{
    private async Task<object> RequireMap015FastTruckNativeProofAsync(
        OverviewMapScanSession session, string runId, int serverId, CancellationToken token)
    {
        if (overviewLifecycleService is null ||
            !overviewLifecycleService.MatchesOwnedMapScanSession(session))
            throw new InvalidDataException("MAP015 fast Truck native proof lost its exact owned game.");
        string path = Path.Combine(overviewLifecycleService.LiveResourceRuntimeRoot, "fast-truck-scan-result.json");
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path, token));
        JsonElement receipt = document.RootElement;
        if (receipt.GetProperty("state").GetString() != "proven" ||
            receipt.GetProperty("scanRunId").GetString() != runId ||
            receipt.GetProperty("profileId").GetString() != session.ProfileId ||
            receipt.GetProperty("launchSessionId").GetString() != session.SessionId ||
            receipt.GetProperty("challenge").GetString() != session.Challenge ||
            receipt.GetProperty("gamePid").GetInt32() != session.GamePid ||
            receipt.GetProperty("serverId").GetInt32() != serverId ||
            receipt.GetProperty("worldId").GetInt32() != 0 ||
            receipt.GetProperty("cameraStable").ValueKind != JsonValueKind.True ||
            receipt.GetProperty("curViewRestored").ValueKind != JsonValueKind.True ||
            receipt.GetProperty("initialRequestsSent").GetInt32() != 625 ||
            receipt.GetProperty("requestsSent").GetInt32() < 625 ||
            receipt.GetProperty("pushesObserved").GetInt32() < 625 ||
            receipt.GetProperty("pushesObserved").GetInt32() > receipt.GetProperty("requestsSent").GetInt32() ||
            receipt.GetProperty("rectanglesCompleted").GetInt32() != 625 ||
            receipt.GetProperty("unprovenRectangles").GetInt32() != 0 ||
            receipt.GetProperty("repairRequestsSent").GetInt32() !=
                receipt.GetProperty("requestsSent").GetInt32() - 625 ||
            receipt.GetProperty("repairPasses").GetInt32() is < 0 or > 2 ||
            receipt.GetProperty("responseAttribution").GetString() != "bounded_sequential_fences" ||
            receipt.GetProperty("requestsPerSecond").GetInt32() != 20 ||
            receipt.GetProperty("responseFenceSize").GetInt32() != 25)
            throw new InvalidDataException("MAP015 packaged Truck scan did not complete 625 typed native pushes with restoration.");
        JsonElement records = receipt.GetProperty("trainRecords");
        if (records.ValueKind != JsonValueKind.Array || records.GetArrayLength() == 0 ||
            records.GetArrayLength() != receipt.GetProperty("trainRecordCount").GetInt32())
            throw new InvalidDataException("MAP015 packaged Truck native rows were empty or incomplete.");
        foreach (JsonElement row in records.EnumerateArray())
        {
            if (!row.TryGetProperty("trainType", out var type) || type.GetInt32() != 1 ||
                !row.TryGetProperty("truckMetadataKnown", out var metadata) || metadata.ValueKind != JsonValueKind.True ||
                !row.TryGetProperty("truckCurrentGoodsRaw", out var goods) || goods.ValueKind != JsonValueKind.Array ||
                !row.TryGetProperty("ownerName", out var owner) || string.IsNullOrWhiteSpace(owner.GetString()) ||
                !row.TryGetProperty("trainQuality", out var quality) || quality.GetInt32() < 1 ||
                !row.TryGetProperty("power", out var power) || !power.TryGetInt64(out _) ||
                !row.TryGetProperty("arriveTs", out var arrive) || !arrive.TryGetInt64(out long value) || value <= 0 ||
                !row.TryGetProperty("capturedAtMs", out var captured) ||
                !captured.TryGetInt64(out long capturedMs) || capturedMs <= 0 ||
                !CurrentClientMapBlockSource.FastTruckArrivalIsAfter(row, capturedMs))
                throw new InvalidDataException("MAP015 packaged fast Truck record lacks camera-parity fields or native Truck type.");
        }
        // A native sweep can be proven while its managed importer later fails,
        // leaving the scan engine to publish a *camera* fallback. Require the
        // actual SQLite Truck rows to carry the fast-producer marker, which is
        // set only after managed native admission and survives real publication.
        if (productionApplicationRoot is null)
            throw new InvalidDataException("MAP015 packaged fast Truck proof has no owned publication root.");
        string dbPath = Path.Combine(productionApplicationRoot, "profiles", session.ProfileId,
            "map-data", "map-data.db");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly,
        }.ToString()))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT data_json FROM map_records WHERE kind='truck' AND server_id=$server";
            command.Parameters.AddWithValue("$server", serverId);
            using SqliteDataReader reader = command.ExecuteReader();
            int published = 0;
            while (reader.Read())
            {
                using JsonDocument saved = JsonDocument.Parse(reader.GetString(0));
                if (!saved.RootElement.TryGetProperty("captureProtocol", out JsonElement protocol) ||
                    protocol.GetString() != "current_fast_truck_625_v1")
                    throw new InvalidDataException("MAP015 packaged Truck rows were published by camera fallback, not the verified native fast producer.");
                published++;
            }
            if (published == 0)
                throw new InvalidDataException("MAP015 packaged Truck fast-producer publication is empty.");
        }
        return new { nativePath = path, requestsPerSecond = receipt.GetProperty("requestsPerSecond").GetInt32(),
            responseFenceSize = receipt.GetProperty("responseFenceSize").GetInt32(),
            pushes = 625, repairPasses = receipt.GetProperty("repairPasses").GetInt32(),
            repairRequests = receipt.GetProperty("repairRequestsSent").GetInt32(),
            trainRecordCount = records.GetArrayLength(), cameraStable = true, curViewRestored = true };
    }

    private async Task<object> RunMap015TruckFilterUiProofAsync(
        CoreWebView2 core, string directory, JsonElement rows, int serverId, CancellationToken token)
    {
        JsonElement? witness = null;
        string[]? keys = null;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("power", out var power) || !power.TryGetInt64(out long strength) ||
                strength < 0 || strength > 25_000_000 ||
                !row.TryGetProperty("currentGoods", out var goods) || goods.ValueKind != JsonValueKind.Array)
                continue;
            string[] names = goods.EnumerateArray()
                .Where(good => good.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String)
                .Select(good => good.GetProperty("key").GetString()!)
                .Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).Take(2).ToArray();
            if (names.Length != 2) continue;
            witness = row.Clone(); keys = names; break;
        }
        if (witness is null || keys is null)
            throw new InvalidDataException("Fresh fast Truck results lack a positive two-reward power-filter witness.");
        long before = GetNormalUiProofSearchSequence();
        string keysJson = JsonSerializer.Serialize(keys);
        string clicked = await core.ExecuteScriptAsync($"(()=>{{const keys={keysJson};const buttons=[...document.querySelectorAll('.map-item-filter-menu button')];for(const key of keys){{const b=buttons.find(b=>b.dataset.mapRewardKey===key);if(!b)return false;b.click();}}const input=document.querySelector('.map-searchbar input[inputmode=decimal]');if(!input)return false;Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'25');input.dispatchEvent(new Event('input',{{bubbles:true}}));input.dispatchEvent(new Event('change',{{bubbles:true}}));return true;}})()");
        if (clicked != "true") throw new InvalidDataException("Native Truck item/power filter controls were unavailable.");
        await Task.Delay(200, token);
        if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('.map-searchbar > button');if(!b||b.disabled)return false;b.click();return true;})()") != "true")
            throw new InvalidDataException("Native Truck Search button was unavailable after filters.");
        NormalUiResourceProofSearchObservation? filtered = null;
        for (int i = 0; i < 180; i++)
        {
            token.ThrowIfCancellationRequested();
            NormalUiResourceProofSearchObservation? seen = ReadNormalUiProofSearchAfter(before);
            if (seen is not null)
            {
                MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(seen.Payload);
                if (query.Kind == "truck" && query.ServerId == serverId &&
                    seen.Payload.GetProperty("query").TryGetProperty("escortPowerMaxMillions", out var max) && max.GetDouble() == 25 &&
                    seen.Payload.GetProperty("query").TryGetProperty("itemKeys", out var selected) &&
                    selected.ValueKind == JsonValueKind.Array &&
                    selected.EnumerateArray().Select(key => key.GetString()).OrderBy(key => key).SequenceEqual(keys.OrderBy(key => key)))
                { filtered = seen; break; }
            }
            await Task.Delay(100, token);
        }
        if (filtered is null || filtered.Result.GetProperty("total").GetInt32() < 1)
            throw new InvalidDataException("Native Truck combined filters did not return a positive current-server result.");
        foreach (JsonElement row in filtered.Result.GetProperty("rows").EnumerateArray())
        {
            if (!row.TryGetProperty("power", out var power) || power.GetDouble() > 25_000_000 ||
                !row.TryGetProperty("currentGoods", out var goods) || goods.ValueKind != JsonValueKind.Array ||
                !keys.All(key => goods.EnumerateArray().Any(good => good.TryGetProperty("key", out var value) && value.GetString() == key)))
                throw new InvalidDataException("Truck filter returned a power/item mismatch.");
        }
        if (!filtered.Result.GetProperty("rows").EnumerateArray().Any(row =>
            row.GetProperty("uuid").ToString() == witness.Value.GetProperty("uuid").ToString()))
            throw new InvalidDataException("Truck item/power filter omitted its genuine positive witness.");
        string screenshot = Path.Combine(directory, "map015-truck-filtered.png");
        await using (var file = File.Create(screenshot))
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
        return new { source = "actual_native_map_search", selectedItemKeys = keys,
            powerMaxMillions = 25, filteredTotal = filtered.Result.GetProperty("total").GetInt32(), screenshot };
    }

    private async Task<object> RunMap015TruckOnlyUiProofAsync(
        CoreWebView2 core, string directory, JsonElement status,
        OverviewMapScanSession session, CancellationToken token)
    {
        string runId = ReadStatusString(status, "scanRunId") ?? "";
        int serverId = ReadStatusInt32(status, "serverId") ?? 0;
        if (runId.Length == 0 || serverId <= 0 ||
            ReadStatusInt32(status, "totalBlocks") != 2500 ||
            ReadStatusInt32(status, "readBlocks") != 2500)
            throw new InvalidDataException("Packaged Truck-only scan lacked full publication.");
        object native = await RequireMap015FastTruckNativeProofAsync(session, runId, serverId, token);
        long before = GetNormalUiProofSearchSequence();
        if (await core.ExecuteScriptAsync("(()=>{const b=document.querySelectorAll('.map-tabs button')[3];if(!b)return false;b.click();return true;})()") != "true")
            throw new InvalidDataException("Packaged Truck results tab was missing.");
        NormalUiResourceProofSearchObservation? search = null;
        for (int i = 0; i < 240; i++)
        {
            var seen = ReadNormalUiProofSearchAfter(before);
            if (seen is not null && MapDataQueryContract.NormalizeSearch(seen.Payload).Kind == "truck" &&
                MapDataQueryContract.NormalizeSearch(seen.Payload).ServerId == serverId)
            { search = seen; break; }
            await Task.Delay(100, token);
        }
        if (search is null)
        {
            before = GetNormalUiProofSearchSequence();
            await core.ExecuteScriptAsync("document.querySelector('.map-searchbar > button')?.click()");
            for (int i = 0; i < 200; i++)
            {
                var seen = ReadNormalUiProofSearchAfter(before);
                if (seen is not null && MapDataQueryContract.NormalizeSearch(seen.Payload).Kind == "truck")
                { search = seen; break; }
                await Task.Delay(100, token);
            }
        }
        if (search is null || search.Result.GetProperty("total").GetInt32() == 0)
            throw new InvalidDataException("Packaged Truck-only UI had no positive same-run native rows.");
        object filter = await RunMap015TruckFilterUiProofAsync(core, directory,
            search.Result.GetProperty("rows"), serverId, token);
        return new { runId, serverId, native, total = search.Result.GetProperty("total").GetInt32(), filter };
    }
}
