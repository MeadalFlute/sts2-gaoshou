using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib;
using STS2RitsuLib.Utils;
using Gaoshou.Characters;
using Gaoshou.Keywords;
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
//   - 教程进度记在**模组自己**的数据存储里（GaoshouTutorialSettings），不再用原版 SeenFtue：
//     原版 SeenFtue(key) 把"玩家关掉了教程"和"该 key 已完成"混成同一个 true，既分不清也没法重置。
//     现在：教程开关 = 模组自己的设置项（默认开，**不**跟随原版的 EnableFtues）；是否播过 = 模组自己的记录。
//     全部教程播过一遍后不再触发，设置页「新手教程 → 重置」可以清空记录重新播。
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

    // ---- 关键词教程（2026-10-08 追加）----
    // 触发点：**卡牌奖励**里出现了带某个关键词的牌（原版同款场景：NPowerCardFtue 就是"奖励里出现能力牌就讲解"）。
    // 每个关键词只讲一次（进度同样记在 GaoshouTutorialSettings，设置页可重置 ✓）。
    public const string FlowFtueKey = "gaoshou_kw_flow_ftue";
    public const string MiracleFtueKey = "gaoshou_kw_miracle_ftue";
    public const string StormFtueKey = "gaoshou_kw_storm_ftue";

    /// <summary>
    /// 关键词讲解的固定顺序（一屏里出现多个新关键词时，按这个顺序排页）。
    /// </summary>
    public static readonly CardKeyword[] KeywordTutorialOrder =
    [
        GaoshouKeyword.Flow,
        GaoshouKeyword.Miracle,
        GaoshouKeyword.Storm,
    ];

    /// <summary>关键词 → 教程进度 key。</summary>
    public static string FtueKeyFor(CardKeyword keyword)
    {
        if (keyword == GaoshouKeyword.Flow) return FlowFtueKey;
        if (keyword == GaoshouKeyword.Miracle) return MiracleFtueKey;
        return StormFtueKey;
    }

    /// <summary>关键词 → 文案 key 的中段（gaoshou.kw.&lt;stem&gt;.title / .body）。</summary>
    private static string StemFor(CardKeyword keyword)
    {
        if (keyword == GaoshouKeyword.Flow) return "flow";
        if (keyword == GaoshouKeyword.Miracle) return "miracle";
        return "storm";
    }

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

    /// <summary>
    /// 通用前置检查：本端是不是"该看到教程的那个玩家"，以及教程开关 / 播放进度是否允许。
    /// **每一条跳过原因都会写日志** —— 排查"怎么没弹教程"全指望这几行（2026-09-23 加）。
    /// </summary>
    private static bool MayShowTutorial(string label, IRunState runState, ICombatState? combatState, string ftueKey)
    {
        if (TestMode.IsOn)
        {
            _logger?.Info($"[GaoshouTutorial] {label} skipped: test mode.");
            return false;
        }

        // 不是高手玩家的那一端：静默（联机里其它角色不需要看到我们的教程）。
        if (LocalContext.GetMe(combatState)?.Character is not GaoshouCharacter)
            return false;

        // 模组自己的教程开关（默认开）。
        // 刻意**不**跟随原版 Progress.EnableFtues：那个开关只关原版教程，很多玩家/开发者早就把它关了，
        // 跟着它会导致模组教程永远不弹（2026-09-23 实测：日志里两条 "EnableFtues=false" 就是这个原因）。
        if (!GaoshouTutorialSettings.IsEnabled)
        {
            _logger?.Info($"[GaoshouTutorial] {label} skipped: 模组设置里关闭了新手教程。");
            return false;
        }

        // 已经播过就不弹（模组自己记的进度；全部播完后自然再也不触发）。
        if (GaoshouTutorialSettings.IsPlayed(ftueKey))
        {
            _logger?.Info($"[GaoshouTutorial] {label} skipped: 已经播放过（模组进度里已记录；设置页可重置）。");
            return false;
        }

        // 非标准局（自定义 / 每日等）也放行 —— 2026-09-23 起按"默认为触发"处理：
        // 自测常在自定义模式里进行，原先只在标准局弹会表现为"怎么都不弹"。这里只留一行日志备查。
        if (runState.GameMode != GameMode.Standard)
            _logger?.Info($"[GaoshouTutorial] {label}: GameMode={runState.GameMode}（非标准局同样放行）。");

        return true;
    }

    private static void OnCombatStarting(CombatStartingEvent evt)
    {
        if (!MayShowTutorial("intro", evt.RunState, evt.CombatState, IntroFtueKey))
            return;

        if (!IsFirstCombatOfRun(evt.RunState))
        {
            _logger?.Info("[GaoshouTutorial] intro skipped: 不是本局第一场战斗" +
                          $"（TotalFloor={evt.RunState.TotalFloor}）—— 开场教程只在第一场战斗弹，换新局即可。");
            return;
        }

        ShowDeferred(IntroFtueKey, BuildIntroPages, () => _introShownThisRun = true);
    }

    private static void OnCombatVictory(CombatVictoryEvent evt)
    {
        if (!MayShowTutorial("first-win", evt.RunState, evt.CombatState, FirstWinFtueKey))
            return;

        if (!IsFirstCombatOfRun(evt.RunState))
        {
            _logger?.Info("[GaoshouTutorial] first-win skipped: 不是本局第一场战斗。");
            return;
        }

        // ① 既没在本局弹过、历史上也没播过 → 说明 ① 这次被跳过了（例如模态被原版 FTUE 长时间占用），
        // 这时单独弹 ② 会很突兀，直接跳过（下次开局仍会补弹 ①）。
        if (!_introShownThisRun && !GaoshouTutorialSettings.IsPlayed(IntroFtueKey))
        {
            _logger?.Info("[GaoshouTutorial] first-win skipped: 开场教程这次没弹过，先等下次开局把开场补上。");
            return;
        }

        ShowDeferred(FirstWinFtueKey, BuildFirstWinPages);
    }

    // ---- 关键词教程：卡牌奖励触发（2026-10-08）----

    /// <summary>
    /// 卡牌奖励场景的准入检查（这个时机没有 CombatState）：本端是不是高手玩家 + 教程开关 + 测试模式。
    /// 与 <see cref="MayShowTutorial" /> 同一套口径，只是角色判定改用 <see cref="IRunState" />（它本身就是玩家集合 ✓）。
    /// </summary>
    public static bool CanShowRewardTutorial(IRunState? runState)
    {
        if (TestMode.IsOn)
            return false;

        // 不是高手玩家的那一端：静默（联机里其它角色不需要看到我们的教程）。
        if (LocalContext.GetMe(runState)?.Character is not GaoshouCharacter)
            return false;

        return GaoshouTutorialSettings.IsEnabled;
    }

    /// <summary>奖励界面里的一张候选牌：卡 + 它在界面里的节点（节点用来抬到遮罩之上 ✓）。</summary>
    public readonly record struct KeywordCardOffer(CardModel Card, Control Holder);

    /// <summary>排队中的一个讲解步骤（关键词 + 示例牌 + 牌节点 ✓）。</summary>
    private readonly record struct KeywordStep(CardKeyword Keyword, CardModel Card, Control Holder);

    /// <summary>
    /// 卡牌奖励出现时调用（补丁见 Patches\CardRewardKeywordTutorialPatch.cs）：
    /// 把"这一屏里有、且还没讲解过"的关键词按 <see cref="KeywordTutorialOrder" /> 顺序**逐个**弹出来 ——
    /// 完全照原版能力牌 FTUE 的样子（暗色遮罩 + 把**那张牌**抬到遮罩之上 + 弹窗里写说明 + 展开它的悬浮提示 ✓，
    /// 实现见 <see cref="KeywordTutorialPopup" />）。点确认接着弹下一个 ⇒ 不会叠窗 ✓
    /// </summary>
    public static void TryShowForCardReward(IReadOnlyList<KeywordCardOffer>? offered)
    {
        if (offered == null || offered.Count == 0 || _keywordTutorialQueued)
            return;

        var queue = new List<KeywordStep>();
        foreach (var keyword in KeywordTutorialOrder)
        {
            if (GaoshouTutorialSettings.IsPlayed(FtueKeyFor(keyword)))
                continue;   // 已经讲过（含用户手动重置前的记录）

            foreach (var offer in offered)
            {
                if (offer.Card == null || offer.Holder == null)
                    continue;
                if (!offer.Card.Keywords.Contains(keyword))
                    continue;

                queue.Add(new KeywordStep(keyword, offer.Card, offer.Holder));
                break;   // 每个关键词只用一张牌做示例
            }
        }

        if (queue.Count == 0)
            return;

        _logger?.Info($"[GaoshouTutorial] keyword tutorial queue: {queue.Count} 条");
        _keywordTutorialQueued = true;
        _keywordQueue.Clear();
        _keywordQueue.AddRange(queue);
        ShowNextKeywordPopup();
    }

    private static readonly List<KeywordStep> _keywordQueue = [];

    /// <summary>关键词教程是否已排队（防重入；队列走完或失败时清掉）。</summary>
    private static bool _keywordTutorialQueued;

    /// <summary>弹下一个关键词讲解；队列空了就收尾并解除防重入 ✓。</summary>
    private static void ShowNextKeywordPopup()
    {
        if (_keywordQueue.Count == 0)
        {
            _keywordTutorialQueued = false;
            return;
        }

        var step = _keywordQueue[0];
        _keywordQueue.RemoveAt(0);
        // 按钮回调里不能再碰 UI（原版弹窗正在关闭）⇒ 递延到下一帧 ✓
        Callable.From(() => _ = ShowKeywordPopupAsync(step)).CallDeferred();
    }

    private static async Task ShowKeywordPopupAsync(KeywordStep step)
    {
        try
        {
            var container = NModalContainer.Instance;
            if (container == null)
            {
                AbortKeywordQueue();
                return;
            }

            // 原版 FTUE / 其它模态占用时排队等待（最长约 15 秒），不抢占 —— 与开场教程同一套逻辑 ✓
            for (var i = 0; i < 75 && container.OpenModal != null; i++)
            {
                await DelayAsync(0.2);
                container = NModalContainer.Instance;
                if (container == null)
                {
                    AbortKeywordQueue();
                    return;
                }
            }

            if (container.OpenModal != null)
            {
                _logger?.Info("[GaoshouTutorial] keyword tutorial: another modal stayed open; skipped without marking.");
                AbortKeywordQueue();   // 不标记 ⇒ 下次再试 ✓
                return;
            }

            // 奖励牌行是刚 Push 进来的，**布局还没跑完**（卡位/卡尺寸都还是 0）⇒ 等它摆好再开讲：
            // 提示控件按卡位算绝对坐标，早了就摆错（2026-10-11 实机"提示错位"就是这么来的 ✗）。
            await WaitForRewardRowLayoutAsync(step.Holder);

            var stem = StemFor(step.Keyword);
            var title = Text($"gaoshou.kw.{stem}.title", FallbackKeywordTitle(stem));
            if (!KeywordTutorialPopup.TryShow(step.Card, step.Holder, step.Keyword, title, BuildKeywordBody(stem),
                    ShowNextKeywordPopup))
            {
                AbortKeywordQueue();
                return;
            }

            // 弹出即标记（与原版 NRewardsScreen.RewardFtueCheck 同一口径：避免强退后反复重弹 ✓）
            GaoshouTutorialSettings.MarkPlayed(FtueKeyFor(step.Keyword));
        }
        catch (Exception e)
        {
            _logger?.Error($"[GaoshouTutorial] keyword popup failed: {e}");
            AbortKeywordQueue();
        }
    }

    private static void AbortKeywordQueue()
    {
        _keywordQueue.Clear();
        _keywordTutorialQueued = false;
    }

    /// <summary>把一个关键词的多条说明合成弹窗正文（BBCode + 换行 ✓；全缺时退回兜底英文 ✓）。</summary>
    private static string BuildKeywordBody(string stem)
    {
        var body = new System.Text.StringBuilder();
        for (var i = 1; i <= 3; i++)
        {
            var line = Text($"gaoshou.kw.{stem}.line{i}", string.Empty);
            if (string.IsNullOrEmpty(line))
                continue;

            if (body.Length > 0)
                body.Append('\n');
            body.Append(line);
        }

        return body.Length > 0 ? body.ToString() : FallbackKeywordBody(stem);
    }

    // 文案兜底（正常情况下走 localization\tutorial\{zhs,eng}.json；缺 key 时至少不是空白）。
    private static string FallbackKeywordTitle(string stem) => stem switch
    {
        "flow" => "Flow",
        "miracle" => "Miracle",
        _ => "Storm",
    };

    private static string FallbackKeywordBody(string stem) => stem switch
    {
        "flow" => "Triggers an extra effect when its condition is met.",
        "miracle" => "When played, triggers if this card was not drawn at the start of this turn. Cards retained from the previous turn also qualify.",
        _ => "Triggers after you play it, if your Energy meets the printed condition.",
    };

    private static void ShowDeferred(string ftueKey, Func<List<GaoshouTutorialPage>> pagesFactory, Action? onShown = null)
        => ShowDeferred([ftueKey], pagesFactory, onShown);

    private static void ShowDeferred(
        IReadOnlyList<string> ftueKeys, Func<List<GaoshouTutorialPage>> pagesFactory,
        Action? onShown = null, Action? onDone = null)
    {
        // 事件回调里不能 await / 不能直接碰 UI，递延到下一帧的主循环再处理。
        Callable.From(() => _ = ShowAsync(ftueKeys, pagesFactory, onShown, onDone)).CallDeferred();
    }

    /// <param name="onShown">真正弹出来了才调用（用于"本局已弹过"这类标记）。</param>
    /// <param name="onDone">无论弹出还是被跳过都会调用（用于清防重入标记）。</param>
    private static async Task ShowAsync(
        IReadOnlyList<string> ftueKeys, Func<List<GaoshouTutorialPage>> pagesFactory,
        Action? onShown, Action? onDone)
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
                _logger?.Info($"[GaoshouTutorial] {string.Join(",", ftueKeys)}: another modal stayed open; skipped without marking.");
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
            // 弹出即标记（与原版 NRewardsScreen.RewardFtueCheck 同一口径：避免强退后反复重弹）。
            // 注意标记的是**模组自己的进度**（全部播完后即停止触发；设置页可重置）。
            foreach (var key in ftueKeys)
                GaoshouTutorialSettings.MarkPlayed(key);
            onShown?.Invoke();
            panel.Start();
        }
        catch (Exception e)
        {
            _logger?.Error($"[GaoshouTutorial] show '{string.Join(",", ftueKeys)}' failed: {e}");
        }
        finally
        {
            onDone?.Invoke();
        }
    }

    private static async Task DelayAsync(double seconds)
    {
        if (Engine.GetMainLoop() is SceneTree tree)
            await tree.ToSignal(tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        else
            await Task.Delay(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    /// 等卡牌奖励那一行**真正摆好版**（最多约 0.5 秒），再弹关键词讲解。
    /// 牌行刚 Push 进来时 holder/hitbox 的尺寸都还是 0，此时算出来的提示位置是错的 ✗。
    /// </summary>
    private static async Task WaitForRewardRowLayoutAsync(Control holder)
    {
        for (var i = 0; i < 30 && !IsCardHolderLaidOut(holder); i++)
            await FramesAsync(1);

        // 再让一帧：原版提示控件要靠这一帧算自己的换行与高度 ✓
        await FramesAsync(1);
    }

    private static bool IsCardHolderLaidOut(Control holder)
    {
        if (holder is NCardHolder cardHolder)
            return cardHolder.Hitbox.Size.X > 1f && cardHolder.Hitbox.Size.Y > 1f;

        return holder.Size.X > 1f && holder.Size.Y > 1f;
    }

    /// <summary>等 N 个渲染帧（用 <c>process_frame</c> 信号，不依赖计时器 ✓）。</summary>
    private static async Task FramesAsync(int count)
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
            return;

        for (var i = 0; i < count; i++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
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
