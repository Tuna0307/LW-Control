namespace LWBridge.Map317;

public sealed class MapExporter
{
    private readonly MapStore store;

    public MapExporter(MapStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static string DefaultCityFileName(int serverId, DateTimeOffset utcNow)
    {
        MapStore.ValidateServerId(serverId);
        DateTimeOffset value = utcNow.ToUniversalTime();
        return $"map-cities-{serverId}-{value:yyyyMMdd-HHmmss}.xlsx";
    }

    public CityExportResult ExportCitiesToPath(MapQuery query, CityExportOptions options, string path)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(query.Kind, "city", StringComparison.Ordinal))
            throw new BridgeCommandException("MAP_EXPORT_FAILED", "city export requires a City query");
        MapStore.ValidateServerId(query.ServerId);
        if (options.Headers.Count != 12 || options.Headers.Any(string.IsNullOrWhiteSpace))
            throw new BridgeCommandException("MAP_EXPORT_FAILED", "city export headers are invalid");
        if (string.IsNullOrWhiteSpace(path))
            return new CityExportResult(true, 0, null);

        var rows = new List<System.Text.Json.JsonElement>();
        for (int page = 1; page <= 1000; page++)
        {
            MapSearchResult result = store.Search(query with { Page = page, PageSize = 200 }, cityExportRows: true);
            if (result.Total > MapStore.MaxCityExportRows)
                throw new BridgeCommandException("MAP_EXPORT_FAILED", "city export exceeded the row limit");
            if (result.Rows.Count == 0) break;
            rows.AddRange(result.Rows);
            if (rows.Count >= result.Total) break;
            if (page == 1000)
                throw new BridgeCommandException("MAP_EXPORT_FAILED", "city export exceeded the row limit");
        }

        if (rows.Count > MapStore.MaxCityExportRows)
            throw new BridgeCommandException("MAP_EXPORT_FAILED", "city export exceeded the row limit");
        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using FileStream stream = File.Create(path);
            CityExportWorkbookWriter.Write(stream, rows,
                new CityExportWorkbookOptions(options.Headers,
                    string.IsNullOrWhiteSpace(options.SheetName) ? "Cities" : options.SheetName,
                    string.IsNullOrEmpty(options.YesLabel) ? "Yes" : options.YesLabel,
                    string.IsNullOrEmpty(options.NoLabel) ? "No" : options.NoLabel));
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new BridgeCommandException("MAP_EXPORT_FAILED", "city export failed", error.Message);
        }
        return new CityExportResult(false, rows.Count, Path.GetFullPath(path));
    }
}
