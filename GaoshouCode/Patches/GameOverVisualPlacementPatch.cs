using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 「放弃游戏」后的游戏结束界面里，把本模组的形象**摆到容器中心**（对齐原版 else 分支的摆法）。
///
/// ═══════════════════════【BUG】═══════════════════════
/// 用户实测：在**假商人事件**和**营火**里放弃，人物与死亡动画出现在**左下角**而不是居中。
///
/// ═══════════════════════【根因：RitsuLib 的替换版漏了定位】═══════════════════════
/// <c>NGameOverScreen.MoveCreaturesToDifferentLayerAndDisableUi()</c> 被 RitsuLib 的
/// <c>CharacterGameOverScreenCompatibilityPatch</c> **整体替换**（Prefix 返回 false）。
/// 把两边逐分支对比就看出问题了：
///
/// | 分支 | 原版 | RitsuLib 替换版 |
/// |---|---|---|
/// | 商店 | <c>Reparent(container)</c> | 同 |
/// | **假商人** | **没有这个分支** ⇒ 落到 else、**居中** | **新增分支，只 Reparent 不设位置** ⇒ 停在房间原位 ✗ |
/// | 营火 | <c>GlobalPosition = characterForPlayer.GlobalPosition</c> | 同，但**多加了 <c>if (characterForPlayer == null) continue;</c>** ⇒ null 时位置停在 (0,0) ⇒ 容器左下角 ✗ |
/// | 其它 | <c>Position = container.Size*0.5 + (0,200)</c> | 同 |
///
/// 也就是说：RitsuLib 为了支持程序化摊位新增的假商人分支、以及营火分支的 null 兜底，
/// 都**没有**像 else 分支那样把节点摆到容器中心 ⇒ 用户看到的「左下角」。
/// 这**改样式没用**：样式只管 Sprite2D 的局部变换，管不到 Reparent 之后的节点定位。
///
/// ═══════════════════════【修法】═══════════════════════
/// 在同一个方法上挂 **Postfix**。RitsuLib 是用 Prefix 返回 false 跳过原方法的，
/// 而 Harmony 的 **Postfix 在 Prefix 跳过原方法后仍会执行** ⇒ 我们的定位代码正好跑在它之后，
/// 可以把最终位置覆盖掉 ✓。
///
/// 定位公式**照抄原版 else 分支**（含多人时的横向排布）：
/// <c>Position = container.Size * 0.5 + (startOffset, 200)</c>，多人时按 250px 间隔居中排开
/// ⇒ 单人是正中央 ✓，多人也不会重叠 ✓。
///
/// **只动我们自己的节点**：用「子树里有没有 <c>res://Gaoshou/</c> 的贴图」来判定，
/// 不碰原版角色、不碰别人的模组 ✓（判据见 <see cref="LooksLikeOurs" />）。
/// </summary>
public sealed class GameOverVisualPlacementPatch : IPatchMethod
{
    /// <summary>本模组所有贴图都在这个前缀下。</summary>
    private const string OurResourcePrefix = "res://Gaoshou/";

    /// <summary>节点元数据键：居中**之前**的原始 <c>Position</c>（用于还原，见 Postfix 里的注释）。</summary>
    private const string PrevPositionMeta = "gaoshou_prev_pos";

    /// <summary>容器元数据键：还原处理器是否已经接上（避免重复订阅信号）。</summary>
    private const string RestoreHookedMeta = "gaoshou_restore_hooked";

    public static string PatchId => "gaoshou_game_over_visual_placement";

