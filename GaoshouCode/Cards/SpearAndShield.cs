using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 枪盾：攻击。耗 1 能量 1 星辉。获得 6 格挡、造成 8 伤害。升级后：9 格挡、11 伤害。
[RegisterCard(typeof(GaoshouCardPool))]
[RegisterCharacterStarterCard(typeof(GaoshouCharacter), 1, Order = 30)]
[RegisterArchaicToothTranscendence(typeof(ChargeBlade))]   // 古老牙齿：把枪盾古化为盾斧
public sealed class SpearAndShield : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Colorless;

    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Basic;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    // 词条：保留（回合结束时留在手牌）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/SpearAndShield.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
        new DamageVar(8m, ValueProp.Move),
    ];

    public SpearAndShield() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 1 能量 1 星辉：星辉费用通过覆写 CanonicalStarCost 设置。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);           // 6 -> 9
        DynamicVars.Damage.UpgradeValueBy(3m);          // 8 -> 11
    }
}
