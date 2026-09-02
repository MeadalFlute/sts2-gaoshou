using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using Gaoshou.Relics;

namespace Gaoshou.Keywords;

// 风暴泛光助手：风暴在打出后才判定，因此"就绪"需先扣除本卡自身费用。
// 高手护符（初始遗物）会在消耗后把能量↔星辉互相转化（每回合 3 次）——消耗会部分互抵。
// 若护符剩余次数足够覆盖本卡的两个方向消耗，则打出后资源约等于打出前（护符互抵模型）。
public static class StormGlow
{
    public static bool Ready(CardModel card, int needEnergy, int needStars)
    {
        if (card.Owner?.PlayerCombatState is not { } pcs)
            return false;
        var e = pcs.Energy;
        var s = pcs.Stars;
        var costE = card.EnergyCost.GetResolved();
        var costS = card.CanonicalStarCost;
        var amulet = card.Owner.GetRelic<GaoshouAmulet>();

        // 护符互抵（事件制）：每次"消耗能量"事件 +1 星辉、每次"消耗星辉"事件 +1 能量（与消耗量无关）。
        // 剩余次数 1：只补第一个方向（先补星辉）；剩余次数 2：两个方向各 +1。
        if (amulet != null)
        {
            var uses = System.Math.Max(0, amulet.RemainingUses);
            int eGain = 0, sGain = 0;
            if (costE > 0 && uses > 0) { sGain = 1; uses--; }
            if (costS > 0 && uses > 0) { eGain = 1; }
            e = e - costE + eGain;
            s = s - costS + sGain;
        }
        else
        {
            e -= costE;
            s -= costS;
        }

        return e >= needEnergy && s >= needStars;
    }
}