    public static string Description =>
        "center Gaoshou visuals on the game over screen (RitsuLib's replacement skips positioning)";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(NGameOverScreen), "MoveCreaturesToDifferentLayerAndDisableUi"),
    ];

    public static void Postfix(NGameOverScreen __instance)
    {
        if (__instance.GetNodeOrNull<Control>("%CreatureContainer") is not { } container)
            return;

        var ours = new List<Node2D>();
        foreach (var child in container.GetChildren())
        {
            if (child is Node2D node && LooksLikeOurs(node))
                ours.Add(node);
        }

        if (ours.Count == 0)
            return;

        // 完全照抄原版 else 分支的摆法（多人时按 250px 居中排开）。
        var spacing = ours.Count == 1
            ? 0f
            : Math.Min(250f, (container.Size.X - 200f) / (ours.Count - 1));
        var startOffset = (ours.Count - 1) * (0f - spacing) * 0.5f;
        foreach (var visual in ours)
        {
            // ⚠️⚠️【2026-10-06 修复"所有死亡动画都被居中"】本补丁只该管**游戏结束界面**里的摆位，
            //    但 RitsuLib 会把同一个形象节点**复用到战斗 / 建筑师演出**等场合 ×
            //    而我们写下的 `Position` 是**节点自身**的局部坐标 ⇒ 它会被**带回去** ✗
            //    ⇒ 实机症状：战斗死亡、建筑师演出时角色**瞬移到屏幕中间**（原本应死在左侧原位）✗。
            // 修法（按用户要求"把商店/火堆的居中与其它场合区分开"）：
            //   ① 居中前先把**原位置**快照到节点元数据（只记一次）；
            //   ② 节点**离开容器**时（= 被换回战斗/过场）把位置**还原** ⇒ 居中不再"粘"出去 ✓。
            if (!visual.HasMeta(PrevPositionMeta))
            {
                visual.SetMeta(PrevPositionMeta, visual.Position);
            }

            visual.Position = container.Size * 0.5f + new Vector2(startOffset, 200f);
            startOffset += spacing;
        }

        // 还原的接线**只做一次**（挂在容器上，处理器是**具名静态方法**、不带闭包 ✗）：
        // ⚠️ 本仓库铁律：补丁回调路径里绝不出现编译器生成的闭包 / lambda
        //    （MonoMod 的 JIT hook 编译它们时会抛 ArgumentException，历史事故见 GaoshouAttackStyle 的类注释 ✗）。
        //    所以"哪个节点要还原"这个信息**存在节点自己的元数据里**，处理器只按元数据干活 ✓。
        if (!container.HasMeta(RestoreHookedMeta))
        {
            container.SetMeta(RestoreHookedMeta, true);
            container.ChildExitingTree += OnChildExitingContainer;
        }

        Entry.Logger.Info(
            $"[GameOver] 已把 {ours.Count} 个模组形象居中到容器 {container.Size}（原版 else 分支的摆法）");
    }

    /// <summary>
    /// 判定这棵子树是不是本模组的形象：只要有任一 <c>Sprite2D</c> 的贴图来自 <c>res://Gaoshou/</c> 就算。
    /// 兼容两种形态：程序化商人节点（子 <c>Sprite2D</c> 名 <c>Visuals</c>）与
    /// <c>gaoshou_visuals.tscn</c> 实例（根就是 Sprite2D 或含 Sprite2D 子节点）✓。
    /// </summary>
    private static bool LooksLikeOurs(Node root)
    {
        if (root is Sprite2D { Texture: { } own } && IsOurs(own))
            return true;

        foreach (var descendant in root.FindChildren("*", "Sprite2D", recursive: true, owned: false))
        {
            if (descendant is Sprite2D { Texture: { } texture } && IsOurs(texture))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 节点**离开容器**时把位置还原（= 形象被换回战斗 / 建筑师演出等场合）✓
    /// 具名静态方法、无闭包（理由见 Postfix 里的注释 ✓）；靠节点元数据判断该不该还原 ✓。
    /// </summary>
    private static void OnChildExitingContainer(Node child)
    {
        if (child is not Node2D visual || !visual.HasMeta(PrevPositionMeta))
            return;

        visual.Position = (Vector2)visual.GetMeta(PrevPositionMeta);
        visual.RemoveMeta(PrevPositionMeta);
    }

    private static bool IsOurs(Texture2D texture) =>
        texture.ResourcePath?.StartsWith(OurResourcePrefix, StringComparison.Ordinal) == true;
}
