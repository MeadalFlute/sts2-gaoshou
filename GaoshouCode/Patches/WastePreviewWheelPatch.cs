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
        // 【埋点前置】第一行就打（前 10 次）：即使随后抛异常，也能知道"进到了哪一步、当时有没有登记"。
        WastePreviewPatch.DiagEnterWheel(WastePreviewPatch.PreviewRegistered);

        // 调用点是原版输入回调：任何异常都会污染原版输入处理并在日志里刷 ERROR ⇒ 兜住，但**记完整栈** ✓
        try
        {
            return PrefixCore(__instance, inputEvent);
        }
        catch (System.Exception e)
        {
            WastePreviewPatch.WarnFull(ref _warned, "[WastePreview] wheel handler skipped after an error", e);
            return true;
        }
    }

    private static bool _warned;

    /// <summary>
    /// **纯数据 + 原生独占版**：
    ///   * 判定只用**纯字段读取**：<see cref="WastePreviewPatch.PreviewRegistered" />（我方 bool）与
    ///     <see cref="WastePreviewPatch.PreviewActive" />（由 `_Process` 那个**已实证可用**的上下文算好的字段 ✓）
    ///     ⇒ 滚轮回调里**零节点操作、零"我方方法首次 JIT"** ✓；
    ///   * 独占只调**Godot 原生方法**：`__instance.GetViewport()` + `viewport.SetInputAsHandled()` ✓
    ///     （原生 P/Invoke，不涉及我们 IL 的 JIT ⇒ 安全 ✓；同文件里 `Time.GetTicksMsec()` 一直是这么用的 ✓）；
    ///   * 轮播数据仍走 `WastePreview.Step`（已 PrepareMethod 编译 ✓）+ `RequestRefresh()`，
    ///     真正的节点更新交给 `_Process` 补丁下一帧执行 ✓；
    ///   * 返回 `true` = **不跳过**原版 `NCursorManager._Input`（只把事件标记为已处理，
    ///     使 GUI/`_UnhandledInput` 阶段收不到这个滚轮 ⇒ 底层界面不再同时滚动 ✓）
    ///     —— 不返回 false 是为了不改变原方法的其它职责（光标更新等）✓
    /// </summary>
    private static bool PrefixCore(NCursorManager __instance, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
            return true;

        if (!WastePreviewPatch.PreviewRegistered)
            return true;                    // 屏幕上没有我们的悬浮窗 ⇒ 完全放行（纯字段判断 ✓）

        var delta = mouseButton.ButtonIndex switch
        {
            MouseButton.WheelUp => -1,      // 上滚：上一张
            MouseButton.WheelDown => 1,     // 下滚：下一张
            _ => 0,
        };
        if (delta == 0)
            return true;

        WastePreview.Step(delta, Time.GetTicksMsec());
        WastePreviewPatch.RequestRefresh();

        // 独占：只有"预览**此刻真的在显示**"时才吃这个滚轮 ✓
        // ⚠️ 不能用"缓存里有预览节点"判 ✗（缓存长期为真 ⇒ 没呼出预览时也吃滚轮 ✗ 用户实测反馈）。
        //    改用 `_Process` 刷新的时间戳：最近 250ms 内我们的悬浮窗被处理过 ⇒ 才算"正在显示" ✓
        //    （纯字段读 + 原生 `Time.GetTicksMsec()`，与上面同一类安全调用；没有任何我方节点/工具方法调用 ✗）
        var fresh = Time.GetTicksMsec() - WastePreviewPatch.LastActiveMs <= 250;
        if (WastePreviewPatch.PreviewRegistered && fresh)
        {
            try
            {
                var viewport = __instance.GetViewport();
                if (viewport != null)
                    viewport.SetInputAsHandled();
            }
            catch (System.Exception e)
            {
                // 独占失败不影响轮播本身：记一次完整栈即可 ✓
                WastePreviewPatch.WarnFull(ref _occupyWarned, "[WastePreview] wheel occupy skipped after an error", e);
            }
        }

        return true;                        // 不改原方法本体：滚轮本来就不走它的分支
    }

    private static bool _occupyWarned;
}
