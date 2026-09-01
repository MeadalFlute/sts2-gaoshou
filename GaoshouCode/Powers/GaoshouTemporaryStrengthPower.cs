using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 临时力量（自定义）：等同于力量，但：
//  - 回合结束时：移除等量力量，然后本层数减半。
//  - 回合开始时：由本能力的 AfterSideTurnStart 把剩余层数对应的力量补回（per-owner，多人安全）。
// 注意：PowerCmd 的"叠加路径（ModifyAmount）"不会调用 BeforeApplied，
// 若在 BeforeApplied 里同步力量，第二次及以后的授予不会给力量，回合末却会扣全量力量导致负数。
// 因此这里【不做】自动同步，改为调用侧统一走静态助手 GrantAsync（力量 + 临时力量一起授予）。
[RegisterPower]
public sealed class GaoshouTemporaryStrengthPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/temp_strength.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/temp_strength.png");

    /// <summary>
    /// 授予临时力量：必须同时授予等量力量（回合结束会移除等量力量）。
    /// 所有"获得临时力量"的调用点都应走这里，避免力量与临时力量不同步。
    /// </summary>
    public static async Task GrantAsync(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0)
            return;

        // 遵循古道：持有该能力且星辉>=1 → 消耗 1 星辉，改为 +1 基础力量（不获得临时力量）。
        if (target.Player != null
            && target.Powers.OfType<OldWayPower>().Any()
            && target.Player.PlayerCombatState!.Stars >= 1)
        {
            await PlayerCmd.LoseStars(1, target.Player);
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), target, 1m, applier, cardSource, true);
            return;
        }

        // 先给等量力量（静默，避免双闪），再给临时力量计数。
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), target, amount, applier, cardSource, true);
        await PowerCmd.Apply<GaoshouTemporaryStrengthPower>(choiceContext, target, amount, applier, cardSource);
    }

    // 回合结束（本方回合）：移除等量力量，然后本层数减半。
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side) return;
        // 多人：只在自己回合结束时结算（participants 为正在结束回合的 Creature）。
        if (!participants.Contains(Owner)) return;

        // 神经超频器：持有且 >3 层 → 仅失去 1 层 + 失去 1 点生命（不再减半）。
        if (Owner.Player?.Relics.Any(r => r is Sandevistan) == true && Amount > 3)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, -1m, Owner, null);
            Amount -= 1;
            InvokeDisplayAmountChanged();
            await CreatureCmd.Damage(choiceContext, Owner, 1m,
                ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, Owner, null, null);
            return;
        }

        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, -Amount, Owner, null);
        var halved = (int)Math.Floor(Amount / 2m);
        if (halved <= 0)
        {
            await PowerCmd.Remove(this);
            return;
        }
        Amount = halved;
    }

    // 回合开始（本方回合）：把剩余层数对应的力量补回（多人时只在自身回合开始补发）。
    // 原先用全局 SideTurnStartedEvent + PlayerCreatures.FirstOrDefault() 订阅，
    // 多人客机时 FirstOrDefault 拿到主机导致客机不补发——改为能力自身钩子，per-owner 判定。
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side) return;
        if (!participants.Contains(Owner)) return;
        if (Amount <= 0) return;

        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner, Amount, Owner, null);
    }
}