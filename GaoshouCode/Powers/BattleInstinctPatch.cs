using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Powers;

// 拦截敌人意图渲染的两条路径：
// 1) NCreature.RefreshIntents —— 回合/行动切换时的整场刷新；
// 2) NCreature.UpdateIntent —— 施加能力等事件触发的单目标意图刷新（最后一个敌人攻击后最常见）。
// 开关 = 战斗内能力存在性（HasPower），随战斗结束自然失效，不会带到下一场战斗。
// 同时在意图刷新时隐藏该敌人的血条：新召唤/新入场敌人的血条不会自动隐藏，借此机会补隐藏一次。
public class BattleInstinctPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_battle_instinct";
    public static string Description => "hide enemy intents and health bars while Battle Instinct is active";
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
            // 新敌人入场/动作时刷新意图：顺带隐藏该敌人的血条（新召唤敌人的血条不会自动隐藏）。
            try
            {
                if (__instance.GetNodeOrNull<Control>("%HealthBar") is { } hpBar)
                    hpBar.Visible = false;
                __instance.AnimHideIntent();
            }
            catch
            {
                // UI 不可用时静默（属性/意图拦截不受影响）。
            }
            __result = Task.CompletedTask;
            return false;
        }
        return true;
    }
}