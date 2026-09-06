using System.Text.Json;
using LWControl.Core;

var options = new JsonSerializerOptions { WriteIndented = true };
try
{
    if (args.Length == 1 && args[0] == "inspect")
    {
        Console.WriteLine(JsonSerializer.Serialize(new CurrentDailyTaskRuntimeClient().Inspect(), options));
        return 0;
    }
    if (args.Length is 1 or 2 && args[0] == "run-once")
    {
        int maximumClaims = args.Length == 2 && int.TryParse(args[1], out int parsed) ? parsed : 20;
        var result = await new CurrentDailyTaskRuntimeClient().RunOnceAsync(maximumClaims);
        Console.WriteLine(JsonSerializer.Serialize(result, options));
        return result.State == "completed" ? 0 : 2;
    }
    Console.Error.WriteLine("Usage: LWControl.DailyTaskCli inspect | run-once [1..20]");
    return 64;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
