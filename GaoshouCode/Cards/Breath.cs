using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 呼吸：技能（罕见）。耗 1 能量（升级 0）。
// 若被打出：抽 4 张牌，消耗。若被弃置：获得 1 能量（不消耗）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Breath : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedBlue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Breath.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    // 能量图标变量（弃置奖励）+ 抽牌数。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Int("Cards", 4),
    ];

    public Breath() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 若被打出：抽 4 张牌（消耗由 Exhaust 词条处理）。
        await CardPileCmd.Draw(choiceContext, DynamicVars.GetRequired<IntVar>("Cards").BaseValue, Owner);
    }

    // 若被弃置：获得 1 能量（不触发抽牌/消耗）。
    public override Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (card != this)
            return Task.CompletedTask;
        return PlayerCmd.GainEnergy(1, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);   // 能量 1 -> 0
    }
}