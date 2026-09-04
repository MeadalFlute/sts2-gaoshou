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

// 双鞭：攻击（罕见）。耗 1 能量 1 星辉。造成 6(8) 点伤害；对随机敌人造成 6(8) 点伤害。流转：获得 1 能量、1 星辉。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class DoalBian : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/DoalBian.png");

    // 流转就绪（颜色与上一张牌完全不同）时泛橙光。
    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowReady(this);

    // 词条：流转（供流转系能力检测）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new DamageVar("secondHit", 5m, ValueProp.Move),
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Stars("Stars", 1),
    ];

    public DoalBian() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);

        var enemies = (this.CombatState?.HittableEnemies ?? []).ToList();
        var random = Owner.RunState.Rng.CombatTargets.NextItem(enemies)!;
        if (random != null)
            await DamageCmd.Attack(DynamicVars.GetRequired<DamageVar>("secondHit").BaseValue)
                .FromCard(this, cardPlay).Targeting(random).Execute(choiceContext);

        // 流转（颜色与上一张牌完全不同时触发）：获得 1 能量、1 星辉。
        if (GaoshouFlowTracker.IsFlowReady(this))
        {
            await PlayerCmd.GainEnergy(DynamicVars.GetRequired<EnergyVar>("Energy").BaseValue, Owner);
            await PlayerCmd.GainStars(DynamicVars.GetRequired<StarsVar>("Stars").BaseValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);                          // 3 -> 4
        DynamicVars.GetRequired<DamageVar>("secondHit").UpgradeValueBy(2);   // 5 -> 6
    }
}
