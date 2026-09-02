using System.Linq;
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

// 混乱打击：攻击（普通）。耗 2 能量 0 星辉（升级后伤害 3->4、次数 4->5）。
// 对随机敌人造成 4(5) 次 3(4) 点伤害。词条：幻影。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class ChaosStrike : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.RandomEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 词条：幻影（自定义词条）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Phantom,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        ModCardVars.Int("Times", 4),
    ];

    public ChaosStrike() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉：不覆写 CanonicalStarCost，保持默认"无星辉费用"。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 对随机敌人造成 Times 次伤害（弹射同款：每次命中独立随机，单次 Execute）。
        var times = DynamicVars.GetRequired<IntVar>("Times").BaseValue;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount((int)times)
            .FromCard(this, cardPlay)
            .TargetingRandomOpponents(this.CombatState)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1);                       // 3 -> 4
        DynamicVars.GetRequired<IntVar>("Times").UpgradeValueBy(1); // 4 -> 5
    }
}