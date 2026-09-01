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

// 三尖两刃枪：攻击（稀有）。耗 1 能量。对随机一名敌人造成 6(10) 点伤害。回响、幻影（均为词条，暂未实装）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TriPointDoubleEdgedSpear : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Colorless;

    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.RandomEnemy;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/TriPointDoubleEdgedSpear.png");

    // 词条：回响（打出后回到手牌）、幻影（加入一张费用-1消耗的复制品）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Echo,
        GaoshouKeyword.Phantom,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
    ];

    public TriPointDoubleEdgedSpear() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 对随机一名敌人造成伤害。RandomEnemy 目标由游戏选出并放入 cardPlay.Target。
        var enemy = cardPlay.Target;
        if (enemy == null)
        {
            var enemies = this.CombatState?.HittableEnemies.ToList() ?? [];
            if (enemies.Count == 0)
                return;
            enemy = Owner.RunState.Rng.CombatTargets.NextItem(enemies)!;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(enemy)
            .Execute(choiceContext);

        // 回响：打出后回到手牌（仅当本卡持有【回响】词条时——
        // 幻影复制品已移除该词条，故复制品打出后不再回手，只正常进消耗堆）。
        if (Keywords.Contains(GaoshouKeyword.Echo))
            await CardPileCmd.Add(this, PileType.Hand, CardPilePosition.Top, this, false);

        // TODO(幻影): 加入一张费用-1、无幻影、带消耗的临时复制品（需要卡牌复制 API；暂未实现）。
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);   // 6 -> 10
    }
}
