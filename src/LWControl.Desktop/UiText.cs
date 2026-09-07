using System.Globalization;
using LWControl.Core;

namespace LWControl.Desktop;

public enum UiLanguage
{
    English,
    SimplifiedChinese,
}

public static class UiText
{
    private static readonly Dictionary<string, (string English, string Chinese)> Strings = new()
    {
        ["Title"] = ("LW Control", "LW Control"),
        ["Header"] = ("LW CONTROL\nDaily Task live runtime · Proven full-world scan", "LW CONTROL\n每日任务实时运行 · 已验证全地图扫描"),
        ["Enable"] = ("Enable daily-claim planning", "启用每日奖励规划"),
        ["Expiry"] = ("Prefer expiring rewards (reference core only)", "优先即将过期的奖励（仅参考核心）"),
        ["Chests"] = ("Then prioritize task chests", "然后优先任务宝箱"),
        ["ClaimsPerRun"] = ("Claims per run", "每次领取上限"),
        ["Categories"] = ("Reward categories", "奖励类别"),
        ["Language"] = ("Language", "语言"),
        ["LoadObservations"] = ("Load observations…", "载入观察数据…"),
        ["LoadSample"] = ("Load sample", "载入示例"),
        ["BuildPlan"] = ("Build plan", "生成计划"),
        ["InspectBridge"] = ("Inspect bridge (read-only)", "检查桥接（只读）"),
        ["ClaimDailyTasks"] = ("Claim daily tasks", "领取每日任务奖励"),
        ["WorldScan"] = ("World Scan", "世界扫描"),
        ["SaveSettings"] = ("Save settings", "保存设置"),
        ["ExportPlan"] = ("Export plan", "导出计划"),
        ["NoObservations"] = ("No observations loaded.", "尚未载入观察数据。"),
        ["Ready"] = ("Ready. Daily Task Claim can run when the current runtime is installed and healthy.", "就绪。安装并正常运行当前版本运行组件后，可执行每日任务领取。"),
        ["Imported"] = ("Imported observations", "已导入观察数据"),
        ["SampleData"] = ("SAMPLE DATA", "示例数据"),
        ["Selected"] = ("Selected for preview", "已选中用于预览"),
        ["Skipped"] = ("Skipped", "已跳过"),
        ["BuildReview"] = ("Build a plan to review", "生成计划后查看"),
        ["ActionsSent"] = ("actions sent", "个操作已发送"),
        ["ObservationJson"] = ("Observation JSON (*.json)|*.json", "观察数据 JSON (*.json)|*.json"),
        ["Json"] = ("JSON (*.json)|*.json", "JSON (*.json)|*.json"),
        ["WarningTitle"] = ("LW Control", "LW Control"),
    };

    public static UiLanguage DetectDefault() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? UiLanguage.SimplifiedChinese
            : UiLanguage.English;

    public static string Get(UiLanguage language, string key)
    {
        var value = Strings[key];
        return language == UiLanguage.SimplifiedChinese ? value.Chinese : value.English;
    }

    public static string ClaimKindName(UiLanguage language, ClaimKind kind) => language switch
    {
        UiLanguage.English => kind switch
        {
            ClaimKind.VipDailyReward => "VIP daily reward",
            ClaimKind.StoreFreePack => "Store free pack",
            ClaimKind.DailyTaskChest => "Daily task chest",
            ClaimKind.WeeklyTaskChest => "Weekly task chest",
            ClaimKind.LoginReward => "Login reward",
            ClaimKind.TavernFreeRecruit => "Tavern free recruit",
            ClaimKind.CampaignIdleReward => "Campaign idle reward",
            _ => kind.ToString(),
        },
        _ => kind switch
        {
            ClaimKind.VipDailyReward => "VIP 每日奖励",
            ClaimKind.StoreFreePack => "商店免费礼包",
            ClaimKind.DailyTaskChest => "每日任务宝箱",
            ClaimKind.WeeklyTaskChest => "每周任务宝箱",
            ClaimKind.LoginReward => "登录奖励",
            ClaimKind.TavernFreeRecruit => "酒馆免费招募",
            ClaimKind.CampaignIdleReward => "战役挂机奖励",
            _ => kind.ToString(),
        },
    };
}

public sealed record ClaimKindOption(ClaimKind Kind, string Text)
{
    public override string ToString() => Text;
}
