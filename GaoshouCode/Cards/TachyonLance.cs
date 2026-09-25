using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Patches;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 速子长矛：攻击（稀有）。X 辉星费用（0 能量）。
// 对随机敌人造成 2 点伤害，获得 1 层临时力量，重复 X 次；本场每触发一次奇迹，额外攻击一次（X +1）
// ⇒ 命中数 = X + 本场奇迹次数（奇迹次数为 0 时就是普通的 X 连击；参考铁甲战士-扯碎）。
//
// 【2026-09-24 平衡调整】原效果是"本场每触发一次奇迹就**重放整张牌一次**"（命中数 = X × 奇迹次数），
// 奇迹攒起来之后是乘算爆炸；现改成加算 —— 每次奇迹只让 X +1（多打一下）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TachyonLance : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.RandomEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

        // 悬浮释义：临时力量、奇迹。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
        HoverTipFactory.FromKeyword(GaoshouKeyword.Miracle),
    ];

public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        ModCardVars.Int("TemporaryStrength", 1),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        // 额外命中数 = 本场奇迹次数（每次奇迹让 X +1 ⇒ 多打一下）。
        new CalculatedVar("CalculatedHits").WithMultiplier(static (card, _) =>
            MiracleCounter.GetMiracleCount(card.Owner!)),
        // 预览用：本场奇迹触发次数（= 额外攻击次数）。
        new CalculatedVar("MiracleCount").WithMultiplier(static (card, _) =>
            MiracleCounter.GetMiracleCount(card.Owner!)),
        // 预览用：本回合预计总次数（X + 奇迹次数）。
        new CalculatedVar("TotalHits").WithMultiplier(static (card, _) =>
            ResolveX(card) + MiracleCounter.GetMiracleCount(card.Owner!)),
        // 预览用：本回合预计总伤害。
        // 单段伤害走引擎自己的预览管线 Hook.ModifyDamage(..., CardPreviewMode.Normal)，
        // 所以力量/活力/双倍伤害/其它 mod 加的 buff 全部自动算在内；
        // 又因为本卡每段结束会给自己 +1 层临时力量 → 伤害是等差数列，
        // 总伤 = N×单段 + N(N−1)/2（N 段的递增），再加上第 2 段起手工补的活力 (N−1)×vigor。
        new CalculatedVar("CalculatedTotal").WithMultiplier(static (card, target) =>
        {
            var owner = card.Owner;
            var state = card.CombatState;
            if (owner == null || state == null)
                return 0m;

            var perHit = Hook.ModifyDamage(owner.RunState, state, target, owner.Creature,
                card.DynamicVars.Damage.BaseValue, ValueProp.Move, card, null,
                ModifyDamageHookType.All, CardPreviewMode.Normal, out _);

            var hits = ResolveX(card) + MiracleCounter.GetMiracleCount(owner);
            if (hits <= 0 || perHit <= 0m)
                return 0m;

            // 第 1 段由引擎自己吃活力；第 2 段起我们在 OnPlay 里手工补同样的层数（奇迹多打的也算）。
            var vigor = owner.Creature.GetPowerAmount<VigorPower>();
            return hits * perHit
                   + hits * (hits - 1) / 2m
                   + (hits - 1) * Math.Max(0m, vigor);
        }),
    ];

    // 虚无（升级后移除）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal,
    ];

    // X 辉星费用（RitsuLib 原生 X）。
    public override bool HasStarCostX => true;

    /// <summary>
    /// 本卡实际的 X 值（含化学物X等 X 修正）。
    ///
    /// 原版 X 费卡（如储君「星尘」Stardust）走 <c>CardModel.ResolveStarXValue()</c>
    /// = <c>Hook.ModifyXValue(CombatState, this, LastStarsSpent)</c>，化学物X 就在这条钩子上 +2；
    /// 我们直接读裸 <c>LastStarsSpent</c> 会漏掉它，所以这里统一走同一个钩子。
    ///
    /// 取值来源分两种：
    ///  - 打出中（牌已在 Play 堆）：用实付辉星 <c>LastStarsSpent</c>；
    ///  - 其余情况（手牌预览、卡库等）：用当前辉星 —— 不能用 LastStarsSpent，那是上一次打出的陈旧值
    ///    （回响/回响类把牌收回手牌后，预览会算错）。
    /// 两种情况都要过 <see cref="Hook.ModifyXValue" />，预览里的 X 才会带上化学物X 的修正。
    /// </summary>
    private static int ResolveX(CardModel card)
    {
        var spent = card.Pile?.Type == PileType.Play
            ? card.LastStarsSpent
            : (card.Owner?.PlayerCombatState?.Stars ?? 0);

        var state = card.CombatState;
        return state == null ? spent : Hook.ModifyXValue(state, card, spent);
    }

    public TachyonLance() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 总命中 = X（基础，含化学物X等修正） + 本场奇迹次数（每次奇迹让 X +1）。
        var x = ResolveX(this);
        var extra = MiracleCounter.GetMiracleCount(Owner!);
        var hits = x + extra;
        if (hits <= 0)
            return;

        // 胆小（Skittish，花园幽灵鳗）：「挨第一下后获得格挡」在原版挂在 AfterAttack 上，原版多段攻击是一条 AttackCommand，
        // 所以它在全部命中打完后才结算；我们是每命中一次一条指令 → 用这个作用域把这条反应压到全部命中打完（见 Patches/HitReactionDelay.cs）。
        // （蜷身 CurlUp 挂的是出牌结束，本来就在全部段数之后，不需要处理。）
        await using var hitReactionDelay = HitReactionDelay.Begin(choiceContext);

        // 活力补偿：X 费那几次是**分开的多次攻击**（各自一次 DamageCmd.Attack），
        // 而原版 VigorPower 的加成绑定在"一次 AttackCommand"上（出手前快照 → 每次伤害实例加 → 攻击后一次性扣光），
        // 所以只有第一段能吃到活力。这里在出手前快照层数，第 2 段起手工补上同样的加成。
        // 注：奇迹多打出来的那几下现在**也算在 X 里**（X+1），所以一并补活力 ——
        // 这与旧的"重放"版本不同（那时重放段不补，因为活力已被扣光且重放等同原版 replay）。
        var vigor = Owner!.Creature.GetPowerAmount<VigorPower>();

        // 逐命中交错循环：每次 攻击（随机索敌）→ 叠 1 层临时力量。
        var enemies = (this.CombatState?.HittableEnemies ?? []).ToList();
        for (var i = 0; i < hits; i++)
        {
            var damage = DynamicVars.Damage.BaseValue;
            if (i > 0)
                damage += vigor;

            var random = enemies.Count > 0 ? Owner.RunState.Rng.CombatTargets.NextItem(enemies) : null;
            if (random != null)
                await DamageCmd.Attack(damage)
                    .FromCard(this, cardPlay)
                    .Targeting(random)
                    .Execute(choiceContext);

            await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, Owner.Creature,
                DynamicVars.GetRequired<IntVar>("TemporaryStrength").BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后移除"虚无"（临时力量固定 1 层，不再升级加层）。
        RemoveKeyword(CardKeyword.Ethereal);
    }
}