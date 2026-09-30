namespace LWBridge.Map317.Checks;

internal static class QueryChecks
{
    internal static void Run()
    {
        using MapStore store = MapStore.CreateInMemory();
        store.UpsertRecord(StoreChecks.Record("city", 1, "a", 100, "a", "Alice", "AAA", 10, null, null,
            "{\"ownerUid\":\"111\",\"ownerName\":\"Alice\",\"health\":50,\"protectEndTime\":4102444800000}"));
        store.UpsertRecord(StoreChecks.Record("city", 1, "b", 100, "b", "Bob", null, 20, null, null,
            "{\"ownerUid\":\"222\",\"ownerName\":\"Bob\",\"health\":100}"));
        store.SetPlayerMark(new MapPlayerMark(1, "111", "marked", 100, null, "{\"ownerUid\":\"111\"}"), true);

        MapSearchResult marked = store.Search(new MapQuery("city", 1, MarkedOnly: true));
        TestAssert.Equal(1, marked.Total, "markedOnly mismatch");
        TestAssert.Equal("111", marked.Rows[0].GetProperty("ownerUid").GetString(), "mark join identity mismatch");

        MapSearchResult alliance = store.Search(new MapQuery("city", 1, Alliance: "AAA"));
        TestAssert.Equal(1, alliance.Total, "alliance filter mismatch");
        MapSearchResult noAlliance = store.Search(new MapQuery("city", 1, WithoutAlliance: true));
        TestAssert.Equal(1, noAlliance.Total, "withoutAlliance mismatch");
        MapSearchResult literal = store.Search(new MapQuery("city", 1, Keyword: "Ali"));
        TestAssert.Equal(1, literal.Total, "keyword filter mismatch");

        MapSearchResult health = store.Search(new MapQuery("city", 1, Sorts: [new MapSort("health", "desc")]));
        TestAssert.Equal("222", health.Rows[0].GetProperty("ownerUid").GetString(), "health sort mismatch");
        MapSearchResult shield = store.Search(new MapQuery("city", 1, Sorts: [new MapSort("shield", "desc")]), 1_700_000_000_000);
        TestAssert.Equal("111", shield.Rows[0].GetProperty("ownerUid").GetString(), "shield sort mismatch");

        store.UpsertRecord(StoreChecks.Record("monster", 1, "m1", 1, "m1", "One", null, 1, null, null,
            "{\"monsterNameKey\":\"zombie\",\"id\":\"m1\"}") with { Distance = 20 });
        store.UpsertRecord(StoreChecks.Record("monster", 1, "m2", 2, "m2", "Two", null, 1, null, null,
            "{\"monsterNameKey\":\"zombie\",\"id\":\"m2\"}") with { Distance = 5 });
        MapSearchResult distance = store.Search(new MapQuery("monster", 1,
            Sorts: [new MapSort("distance", "desc")], MonsterNameKey: "zombie"));
        TestAssert.Equal("m2", distance.Rows[0].GetProperty("id").GetString(),
            "monster distance must force ascending even for desc request");

        store.InsertScanRun(new MapScanRun("active", 1, ["city"], "running", 2, 1, 0, 1000, 1000, null));
        store.StageRecord("active", StoreChecks.Record("city", 1, "stage", 1001, "stage", "Stage", null, 5, null, null,
            "{\"ownerUid\":\"stage\",\"ownerName\":\"Stage\"}"));
        TestAssert.Equal(1, store.Search(new MapQuery("city", 1, ScanRunId: "active")).Total,
            "explicit active run must query staging only");
        TestAssert.Equal(2, store.Search(new MapQuery("city", 1)).Total,
            "published search must not leak staging");

        MapOptionSet options = store.ReadOptions(1, "active");
        TestAssert.Equal(1, options.Counts["city"], "active option counts must use staging");
        TestAssert.True(store.SummaryCounts(1)["city"] == 2, "published summary count mismatch");
    }
}
