using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 双持冲锋枪：攻击（稀有）。耗 1 能量 1 辉星。对随机敌人造成 2(3) 点伤害，重复 4(6) 次。消耗。风暴（红&蓝）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class DualSMG : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.AllEnemies;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedBlue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：装弹（升级后：装弹+，变化目标卡）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<Reload>(),
    ];

    // 词条：风暴、消耗（打出后生成装弹加入抽牌堆）。
    // 泛光：风暴条件可触发（扣除本卡费用后）。
    protected override bool ShouldGlowGoldInternal =>
        StormGlow.Ready(this,
            (int)DynamicVars.GetRequired<EnergyVar>("EnergyStorm").BaseValue,
            (int)DynamicVars.GetRequired<StarsVar>("StarsStorm").BaseValue);

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Storm,
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        
        // 配对编号的存储位（由本实例在生成装弹时写入；卡面不引用它，所以不会显示）。
        ModCardVars.Int("PairId", 0),
        new DamageVar(2m, ValueProp.Move),
        ModCardVars.Int("Times", 3),
        ModCardVars.Energy("EnergyStorm", 1),
        ModCardVars.Stars("StarsStorm", 1),
    ];

    public DualSMG() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 风暴：重放 1。用 OnPlay 内部执行两次代替 BaseReplayCount——
    // 后者依赖 AfterCreated 设置，多人远端克隆不会执行会导致状态分歧。
    private async Task PlayOnce(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var times = DynamicVars.GetRequired<IntVar>("Times").BaseValue;
        // 逐敌依次攻击（保留演出）：每个敌人的 times 次命中在单次 Execute 内完成——
        // 该敌人全部命中结算完后才触发其受击效果，再轮到下一个敌人。
        foreach (var enemy in this.CombatState?.HittableEnemies ?? [])
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .WithHitCount((int)times)
                .FromCard(this, cardPlay)
                .Targeting(enemy)
                .Execute(choiceContext);
        }
    }

    // 装弹数量不应超过当前战斗中可由消耗区双持冲锋枪支持的数量。
    // 当前这张牌在 OnPlay 结束前尚未进入消耗区，因此目标数量需要额外加 1。
    private bool ShouldGenerateReload()
    {
        var exhaust = PileType.Exhaust.GetPile(Owner);
        var reloadId = ModelDb.GetId(typeof(Reload));
        var dualSmgId = ModelDb.GetId(typeof(DualSMG));

        var reloadCount =
            (PileType.Draw.GetPile(Owner)?.Cards.Count(c => c.Id == reloadId) ?? 0)
            + (PileType.Hand.GetPile(Owner)?.Cards.Count(c => c.Id == reloadId) ?? 0)
            + (PileType.Discard.GetPile(Owner)?.Cards.Count(c => c.Id == reloadId) ?? 0);
        var dualSmgTarget = (exhaust?.Cards.Count(c => c.Id == dualSmgId) ?? 0) + 1;

        return reloadCount < dualSmgTarget;
    }

    // 1 能量 1 辉星。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 依次攻击每个敌人（保留的演出：逐敌、逐次命中）。
        await PlayOnce(choiceContext, cardPlay);

        // 风暴（能量、辉星）：当前能量>=EnergyStorm 且 辉星>=StarsStorm 才重复打出一次。
        if (Owner.PlayerCombatState!.Energy >= (int)DynamicVars.GetRequired<EnergyVar>("EnergyStorm").BaseValue
            && Owner.PlayerCombatState.Stars >= (int)DynamicVars.GetRequired<StarsVar>("StarsStorm").BaseValue)
            await PlayOnce(choiceContext, cardPlay);

        // 仅在现有装弹数量不足时生成一张装弹（装弹本身不可升级）。
        if (ShouldGenerateReload())
        {
            var reload = Owner.Creature.CombatState?.CreateCard(ModelDb.Card<Reload>(), Owner);
            if (reload != null)
            {
                // 一对一配对：给"这张冲锋枪"与"它生成的装弹"写同一个编号（双方写成同一个数），
                // 装弹结算时按编号精确找回这一张 —— 不再依赖装弹自身的升级状态（局内升级/降级不再影响回收）。
                var pairId = NextPairId();
                DynamicVars.GetRequired<IntVar>("PairId").BaseValue = pairId;
                reload.DynamicVars.GetRequired<IntVar>("PairId").BaseValue = pairId;

                CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(reload, PileType.Draw, Owner, CardPilePosition.Random));
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("Times").UpgradeValueBy(1);    // 3 -> 4（伤害不变）
    }
    // 本场战斗内递增的配对编号（确定性：两端在同一出牌点各自 +1，结果一致；
    // 编号只在"同一实例对"之间比较，不需要跨战斗唯一）。
    private static int _nextPairId = 1;

    private static int NextPairId() => _nextPairId++;

}
