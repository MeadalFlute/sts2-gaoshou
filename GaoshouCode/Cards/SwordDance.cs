using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 剑舞（先古）：能力。耗 3 能量（升级 2）。
// 每当你触发【流转】或【风暴】效果时：抽 2 张牌，获得 1 能量。由尘封魔典获取（取代原"占位"）。
[RegisterCard(typeof(ColorlessCardPool))]
public sealed class SwordDance : ModCardTemplate
{
    private const int BaseEnergyCost = 3;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Ancient;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedPurple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：流转、风暴（自定义词条）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Flow),
        HoverTipFactory.FromKeyword(GaoshouKeyword.Storm),
    ];

    // 显示变量（效果在能力里）：抽牌数 + 能量图标。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("draw", 1),
        ModCardVars.Energy("EnergyGain", 1),
    ];

    public SwordDance() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SwordDancePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);   // 3 -> 2
    }
}