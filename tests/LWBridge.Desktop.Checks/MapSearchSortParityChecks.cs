using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapSearchSortParityChecks
{
    internal static void Run()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();

        ExpectUnrecovered(
            store,
            Normalize("monster", new[] { new { sortBy = "distance", sortOrder = "desc" } }),
            "Monster distance");
        ExpectUnrecovered(
            store,
            Normalize("city", new[] { new { sortBy = "shield", sortOrder = "asc" } }),
            "City shield");
        ExpectUnrecovered(
            store,
            Normalize("railway", new[] { new { sortBy = "quality", sortOrder = "desc" } }),
            "Railway quality");

        ExpectRecovered(
            store,
            Normalize("monster", new[]
            {
                new { sortBy = "level", sortOrder = "asc" },
                new { sortBy = "updatedAt", sortOrder = "desc" },
            }),
            "Monster recovered sorts");
        ExpectRecovered(
            store,
            Normalize("city", new[]
            {
                new { sortBy = "health", sortOrder = "desc" },
                new { sortBy = "level", sortOrder = "asc" },
                new { sortBy = "updatedAt", sortOrder = "desc" },
            }),
            "City recovered sorts");
        ExpectRecovered(
            store,
            Normalize("railway", new[]
            {
                new { sortBy = "power", sortOrder = "desc" },
                new { sortBy = "protectTime", sortOrder = "asc" },
                new { sortBy = "updatedAt", sortOrder = "desc" },
            }),
            "Railway recovered sorts");
        MapDataQueryOptions railwayItemCount = MapDataQueryContract.NormalizeSearch(
            JsonSerializer.SerializeToElement(new
            {
                kind = "railway",
                query = new
                {
                    serverId = 1,
                    itemKey = "reward:7:2270000",
                    sorts = new[]
                    {
                        new { sortBy = "itemCount", sortOrder = "desc" },
                    },
                },
            }));
        ExpectRecovered(store, railwayItemCount, "Railway itemCount");
    }

    private static MapDataQueryOptions Normalize(
        string kind,
        object sorts)
    {
        return MapDataQueryContract.NormalizeSearch(
            JsonSerializer.SerializeToElement(new
            {
                kind,
                query = new { serverId = 1, sorts },
            }));
    }
    private static void ExpectUnrecovered(
        MapDataStore store,
        MapDataQueryOptions options,
        string name)
    {
        Require(options.UnsupportedFeatures.Contains("sorts", StringComparer.Ordinal),
            $"{name} must be classified as an unrecovered sort");
        try
        {
            _ = store.SearchIndexed(options);
            throw new InvalidOperationException($"{name} unexpectedly executed");
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == "MAP_QUERY_UNRECOVERED",
                $"{name} must fail closed with MAP_QUERY_UNRECOVERED");
        }
    }

    private static void ExpectRecovered(
        MapDataStore store,
        MapDataQueryOptions options,
        string name)
    {
        Require(!options.UnsupportedFeatures.Contains("sorts", StringComparer.Ordinal),
            $"{name} must remain in the admitted evidenced-expression sort set");
        MapSearchResult result = store.SearchIndexed(options);
        Require(result.Total == 0 && result.Rows.Count == 0,
            $"{name} must execute against an empty recovered store");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
