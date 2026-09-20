using Gaoshou.Cards;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 废品牌预览的**鼠标滚轮换卡（独占）**。
///
/// 为什么挂在 <c>NCursorManager._Input</c>：
///   * 它挂在 <c>NGame</c> 下的常驻节点（<c>NGame.cs:162</c> 的 %CursorManager），
///     <c>_Input</c> 对树里每个重写了它的节点都会调用，因此**所有界面都覆盖**（战斗手牌、牌库网格、卡牌详情）；
///   * Godot 的输入顺序是 <c>_Input</c> → GUI(<c>_GuiInput</c>) → <c>_UnhandledInput</c>，
///     而在 <c>_Input</c> 里调 <c>SetInputAsHandled()</c> 就会**掐掉后面的 GUI 阶段** ——
///     原版自己也这么干（<c>NMouseCardPlay._Input</c>、<c>NPlayerHand</c>、<c>NTargetManager</c> 都这么消费输入）。
///     这正是"独占"的关键：牌库的滚动发生在 <c>NScrollableContainer._GuiInput → ProcessScrollEvent</c>，
///     在 GUI 阶段标记已处理，滚动容器就收不到这个滚轮了。
///     （第一版挂在 <c>NCardHolder._GuiInput</c> 上只能覆盖"滚轮能传到 holder"的界面 → 牌库网格有效、手牌与详情无效。）
///
/// 判定"鼠标在那三张卡上"用两道闸门，任一命中即算：
///   1. 屏幕上正显示着废品牌预览卡（悬浮窗只在悬停那三张卡时存在，与界面无关）；
///   2. 退回控件链判断：<c>Viewport.GuiGetHoveredControl()</c> 起往上找 <c>NCardHolder.CardModel</c> / <c>NCard.Model</c>。
///
/// 开销：只对"按下的滚轮"事件做判断，其它输入（尤其是鼠标移动）在头两行就返回。
/// </summary>
public sealed class WastePreviewWheelPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_waste_preview_wheel";

    public static string Description => "step the Waste-card preview with the mouse wheel (exclusive, global _Input)";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method(typeof(NCursorManager), "_Input", typeof(InputEvent)),
    ];

    public static bool Prefix(NCursorManager __instance, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { Pressed: true } mouseButton)
            return true;

        var delta = mouseButton.ButtonIndex switch
        {
            MouseButton.WheelUp => -1,      // 上滚：上一张
            MouseButton.WheelDown => 1,     // 下滚：下一张
            _ => 0,
        };
        if (delta == 0)
            return true;

        var viewport = __instance.GetViewport();
        if (viewport == null || viewport.IsInputHandled())
            return true;                    // 已被别处处理，别重复吃

        if (!IsHoveringPreviewCard(viewport))
            return true;                    // 鼠标不在这三张卡上 → 完全放行（手牌照常滚动）

        WastePreview.Step(delta, Time.GetTicksMsec());
        WastePreviewPatch.RefreshVisiblePreviews();

        // 独占：GUI 阶段（含 NScrollableContainer 的滚轮滚动）与 _UnhandledInput 都收不到这个滚轮。
        viewport.SetInputAsHandled();
        return true;                        // 不改原方法本体：滚轮本来就不走它的分支
    }

    private static bool IsHoveringPreviewCard(Viewport viewport)
    {
        // 闸门 1：屏幕上正显示着"我们的"废品牌预览卡（那个悬浮窗属于我们的三张卡之一）。
        if (WastePreviewPatch.HasVisiblePreview())
            return true;

        // 闸门 2：悬浮窗还没建好的那一帧，退回控件链判断（同样只认那三张卡）。
        return WastePreviewPatch.IsPreviewOwner(viewport.GuiGetHoveredControl());
    }
}
