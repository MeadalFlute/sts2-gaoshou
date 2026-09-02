using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 旋风斧：攻击（普通）。耗 1 能量 1 星辉。获得 6(9) 层临时力量，对随机敌人造成 8(12) 点伤害。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class FlyingAxe : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy; // 手动应用修复
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/FlyingAxe.png");

    // 悬浮释义：易伤（施加给目标）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Power<VulnerablePower>(2),
        new DamageVar(8m, ValueProp.Move),
    ];

    public FlyingAxe() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // AnyEnemy 目标必然存在（UI 强制选敌）；兜底防分析器/极端路径。
        if (cardPlay.Target is not { } target)
            return;

        // 普通攻击：对所选目标造成 8(12) 点伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);

        // 施加 2(3) 层易伤。
        await PowerCmd.Apply<VulnerablePower>(choiceContext, target,
            DynamicVars.GetRequired<PowerVar<VulnerablePower>>("VulnerablePower").BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<PowerVar<VulnerablePower>>("VulnerablePower").UpgradeValueBy(1);   // 2 -> 3
        DynamicVars.Damage.UpgradeValueBy(4);                                  // 8 -> 12
    }
}
