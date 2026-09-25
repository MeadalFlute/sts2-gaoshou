using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaoshou.Patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Events;

// 超级强化机（出现章节：Glory / ACT3）：
// 设计意图：一台投币式强化机，投入金币或生命来抽「随机升级一张牌」。
// 成功率：基础 40%，**每失败一次 +20%**（投币 / 投命两条路共用同一份连败计数），
//         成功后立刻清零（2026-09-23 应作者要求加）。loc 里以 [gold]{Chance}%[/gold] 显示当前值。
// 次数：**无限**（2026-09-23 应作者要求改；原本是抽 5 次后进 EMPTY 页"机器空了"）。
// 做法照抄原版「滑脚木桥」SLIPPERY_BRIDGE：计数照涨，但页 key 到阈值后**饱和**成一个固定页
// （原版第 7 次之后固定用 "LOOP"），于是可以一直复用同一页的文案与选项，永远抽下去。
// 副作用：EMPTY 页不再可达 —— loc 里的 EMPTY 条目保留但已无人使用。
[RegisterActEvent(typeof(Glory))]
public sealed class SuperUpgrader : ModEventTemplate
{
    /// <summary>抽奖成功的**基础**概率（百分数）。</summary>
    private const decimal BaseChance = 40m;

    /// <summary>每失败一次累加的成功概率（百分数）；成功后清零。</summary>
    private const decimal ChancePerFailure = 20m;

    /// <summary>前 5 次使用的页面：机身上并排 5 个一模一样的按钮。</summary>
    private static readonly string[] PageOrder = ["INITIAL", "ROUND1", "ROUND2", "ROUND3", "ROUND4"];

    /// <summary>超出 PageOrder 之后**永远**使用的饱和页（做法同原版 SLIPPERY_BRIDGE 的 "LOOP"）。</summary>
    private const string LoopPage = "LOOP";

    /// <summary>
    /// 卡组里已经没有可升级的牌时，两个付费选项改用这一"页"下的文案 ——
    /// 标题照旧（付出金钱 / 付出生命），描述变成「我没有可以升级的牌。」。
    /// 这只是一个固定的本地化键前缀（选项键 = {Id}.pages.{页}.options.{选项}），与当前实际翻到第几页无关。
    /// </summary>
    private const string NoUpgradePage = "NO_UPGRADE";

    /// <summary>当前是第几页（PageOrder 的下标）。</summary>
    private int _roundIndex;

    /// <summary>连续失败次数（成功即清零）；成功率 = 基础 40% + 20% × 它。</summary>
    private int _failureStreak;

    /// <summary>当前成功率（百分数），供 loc 的 [gold]{Chance}%[/gold] 显示。</summary>
    private decimal CurrentChance => BaseChance + ChancePerFailure * _failureStreak;

    /// <summary>把当前成功率写进 DynamicVar（改完连败计数后、刷新页面前调用）。</summary>
    private void SyncChanceVar() => DynamicVars["Chance"].BaseValue = CurrentChance;

