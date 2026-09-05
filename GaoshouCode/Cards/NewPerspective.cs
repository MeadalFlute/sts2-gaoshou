using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 新视野：技能（稀有）。耗 1 能量（升级 0）。交换你的抽牌堆和弃牌堆。
// 奇迹（非回合开始抽牌进入手牌）：抽 3 张牌。消耗。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class NewPerspective : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Miracle,
        CardKeyword.Exhaust,
    ];

    // 奇迹就绪（非回合开始抽牌进入手牌）时泛橙光。
    protected override bool ShouldGlowGoldInternal => MiracleCounter.IsMiracleReady(this);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Cards", 3),
    ];

    public NewPerspective() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 交换抽牌堆与弃牌堆（保留各自原有顺序；先移弃牌→抽牌，再移原抽牌→弃牌）。
        var drawCards = PileType.Draw.GetPile(Owner)?.Cards.ToList() ?? [];
        var discCards = PileType.Discard.GetPile(Owner)?.Cards.ToList() ?? [];

        foreach (var c in discCards)
            await CardPileCmd.Add(c, PileType.Draw);
        foreach (var c in drawCards)
            await CardPileCmd.Add(c, PileType.Discard);

        // 奇迹：非回合开始抽牌进入手牌 → 抽 3 张牌。
        if (MiracleCounter.IsMiracleReady(this))
            await CardPileCmd.Draw(choiceContext, DynamicVars.GetRequired<IntVar>("Cards").BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);   // 1 -> 0
    }
}
