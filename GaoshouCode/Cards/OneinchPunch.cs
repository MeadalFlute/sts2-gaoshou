using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 寸劲：技能（罕见）。耗 2 能量（无升级费用变化）。
// 获得 2(3) 点临时力量；流转（直接触发）：获得 2 点能量。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class OneinchPunch : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/OneinchPunch.png");

    // 流转就绪（颜色与上一张牌完全不同）时泛橙光。
    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowReady(this);

    // 悬浮释义：临时力量（能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
    ];

    // 词条：流转。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("TemporaryStrength", 2),
        ModCardVars.Energy("Energy", 2),
    ];

    public OneinchPunch() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("TemporaryStrength").BaseValue, Owner.Creature, this);

        // 流转（颜色与上一张牌完全不同时触发）：获得 2 能量。
        if (GaoshouFlowTracker.IsFlowReady(this))
            await PlayerCmd.GainEnergy(DynamicVars.GetRequired<EnergyVar>("Energy").BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("TemporaryStrength").UpgradeValueBy(1);   // 2 -> 3
    }
}
