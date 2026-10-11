using MegaCrit.Sts2.Core.Commands;
using STS2RitsuLib.Cards.DynamicVars;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 狙击枪：技能（罕见）。耗 1 能量 0 辉星。
// 【2026-09-29 用户调整】**第一行新增**：获得 3 层临时力量（临时力量 = GaoshouTemporaryStrengthPower，
//   经 GrantAsync 同时给等量 StrengthPower + 临时力量计数，回合结束回收 —— 与本 mod 其它"临时力量"卡一致）。
// 第二行（原有，未动）：造成当前力量 3 倍的伤害。其余（消耗 / 升级后幻影 / 战斗内伤害预览）全部原样保留。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class SniperRifle : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：临时力量（本 mod 能力）+ 力量（游戏能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
    ];

    // 消耗；升级后追加幻影（在 OnUpgrade 里 AddKeyword，避免 IsUpgraded 惰性缓存问题）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,

        CardKeyword.Exhaust,

        CardKeyword.Retain, // 追加保留
    ];

    // 计算伤害：基础 0 + 1 × 乘数函数（= 手算值，见下）。
    // 描述 {CalculatedDamage:diff()} 显示战斗内伤害预览。
    // 【2026-10-06 用户调整】预览必须**算上本牌自己给的 3 层临时力量**（出牌顺序是从上到下，
    //   第一行的临时力量在伤害之前结算 ⇒ 实打伤害 = (力量 + 3) × 3）。
    //
    // 【2026-10-12 BUG 修复】用户实测：1 点力量时预览显示 13、实伤 16，而预期都是 (1+3)×3 = 12。
    //   根因：**原版伤害管线会自己再加一次力量** —— Hook.ModifyDamage 会调用
    //   `StrengthPower.ModifyDamageAdditive`，而它只在 `props.IsPoweredAttack()`
    //   （= 带 `ValueProp.Move` 且**不带** `ValueProp.Unpowered`）时生效；
    //   本卡用的是手算力量（`GetPowerAmount<StrengthPower>()`），于是力量被算了两次：
    //     * 实伤 = 3×力量(4) + 力量(4) = 16 ⇒ 等于"4 倍力量" ✗
    //     * 预览 = 3×(1+3) + 力量(1) = 13 ⇒ 多算 1 次 ✗
    //   修法：手算时**先扣掉引擎一定会补的那一次力量**，其余（ValueProp.Move ⇒ 易伤/虚弱/活力等
    //   互动）全部保持原样 ✓：
    //     * 出牌：给完临时力量后力量 = s+t ⇒ 基础伤害 = N(s+t) − (s+t) = (N−1)(s+t)，
    //             引擎再补 (s+t) ⇒ 合计 N(s+t) ✓
    //     * 预览：预览发生在出牌**之前**，引擎只会补"当前力量 s" ⇒ 乘数函数取 N(s+t) − s
    //             = (N−1)s + N·t（N = 卡面上的 Multiplier = 3），引擎再补 s ⇒ N(s+t) ✓
    //   ⇒ 预览与实伤都是 N × 力量 ✓（本卡是全动态伤害，按原版 BodySlam 的写法：base 0 + ExtraDamage 1 ✓）
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 【2026-09-29 用户调整】第一行：获得 3 层临时力量（变量名与其它临时力量卡一致：TemporaryStrength）。
        ModCardVars.Int("TemporaryStrength", 3),
        new CalculationBaseVar(0m),
        new ExtraDamageVar(1m),
        ModCardVars.Int("Multiplier", 3),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(
            static (card, target) =>
            {
                var times = card.DynamicVars.GetRequired<IntVar>("Multiplier").BaseValue;
                var temporary = card.DynamicVars.GetRequired<IntVar>("TemporaryStrength").BaseValue;
                // (N−1)×当前力量 + N×本牌临时力量 —— 引擎随后会再补一次当前力量，合计正好 N×(力量+临时) ✓
                return card.Owner!.Creature.GetPowerAmount<StrengthPower>() * (times - 1m) + temporary * times;
            }),
    ];

    public SniperRifle() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 双资源费用（1 能量 + 1 辉星）见下面的 CanonicalStarCost。
    }

    // 【2026-09-29 用户调整】费用 = 1 能量 + 1 辉星。
    // 写法照本 mod 的双资源卡范例 DualSMG（Cards/DualSMG.cs:100-101）与 Reload（Cards/Reload.cs:66）：
    // 能量走基类 BaseEnergyCost，辉星覆写 CanonicalStarCost。
    // 仓库惯例：升级只加效果、**不降费**（DualSMG 升级只 +1 Times、装弹费用不变）⇒ 本卡升级后仍是 1/1 ✓。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 【2026-09-29 用户调整】第一行：获得 3 层临时力量。
        // 走本 mod 统一的 GaoshouTemporaryStrengthPower.GrantAsync（内部先给等量 StrengthPower 再记临时力量层数，
        // 回合结束回收；与 HorseSlayer.cs:70-71 / OneinchPunch.cs:60-61 / TachyonLance.cs:167-168 同一写法）。
        // ⚠️ 放在伤害之前 ⇒ 下面那句"当前力量 3 倍"会**算上刚获得的这份临时力量**（卡面从上到下结算）。
        await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("TemporaryStrength").BaseValue, Owner.Creature, this);

        // 第二行（原有、未改设计）：造成当前力量 N 倍的伤害。
        // ⚠️ strength 取的是**给完临时力量之后**的值（卡面从上到下结算）；
        //    引擎随后按 ValueProp.Move 自动再补一次力量 ⇒ 这里先扣掉那一次，合计才是 N 倍 ✓（见 CanonicalVars 注释）
        var strength = Owner.Creature.GetPowerAmount<StrengthPower>();
        var times = DynamicVars.GetRequired<IntVar>("Multiplier").BaseValue;
        var damage = strength * (times - 1m);
        if (damage <= 0)
            return;

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 升级后获得幻影（直接改实例词条）。
        AddKeyword(GaoshouKeyword.Phantom);
    }
}