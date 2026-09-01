using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 枪斗术：能力（稀有）。耗 1 能量 1 星辉。每当你触发【流转】后，获得 4 点格挡。虚无（升级后移除）。
// 流转机制暂未实装，简化为"每当你打出带流转词条的牌后"触发。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class GunFu : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.RedBlue;

    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

        protected override IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("BlockGain", 3),
    ];

public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 虚无：回合结束若在手牌则移除。升级后移除虚无。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
    ];

    public GunFu() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 1 能量 1 星辉。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<GunFuPower>(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("BlockGain").BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("BlockGain").UpgradeValueBy(1);   // 3 -> 4
    }
}
