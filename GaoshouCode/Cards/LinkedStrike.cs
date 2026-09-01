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

// 连环打击：攻击。耗 2 能量 0 星辉。造成 4 伤害一次，然后造成 5 伤害一次。升级后：6、9。
[RegisterCard(typeof(GaoshouCardPool))]
[RegisterCharacterStarterCard(typeof(GaoshouCharacter), 4, Order = 10)]
public sealed class LinkedStrike : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Basic;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/LinkedStrike.png");

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    // 第一段伤害使用默认 "Damage" 变量，第二段使用具名 "secondHit"。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new DamageVar("secondHit", 5m, ValueProp.Move),
    ];

    public LinkedStrike() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉：不覆写 CanonicalStarCost，保持默认“无星辉费用”。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        await DamageCmd.Attack(DynamicVars.GetRequired<DamageVar>("secondHit").BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);                                  // 4 -> 6
        DynamicVars.GetRequired<DamageVar>("secondHit").UpgradeValueBy(2);     // 5 -> 7
    }
}
