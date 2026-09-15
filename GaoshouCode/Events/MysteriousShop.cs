using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaoshou.Patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Events;

// 神秘商店（出现章节：Hive / ACT2）：
// 设计意图：只对「身上有 100 金币以上」的玩家开放的隐藏商店（不满 100 金币根本不会遇到）。
// 三件商品价格递增（卡牌 60 / 罕见遗物 100 / 稀有遗物 120），买不起时不会白扣钱，直接不卖给你。
[RegisterActEvent(typeof(Hive))]
public sealed class MysteriousShop : ModEventTemplate
{
    // 立绘：本模组自制底图（图由美术侧生成）。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/_base_notebook.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar("BuyCardsCost", 60),
        new GoldVar("BuyUncommonCost", 100),
        new GoldVar("BuyRareCost", 120),
    ];

    /// <summary>
    /// 注意：IsAllowed 里 Owner 还没被赋值（连 canonical 实例都会走到这里），
    /// 所以金币必须从 runState.Players 里取（原版 WelcomeToWongos 就是这么判断的）。
    /// </summary>
    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled("mysterious_shop")
           && runState.CurrentActIndex == 1
           && runState.Players.All(p => p.Gold >= 100);

    // 买不起的商品直接「锁定」：EventOption 的 IsLocked 由 onChosen == null 决定（原版也是这么做的，
    // 例如 LuminousChoir / TeaMaster / SelfHelpBook 的 *_LOCKED 选项），锁定选项是灰色不可点的，
    // 所以不可能出现"点了没反应"或白扣钱。文案用 *_LOCKED 变体，明确告诉玩家是钱不够。
    //
    // 锁定状态不会过期：本页只在进入事件时生成一次，而金币只会在我们自己买东西时减少
    // （买完立刻 SetEventFinished 结束事件），所以不会出现"页面显示能买、实际买不起"的窗口。
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var gold = Owner?.Gold ?? 0m;
        return
        [
            BuyOption("BUY_CARDS", "BuyCardsCost", BuyCards, gold),
            BuyOption("BUY_UNCOMMON", "BuyUncommonCost", BuyUncommon, gold),
            BuyOption("BUY_RARE", "BuyRareCost", BuyRare, gold),
            new EventOption(this, Leave, InitialOptionKey("LEAVE")),
        ];
    }

    /// <summary>买得起 = 正常选项；买不起 = 用 *_LOCKED 文案锁定（onChosen 传 null）。</summary>
    private EventOption BuyOption(string optionKey, string costKey, Func<Task> onBuy, decimal gold)
        => gold >= DynamicVars[costKey].BaseValue
            ? new EventOption(this, onBuy, InitialOptionKey(optionKey))
            : new EventOption(this, null, InitialOptionKey(optionKey + "_LOCKED"));

    /// <summary>买点卡牌：60 金币换一组**稀有**卡牌奖励（三选一，全部限定 Rare 稀有度）。</summary>
    private async Task BuyCards()
    {
        if (!await TryPay("BuyCardsCost"))
            return;

        // 玩家反馈：这里原本用 ForNonCombatWithDefaultOdds 出的是"常规（按稀有度权重）"奖励，
        // 与文案「获得一组稀有卡牌奖励」不符。改成按稀有度过滤 + 等概率，只出 Rare。
        var options = CardCreationOptions
            .ForNonCombatWithUniformOdds([Owner!.Character.CardPool], c => c.Rarity == CardRarity.Rare)
            .WithFlags(CardCreationFlags.NoRarityModification);

        await RewardsCmd.OfferCustom(Owner!, [new CardReward(options, 3, Owner)]);
        SetEventFinished(PageDescription("DONE"));
    }

    /// <summary>买点装备：100 金币换一个随机罕见遗物。</summary>
    private async Task BuyUncommon()
        => await BuyRelic("BuyUncommonCost", RelicRarity.Uncommon);

    /// <summary>买点超厉害装备：120 金币换一个随机稀有遗物。</summary>
    private async Task BuyRare()
        => await BuyRelic("BuyRareCost", RelicRarity.Rare);

    private async Task BuyRelic(string costKey, RelicRarity rarity)
    {
        if (!await TryPay(costKey))
            return;

        // 遗物池可能被抽干，必须判空。
        RelicModel? relic = RelicFactory.PullNextRelicFromFront(Owner!, rarity, _ => true)?.ToMutable();
        if (relic != null)
            await RelicCmd.Obtain(relic, Owner!);

        SetEventFinished(PageDescription("DONE"));
    }

    /// <summary>
    /// 付钱。调用前该商品已经按金币「锁定/解锁」过（买不起的选项是锁定的、点不动），
    /// 所以正常流程不会走到"钱不够"这一支；这里保留一道兜底：
    /// 真的不够时一分钱不扣、不给奖励，进入「买不起」页，绝不白扣玩家的钱。
    /// 钱够时扣钱并返回 true，调用方再去发奖励。
    /// </summary>
    private async Task<bool> TryPay(string costKey)
    {
        decimal cost = DynamicVars[costKey].BaseValue;
        if (Owner!.Gold < cost)
        {
            SetEventState(PageDescription("NO_GOLD"),
            [
                new EventOption(this, LeaveNoGold, ModOptionKey("NO_GOLD", "LEAVE2"), true, true),
            ]);
            return false;
        }

        await PlayerCmd.LoseGold(cost, Owner, GoldLossType.Spent);
        return true;
    }

    private async Task LeaveNoGold()
    {
        await Cmd.CustomScaledWait(0.3f, 0.5f);
        SetEventFinished(PageDescription("DONE"));
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("DONE"));
        return Task.CompletedTask;
    }
}
