using MegaCrit.Sts2.Core.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
    using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
    using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 浮光掠影（能力）：每当你通过打出的卡牌对敌人造成伤害时，获得与当前层数等量的格挡。
// 每当你受到未被格挡的伤害时，层数 -1（归零移除）。
[RegisterPower]
public sealed class LoomingPresencePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 每次受到未被格挡的伤害时减少的层数。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("LossPerHit", 1),
    ];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/loomingpresence.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/loomingpresence.png");

    // 仅响应"打向敌方"的卡牌伤害。
    //
    // ⚠️ 不能只判断 dealer / cardSource：状态牌（灼伤 BURN / 感染 INFECTION / 凋萎 WITHER / 腐朽 DECAY）
    // 的伤害是"玩家打给自己"的，但 CreatureCmd.Damage(ctx, target, damageVar, cardSource, cardPlay)
    // 会把 dealer 记成 cardSource.Owner.Creature（= 玩家本人），cardSource 又是那张状态牌 ——
    // 两个条件都会成立，于是"挨灼伤"也会叠格挡（2026-09-22 据 bug report 修复）。
    // 原版同类能力（EnvenomPower / PaperCutsPower）靠 props.IsPoweredAttack() 排除 Unpowered 的状态牌伤害；
    // 我们按设计要算"任意卡牌伤害"，所以改用目标阵营判断。
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature? target, CardModel? cardSource)
    {
        if (dealer != Owner || cardSource == null || Amount <= 0)
            return;
        // 目标必须是敌方（排除自伤、以及联机里误伤队友/宠物）。
        if (target == null || target.Side == Owner.Side)
            return;
        // 不吃敏捷（Unpowered）。
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move | ValueProp.Unpowered, null);
    }

    // 每当你受到未被格挡的伤害时：层数 -1（归零移除）。
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature? target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0)
            return;
        var loss = DynamicVars.GetRequired<IntVar>("LossPerHit").IntValue;
        if (Amount <= loss)
        {
            await PowerCmd.Remove(this);
            return;
        }
        Amount -= loss;
        InvokeDisplayAmountChanged();
    }

    /// <summary>
    /// 卡面上的能力悬浮释义走的是这里（HoverTipFactory.FromPower），不是 loc 表的自动注入路径，
    /// 所以必须自己把 DynamicVars 加进去；否则会出现 {LossPerHit} 原文。
    /// </summary>
    public override LocString Description
    {
        get
        {
            var text = base.Description;
            DynamicVars.AddTo(text);
            return text;
        }
    }
}
