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
// 每当你打出【临时】牌后，随机获得 1 点能量或 1 点星辉（临时暂未实装，简化为"打出任意牌后"触发）。
// 升级后获得"固有"。
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

    // 能量/星辉图标变量（描述用 {Energy:energyIcons()}、{Stars:starIcons()}）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Stars("Stars", 1),
    ];

    public Spark() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 施加能力：计数 buff（Amount=0 起步，每 5 张临时牌结算）。
        // 以 1 层起步（Apply 0 层不会被挂载）；计数语义见 SparkPower（每 5 张 → 1 能 1 星）。
        await PowerCmd.Apply<SparkPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级后获得"固有"（直接改实例词条）。
        AddKeyword(CardKeyword.Innate);
    }
}