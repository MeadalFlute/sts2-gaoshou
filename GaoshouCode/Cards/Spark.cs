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

// 火花：能力（罕见）。耗 1 能量。
// 你每打出 5（升级 4）张【临时】牌，获得 1 能量、1 星辉。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Spark : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.BluePurple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：临时（词条）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Temporary),
    ];

    // 能量/星辉图标变量、触发阈值 Count（描述用 {Energy:energyIcons()}、{Stars:starIcons()}、{Count:diff()}）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Count", 5),
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Stars("Stars", 1),
    ];

    public Spark() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 施加能力：计数 buff（Amount=0 起步，每 Count 张临时牌结算）。
        // 以 1 层起步（Apply 0 层不会被挂载）；阈值为卡牌的 Count 变量（升级 5->4）。
        var power = await PowerCmd.Apply<SparkPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        if (power != null)
            power.SetThreshold((int)DynamicVars.GetRequired<IntVar>("Count").BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("Count").UpgradeValueBy(-1);   // 阈值 5 -> 4（不再获得固有）
    }
}