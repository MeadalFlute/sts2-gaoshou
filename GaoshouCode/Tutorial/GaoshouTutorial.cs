using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib;
using STS2RitsuLib.Utils;
using Gaoshou.Characters;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace Gaoshou.Tutorial;

// 高手新手教程（FTUE）。文案/分页由作者（玩家）定稿，见 Gaoshou\localization\tutorial\{zhs,eng}.json：
//   ① 第一场战斗开场（4 页，page1~page4.png）：辉星是什么 / 辉星从哪来 / 认识护符（带圈+箭头标注）/ 别把辉星花光
//   ② 第一场战斗胜利后（1 页，page5.png）：首战之后该补什么牌
// 每页都是"整页底图（左半配图 + 右半便签纸）+ 写在纸上的标题与正文"，版式比例见 GaoshouTutorialFtue。
//
// 文案注意：
//   - 资源名必须用游戏官方中文术语「辉星」（见游戏 zhs/static_hover_tips.json 的 STAR_COUNT.title），
//     不要写成「辉星」；能量、固有、进阶等也都用官方术语。
//   - 纸面是浅色，正文用 [color=#B03A1E] 这类普通 BBCode 指定强调色（不要用游戏的 [gold]/[blue]，
//     它们是为深色 UI 配的，在纸上对比度差）；标题是 MegaLabel（Label 不支持 BBCode），不能带标签。
//
// 关键约束（踩坑记录）：
//   - 复用游戏自身的 FTUE 持久化（SaveManager.SeenFtue / MarkFtueAsComplete）：
//     玩家在设置里关掉教程时 SeenFtue 恒返回 true，我们无需自己判断开关。
//   - 用 RitsuLib 生命周期事件触发，不写 Harmony 补丁；回调里只做 CallDeferred，
//     绝不 await（在 CombatStarting 里 await 会卡住战斗启动流程）。
//   - NModalContainer 同时只允许一个模态：遇到原版 FTUE 占用时排队等待（轮询 OpenModal），
//     不抢占、绝不调用 Clear()；等待超时就本次不展示且不标记（下一局再试）。
//   - 只在"本地玩家是高手"的那一端弹（LocalContext），纯本地 UI，不影响任何同步状态。
//   - 首战首回合"把枪盾置顶"的逻辑放在 GaoshouAmulet.BeforeHandDraw，只用运行态条件
//     （IsFirstCombatOfRun），保证各端一致，避免手牌/校验和分歧。
public static class GaoshouTutorial
{
    public const string IntroFtueKey = "gaoshou_intro_ftue";
    public const string FirstWinFtueKey = "gaoshou_first_win_ftue";

    // 教程配图目录：文件名与页面一一对应，缺图时面板自动隐藏图片区（见 GaoshouTutorialFtue.ApplyImage）。
    private const string TutorialImageDir = $"{Entry.ResPath}/images/tutorial";

    // 与本局是否"已经展示过 ①"有关：用于判断 ② 是否该单独弹（见 OnCombatVictory）。
    private static bool _introShownThisRun;

    private static readonly List<IDisposable> Subscriptions = [];
    private static I18N? _i18n;
    private static Logger? _logger;

