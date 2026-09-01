using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 铁山靠：攻击（普通）。耗 1 能量。你每有 3 点格挡，就对目标敌人造成 4(6) 点伤害。
// 描述内嵌战斗伤害预览（{CalculatedDamage:diff()}）＝floor(格挡/3) × 4。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class MountainLean : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/MountainLean.png");

    // 计算伤害：基础 0 + (格挡/3) × ExtraDamage(4->6)，战斗内实时预览。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0m),
        new ExtraDamageVar(4m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(
            static (card, target) => (int)(card.Owner!.Creature.Block / 3m)),
    ];

    public override int CanonicalStarCost => 2;

    public MountainLean() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        var block = Owner.Creature.Block;
        int times = (int)(block / 3m);
        if (times <= 0) return;

        await DamageCmd.Attack(times * DynamicVars.ExtraDamage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.ExtraDamage.UpgradeValueBy(2m);   // 每层伤害 4 -> 6
    }
}
