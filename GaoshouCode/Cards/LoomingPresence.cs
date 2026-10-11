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

// 浮光掠影：能力（稀有）。耗 2 能量（升级 1）。获得 1 层浮光掠影；流转：再获得 1 层。
// 【2026-10-07 用户调整】①层数由每段 2 收紧为**每段 1**（线上实测偏强）；
//   ②**不再自带虚无**（Ethereal 移除，词条表里也不再声明）；
//   ③升级效果由"移除虚无"改为**能量花费 -1**（2 -> 1）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class LoomingPresence : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 泛光：流转就绪时（颜色与上一张牌完全不同）。
    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowGlowReady(this);

    // 词条：流转（**不再带虚无** —— 2026-10-07 用户要求移除）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<LoomingPresencePower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("FlowGain", 1),
    ];

    public LoomingPresence() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得 1 层浮光掠影。
        await PowerCmd.Apply<LoomingPresencePower>(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("FlowGain").BaseValue, Owner.Creature, this);

        // 流转（颜色与上一张牌完全不同）：再获得 1 层。
        if (GaoshouFlowTracker.IsFlowReady(this))
            await PowerCmd.Apply<LoomingPresencePower>(choiceContext, Owner.Creature,
                DynamicVars.GetRequired<IntVar>("FlowGain").BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级：能量花费 2 -> 1（层数与费用之外的一切都不变）。
        EnergyCost.UpgradeBy(-1);
    }
}
