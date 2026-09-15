using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 卸力：技能（普通）。耗 0 能量 2 辉星。获得 7(12) 格挡。流转：对所有敌人施加 1(2) 层虚弱。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class DissipateForce : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override bool GainsBlock => true;

    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowGlowReady(this);

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/DissipateForce.png");

    // 悬浮释义：虚弱（游戏能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move),
        ModCardVars.Power<WeakPower>(1),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
    ];

    public DissipateForce() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override int CanonicalStarCost => 2;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (GaoshouFlowTracker.IsFlowReady(this))
            foreach (var enemy in this.CombatState?.HittableEnemies ?? [])
                await PowerCmd.Apply<WeakPower>(choiceContext, enemy, DynamicVars.GetRequired<PowerVar<WeakPower>>("WeakPower").BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(5m);                                       // 7 -> 12
        DynamicVars.GetRequired<PowerVar<WeakPower>>("WeakPower").UpgradeValueBy(1); // 1 -> 2
    }
}
