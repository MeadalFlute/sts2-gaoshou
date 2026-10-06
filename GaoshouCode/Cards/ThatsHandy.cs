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

// 这个顺手：能力（罕见）。耗 0 能量 1 辉星（升级 0/0）。每当你**生成**一张【临时】牌后，对随机敌人造成 4（5）点伤害。
// 【2026-10-05 重做】触发点由"打出临时牌"改为"生成临时牌"（见 Powers/ThisHandyPower.cs）；伤害 3(4) → 4(5)。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class ThatsHandy : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.BluePurple;

    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/ThatsHandy.png");

    // 悬浮释义：临时（自定义词条）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Temporary),
    ];

    public ThatsHandy() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 1 辉星。
    public override int CanonicalStarCost => 1;

    // 伤害 4（升级 5）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("PowerAmount", 4),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ThisHandyPower>(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("PowerAmount").BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        UpgradeStarCostBy(-1);   // 辉星 1 -> 0（保留原有升级收益）
        DynamicVars.GetRequired<IntVar>("PowerAmount").UpgradeValueBy(1);   // 4 -> 5
    }
}
