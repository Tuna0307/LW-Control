using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018ChestMonitorChecks
{
    private static void Require(bool condition, string error)
    {
        if (!condition)
            throw new InvalidOperationException("FEATURE018_CHEST_MONITOR: " + error);
    }

    private static JsonElement Read(string raw) =>
        JsonSerializer.Deserialize<JsonElement>(raw);

    internal static void Run()
    {
        Require(!LWBridgeWindow.Feature018ChestToggleAllowsMark(null) &&
                !LWBridgeWindow.Feature018ChestToggleAllowsMark(new HotkeyConfig()) &&
                LWBridgeWindow.Feature018ChestToggleAllowsMark(
                    new HotkeyConfig(TreasureChestHint: true)),
            "persisted Mini Games hint opt-in was not respected");

        JsonElement eligible = Read("""
            {"observation":"native_treasure_chest_view","markerEligible":true,
             "nativeSwitchState":"SwitchEnd","bestRewardBoxIndex":2,
             "x":10.5,"y":-4,"z":1}
            """);
        Require(LWBridgeWindow.Feature018ChestSignature(eligible) == "2/10.5/-4/1",
            "genuine post-shuffle owned chest receipt was not classified");
        Require(!LWBridgeWindow.Feature018ChestReceipt(eligible,
                    "native_treasure_chest_view","markerEligible",false),
            "opposite marker eligibility was accepted");

        foreach (string invalid in new[]
        {
            """{"observation":"inferred_best_chest","markerEligible":true,"nativeSwitchState":"SwitchEnd","bestRewardBoxIndex":2,"x":10,"y":4,"z":1}""",
            """{"observation":"native_treasure_chest_view","markerEligible":false,"nativeSwitchState":"SwitchEnd","bestRewardBoxIndex":2,"x":10,"y":4,"z":1}""",
            """{"observation":"native_treasure_chest_view","markerEligible":true,"nativeSwitchState":"Switching","bestRewardBoxIndex":2,"x":10,"y":4,"z":1}""",
            """{"observation":"native_treasure_chest_view","markerEligible":true,"nativeSwitchState":"SwitchEnd","bestRewardBoxIndex":4,"x":10,"y":4,"z":1}""",
            """{"observation":"native_treasure_chest_view","markerEligible":true,"nativeSwitchState":"SwitchEnd","bestRewardBoxIndex":"2","x":10,"y":4,"z":1}""",
            """{"observation":"native_treasure_chest_view","markerEligible":true,"nativeSwitchState":42,"bestRewardBoxIndex":2,"x":10,"y":4,"z":1}""",
            """{"observation":"native_treasure_chest_view","markerEligible":true,"nativeSwitchState":"SwitchEnd","bestRewardBoxIndex":2,"x":"10","y":4,"z":1}""",
            """{"observation":"native_treasure_chest_view","markerEligible":true,"nativeSwitchState":"SwitchEnd","bestRewardBoxIndex":2,"x":10,"y":4}""",
        })
        {
            Require(LWBridgeWindow.Feature018ChestSignature(Read(invalid)) is null,
                "invalid, premature or fake chest receipt was admitted");
        }

        JsonElement marked = Read("""
            {"observation":"native_treasure_chest_view","markerVisible":true,
             "markerSource":"UITreasureChestView.ShowSwitchEffect"}
            """);
        JsonElement cleared = Read("""
            {"observation":"native_treasure_chest_switch_effect","markerVisible":false}
            """);
        Require(LWBridgeWindow.Feature018ChestReceipt(marked,
                    "native_treasure_chest_view","markerVisible",true) &&
                LWBridgeWindow.Feature018ChestReceipt(cleared,
                    "native_treasure_chest_switch_effect","markerVisible",false),
            "source-native marker lifecycle receipts were rejected");
        Require(!LWBridgeWindow.Feature018ChestReceipt(marked,
                    "native_treasure_chest_switch_effect","markerVisible",true),
            "wrong source-native marker receipt was trusted");
        Console.WriteLine("FEATURE018_CHEST_MARKER_MONITOR_OFFLINE_PASS");
    }
}
