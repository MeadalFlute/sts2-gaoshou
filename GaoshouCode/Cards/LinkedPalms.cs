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

// 连还手：技能。耗 1（升级后 0）能量 0 星辉。获得 1 能量、1 星辉。固有、消耗。
[RegisterCard(typeof(GaoshouCardPool))]
[RegisterCharacterStarterCard(typeof(GaoshouCharacter), 1, Order = 40)]
public sealed class LinkedPalms : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Basic;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 固有：开局在手牌；消耗：打出后移除。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Innate,
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Stars("Stars", 1),
    ];

    public LinkedPalms() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉：不覆写 CanonicalStarCost。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(DynamicVars.GetRequired<EnergyVar>("Energy").BaseValue, Owner);
        await PlayerCmd.GainStars(DynamicVars.GetRequired<StarsVar>("Stars").BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);   // 1 能量 -> 0 能量
        DynamicVars.GetRequired<StarsVar>("Stars").UpgradeValueBy(1);   // 1 星辉 -> 2 星辉
    }
}
