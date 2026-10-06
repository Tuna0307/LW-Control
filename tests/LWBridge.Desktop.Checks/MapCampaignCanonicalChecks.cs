using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using LWBridge.Map317;
using CanonicalMap = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Canonical deterministic Map317 acceptance for the offline Home/Map campaign.
/// This class is deliberately not a ModuleInitializer: a coordinator must dispatch
/// RunAsync explicitly under a bounded non-live flag.
/// </summary>
internal static class MapCampaignCanonicalChecks
{
    private const int ServerId = 317;
    private const long Now = 1_800_000_000_000L;
    private static readonly XNamespace SheetNs =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    internal static async Task<JsonElement> RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwb317-map-campaign-canonical-" + Guid.NewGuid().ToString("N"));
        string profileAPath = MapStore.DatabasePathForRuntimeRoot(root, "campaign-profile-a");
        string profileBPath = MapStore.DatabasePathForRuntimeRoot(root, "campaign-profile-b");
        string freshProcessPath = MapStore.DatabasePathForRuntimeRoot(root, "campaign-fresh-process");
        string clearAuthorizationPath = MapStore.DatabasePathForRuntimeRoot(root, "campaign-clear-authorization");
        string exportEdgesPath = MapStore.DatabasePathForRuntimeRoot(root, "campaign-export-edges");
        string exportPath = Path.Combine(root, "exports", "campaign-cities.xlsx");

        using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            Require(!string.Equals(profileAPath, profileBPath, StringComparison.OrdinalIgnoreCase),
                "Map317 profile database paths must be isolated");

            SeedPublishedEightKindMap(profileAPath);
            SeedPublishedEightKindMap(freshProcessPath);
            VerifyProfileIsolation(profileBPath);
            VerifyReopenedMapData(profileAPath);
            await VerifyFreshProcessContextAsync(freshProcessPath, bounded.Token).ConfigureAwait(false);
            await VerifyServiceClearAuthorizationAsync(clearAuthorizationPath, bounded.Token).ConfigureAwait(false);
            VerifyCityWorkbook(profileAPath, exportPath);
            VerifyCityWorkbookEdges(exportEdgesPath, Path.Combine(root, "export-edges"));
            PlunderAcceptance plunder = await VerifyPlunderDurabilityAsync(
                profileAPath,
                bounded.Token).ConfigureAwait(false);

