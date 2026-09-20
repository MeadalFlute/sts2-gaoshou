using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 装弹：状态（无色衍生）。耗 1 能量 1 辉星。把**生成它的那一张**双持冲锋枪从消耗牌堆拿回手牌（按 PairId 精确配对）。
[RegisterCard(typeof(TokenCardPool))]
public sealed class Reload : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Status;
    private const CardRarity CardRarityValue = CardRarity.Token;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Reload.png");

    // 悬浮释义：战斗中**精确指向"生成它的那一张"双持冲锋枪**（含它自己的升级状态）；
    // 局外（百科/牌组等没有 Owner 或没有配对信息的场景）回落成常规的双持冲锋枪。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            var paired = FindPairedDualSmg();
            return [paired != null ? HoverTipFactory.FromCard(paired) : HoverTipFactory.FromCard<DualSMG>()];
        }
    }

    /// <summary>
    /// 按 PairId 在消耗牌堆里找回"生成它的那一张"双持冲锋枪；找不到（或不在战斗中）返回 null。
    /// 用实例本身构造释义，所以那张冲锋枪是不是升级版、被谁改过，都如实显示。
    /// </summary>
    private CardModel? FindPairedDualSmg()
    {
        if (Owner == null
            || !DynamicVars.TryGetValue("PairId", out var mine) || (int)mine.BaseValue <= 0)
            return null;

        var pairId = (int)mine.BaseValue;
        var dualSmgId = ModelDb.GetId(typeof(DualSMG));

        return PileType.Exhaust.GetPile(Owner)?.Cards.FirstOrDefault(c =>
            c.Id == dualSmgId
            && c.DynamicVars.TryGetValue("PairId", out var source)
            && (int)source.BaseValue == pairId);
    }

    /// <summary>装弹不可升级：它对回收的影响改由 PairId（生成它的那张冲锋枪写入）承载，
    /// 这样局内任何升级/降级效果都不会再改变它的行为。</summary>
    public override int MaxUpgradeLevel => 0;

    public override int CanonicalStarCost => 1;

    public Reload() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    // 返回 1 张。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Cards(1),
        // 配对编号：由生成它的那张双持冲锋枪写入（两边写同一个数）；回收时按它精确找回那一张。
        ModCardVars.Int("PairId", 0),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 只回收"生成它的那一张具体实例"：按 PairId 精确配对（该编号由生成它的双持冲锋枪写在双方实例上）。
        // 不再看装弹自身的升级状态，也不再顺手把回收来的冲锋枪升级 —— 局内升级/降级效果不再影响回收。
        var pairId = (int)DynamicVars.GetRequired<IntVar>("PairId").BaseValue;
        if (pairId <= 0)
            return;   // 不是由冲锋枪生成的装弹：无事发生。

        var dualSmgId = ModelDb.GetId(typeof(DualSMG));

        for (var i = 0; i < DynamicVars.Cards.IntValue; i++)
        {
            var exhaust = PileType.Exhaust.GetPile(Owner)?.Cards
                .Where(c => c.Id == dualSmgId
                            && c.DynamicVars.TryGetValue("PairId", out var sourcePair)
                            && (int)sourcePair.BaseValue == pairId)
                .ToList() ?? [];
            if (exhaust.Count == 0)
                return;   // 配对的冲锋枪不在消耗牌堆：无事发生。

            await CardPileCmd.Add(exhaust[0], PileType.Hand);
        }
    }

    // 装弹已设为不可升级（MaxUpgradeLevel == 0），故不再有 OnUpgrade 逻辑。
}