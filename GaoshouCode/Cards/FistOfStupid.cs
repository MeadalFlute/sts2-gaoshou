using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 没脑子拳：攻击（罕见）。耗 0 能量。对随机敌人造成 2 点伤害 2 次；将 1 张眩晕加入你的弃牌堆。
// 风暴（能量、能量）：满足时重复打出一次。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class FistOfStupid : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.RandomEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：眩晕（游戏原生状态牌）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<Dazed>(),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Storm,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        ModCardVars.Int("Times", 3),
        ModCardVars.Energy("EnergyA", 1),
        ModCardVars.Energy("EnergyB", 1),
    ];

    public FistOfStupid() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 风暴（能量、能量）：打出一次主效果；当前能量>=2 时额外重放一次。
        await PlayOnceAsync(choiceContext, cardPlay);
        if (Owner.PlayerCombatState!.Energy >= 2)
            await PlayOnceAsync(choiceContext, cardPlay);
    }

    private async Task PlayOnceAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var enemies = (this.CombatState?.HittableEnemies ?? []).ToList();

        // 对随机敌人造成 2 点伤害 2(3) 次。
        var times = (int)DynamicVars.GetRequired<IntVar>("Times").BaseValue;
        for (var i = 0; i < times; i++)
        {
            var random = enemies.Count > 0 ? Owner.RunState.Rng.CombatTargets.NextItem(enemies) : null;
            if (random == null)
                break;
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay).Targeting(random).Execute(choiceContext);
        }

        // 将 1 张眩晕加入你的弃牌堆（突破极限「凋萎」同款模式）。
        var dazed = Owner.Creature.CombatState?.CreateCard(ModelDb.Card<Dazed>(), Owner);
        if (dazed != null)
            CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(dazed, PileType.Discard, Owner));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("Times").UpgradeValueBy(1);   // 次数 3 -> 4（伤害固定 2）   // 2 -> 3
    }
}