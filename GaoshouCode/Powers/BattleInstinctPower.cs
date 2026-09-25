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

// 战斗直觉（能力）：敌人的血条与意图被隐藏 —— **全场玩家都看不见**。
// 意图：由 BattleInstinctPatch 在渲染源头拦截；血条：打出时隐藏一次 + 每次意图刷新(含新敌人入场)时由补丁再隐藏一次。
//
// 为什么代价是全场而不是只施放者（2026-09-22 确认）：多人下玩家之间可以交流，只让施放者一个人看不见
// 等于没有代价；所以补丁按"场上任一玩家持有本能力"判断，隐藏所有客户端的信息。
//
// 本能力登记为 **Buff**（而不是 Debuff），是刻意的：效果虽然是负面的（看不见敌人血条与意图），
// 但**不希望它被"净化/移除负面效果"清掉** —— 否则队友一个净化就能把全场的代价解掉。
// PowerType 只影响图标呈现与"能否被净化"这一层；隐藏逻辑由 BattleInstinctPatch 独立判断，与 PowerType 无关。
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