    public static void Initialize(Logger logger)
    {
        _logger = logger;

        try
        {
            _i18n = RitsuLibFramework.CreateModLocalization(
                Entry.ModId,
                "tutorial",
                pckFolders: [$"{Entry.ResPath}/localization/tutorial"]);
        }
        catch (Exception e)
        {
            logger.Error($"[GaoshouTutorial] I18N init failed: {e}");
        }

        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(_ => _introShownThisRun = false));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(_ => _introShownThisRun = false));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(_ => _introShownThisRun = false));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(OnCombatStarting));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<CombatVictoryEvent>(OnCombatVictory));

        logger.Info("[GaoshouTutorial] initialized.");
    }

    /// <summary>教程文案（I18N）；取不到时用传入的兜底文本。</summary>
    public static string Text(string key, string fallback)
    {
        return _i18n?.Get(key, fallback) ?? fallback;
    }

    public static void LogError(string message)
    {
        _logger?.Error(message);
    }

    /// <summary>
    /// 本局是否还没有"完成过战斗"——即当前（或最近一次）战斗就是本局第一场战斗。
    /// 只用运行态数据（地图点历史里的战斗房间数），各端一致，可作为"是否改动状态"的判据。
    /// </summary>
    public static bool IsFirstCombatOfRun(IRunState runState)
    {
        var combats = 0;
        foreach (var act in runState.MapPointHistory)
        {
            foreach (var point in act)
            {
                combats += point.Rooms.Count(room =>
                    room.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss);
            }
        }

        return combats <= 1;
    }

    private static bool IsLocalGaoshouInStandardRun(IRunState runState, ICombatState? combatState)
    {
        // 与原版 NCombatRulesFtue.Create() / RunManager.ShouldApplyTutorialModifications 的口径一致。
        if (TestMode.IsOn)
            return false;
        if (runState.GameMode != GameMode.Standard)
            return false;

        var me = LocalContext.GetMe(combatState);
        return me?.Character is GaoshouCharacter;
    }

    private static void OnCombatStarting(CombatStartingEvent evt)
    {
        if (!IsLocalGaoshouInStandardRun(evt.RunState, evt.CombatState))
            return;

        if (SaveManager.Instance.SeenFtue(IntroFtueKey))
            return;

        if (!IsFirstCombatOfRun(evt.RunState))
        {
            // 诊断（首战验收通过后可删）：确认"第一场战斗"判定是否符合预期。
            _logger?.Info($"[GaoshouTutorial] intro skipped: not the run's first combat " +
                          $"(TotalFloor={evt.RunState.TotalFloor})");
            return;
        }

        ShowDeferred(IntroFtueKey, BuildIntroPages, () => _introShownThisRun = true);
    }

    private static void OnCombatVictory(CombatVictoryEvent evt)
    {
        if (!IsLocalGaoshouInStandardRun(evt.RunState, evt.CombatState))
            return;

        if (SaveManager.Instance.SeenFtue(FirstWinFtueKey))
            return;

        if (!IsFirstCombatOfRun(evt.RunState))
            return;

        // ① 既没在本局弹过、历史上也没标记过 → 说明 ① 被跳过了（例如模态被原版 FTUE 长时间占用），
        // 这时单独弹 ② 会很突兀，直接跳过（下次开局仍会补弹 ①）。
        if (!_introShownThisRun && !SaveManager.Instance.SeenFtue(IntroFtueKey))
            return;

        ShowDeferred(FirstWinFtueKey, BuildFirstWinPages);
    }

    private static void ShowDeferred(string ftueKey, Func<List<GaoshouTutorialPage>> pagesFactory, Action? onShown = null)
    {
        // 事件回调里不能 await / 不能直接碰 UI，递延到下一帧的主循环再处理。
        Callable.From(() => _ = ShowAsync(ftueKey, pagesFactory, onShown)).CallDeferred();
    }

    private static async Task ShowAsync(string ftueKey, Func<List<GaoshouTutorialPage>> pagesFactory, Action? onShown)
    {
        try
        {
            var container = NModalContainer.Instance;
            if (container == null)
                return;

            // 原版 FTUE / 其它模态占用时排队等待（最长约 15 秒），不抢占。
            for (var i = 0; i < 75 && container.OpenModal != null; i++)
            {
                await DelayAsync(0.2);
                container = NModalContainer.Instance;
                if (container == null)
                    return;
            }

            if (container.OpenModal != null)
            {
                _logger?.Info($"[GaoshouTutorial] {ftueKey}: another modal stayed open; skipped without marking.");
                return;   // 不标记 → 下一局再试
            }

            var panel = GaoshouTutorialFtue.Create(
                pagesFactory(),
                Text("gaoshou.button.confirm", "Got it!"),
                Text("gaoshou.button.prev", "Back"),
                Text("gaoshou.button.next", "Next"));
            if (panel == null)
                return;   // 构建失败：不标记，下一局再试

            container.Add(panel, false);
            // 与原版 NRewardsScreen.RewardFtueCheck 一致：弹出即标记，避免强退后反复重弹。
            SaveManager.Instance.MarkFtueAsComplete(ftueKey);
            onShown?.Invoke();
            panel.Start();
        }
        catch (Exception e)
        {
            _logger?.Error($"[GaoshouTutorial] show '{ftueKey}' failed: {e}");
        }
    }

    private static async Task DelayAsync(double seconds)
    {
        if (Engine.GetMainLoop() is SceneTree tree)
            await tree.ToSignal(tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        else
            await Task.Delay(TimeSpan.FromSeconds(seconds));
    }

    // 底图（page1~page5.png）由作者做好，图里已经带了配图与标注，代码**不要**再往图上画东西。
    // 代码只负责"指向游戏界面上真实控件"的标注（屏幕空间，画在教程面板之上）：
    // 第 3 页用 RelicBarPointer 指战斗画面左上角的遗物栏。坐标是**视口比例**（左上角 0,0），
    // 实测：2560x1440 下遗物栏中心 (60,160)，半径约 50px → (60/2560, 160/1440)、半径 50/2560。
    // Radius 取宽度比例，画出来才是正圆（不随宽高比变形）。
    private static readonly TutorialAnnotation RelicBarPointer = new(
        Target: new Vector2(60f / 2560f, 160f / 1440f),
        Radius: 50f / 2560f,
        ArrowFrom: Vector2.Zero);   // 0 = 由面板自动放在教程页面左边缘外侧

    // 作者在左半页预留的两个文字框（坐标是在 3920×2475 的原始素材上量的，这里换算成比例）。
    //   图片3：(510,1530)-(1430,1920)   图片4：(510,640)-(1750,1170)
    private static readonly Rect2 LeftBoxPage3 = new(
        510f / 3920f, 1530f / 2475f, (1430f - 510f) / 3920f, (1920f - 1530f) / 2475f);

    private static readonly Rect2 LeftBoxPage4 = new(
        510f / 3920f, 640f / 2475f, (1750f - 510f) / 3920f, (1170f - 640f) / 2475f);

    private static List<GaoshouTutorialPage> BuildIntroPages()
    {
        return
        [
            Page("gaoshou.intro.page1",
                "Star: Your Second Resource",
                "Gaoshou plays differently from the other characters, so here is a quick crash course.\n\n" +
                "Unlike everyone else, many Gaoshou cards cost [color=#B03A1E]Star[/color] to play. Run out of " +
                "Star and all the Energy in the world will not help — those cards simply will not come out!",
                "page1.png"),
            Page("gaoshou.intro.page2",
                "Where Stars Come From",
                "Do not worry: plenty of Gaoshou cards hand you Star, like [color=#B03A1E]Linked Palms[/color] in " +
                "your starting deck.\n\nStar matters a lot, so keep an eye out for cards that make it!\n\n" +
                "A tip: keep the Star your deck makes roughly equal to the Star it spends.",
                "page2.png"),
            Page("gaoshou.intro.page3",
                "Meet Your Amulet",
                "Early on, few cards make Star — that is what your starter relic, the " +
                "[color=#B03A1E]Gaoshou Amulet[/color], is for.\n\n" +
                "It sits in the top-left corner; click it to read the details.\n\n" +
                "Your uses are limited, so plan your play order!",
                "page3.png",
                RelicBarPointer,
                "The number in the relic's bottom-right corner is how many uses are left this turn.",
                LeftBoxPage3),
            Page("gaoshou.intro.page4",
                "Do Not Burn Your Stars",
                "Star is scarce early on, so here is some strong advice: try to end each turn with 1 Star banked.\n\n" +
                "If you have to, spending health to block an attack can be worth it to save Star.\n\n" +
                "Do not let that go in one ear and out the other — this comes from experience!",
                "page4.png",
                leftText: "Star does not refill on its own, but whatever is left at the end of a turn carries over.",
                leftTextRect: LeftBoxPage4),
        ];
    }

    private static List<GaoshouTutorialPage> BuildFirstWinPages()
    {
        return
        [
            Page("gaoshou.firstwin",
                "After Your First Fight",
                "Your starting deck is wildly inconsistent, so grab [color=#B03A1E]Star generation[/color], " +
                "[color=#B03A1E]card draw[/color] and [color=#B03A1E]cheap cards[/color] fast.\n\n" +
                "Good news, though: the starting deck is tiny, so building a fresh deck is surprisingly easy.\n\n" +
                "When you loot, think hard about whether the card really fits your deck!",
                "page5.png"),
        ];
    }

    private static GaoshouTutorialPage Page(
        string keyPrefix, string titleFallback, string bodyFallback, string imageFileName,
        TutorialAnnotation? annotation = null, string? leftText = null, Rect2? leftTextRect = null)
    {
        return new GaoshouTutorialPage(
            Text($"{keyPrefix}.title", titleFallback),
            Text($"{keyPrefix}.body", bodyFallback),
            $"{TutorialImageDir}/{imageFileName}",
            annotation,
            leftText == null ? null : Text($"{keyPrefix}.left", leftText),
            leftTextRect);
    }
}
