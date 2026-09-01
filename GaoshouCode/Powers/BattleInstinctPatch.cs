using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Powers;

// 拦截敌人意图渲染的两条路径：
// 1) NCreature.RefreshIntents —— 回合/行动切换时的整场刷新；
// 2) NCreature.UpdateIntent —— 施加能力等事件触发的单目标意图刷新（最后一个敌人攻击后最常见）。
// 开关 = 战斗内能力存在性（HasPower），随战斗结束自然失效，不会带到下一场战斗。
public class BattleInstinctPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_battle_instinct";
    public static string Description => "hide enemy intents while Battle Instinct is active";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(NCreature), "RefreshIntents"),
        new(typeof(NCreature), "UpdateIntent"),
    ];

    public static bool Prefix(NCreature __instance, ref Task __result)
    {
        // 任一参战玩家持有战斗直觉 → 意图不渲染。return false 跳过原方法（真取消）。
        if (__instance?.Entity is Creature creature && creature.CombatState is { } state &&
            state.PlayerCreatures.Any(pc => pc.HasPower<BattleInstinctPower>()))
        {
            __result = System.Threading.Tasks.Task.CompletedTask;
            return false;
        }
        return true;
    }
}