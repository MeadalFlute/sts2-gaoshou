using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using Gaoshou.Relics;

namespace Gaoshou.Keywords;

// 风暴泛光助手：风暴在打出后才判定，因此"就绪"需先模拟打出后的资源。
// 打出会先扣能量、再扣星辉（游戏扣费顺序）；高手护符（初始遗物）会在"消耗能量/星辉"事件里互抵补回。
// 规则（事件制，每回合 3 次）：每次"消耗能量"事件 +1 星辉；每次"消耗星辉"事件 +1 能量（与消耗量无关）。
// 模拟顺序（与伪代码一致）：
//   1) 若持护符且有能量费：预测补 1 星辉，护符次数 -1（先补星辉方向）。
//   2) 若持护符（剩余次数被上一步递减后仍有）且有星辉费：预测补 1 能量。
//   3) 风暴就绪 = 打出后星辉 >= StarsStorm 且 打出后能量 >= EnergyStorm。
// 注意：护符次数只有 1 时，第 1 步用掉后第 2 步不再补（避免单次次数被两个方向都消耗）。
public static class StormGlow
{
    public static bool Ready(CardModel card, int needEnergy, int needStars)
    {
        if (card.Owner?.PlayerCombatState is not { } pcs)
            return false;

        var costE = card.EnergyCost.GetResolved();
        // CanonicalStarCost 默认 = -1 表示"无星辉费"；clamp 到 0 避免被当作负费用（造成 s 凭空 +1）。
        var costS = System.Math.Max(0, card.CanonicalStarCost);
        var amulet = card.Owner.GetRelic<GaoshouAmulet>();

        // 当前资源（打出前）。预测要模拟：扣掉自身费用后，护符按"消耗事件"补回。
        var e = pcs.Energy - costE;
        var s = pcs.Stars - costS;

        if (amulet != null)
        {
            var uses = System.Math.Max(0, amulet.RemainingUses);   // 护符剩余次数
            // 先能量后星辉的扣费顺序 → 先检查"能量消耗"是否补星辉。
            if (costE > 0 && uses > 0)
            {
                s += 1;   // 消耗能量 → 补 1 星辉
                uses--;
            }
            // 再检查"星辉消耗"是否补能量（用的是递减后次数，阻止单次护符被双方向消耗）。
            if (costS > 0 && uses > 0)
            {
                e += 1;   // 消耗星辉 → 补 1 能量
            }
        }

        return e >= needEnergy && s >= needStars;
    }
}
