using System.Collections.Generic;
using System.Linq;
using Gaoshou.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

// 把高手卡池并入事件「色彩哲学家」(COLORFUL_PHILOSOPHERS) 的备选颜色池。
//
// 原版 CardPoolColorOrder 固定给出 5 个本体角色卡池，GenerateInitialOptions 会：
//   1) 跳过「自己角色的卡池」；
//   2) 为每个候选池生成选项，key = "COLORFUL_PHILOSOPHERS.pages.INITIAL.options." + 该池 EnergyColorName 大写；
//   3) 用事件自己的 Rng 从候选中等概率随机裁剪到最多 3 个。
//
// 所以只要把高手卡池追加进这个序列，高手选项就会与原版颜色选项**完全同权**地参与随机抽取
// （被抽中时由原版代码调用 OfferRewards(pool) 发奖励），而玩高手时会被第 1 步自动排除。
//
// 选项文案：Gaoshou/localization/{zhs,eng}/events.json 中的
//   COLORFUL_PHILOSOPHERS.pages.INITIAL.options.GAOSHOU.title / .description
// （key 里的 GAOSHOU 就是高手卡池 EnergyColorName 的大写形式，与原版命名规则一致。）
public sealed class ColorfulPhilosophersPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_colorful_philosophers_pool";

    public static string Description => "add the Gaoshou card pool to the Colorful Philosophers color pool";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Getter<ColorfulPhilosophers>("CardPoolColorOrder"),
    ];

    public static void Postfix(ref IEnumerable<CardPoolModel> __result)
    {
        if (__result == null)
            return;

        var pools = __result.ToList();
        CardPoolModel gaoshouPool = ModelDb.CardPool<GaoshouCardPool>();
        if (pools.Contains(gaoshouPool))
            return; // 幂等：将来原版或其它 mod 已经加入过时不重复追加。

        pools.Add(gaoshouPool);
        __result = pools;
    }
}
