using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 狠狠打击：攻击（罕见）。耗 4 能量。移除目标的护甲；造成 30(38) 点伤害；施加 3(5) 层易伤。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class HeavyStrick : ModCardTemplate
{
    private const int BaseEnergyCost = 4;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：易伤（游戏能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(30m, ValueProp.Move),
        ModCardVars.Power<VulnerablePower>(3),
    ];

    public HeavyStrick() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var enemy = cardPlay.Target;

        // 移除目标的护甲。
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(choiceContext, enemy, enemy.Block, Owner.Creature);

        // 造成 14(18) 点伤害；施加 3(5) 层易伤。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(enemy).Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy,
            DynamicVars.GetRequired<PowerVar<VulnerablePower>>("VulnerablePower").BaseValue,
            Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(8);                                       // 14 -> 18
        DynamicVars.GetRequired<PowerVar<VulnerablePower>>("VulnerablePower").UpgradeValueBy(2); // 3 -> 5
    }
}