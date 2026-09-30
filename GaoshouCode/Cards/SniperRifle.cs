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

    // 计算伤害：基础 0 + 3 × 当前力量（描述 {CalculatedDamage:diff()} 显示战斗内伤害预览）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 【2026-09-29 用户调整】第一行：获得 3 层临时力量（变量名与其它临时力量卡一致：TemporaryStrength）。
        ModCardVars.Int("TemporaryStrength", 3),
        new CalculationBaseVar(0m),
        new ExtraDamageVar(2m),
        ModCardVars.Int("Multiplier", 3),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(
            static (card, target) => card.Owner!.Creature.GetPowerAmount<StrengthPower>()),
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

        // 第二行（原有、未改）：造成当前力量 3 倍的伤害。
        var strength = Owner.Creature.GetPowerAmount<StrengthPower>();
        var damage = strength * DynamicVars.GetRequired<IntVar>("Multiplier").BaseValue;
        if (damage <= 0)
            return;

        await DamageCmd.Attack(damage)
            // 游戏 2026-09-30 更新后签名变化，此处同步适配
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 升级后获得幻影（直接改实例词条）。
        AddKeyword(GaoshouKeyword.Phantom);
    }
}