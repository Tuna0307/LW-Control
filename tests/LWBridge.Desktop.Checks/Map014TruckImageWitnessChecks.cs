using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Map014TruckImageWitnessChecks
{
    internal static void Run()
    {
        static JsonElement Result(int total, params object[] rows) =>
            JsonSerializer.SerializeToElement(new { total, rows });
        static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("MAP014_TRUCK_IMAGE_WITNESS: " + message);
        }

        // Regression for actual saved 193-record Truck store: at a later
        // clock instant the canonical search legitimately returns zero rows.
        // Such an empty DOM cannot constitute a failed PNG/asset transport.
        var expired = Map014TruckImageWitnessClassifier.Classify(Result(0));
        Expect(expired.State == "no_unexpired_truck_rows" &&
               !expired.HasPositiveSource && expired.IconSources == 0,
            "expired source was mislabelled as native image failure");

        var positive = Map014TruckImageWitnessClassifier.Classify(Result(2,
            new { currentGoods = new object[] {
                new { iconPath = "Assets/Main/Sprites/itemA.png" },
                new { iconPath = "Assets/Main/Sprites/itemB.png" },
                new { iconPath = "Assets/Main/Sprites/itemA.png" } } },
            new { currentGoods = new object[] {
                new { iconPath = "Assets/Main/Sprites/itemB.png" } } }));
        Expect(positive.HasPositiveSource && positive.Total == 2 &&
               positive.PageRows == 2 && positive.IconSources == 4 &&
               positive.DistinctIconSources == 2,
            "real positive source should still require actual native PNG and WebView decoding");

        var noPath = Map014TruckImageWitnessClassifier.Classify(Result(1,
            new { currentGoods = new object[] { new { iconPath = "" } } }));
        Expect(!noPath.HasPositiveSource && noPath.State == "no_native_reward_icon_sources",
            "missing real icon identity must not be marked positive");
        var unpaged = Map014TruckImageWitnessClassifier.Classify(Result(1));
        Expect(!unpaged.HasPositiveSource && unpaged.State == "no_page_rows",
            "a positive total without a page does not prove visible source rows");
        try
        {
            _ = Map014TruckImageWitnessClassifier.Classify(Result(0, new { currentGoods = new object[] { new { iconPath = "x" } } }));
            throw new InvalidOperationException("MAP014_TRUCK_IMAGE_WITNESS: inconsistent page not rejected");
        }
        catch (InvalidDataException) { }

        Console.WriteLine("MAP014_TRUCK_IMAGE_WITNESS_CHECKS_OK");
    }
}