            return JsonSerializer.SerializeToElement(new
            {
                ok = true,
                check = "map-campaign-canonical",
                mapData = new
                {
                    profileIsolation = true,
                    stagedEightKindPublish = true,
                    persistedReopen = true,
                    queryFilterSortPage = true,
                    options = true,
                    playerMarks = true,
                    freshProcessServerRehydration = true,
                    freshProcessClear = true,
                    destructiveClearFreshAuthorization = true,
                },
                cityExport = new
                {
                    actualXlsxPath = true,
                    zipPartsVerified = true,
                    representativeValuesVerified = true,
                    multiPage205Rows = true,
                    unicodeAndLargeUid = true,
                    missingFields = true,
                    secondServerIsolation = true,
                    emptyExport = true,
                    writeFailure = true,
                    dialogCancellationExecuted = false,
                    dialogCancellationGap = "native save-dialog cancellation is owned above Map317 MapExporter",
                },
                plunder = new
                {
                    scheduledDuplicateAdmission = plunder.ScheduledDuplicateAdmission,
                    runningDuplicateRejected = plunder.RunningDuplicateRejected,
                    durableRestart = plunder.DurableRestart,
                    restartReconciled = plunder.RestartReconciled,
                    retry = plunder.Retry,
                    cancel = plunder.Cancel,
                    clear = plunder.Clear,
                    providerMode = "inert-local",
                    externalProviderCalls = 0,
                },
                treasure = new
                {
                    cachedStateOverlay = true,
                    seasonSupplyList = true,
                    claimOrStatusExecuted = false,
                },
                blockers = new
                {
                    treasureClaimStatus = "requires the protected/current-client action provider and is not executed here",
                    ghostExecution = "Ghost plunder preparation requires IMapActionProvider.PrepareGhostPlunderTasksAsync and is not executed here",
                    nativeDialogCancel = "save-dialog cancellation belongs to the Desktop window host above Map317 exporter",
                },
            });
        }
        finally
        {
            TryDeleteFile(exportPath);
            TryDeleteDirectory(root);
        }
    }

    private static void SeedPublishedEightKindMap(string databasePath)
    {
        using var store = new MapStore(databasePath);
        const string runId = "campaign-eight-kind-publish";
        store.InsertScanRun(new MapScanRun(
            runId,
            ServerId,
            MapKinds.All,
            "running",
            TotalBlocks: 1,
            CompletedBlocks: 1,
            FailedBlocks: 0,
            CreatedAt: Now,
            UpdatedAt: Now,
            Error: null));

        foreach (MapRecord record in RepresentativeRecords())
            store.StageRecord(runId, record);

        store.CompleteScan(runId, Now + 10);
        MapScanRun? completed = store.ReadScanRun(runId);
        Require(completed is { Status: "completed" } && completed.SelectedTypes.SequenceEqual(MapKinds.All),
            "canonical all-eight scan must publish as one completed Map317 run");

        store.SetPlayerMark(
            new CanonicalMap.MapPlayerMark(
                ServerId,
                "owner-alpha",
                "active",
                Now + 20,
                null,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId,
                    ownerUid = "owner-alpha",
                    ownerName = "Alpha City",
                })),
            marked: true);

        store.UpsertTreasureClaimState(
            ServerId,
            "player-1",
            "treasure-1",
            Now + 600_000,
            Now + 30,
            JsonSerializer.Serialize(new
            {
                uuid = "treasure-1",
                claimPriority = 0,
                cachedState = "ready",
            }),
            Now);
    }

    private static void VerifyProfileIsolation(string databasePath)
    {
        using var store = new MapStore(databasePath);
        IReadOnlyDictionary<string, int> counts = store.SummaryCounts(ServerId);
        Require(counts.Count == MapKinds.All.Length && counts.Values.All(value => value == 0),
            "a second profile-owned Map317 database must start with zero rows for every kind");
        Require(store.ReadPlayerMark(ServerId, "owner-alpha") is null,
            "player marks must not leak between profile-owned Map317 databases");
    }

    private static void VerifyReopenedMapData(string databasePath)
    {
        using var store = new MapStore(databasePath);

        IReadOnlyDictionary<string, int> counts = store.SummaryCounts(ServerId);
        Require(counts.Keys.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(MapKinds.All.OrderBy(value => value, StringComparer.Ordinal)),
            "Map317 summary must expose exactly the eight canonical kinds after reopen");
        Require(counts["city"] == 2 && counts["treasure"] == 2 &&
                MapKinds.All.Where(kind => kind is not "city" and not "treasure")
                    .All(kind => counts[kind] == 1),
            "all staged representative rows must survive publish and store reopen");

        CanonicalMap.MapSearchResult cityPage1 = store.Search(new MapQuery(
            "city",
            ServerId,
            Page: 1,
            PageSize: 1,
            Sorts: [new MapSort("updatedAt", "desc")]), Now);
        CanonicalMap.MapSearchResult cityPage2 = store.Search(new MapQuery(
            "city",
            ServerId,
            Page: 2,
            PageSize: 1,
            Sorts: [new MapSort("updatedAt", "desc")]), Now);
        Require(cityPage1.Total == 2 && cityPage1.Rows.Count == 1 &&
                cityPage1.Rows[0].GetProperty("ownerName").GetString() == "Alpha City" &&
                cityPage2.Rows.Count == 1 &&
                cityPage2.Rows[0].GetProperty("ownerName").GetString() == "Beta City",
            "City query sort/page must survive Map317 store reopen");

        RequireSingle(store.Search(new MapQuery(
                "city", ServerId,
                Sorts: [new MapSort("level", "desc")],
                Keyword: "Alpha",
                Alliance: "ONE",
                MarkedOnly: true), Now),
            "ownerName", "Alpha City", "City keyword/alliance/mark filter");
        RequireSingle(store.Search(new MapQuery(
                "resource", ServerId,
                Sorts: [new MapSort("level", "desc")],
                ResourceNameKey: "resource.iron"), Now),
            "resourceNameKey", "resource.iron", "Resource name filter");
        RequireSingle(store.Search(new MapQuery(
                "monster", ServerId,
                Sorts: [new MapSort("level", "desc")],
                MonsterNameKey: "monster.doom"), Now),
            "monsterNameKey", "monster.doom", "Monster name filter");
        RequireSingle(store.Search(new MapQuery(
                "truck", ServerId,
                Sorts: [new MapSort("quality", "desc")],
                Quality: "ur",
                ItemKey: "item:iron",
                PlunderableOnly: true), Now),
            "uuid", "truck-1", "Truck quality/item/plunderable filter");
        RequireSingle(store.Search(new MapQuery(
                "railway", ServerId,
                Sorts: [new MapSort("quality", "desc")],
                Quality: "ssr",
                ItemKey: "item:food",
                PlunderableOnly: true), Now),
            "uuid", "railway-1", "Railway quality/item/plunderable filter");
        RequireSingle(store.Search(new MapQuery(
                "dispatch", ServerId,
                Sorts: [new MapSort("completionTime", "desc")],
                CompletionStatus: "completed",
                PlunderableOnly: true,
                SpecialOnly: true,
                MinLevel: 5,
                MaxLevel: 5), Now),
            "uuid", "dispatch-1", "Dispatch completion/plunder/special/level filter");
        RequireSingle(store.Search(new MapQuery(
                "ghost", ServerId,
                Sorts: [new MapSort("completionTime", "asc")],
                Quality: "ssr",
                CompletionStatus: "pending"), Now),
            "uuid", "ghost-1", "Ghost read-only query filter");

        CanonicalMap.MapSearchResult treasure = store.Search(new MapQuery(
            "treasure",
            ServerId,
            Sorts: [new MapSort("updatedAt", "desc")],
            TreasureType: 5,
            IncludeForeignRadarTreasures: false,
            LuckyFirst: true,
            ViewerUid: "player-1",
            ViewerAllianceId: "A1"), Now);
        Require(treasure.Total == 1 && treasure.Rows.Count == 1 &&
                treasure.Rows[0].GetProperty("uuid").GetString() == "treasure-1" &&
                treasure.Rows[0].GetProperty("cachedState").GetString() == "ready" &&
                treasure.Rows[0].GetProperty("claimPriority").GetInt32() == 0,
            "Treasure search must overlay persisted read-only cached state after reopen");

        IReadOnlyList<JsonElement> seasonSupplies = store.ReadSeasonSupplyTreasureRows(ServerId);
        Require(seasonSupplies.Count == 1 &&
                seasonSupplies[0].GetProperty("uuid").GetString() == "treasure-2" &&
                seasonSupplies[0].GetProperty("suppliesType").GetInt32() == 3,
            "Treasure season-supply list must remain a local read-only Map317 store operation");

        CanonicalMap.MapPlayerMark? mark = store.ReadPlayerMark(ServerId, "owner-alpha");
        Require(mark is { State: "active" } && mark.PlayerJson.Contains("Alpha City", StringComparison.Ordinal),
            "Map317 player mark must survive store reopen");

        MapOptionSet options = store.ReadOptions(ServerId, nowUnixMilliseconds: Now);
        Require(options.Counts["city"] == 2 && options.Counts["treasure"] == 2 &&
                MapKinds.All.Where(kind => kind is not "city" and not "treasure")
                    .All(kind => options.Counts[kind] == 1),
            "Map317 options must retain exact all-eight counts after reopen");
        Require(options.Alliances.Any(item => item.Name == "ONE" && item.Count == 1) &&
                options.Alliances.Any(item => item.Name == "TWO" && item.Count == 1),
            "Map317 City alliance options must survive reopen");
        Require(options.Names["resource"].Any(item => item.Key == "resource.iron" && item.Count == 1) &&
                options.Names["monster"].Any(item => item.Key == "monster.doom" && item.Count == 1),
            "Map317 Resource/Monster name options must survive reopen");
        Require(options.DispatchLevels.SequenceEqual(new[] { 5 }),
            "Map317 Dispatch level options must survive reopen");
        Require(options.RewardItems["truck"].Any(item => item.GetProperty("key").GetString() == "item:iron") &&
                options.RewardItems["railway"].Any(item => item.GetProperty("key").GetString() == "item:food"),
            "Map317 Truck/Railway reward options must survive reopen");
        Require(options.TreasureTypes.Any(item => item.GetProperty("key").GetString() == "treasure:5") &&
                options.TreasureTypes.Any(item => item.GetProperty("key").GetString() == "supplies:3"),
            "Map317 Treasure type options must survive reopen");
        Require(options.ScanProgress is { Id: "campaign-eight-kind-publish", Status: "completed" },
            "Map317 options must retain the completed canonical publish run after reopen");
    }

    private static async Task VerifyFreshProcessContextAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        var store = new MapStore(databasePath);
        var provider = new MapProviderAdapter(
            _ => ValueTask.FromResult(new MapProviderContext(
                true, true, ServerId, "live", 1, 100, 100, 1)),
            _ => ValueTask.FromResult(new MapProviderContext(
                true, true, ServerId, "live", 1, 100, 100, 1)),
            (_, _) => ValueTask.FromException<MapProviderStartResult>(
                new InvalidOperationException("fresh-process context proof must not start a scan")),
            _ => ValueTask.CompletedTask);
        using var control = new MapControlPlane(store, provider, () => Now);

        var before = control.ReadSummary();
        Require(before.ServerId == 0 && before.Counts.Values.All(value => value == 0),
            "fresh Map317 control must begin without fabricating a live server");

        MapScanState refreshed = await control.RefreshContextAsync(cancellationToken).ConfigureAwait(false);
        var reopened = control.ReadSummary();
        Require(refreshed.ServerId == ServerId && refreshed.ServerIdSource == "live" &&
                reopened.ServerId == ServerId && reopened.Counts["city"] == 2 && reopened.Counts["treasure"] == 2,
            "provider-backed context refresh must expose durable reopened rows before another scan starts");

        MapScanState cleared = await control.ClearScanAsync(ServerId, cancellationToken).ConfigureAwait(false);
        Require(cleared.ServerId == 0 &&
                control.ReadOptions(ServerId).Counts.Values.All(value => value == 0),
            "fresh-process live ownership must authorize Clear for the observed server and delete its durable rows");
    }

    private static async Task VerifyServiceClearAuthorizationAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        static MapRecord City(int serverId, string suffix) => new(
            "city", serverId, $"clear-{suffix}", 1, $"uuid-{suffix}", $"City {suffix}", "QA",
            20, null, 1_000, 1.0, null, Now,
            JsonSerializer.Serialize(new
            {
                serverId,
                recordKey = $"clear-{suffix}",
                ownerUid = $"owner-{suffix}",
                ownerName = $"City {suffix}",
                uuid = $"uuid-{suffix}",
            }));

        using (var seed = new MapStore(databasePath))
        {
            seed.UpsertRecord(City(321, "321"));
            seed.UpsertRecord(City(322, "322"));
        }

        int liveServerId = 321;
        bool providerUnavailable = false;
        ValueTask<MapProviderContext> Context(CancellationToken _)
        {
            if (providerUnavailable)
            {
                return ValueTask.FromException<MapProviderContext>(
                    new CanonicalMap.BridgeCommandException(
                        "GAME_CONNECTION_UNAVAILABLE",
                        "game connection unavailable"));
            }
            return ValueTask.FromResult(new MapProviderContext(
                true, true, liveServerId, "live", 1, 100, 100, 1));
        }

        var provider = new MapProviderAdapter(
            Context,
            Context,
            (_, _) => ValueTask.FromException<MapProviderStartResult>(
                new InvalidOperationException("clear authorization proof must not start a scan")),
            _ => ValueTask.CompletedTask);
        using var service = new Map317CommandService(
            databasePath,
            provider,
            UnavailableMapActionProvider.Instance,
            startPlunderWorkers: false);

        JsonElement Payload(int serverId) =>
            JsonSerializer.SerializeToElement(new { serverId }, JsonOptions.Default);

        _ = await service.InvokeAsync(
            "map_scan_status", Payload(321), cancellationToken).ConfigureAwait(false);
        providerUnavailable = true;
        await ExpectDesktopBridgeErrorAsync(
            () => service.InvokeAsync("map_scan_clear", Payload(321), cancellationToken),
            MapScanClearOwnership.ServerUnavailableErrorCode,
            MapScanClearOwnership.ServerUnavailableErrorMessage).ConfigureAwait(false);
        using (var verifyUnavailable = new MapStore(databasePath))
        {
            Require(verifyUnavailable.SummaryCounts(321)["city"] == 1,
                "Map317 Clear must preserve current-server rows when fresh provider authorization is unavailable");
        }

        providerUnavailable = false;
        liveServerId = 322;
        await ExpectDesktopBridgeErrorAsync(
            () => service.InvokeAsync("map_scan_clear", Payload(321), cancellationToken),
            MapScanClearOwnership.ServerUnavailableErrorCode,
            MapScanClearOwnership.ServerUnavailableErrorMessage).ConfigureAwait(false);
        using (var verifyForeign = new MapStore(databasePath))
        {
            Require(verifyForeign.SummaryCounts(321)["city"] == 1 &&
                    verifyForeign.SummaryCounts(322)["city"] == 1,
                "Map317 Clear must preserve rows when the requested server is no longer the current live server");
        }

        _ = await service.InvokeAsync(
            "map_scan_clear", Payload(322), cancellationToken).ConfigureAwait(false);
        using var verifyCurrent = new MapStore(databasePath);
        Require(verifyCurrent.SummaryCounts(321)["city"] == 1 &&
                verifyCurrent.SummaryCounts(322)["city"] == 0,
            "Map317 Clear must delete only the freshly authorized current live server");
    }

    private static void VerifyCityWorkbook(string databasePath, string exportPath)
    {
        using var store = new MapStore(databasePath);
        var exporter = new MapExporter(store);
        string[] headers =
        [
            "Server", "X", "Y", "Player", "UID", "UUID",
            "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At",
        ];
        var options = new CityExportOptions(headers, "Cities", "Yes", "No");
        var query = new MapQuery(
            "city",
            ServerId,
            Sorts: [new MapSort("updatedAt", "desc")]);

        try
        {
            CityExportResult result = exporter.ExportCitiesToPath(query, options, exportPath);
            Require(!result.Canceled && result.RowCount == 2 &&
                    string.Equals(result.Path, Path.GetFullPath(exportPath), StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(exportPath) && new FileInfo(exportPath).Length > 0,
                "canonical Map317 exporter must write a real two-row XLSX file");

            using FileStream stream = File.OpenRead(exportPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            string[] requiredParts =
            [
                "[Content_Types].xml",
                "_rels/.rels",
                "xl/workbook.xml",
                "xl/_rels/workbook.xml.rels",
                "xl/styles.xml",
                "xl/worksheets/sheet1.xml",
            ];
            Require(requiredParts.All(part => archive.GetEntry(part) is not null),
                "Map317 City workbook must contain every required XLSX part");

            XDocument sheet = LoadXml(archive, "xl/worksheets/sheet1.xml");
            XElement root = sheet.Root ?? throw new InvalidOperationException("Map317 worksheet root missing");
            Require((string?)root.Element(SheetNs + "dimension")?.Attribute("ref") == "A1:L3" &&
                    root.Descendants(SheetNs + "row").Count() == 3,
                "Map317 City workbook must contain one header plus exactly two City rows");
            Require(InlineText(Cell(root, "D2")) == "Alpha City" &&
                    InlineText(Cell(root, "E2")) == "owner-alpha" &&
                    InlineText(Cell(root, "F2")) == "city-alpha" &&
                    InlineText(Cell(root, "K2")) == "Yes",
                "Map317 City workbook must round-trip representative player/UID/UUID/mark values");
            Require(InlineText(Cell(root, "D3")) == "Beta City" &&
                    InlineText(Cell(root, "K3")) == "No",
                "Map317 City workbook must preserve the second representative City row");
        }
        finally
        {
            TryDeleteFile(exportPath);
        }
    }

    private static void VerifyCityWorkbookEdges(string databasePath, string exportRoot)
    {
        const int secondServerId = 318;
        const int emptyServerId = 319;
        Directory.CreateDirectory(exportRoot);
        using var store = new MapStore(databasePath);
        var rows = new List<MapRecord>(205);
        for (int index = 0; index < 205; index++)
        {
            string uuid = index == 1 ? "missing-uuid" : $"edge-{index:D3}";
            object data = index switch
            {
                0 => new
                {
                    serverId = ServerId, recordKey = "edge-unicode", x = 1, y = 2,
                    ownerName = "玩家🚀 Élodie", ownerUid = "900719925474099312345678901234567890",
                    uuid, allianceName = "聯盟 Ω", level = 30, health = 123456,
                    updatedAt = Now + 1_000_000,
                },
                1 => new
                {
                    serverId = ServerId, recordKey = "edge-missing", x = 3, y = 4,
                    uuid, ownerName = (string?)null, ownerUid = (string?)null,
                    allianceName = (string?)null, level = (int?)null, health = (long?)null,
                    updatedAt = Now + 999_999,
                },
                _ => new
                {
                    serverId = ServerId, recordKey = $"edge-{index:D3}", x = 100 + index, y = 200 + index,
                    ownerName = $"Edge City {index:D3}", ownerUid = $"owner-edge-{index:D3}",
                    uuid, allianceName = "EDGE", level = 20 + (index % 10), health = 1000 + index,
                    updatedAt = Now + index,
                },
            };
            rows.Add(new MapRecord(
                "city", ServerId, index == 0 ? "edge-unicode" : index == 1 ? "edge-missing" : $"edge-{index:D3}",
                index + 1, uuid, index == 1 ? null : index == 0 ? "玩家🚀 Élodie" : $"Edge City {index:D3}",
                index == 1 ? null : index == 0 ? "聯盟 Ω" : "EDGE",
                index == 1 ? null : 20 + (index % 10), null, null, index, null,
                index == 0 ? Now + 1_000_000 : index == 1 ? Now + 999_999 : Now + index,
                JsonSerializer.Serialize(data)));
        }
        PublishRows(store, "export-edge-primary", ServerId, rows);
        PublishRows(store, "export-edge-second", secondServerId,
        [
            new MapRecord(
                "city", secondServerId, "edge-unicode", 1, "second-server-uuid", "Second Server City", "S2",
                31, null, null, 1, null, Now + 2_000_000,
                JsonSerializer.Serialize(new
                {
                    serverId = secondServerId, recordKey = "edge-unicode", x = 9, y = 10,
                    ownerName = "Second Server City", ownerUid = "second-owner", uuid = "second-server-uuid",
                    allianceName = "S2", level = 31, health = 777, updatedAt = Now + 2_000_000,
                }))
        ]);

        Require(store.SummaryCounts(ServerId)["city"] == 205 && store.SummaryCounts(secondServerId)["city"] == 1,
            "canonical Map317 store must isolate conflicting City record keys by server");

        var exporter = new MapExporter(store);
        string[] headers =
        [
            "Server", "X", "Y", "Player", "UID", "UUID",
            "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At",
        ];
        var options = new CityExportOptions(headers, "Cities", "Yes", "No");
        string largePath = Path.Combine(exportRoot, "large.xlsx");
        string secondPath = Path.Combine(exportRoot, "second.xlsx");
        string emptyPath = Path.Combine(exportRoot, "empty.xlsx");
        try
        {
            CityExportResult large = exporter.ExportCitiesToPath(
                new MapQuery("city", ServerId, Sorts: [new MapSort("updatedAt", "desc")]), options, largePath);
            Require(large.RowCount == 205 && File.Exists(largePath),
                "canonical City export must traverse more than one 200-row Map317 query page");
            using (FileStream stream = File.OpenRead(largePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                XElement sheet = LoadXml(archive, "xl/worksheets/sheet1.xml").Root
                    ?? throw new InvalidOperationException("edge workbook worksheet root missing");
                Require((string?)sheet.Element(SheetNs + "dimension")?.Attribute("ref") == "A1:L206" &&
                        sheet.Descendants(SheetNs + "row").Count() == 206,
                    "205-row canonical export must produce one header plus all rows");
                Require(sheet.Descendants(SheetNs + "t").Any(value => value.Value == "玩家🚀 Élodie") &&
                        sheet.Descendants(SheetNs + "t").Any(value => value.Value == "聯盟 Ω") &&
                        sheet.Descendants(SheetNs + "t").Any(value => value.Value == "900719925474099312345678901234567890"),
                    "canonical City export must preserve Unicode and large UID text exactly");
                XElement missingRow = sheet.Descendants(SheetNs + "row")
                    .Single(row => row.Descendants(SheetNs + "t").Any(value => value.Value == "missing-uuid"));
                Require(InlineText(CellInRow(missingRow, "D")) == string.Empty &&
                        InlineText(CellInRow(missingRow, "E")) == string.Empty &&
                        InlineText(CellInRow(missingRow, "G")) == string.Empty,
                    "canonical City export must preserve missing/null text fields as blank cells");
            }

            CityExportResult second = exporter.ExportCitiesToPath(
                new MapQuery("city", secondServerId), options, secondPath);
            Require(second.RowCount == 1, "second-server City export must remain server-scoped");
            using (FileStream stream = File.OpenRead(secondPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                XElement sheet = LoadXml(archive, "xl/worksheets/sheet1.xml").Root!;
                Require(InlineText(Cell(sheet, "D2")) == "Second Server City" &&
                        CellValue(Cell(sheet, "A2")) == secondServerId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "second-server export must contain only the selected server's conflicting record");
            }

            CityExportResult empty = exporter.ExportCitiesToPath(new MapQuery("city", emptyServerId), options, emptyPath);
            Require(empty.RowCount == 0 && File.Exists(emptyPath), "empty canonical City export must still create a valid workbook");
            using (FileStream stream = File.OpenRead(emptyPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                XElement sheet = LoadXml(archive, "xl/worksheets/sheet1.xml").Root!;
                Require((string?)sheet.Element(SheetNs + "dimension")?.Attribute("ref") == "A1:L1" &&
                        sheet.Descendants(SheetNs + "row").Count() == 1,
                    "empty canonical City export must contain only the header row");
            }

            ExpectBridgeError("MAP_EXPORT_FAILED",
                () => exporter.ExportCitiesToPath(new MapQuery("city", ServerId), options, exportRoot),
                "canonical City export write failure");
        }
        finally
        {
            TryDeleteFile(largePath);
            TryDeleteFile(secondPath);
            TryDeleteFile(emptyPath);
        }
    }

    private static void PublishRows(MapStore store, string runId, int serverId, IReadOnlyList<MapRecord> rows)
    {
        store.InsertScanRun(new MapScanRun(
            runId, serverId, ["city"], "running", 1, 1, 0, Now, Now, null));
        foreach (MapRecord row in rows)
            store.StageRecord(runId, row);
        store.CompleteScan(runId, Now + 10);
    }

    private static async Task<PlunderAcceptance> VerifyPlunderDurabilityAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        long clock = Now + 100_000;
        JsonElement dispatchInitial = DispatchRow(clock + 5_000);
        JsonElement dispatchDuplicate = DispatchRow(clock + 6_000);
        JsonElement truckInitial = TruckRow(clock + 5_000);
        JsonElement truckDuplicate = TruckRow(clock + 6_000);
        string firstTruckJobId;
        string scheduledDuplicateTruckJobId;

        using (var store = new MapStore(databasePath))
        {
            var provider = new InertPlunderProvider(ServerId, armSucceeds: true);
            var actions = new MapActionControlPlane(
                store,
                provider,
                () => clock,
                protectedCallTimeout: TimeSpan.FromMilliseconds(250));
            var worker = new MapPlunderWorker(
                store,
                provider,
                () => clock,
                TimeSpan.FromMilliseconds(250));

            IReadOnlyList<JsonElement> dispatchScheduled = await actions.ScheduleDispatchPlunderAsync(
                [dispatchInitial], cancellationToken).ConfigureAwait(false);
            IReadOnlyList<CanonicalMap.TruckPlunderScheduleResult> truckScheduled = actions.ScheduleTruckPlunder([truckInitial]);
            firstTruckJobId = truckScheduled.Single().JobId;
            Require(dispatchScheduled.Count == 1 && truckScheduled.Count == 1,
                "canonical Map317 Dispatch/Truck scheduling must create one durable row per target");

            clock++;
            _ = await actions.ScheduleDispatchPlunderAsync([dispatchDuplicate], cancellationToken).ConfigureAwait(false);
            CanonicalMap.TruckPlunderScheduleResult duplicateTruck = actions.ScheduleTruckPlunder([truckDuplicate]).Single();
            scheduledDuplicateTruckJobId = duplicateTruck.JobId;
            CanonicalMap.MapPlunderJobsSnapshot duplicateSnapshot = actions.ListPlunderJobs();
            Require(duplicateSnapshot.DispatchJobs.Count == 1 && duplicateSnapshot.TruckJobs.Count == 1 &&
                    duplicateSnapshot.DispatchJobs[0].GetProperty("plunderAt").GetInt64() == clock - 1 + 6_000 &&
                    !string.Equals(firstTruckJobId, scheduledDuplicateTruckJobId, StringComparison.Ordinal) &&
                    duplicateSnapshot.TruckJobs[0].GetProperty("jobId").GetString() == scheduledDuplicateTruckJobId,
                "duplicate scheduled Map317 targets must update the single durable identity rather than duplicate rows");

            await worker.RunOnceAsync(cancellationToken).ConfigureAwait(false);
            CanonicalMap.MapPlunderJobsSnapshot running = actions.ListPlunderJobs();
            Require(Status(running.DispatchJobs.Single()) == "running" &&
                    Status(running.TruckJobs.Single()) == "running" &&
                    provider.DispatchArmCalls == 1 && provider.TruckArmCalls == 1,
                "inert provider arming must transition both due durable jobs to running without network access");

            clock++;
            await ExpectBridgeErrorAsync(
                "MAP_DATA_ERROR",
                async () =>
                {
                    _ = await actions.ScheduleDispatchPlunderAsync(
                        [DispatchRow(clock + 7_000)], cancellationToken).ConfigureAwait(false);
                },
                "running Dispatch duplicate admission").ConfigureAwait(false);
            ExpectBridgeError(
                "MAP_DATA_ERROR",
                () => _ = actions.ScheduleTruckPlunder([TruckRow(clock + 7_000)]),
                "running Truck duplicate admission");
        }

        using (var reopened = new MapStore(databasePath))
        {
            CanonicalMap.MapPlunderJobsSnapshot beforeRecover = reopened.ReadPlunderJobs();
            Require(Status(beforeRecover.DispatchJobs.Single()) == "running" &&
                    Status(beforeRecover.TruckJobs.Single()) == "running",
                "running Map317 plunder rows must survive database reopen before reconciliation");

            clock++;
            var restartProvider = new InertPlunderProvider(ServerId, armSucceeds: false);
            var restartWorker = new MapPlunderWorker(
                reopened,
                restartProvider,
                () => clock,
                TimeSpan.FromMilliseconds(250));
            restartWorker.RecoverAfterRestart();
            CanonicalMap.MapPlunderJobsSnapshot recovered = reopened.ReadPlunderJobs();
            Require(Status(recovered.DispatchJobs.Single()) == "waiting_connection" &&
                    recovered.DispatchJobs.Single().GetProperty("lastError").GetString() ==
                        "DISPATCH_PLUNDER_CLIENT_RESTARTED" &&
                    Status(recovered.TruckJobs.Single()) == "waiting_connection" &&
                    recovered.TruckJobs.Single().GetProperty("lastError").GetString() == "client restarted",
                "MapPlunderWorker restart reconciliation must durably retire orphaned running ownership");

            await restartWorker.RunOnceAsync(cancellationToken).ConfigureAwait(false);
            CanonicalMap.MapPlunderJobsSnapshot failed = reopened.ReadPlunderJobs();
            Require(Status(failed.DispatchJobs.Single()) == "failed" &&
                    failed.DispatchJobs.Single().GetProperty("lastError").GetString() == "TEST_DISPATCH_REJECTED" &&
                    failed.DispatchJobs.Single().GetProperty("attempts").GetInt32() == 2 &&
                    Status(failed.TruckJobs.Single()) == "failed" &&
                    failed.TruckJobs.Single().GetProperty("lastError").GetString() == "TEST_TRUCK_REJECTED" &&
                    failed.TruckJobs.Single().GetProperty("attempts").GetInt32() == 2,
                "inert rejected arms must persist deterministic failed states and attempt counts");

            clock++;
            var retryActions = new MapActionControlPlane(
                reopened,
                restartProvider,
                () => clock,
                protectedCallTimeout: TimeSpan.FromMilliseconds(250));
            retryActions.RetryDispatchPlunder(ServerId, "7001");
            CanonicalMap.TruckPlunderScheduleResult retriedTruck = retryActions.RetryTruckPlunder(ServerId, "8001");
            CanonicalMap.MapPlunderJobsSnapshot retried = retryActions.ListPlunderJobs();
            Require(Status(retried.DispatchJobs.Single()) == "scheduled" &&
                    retried.TruckJobs.Count == 2 &&
                    Status(retried.TruckJobs[0]) == "scheduled" &&
                    retried.TruckJobs[0].GetProperty("jobId").GetString() == retriedTruck.JobId &&
                    Status(retried.TruckJobs[1]) == "failed" &&
                    retriedTruck.ArchivedPreviousAttempt &&
                    !string.Equals(retriedTruck.JobId, scheduledDuplicateTruckJobId, StringComparison.Ordinal),
                "Map317 retry must reschedule Dispatch and archive the prior terminal Truck attempt");

            clock++;
            retryActions.CancelDispatchPlunder(ServerId, "7001");
            retryActions.CancelTruckPlunder(ServerId, "8001");
            CanonicalMap.MapPlunderJobsSnapshot cancelled = retryActions.ListPlunderJobs();
            Require(Status(cancelled.DispatchJobs.Single()) == "cancelled" &&
                    cancelled.TruckJobs.Any(row => Status(row) == "cancelled"),
                "Map317 cancel must transition the retried active durable jobs to terminal cancelled state");

            int clearedDispatch = retryActions.ClearDispatchPlunder(clock + 100, "dispatch");
            int clearedTruck = retryActions.ClearTruckPlunder(clock + 100);
            CanonicalMap.MapPlunderJobsSnapshot cleared = retryActions.ListPlunderJobs();
            Require(clearedDispatch == 1 && clearedTruck == 2 &&
                    cleared.DispatchJobs.Count == 0 && cleared.TruckJobs.Count == 0,
                "Map317 clear must remove terminal Dispatch plus current/archived Truck durable history");
        }

        return new PlunderAcceptance(
            ScheduledDuplicateAdmission: true,
            RunningDuplicateRejected: true,
            DurableRestart: true,
            RestartReconciled: true,
            Retry: true,
            Cancel: true,
            Clear: true);
    }

    private static IReadOnlyList<MapRecord> RepresentativeRecords()
    {
        return
        [
            new MapRecord(
                "city", ServerId, "city-alpha-key", 1, "city-alpha", "Alpha City", "ONE",
                30, null, 9_000_000, 1.0, Now + 60_000, Now + 2_000,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId, recordKey = "city-alpha-key", x = 101, y = 202,
                    ownerName = "Alpha City", ownerUid = "owner-alpha", uuid = "city-alpha",
                    allianceName = "ONE", level = 30, health = 987654,
                    protectEndTime = Now + 60_000, updatedAt = Now + 2_000,
                })),
            new MapRecord(
                "city", ServerId, "city-beta-key", 2, "city-beta", "Beta City", "TWO",
                25, null, 8_000_000, 2.0, null, Now + 1_000,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId, recordKey = "city-beta-key", x = 303, y = 404,
                    ownerName = "Beta City", ownerUid = "owner-beta", uuid = "city-beta",
                    allianceName = "TWO", level = 25, health = 456789, updatedAt = Now + 1_000,
                })),
            new MapRecord(
                "resource", ServerId, "resource-iron-key", 3, "resource-1", "Iron Mine", null,
                10, null, null, 3.0, null, Now + 3_000,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId, recordKey = "resource-iron-key", uuid = "resource-1",
                    resourceNameKey = "resource.iron", level = 10, updatedAt = Now + 3_000,
                })),
            new MapRecord(
                "monster", ServerId, "monster-doom-key", 4, "monster-1", "Doom Elite", null,
                20, null, null, 12.5, null, Now + 4_000,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId, recordKey = "monster-doom-key", uuid = "monster-1",
                    monsterNameKey = "monster.doom", level = 20, distance = 12.5, updatedAt = Now + 4_000,
                })),
            new MapRecord(
                "truck", ServerId, "truck-key", 5, "truck-1", "Truck One", null,
                null, 6, 5_000_000, 5.0, null, Now + 5_000,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId, recordKey = "truck-key", uuid = "truck-1", quality = 6,
                    isSpecialURQuality = false, arriveTs = Now + 1_000_000,
                    remainingLootCount = 2, maxLootCount = 2, robTimes = 0,
                    currentGoods = new[] { new { key = "item:iron", name = "Iron", iconPath = "icons/iron.png", count = 3 } },
                    updatedAt = Now + 5_000,
                })),
            new MapRecord(
                "railway", ServerId, "railway-key", 6, "railway-1", "Railway One", null,
                null, 4, 6_000_000, 6.0, null, Now + 6_000,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId, recordKey = "railway-key", uuid = "railway-1", quality = 4,
                    arriveTs = Now + 1_000_000, protectTime = Now + 120_000,
                    remainingLootCount = 1, maxLootCount = 2, robTimes = 1,
                    currentGoods = new[] { new { key = "item:food", name = "Food", iconPath = "icons/food.png", count = 4 } },
                    updatedAt = Now + 6_000,
                })),
            new MapRecord(
                "dispatch", ServerId, "dispatch-key", 7, "dispatch-1", "Dispatch One", null,
                5, 5, null, 7.0, null, Now + 7_000,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId, recordKey = "dispatch-key", uuid = "dispatch-1", level = 5,
                    quality = 5, isSpecial = true, completionTime = Now - 1_000,
                    plunderAt = Now + 5_000, taskExpireTime = Now + 60_000,
                    stolenCount = 0, maxStealCount = 2, updatedAt = Now + 7_000,
                })),
            new MapRecord(
                "ghost", ServerId, "ghost-key", 8, "ghost-1", "Ghost One", null,
                6, 4, null, 8.0, null, Now + 8_000,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId, recordKey = "ghost-key", uuid = "ghost-1", level = 6,
                    quality = 4, completionTime = Now + 10_000, updatedAt = Now + 8_000,
                })),
            new MapRecord(
                "treasure", ServerId, "treasure-key", 9, "treasure-1", "Treasure Five", null,
                null, null, null, 9.0, null, Now + 9_000,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId, recordKey = "treasure-key", uuid = "treasure-1",
                    treasureType = 5, suppliesType = 0, treasureNameKey = "treasure.five",
                    allianceId = "A1", viewerAllianceId = "A1", viewerUid = "player-1",
                    complete = true, updatedAt = Now + 9_000,
                })),
            new MapRecord(
                "treasure", ServerId, "treasure-supply-key", 10, "treasure-2", "Season Supply", null,
                null, null, null, 10.0, null, Now + 10_000,
                JsonSerializer.Serialize(new
                {
                    serverId = ServerId, recordKey = "treasure-supply-key", uuid = "treasure-2",
                    treasureType = 0, suppliesType = 3, treasureNameKey = "treasure.supply.three",
                    expireTime = Now + 600_000, updatedAt = Now + 10_000,
                })),
        ];
    }

    private static JsonElement DispatchRow(long plunderAt) => JsonSerializer.SerializeToElement(new
    {
        serverId = ServerId,
        ownerServer = ServerId,
        uuid = "7001",
        ownerName = "Canonical Dispatch",
        completionTime = Now + 99_000,
        plunderAt,
        taskExpireTime = Now + 500_000,
        stolenCount = 0,
        maxStealCount = 2,
        rewards = Array.Empty<object>(),
    });

    private static JsonElement TruckRow(long executeAt) => JsonSerializer.SerializeToElement(new
    {
        serverId = ServerId,
        uuid = "8001",
        ownerName = "Canonical Truck",
        executeAt,
        maxLootCount = 2,
        robTimes = 0,
        expireAt = Now + 500_000,
    });

    private static void RequireSingle(
        CanonicalMap.MapSearchResult result,
        string property,
        string expected,
        string label)
    {
        Require(result.Total == 1 && result.Rows.Count == 1 &&
                result.Rows[0].TryGetProperty(property, out JsonElement value) &&
                value.ToString() == expected,
            label + " must return exactly the expected persisted Map317 row");
    }

    private static string Status(JsonElement row) =>
        row.GetProperty("scheduleStatus").GetString() ?? string.Empty;

    private static async Task ExpectBridgeErrorAsync(
        string code,
        Func<Task> action,
        string label)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (CanonicalMap.BridgeCommandException error) when (error.Code == code)
        {
            return;
        }
        throw new InvalidOperationException(label + " did not fail with " + code);
    }

    private static async Task ExpectDesktopBridgeErrorAsync(
        Func<Task<object?>> action,
        string code,
        string message)
    {
        try
        {
            _ = await action().ConfigureAwait(false);
        }
        catch (LWBridge.Desktop.BridgeCommandException error) when (
            error.Code == code && error.Message == message)
        {
            return;
        }
        throw new InvalidOperationException(
            $"Map317 desktop command did not fail with {code}: {message}");
    }

    private static void ExpectBridgeError(string code, Action action, string label)
    {
        try
        {
            action();
        }
        catch (CanonicalMap.BridgeCommandException error) when (error.Code == code)
        {
            return;
        }
        throw new InvalidOperationException(label + " did not fail with " + code);
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        ZipArchiveEntry entry = archive.GetEntry(path) ??
            throw new InvalidOperationException("Map317 workbook part missing: " + path);
        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static XElement Cell(XElement worksheet, string reference) =>
        worksheet.Descendants(SheetNs + "c")
            .Single(cell => string.Equals(
                (string?)cell.Attribute("r"),
                reference,
                StringComparison.Ordinal));

    private static XElement CellInRow(XElement row, string column) =>
        row.Elements(SheetNs + "c")
            .Single(cell => ((string?)cell.Attribute("r"))?.StartsWith(column, StringComparison.Ordinal) == true);

    private static string InlineText(XElement cell) =>
        cell.Element(SheetNs + "is")?.Element(SheetNs + "t")?.Value ?? string.Empty;

    private static string CellValue(XElement cell) =>
        cell.Element(SheetNs + "v")?.Value ?? string.Empty;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Map campaign canonical check failed: " + message);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Test cleanup must not replace the canonical acceptance failure.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Test cleanup must not replace the canonical acceptance failure.
        }
    }

    private sealed record PlunderAcceptance(
        bool ScheduledDuplicateAdmission,
        bool RunningDuplicateRejected,
        bool DurableRestart,
        bool RestartReconciled,
        bool Retry,
        bool Cancel,
        bool Clear);

    private sealed class InertPlunderProvider : IMapActionProvider
    {
        private readonly int serverId;
        private readonly bool armSucceeds;

        internal InertPlunderProvider(int serverId, bool armSucceeds)
        {
            this.serverId = serverId;
            this.armSucceeds = armSucceeds;
        }

        internal int DispatchArmCalls { get; private set; }
        internal int TruckArmCalls { get; private set; }

        public ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(serverId);
        }

        public ValueTask<MapPlunderServerDayProviderResult?> GetMapPlunderServerDayStartAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<MapPlunderServerDayProviderResult?>(null);
        }

        public ValueTask<IReadOnlyList<DispatchPlunderArmResult>> ArmDispatchPlunderAsync(
            IReadOnlyList<DispatchPlunderArmJob> jobs,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DispatchArmCalls++;
            IReadOnlyList<DispatchPlunderArmResult> result = jobs
                .Select(job => new DispatchPlunderArmResult(
                    job.Kind,
                    job.ServerId,
                    job.TaskUuid,
                    armSucceeds,
                    armSucceeds ? null : "TEST_DISPATCH_REJECTED"))
                .ToArray();
            return ValueTask.FromResult(result);
        }

        public ValueTask<TruckPlunderArmResult> ArmTruckPlunderAsync(
            TruckPlunderArmJob job,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TruckArmCalls++;
            return ValueTask.FromResult(new TruckPlunderArmResult(
                armSucceeds,
                armSucceeds ? null : "TEST_TRUCK_REJECTED"));
        }

        public ValueTask<IReadOnlyList<DispatchPlunderResultEvent>> DrainDispatchPlunderResultsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<DispatchPlunderResultEvent>>(
                Array.Empty<DispatchPlunderResultEvent>());
        }

        public ValueTask<IReadOnlyList<TruckPlunderResultEvent>> DrainTruckPlunderResultsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<TruckPlunderResultEvent>>(
                Array.Empty<TruckPlunderResultEvent>());
        }

        public ValueTask ClearTruckPlunderPendingAsync(
            int serverId,
            string trainUuid,
            string jobId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask GotoWorldCoordinateAsync(
            int serverId, int x, int y, CancellationToken cancellationToken = default) =>
            Blocked("coordinate jump");

        public ValueTask GotoWorldMarchAsync(
            int serverId, string marchUuid, CancellationToken cancellationToken = default) =>
            Blocked("march follow");

        public ValueTask GotoServerAsync(int serverId, CancellationToken cancellationToken = default) =>
            Blocked("server jump");

        public ValueTask<TreasureInspectionResult> InspectTreasureStatesAsync(
            IReadOnlyList<JsonElement> records,
            bool refresh,
            CancellationToken cancellationToken = default) =>
            Blocked<TreasureInspectionResult>("Treasure state refresh");

        public ValueTask<JsonElement> GetTreasureClaimStatusAsync(
            CancellationToken cancellationToken = default) =>
            Blocked<JsonElement>("Treasure claim status");

        public ValueTask<JsonElement> ClaimTreasuresAsync(
            TreasureClaimProviderRequest request,
            CancellationToken cancellationToken = default) =>
            Blocked<JsonElement>("Treasure claim");

        public ValueTask<DispatchShareProviderResult> ShareDispatchTaskToAllianceAsync(
            JsonElement row,
            CancellationToken cancellationToken = default) =>
            Blocked<DispatchShareProviderResult>("Dispatch alliance share");

        public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(
            IReadOnlyList<JsonElement> rows,
            CancellationToken cancellationToken = default) =>
            Blocked<IReadOnlyList<JsonElement>>("Ghost plunder preparation");

        private static ValueTask Blocked(string operation) =>
            ValueTask.FromException(new CanonicalMap.BridgeCommandException(
                "TEST_PROVIDER_BLOCKED",
                operation + " is outside deterministic canonical Map acceptance"));

        private static ValueTask<T> Blocked<T>(string operation) =>
            ValueTask.FromException<T>(new CanonicalMap.BridgeCommandException(
                "TEST_PROVIDER_BLOCKED",
                operation + " is outside deterministic canonical Map acceptance"));
    }
}
