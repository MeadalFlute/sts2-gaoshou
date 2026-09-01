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

// 武学宗师：能力（稀有）。耗 1 能量 1 星辉。
// 每当你触发【流转】后，获得 1（2）层「临时力量」。升级后获得"固有"。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class GrandMaster : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：流转（词条）、临时力量（能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Flow),
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
    ];

    public GrandMaster() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 1 能量 1 星辉。
    public override int CanonicalStarCost => 1;

    // 临时力量层数 1（升级 2）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("TempStrengthGain", 1),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 施加能力：触发流转后获得 1（2）层临时力量。
        await PowerCmd.Apply<GrandMasterPower>(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("TempStrengthGain").BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级不再追加固有。
        DynamicVars.GetRequired<IntVar>("TempStrengthGain").UpgradeValueBy(1);   // 1 -> 2
    }
}