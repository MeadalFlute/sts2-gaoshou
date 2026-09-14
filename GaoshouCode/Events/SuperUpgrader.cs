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
// 设计意图：一台投币式强化机，投入金币或生命来抽 40% 概率的「随机升级一张牌」。
// 一共可抽 5 次，每次抽完进入下一页；5 次抽完机器就空了（EMPTY 页），只能离开。
[RegisterActEvent(typeof(Glory))]
public sealed class SuperUpgrader : ModEventTemplate
{
    /// <summary>抽奖成功（升级一张牌）的概率。</summary>
    private const float UpgradeChance = 0.4f;

    /// <summary>页面顺序：机身上并排 5 个一模一样的按钮，按到第 5 个之后机器就空了。</summary>
    private static readonly string[] PageOrder = ["INITIAL", "ROUND1", "ROUND2", "ROUND3", "ROUND4"];

    /// <summary>当前是第几页（PageOrder 的下标）。</summary>
    private int _roundIndex;

    // 立绘：本模组自制底图（图由美术侧生成；素材暂缺时此路径会回退到原版逻辑，不会崩）。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/super_upgrader.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 投币价格：基础 30，CalculateVars 里 ±5。loc 里用 {GoldCost}。
        new GoldVar("GoldCost", 30),
        // 投入的生命：基础 5，CalculateVars 里 -1~+1。loc 里用 {HpCost}。
        new HpLossVar("HpCost", 5m),
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
        return BuildRoundOptions();
    }

    /// <summary>
    /// 构造「一模一样」的一页选项：三个选项每次都必须 new 出新实例，
    /// 否则同一页第二次点击时旧实例的 WasChosen 已为 true，回调不会再触发。
    /// 钱不够时「投币」直接**锁定**（EventOption.IsLocked 由 onChosen == null 决定，原版 LuminousChoir/TeaMaster 同款做法），
    /// 灰色不可点、不会"点了没反应"；每翻一页都会重新算一次锁定状态，所以花掉钱之后的新页会正确锁上。
    /// </summary>
    private List<EventOption> BuildRoundOptions()
    {
        var canPayGold = (Owner?.Gold ?? 0m) >= DynamicVars["GoldCost"].BaseValue;
        Func<Task>? payGold = canPayGold ? PayGold : null;

        return
        [
            new EventOption(this, payGold, ModOptionKey(CurrentPage(), "PAY_GOLD")),
            new EventOption(this, PayHp, ModOptionKey(CurrentPage(), "PAY_HP"))
                .ThatDoesDamage(DynamicVars["HpCost"].BaseValue),
            new EventOption(this, Leave, ModOptionKey(CurrentPage(), "LEAVE")),
        ];
    }

    private string CurrentPage() => PageOrder[_roundIndex];

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

    /// <summary>40% 概率升级一张随机可升级牌；没中或者没有可升级的牌就什么也不发生。</summary>
    private async Task TryRollUpgrade()
    {
        if (Rng.NextFloat() >= UpgradeChance)
        {
            // 没中奖也给一点停顿，让玩家看清"没反应"。
            await Cmd.CustomScaledWait(0.3f, 0.5f);
            return;
        }

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

    /// <summary>抽完一次就翻到下一页；最后一页抽完进入「空了」页（不再回到本页）。</summary>
    private Task AdvanceRound()
    {
        _roundIndex++;
        if (_roundIndex >= PageOrder.Length)
        {
            SetEventState(PageDescription("EMPTY"),
            [
                new EventOption(this, LeaveEmpty, ModOptionKey("EMPTY", "LEAVE2")),
            ]);
            return Task.CompletedTask;
        }

        SetEventState(PageDescription(CurrentPage()), BuildRoundOptions());
        return Task.CompletedTask;
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("DONE"));
        return Task.CompletedTask;
    }

    private Task LeaveEmpty()
    {
        SetEventFinished(PageDescription("DONE"));
        return Task.CompletedTask;
    }
}
