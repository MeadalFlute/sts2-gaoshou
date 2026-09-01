using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 战斗直觉（能力）：敌人的血条与意图被隐藏。
// 意图：由 BattleInstinctPatch 在渲染源头拦截；血条：打出时隐藏一次即持续生效（敌人节点不重置血条显隐）。
[RegisterPower]
public sealed class BattleInstinctPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/battleinstinct.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/battleinstinct.png");

    /// <summary>
    /// 隐藏所有敌人血条与意图（意图渲染已由补丁拦截，此处主要隐藏血条）。
    /// </summary>
    public static void HideEnemyUi(ICombatState? combatState)
    {
        if (combatState == null)
            return;
        foreach (var enemy in combatState.HittableEnemies ?? [])
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(enemy);
            if (node == null)
                continue;
            try
            {
                node.AnimHideIntent();
                var hpBar = node.GetNodeOrNull<Control>("%HealthBar");
                if (hpBar != null)
                    hpBar.Visible = false;
            }
            catch
            {
                // UI 不可用时静默（属性加成不受影响）。
            }
        }
    }
}