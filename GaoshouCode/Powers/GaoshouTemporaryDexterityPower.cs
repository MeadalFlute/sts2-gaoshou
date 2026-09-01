using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
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

// 临时敏捷（自定义）：等同于敏捷，但：
//  - 回合结束时：移除等量敏捷，然后本层数减半。
//  - 回合开始时：由 AfterSideTurnStart 把剩余层数对应的敏捷补回（per-owner，多人安全）。
// 与临时力量同构，调用侧统一走静态助手 GrantAsync（敏捷 + 临时敏捷一起授予）。
[RegisterPower]
public sealed class GaoshouTemporaryDexterityPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/temp_dexterity.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/temp_dexterity.png");

    /// <summary>
    /// 授予临时敏捷：必须同时授予等量敏捷（回合结束会移除等量敏捷）。
    /// </summary>
    public static async Task GrantAsync(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0)
            return;

        // 遵循古道：持有该能力且星辉>=1 → 消耗 1 星辉，改为 +1 基础敏捷。
        if (target.Player != null
            && target.Powers.OfType<OldWayPower>().Any()
            && target.Player.PlayerCombatState!.Stars >= 1)
        {
            await PlayerCmd.LoseStars(1, target.Player);
            await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), target, 1m, applier, cardSource, true);
            return;
        }

        await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), target, amount, applier, cardSource, true);
        await PowerCmd.Apply<GaoshouTemporaryDexterityPower>(choiceContext, target, amount, applier, cardSource);
    }

    // 回合结束（本方回合）：移除等量敏捷，然后本层数减半。
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
            return;

        // 神经超频器：持有且 >3 层 → 仅失去 1 层 + 失去 1 点生命（不再减半）。
        if (Owner.Player?.Relics.Any(r => r is Sandevistan) == true && Amount > 3)
        {
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner, -1m, Owner, null);
            Amount -= 1;
            InvokeDisplayAmountChanged();
            await CreatureCmd.Damage(choiceContext, Owner, 1m,
                ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, Owner, null, null);
            return;
        }

        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner, -Amount, Owner, null);
        var halved = (int)System.Math.Floor(Amount / 2m);
        if (halved <= 0)
        {
            await PowerCmd.Remove(this);
            return;
        }
        Amount = halved;
    }

    // 回合开始（本方回合）：把剩余层数对应的敏捷补回（多人时只在自身回合开始补发）。
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
            return;
        if (Amount <= 0)
            return;
        await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), Owner, Amount, Owner, null);
    }
}