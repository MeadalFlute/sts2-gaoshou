using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 震撼拳：攻击（稀有）。耗 1 能量 1 星辉。造成 12(20) 点伤害；流转（默认直接触发）：击晕目标。消耗。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class StunningFist : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/StunningFist.png");

    // 悬浮释义：击晕。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.Static(MegaCrit.Sts2.Core.HoverTips.StaticHoverTip.Stun),
    ];

    // 流转就绪（颜色与上一张牌完全不同）时泛橙光。
    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowReady(this);

    // 词条：消耗。流转（按颜色判定触发，满足时击晕）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
        GaoshouKeyword.Flow,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(12m, ValueProp.Move),
    ];

    public StunningFist() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 1 能量 1 星辉。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 流转（颜色与上一张牌完全不同时触发）：击晕目标。
        if (GaoshouFlowTracker.IsFlowReady(this))
            await CreatureCmd.Stun(cardPlay.Target, null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(8);   // 12 -> 20
    }
}
