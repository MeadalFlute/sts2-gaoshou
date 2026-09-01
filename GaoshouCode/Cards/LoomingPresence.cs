using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 浮光掠影：能力（稀有）。耗 1 能量。每当你对敌人造成伤害时，获得 3(4) 层格挡。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class LoomingPresence : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<LoomingPresencePower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("FlowGain", 2),
    ];

    public LoomingPresence() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得 1 层浮光掠影。
        await PowerCmd.Apply<LoomingPresencePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        // 流转（颜色与上一张牌完全不同）：额外获得 2(3) 层。
        if (GaoshouFlowTracker.IsFlowReady(this))
            await PowerCmd.Apply<LoomingPresencePower>(choiceContext, Owner.Creature,
                DynamicVars.GetRequired<IntVar>("FlowGain").BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("FlowGain").UpgradeValueBy(1);   // 2 -> 3
    }
}