    // 立绘：本模组自制底图（图由美术侧生成；素材暂缺时此路径会回退到原版逻辑，不会崩）。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/_base_notebook.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 投币价格：基础 30，CalculateVars 里 ±5。loc 里用 {GoldCost}。
        new GoldVar("GoldCost", 30),
        // 投入的生命：基础 5，CalculateVars 里 -1~+1。loc 里用 {HpCost}。
        new HpLossVar("HpCost", 5m),
        // 当前成功率（百分数）：loc 里用 [gold]{Chance}%[/gold]，随连败累积、成功后清零。
        new IntVar("Chance", BaseChance),
    ];

    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled("super_upgrader") && runState.CurrentActIndex == 2;

    public override void CalculateVars()
    {
        // 每次遇到机器价格都略有浮动，让玩家无法把价格背下来。
        // 变量名必须与 CanonicalVars 一致（"GoldCost" / "HpCost"）：
        // 曾经误写成 DynamicVars.Gold → 字典里没有 'Gold' 键，
        // BeginEvent 时抛 KeyNotFoundException（表现为进事件弹 "You found a bug"）。
        DynamicVars["GoldCost"].BaseValue += Rng.NextInt(-5, 6);
        DynamicVars["HpCost"].BaseValue += Rng.NextInt(-1, 2);
        if (DynamicVars["HpCost"].BaseValue < 1m)
            DynamicVars["HpCost"].BaseValue = 1m;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        _roundIndex = 0;
        _failureStreak = 0;
        SyncChanceVar();
        return BuildRoundOptions();
    }

    /// <summary>
    /// 构造「一模一样」的一页选项：三个选项每次都必须 new 出新实例，
    /// 否则同一页第二次点击时旧实例的 WasChosen 已为 true，回调不会再触发。
    /// 锁定条件是 `onChosen == null`（EventOption.IsLocked 的判定方式，原版 LuminousChoir/TeaMaster 同款做法），
    /// 灰色不可点、不会"点了没反应"；每翻一页都会重新算一次，所以花掉钱 / 升满牌之后的新页会正确锁上：
    ///   * 钱不够 → 「投币」锁定；
    ///   * 卡组里没有可升级的牌 → 「投币」和「投命」**都**锁定，且描述改成「我没有可以升级的牌。」
    ///     （2026-09-23 补：原先漏了全升级完的情况）。
    /// </summary>
    private List<EventOption> BuildRoundOptions()
    {
        var canUpgrade = UpgradeableCards().Any();
        // 没有可升级的牌时，两个付费选项改读 NO_UPGRADE 页的文案（标题照旧、描述变成提示）。
        var payPage = canUpgrade ? CurrentPage() : NoUpgradePage;
        var canPayGold = canUpgrade && (Owner?.Gold ?? 0m) >= DynamicVars["GoldCost"].BaseValue;
        Func<Task>? payGold = canPayGold ? PayGold : null;

        return
        [
            new EventOption(this, payGold, ModOptionKey(payPage, "PAY_GOLD")),
            new EventOption(this, canUpgrade ? PayHp : null, ModOptionKey(payPage, "PAY_HP"))
                .ThatDoesDamage(DynamicVars["HpCost"].BaseValue),
            new EventOption(this, Leave, ModOptionKey(CurrentPage(), "LEAVE")),
        ];
    }

    /// <summary>当前页 key：前 5 次按 PageOrder 走，之后**永远**是 LOOP —— 也就是可以无限投币。</summary>
    private string CurrentPage() => _roundIndex < PageOrder.Length ? PageOrder[_roundIndex] : LoopPage;

    private Task PayGold()
    {
        // 钱不够就不能投币：不扣钱、不抽奖，也不消耗一次机会（页面原样刷新，保持"没反应"的观感）。
        if (Owner!.Gold < DynamicVars["GoldCost"].BaseValue)
        {
            SetEventState(PageDescription(CurrentPage()), BuildRoundOptions());
            return Task.CompletedTask;
        }

        return PayGoldAndRoll();
    }

    private async Task PayGoldAndRoll()
    {
        await PlayerCmd.LoseGold(DynamicVars["GoldCost"].BaseValue, Owner!, GoldLossType.Spent);
        await TryRollUpgrade();
        await AdvanceRound();
    }

    private async Task PayHp()
    {
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature,
            DynamicVars["HpCost"].BaseValue, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        await TryRollUpgrade();
        await AdvanceRound();
    }

    /// <summary>
    /// 按当前成功率掷一次。
    /// 失败 → 连败 +1（下次成功率 +20%）；成功 → 升级一张随机可升级牌并**清零**连败加成。
    /// 投币（PayGoldAndRoll）与投命（PayHp）都走这里，所以"不论如何支付"共用同一份连败计数。
    /// </summary>
    private async Task TryRollUpgrade()
    {
        if (Rng.NextFloat() * 100f >= (float)CurrentChance)
        {
            // 没中奖：累积成功率，并给一点停顿让玩家看清"没反应"。
            _failureStreak++;
            await Cmd.CustomScaledWait(0.3f, 0.5f);
            return;
        }

        _failureStreak = 0;

        CardModel? picked = Rng.NextItem(UpgradeableCards().ToList());
        if (picked == null)
            return;

        // 注意：CardCmd.Upgrade 内部**已经**会把升级后的卡放进预览容器（它自带 CardPreviewStyle 参数），
        // 所以这里不能再调 CardCmd.Preview —— 否则会连播两次动画（玩家实测"成功时动画触发两次"）。
        CardCmd.Upgrade(picked);
        await Cmd.CustomScaledWait(0.6f, 1.2f);
    }

    /// <summary>牌组里所有还能升级的牌。</summary>
    private IEnumerable<CardModel> UpgradeableCards()
        => Owner!.Deck.Cards.Where(c => c.IsUpgradable);

    /// <summary>
    /// 抽完一次翻到下一页；到 LOOP 页之后就**一直留在 LOOP**（可以无限抽），不再出现"机器空了"。
    /// 每页都必须 new 出新的 EventOption 实例，所以这里每次都重建选项（原因见 BuildRoundOptions 注释）。
    /// </summary>
    private Task AdvanceRound()
    {
        _roundIndex++;
        SyncChanceVar();   // 刚掷完可能变了成功率（失败 +20%），下一页的文案要显示新值
        SetEventState(PageDescription(CurrentPage()), BuildRoundOptions());
        return Task.CompletedTask;
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("DONE"));
        return Task.CompletedTask;
    }
}
