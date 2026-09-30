using System.Collections.Generic;
using System.Linq;
using Gaoshou.Cards;
using Gaoshou.Keywords;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 废品牌预览的**兜底自动轮播**：挂在 <c>NHoverTipSet._Process</c> 上，
/// 只要这个悬浮窗是**我们为那三张卡创建的**，就每 <see cref="WastePreview.IntervalSeconds" /> 秒换下一张。
///
/// ⚠ 判定必须认"悬浮窗的归属"，不能只看"里面有没有废品牌卡片节点"：
/// 损失规避（LossAversion）的悬浮里就有一张**硬纸板**预览（<c>HoverTipFactory.FromCard&lt;Cardboard&gt;</c>），
/// 硬纸板本身也是废品牌 —— 早先那版因此把它的悬浮也当成轮播对象，导致"损失规避的悬浮错误联想为所有废品牌"。
/// 现在：悬浮窗在创建时由 <see cref="WastePreviewOwnerPatch" /> 记录归属（owner 能解析到那三张卡才记录），
/// 这里只处理记录过的悬浮窗。
///
/// 换卡 = 就地改 <c>NCard.Model</c> + <c>UpdateVisuals(PileType.Deck, CardPreviewMode.Normal)</c>，
/// 与原版 <c>NHoverTipCardContainer.Add</c> 完全相同的两行 → 不重建悬浮窗、不闪烁、不新建节点。
/// 节点列表带缓存（<see cref="EnsurePreviewNodes" />），每帧只做几次有效性校验，不遍历节点树。
///
/// 注：滚轮那条是独立补丁 <see cref="WastePreviewWheelPatch" />
/// （RitsuLib 用 <c>GetMethod("Postfix")</c> 取方法，同名重载会抛歧义异常，所以必须拆开）。
/// </summary>
public sealed class WastePreviewPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_waste_preview_cycle";

    public static string Description => "auto-cycle the Waste-card preview in our hover tips (2s fallback)";

    public static bool IsCritical => false;

    /// <summary>被 <see cref="WastePreviewOwnerPatch" /> 记录下来的"我们的悬浮窗"。</summary>
    internal static readonly HashSet<NHoverTipSet> OurSets = [];

    private static bool _loggedFirstCycle;

    // 缓存的预览节点：命中缓存时每帧只做几次 IsInstanceValid/IsInsideTree，**不再遍历节点树**。
    private static readonly List<NCard> CachedPreviewNodes = [];

    private static ulong _lastSlowFrameWarnMs;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method(typeof(NHoverTipSet), "_Process", typeof(double)),
    ];

    public static void Postfix(NHoverTipSet __instance)
    {
        // 这是**共享悬浮窗的原版 _Process**：任何异常都会污染所有卡牌的 tooltip
        //（实测症状：hovertip 错位到屏幕左上角）⇒ 整体吞掉，只记一次日志。
        try
        {
            Step(__instance);
        }
        catch (System.Exception e)
        {
            WarnFull(ref _warned, "[WastePreview] auto-cycle skipped after an error", e);
        }
    }

    private static bool _warned;

    /// <summary>一次性 Warn，**带完整类型/消息/栈**（只看 Message 会像前几轮那样"看不到抛在哪一行" ✗）。</summary>
    internal static void WarnFull(ref bool flag, string tag, System.Exception e)
    {
        if (flag)
            return;
        flag = true;
        Entry.Logger.Warn($"{tag}: {e.GetType().FullName}: {e.Message}\n{e.StackTrace}");
    }

    private static void Step(NHoverTipSet __instance)
    {
        if (!IsOurSet(__instance))
            return;

        var startUsec = Time.GetTicksUsec();

        if (!EnsurePreviewNodes(__instance))
            return;

        // 滚轮那条补丁只写"纯数据"（WastePreview.Step + RequestRefresh），真正的节点更新在这里做
        //（这个 _Process 上下文做节点操作是**已被实证可用**的 ✓）。
        if (ConsumePendingRefresh())
        {
            ApplyCurrent(CachedPreviewNodes);
            return;
        }

        var now = Time.GetTicksMsec();
        if (WastePreview.LastAdvanceMs == 0)
        {
            WastePreview.LastAdvanceMs = now;
            return;
        }

        if (now - WastePreview.LastAdvanceMs < (ulong)(WastePreview.IntervalSeconds * 1000.0))
            return;

        WastePreview.Step(1, now);
        if (!_loggedFirstCycle)
        {
            _loggedFirstCycle = true;
            Entry.Logger.Info($"[WastePreview] auto-cycle active: {CachedPreviewNodes.Count} preview node(s), now {WastePreview.Current()?.Id.Entry}");
        }

        ApplyCurrent(CachedPreviewNodes);

        // 埋点：正常应是微秒级；万一某天真的变成卡顿源，日志里能直接看到（限流 5 秒一次）。
        var costMs = (Time.GetTicksUsec() - startUsec) / 1000.0;
        if (costMs >= 2.0 && now - _lastSlowFrameWarnMs > 5000)
        {
            _lastSlowFrameWarnMs = now;
            Entry.Logger.Warn($"[WastePreview] slow frame: {costMs:F1} ms (nodes={CachedPreviewNodes.Count})");
        }
    }

    /// <summary>记录一个"我们的"悬浮窗（由 <see cref="WastePreviewOwnerPatch" /> 在创建时调用）。</summary>
    // ---- 纯数据通道（运行时路径**不碰任何 Godot 节点/工具方法**）----

    private static bool _pendingRefresh;

    /// <summary>是否已经有"我们的"悬浮窗被登记（**纯 C# 数据**，不做任何节点操作）。</summary>
    internal static bool PreviewRegistered => OurSets.Count > 0;

    /// <summary>请求刷新（滚轮补丁调用）：只置一个 bool，真正的节点更新交给已实证可用的 <c>_Process</c> 补丁。</summary>
    internal static void RequestRefresh()
    {
        _pendingRefresh = true;
    }

    private static bool ConsumePendingRefresh()
    {
        if (!_pendingRefresh)
            return false;

        _pendingRefresh = false;
        return true;
    }

    internal static void RecordSet(NHoverTipSet? set, Node? owner = null)
    {
        if (set == null)
            return;

        OurSets.Add(set);
        DiagRegistered(set, owner);
    }

    /// <summary>
    /// 悬浮窗的 owner 是不是"那三张卡"（**纯数据版**：只看 owner 自己，**不遍历父链、不做任何 Godot 工具调用** ✗）。
    /// 依据（日志实证）：`owner` 本身就是卡片 holder/卡片节点（`owner=GridCardHolder-CARD_…`）；
    /// 奖励界面与查看卡牌界面里 owner 也分别是奖励按钮 / 屏幕本身 ⇒ 一律"只看自身"即可 ✓
    /// （2026-09-30：沿父链遍历的版本在正式版上被 MonoMod 的 JIT hook 卡住，反复抛 ArgumentException ✗）
    /// </summary>
    internal static bool IsPreviewOwner(Node? owner)
    {
        if (owner == null)
            return false;

        return IsPreviewNodeItself(owner);
    }

    /// <summary>hover 控件自身是不是我们的卡（同"只看自身"，不做父链遍历）。</summary>
    internal static bool IsPreviewControl(Control? control)
    {
        if (control == null)
            return false;

        return IsPreviewNodeItself(control);
    }

    /// <summary>单个节点是不是"我们的"废品牌预览相关节点（纯类型/模型判断：无 LINQ、无委托、无模式 when）。</summary>
    private static bool IsPreviewNodeItself(Node node)
    {
        if (node is NCardHolder holder && IsPreviewCardModel(holder.CardModel))
            return true;

        if (node is NCard card && IsPreviewCardModel(card.Model))
            return true;

        // 「搜刮！」奖励界面的奖励按钮：owner 就是按钮本身，卡片只在悬浮窗里 ⇒ 判奖励内容。
        if (node is NRewardButton button && IsPreviewReward(button.Reward))
            return true;

        // 查看卡牌界面（NInspectCardScreen）：owner 是屏幕本身 ⇒ 判它正在展示的 `_card`。
        if (node is NInspectCardScreen screen && IsPreviewCardModel(screen._card?.Model))
            return true;

        return false;
    }

    /// <summary>安全取父节点：原生调用失败（对象已释放等）只让**这一层**失败，不把整条链否掉。</summary>
    private static Node? TryGetParent(Node node)
    {
        try
        {
            return node.GetParent();
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    /// <summary>奖励内容里有没有"我们的"废品牌（原先用 <c>Cards.Any(方法组)</c>，为躲 JIT 失败改成显式循环）。</summary>
    private static bool IsPreviewReward(Reward? reward)
    {
        if (reward is not CardReward cardReward || cardReward.Cards == null)
            return false;

        foreach (var c in cardReward.Cards)
        {
            if (IsPreviewCardModel(c))
                return true;
        }

        return false;
    }

    private static bool IsPreviewCardModel(CardModel? card) => WastePreview.IsPreviewCard(card);

    // ---- 一次性取证（各前 10 次）：区分"登记根本没发生"与"登记了但查找失败" ----

    private static int _diagRegister;
    private static int _diagMiss;
    private static int _diagHover;
    private static int _diagEnterOwner;
    private static int _diagEnterWheel;

    /// <summary>悬浮窗创建补丁的**第一行**埋点（前 10 次）：证明"进到了登记路径"，以及当时 __result 是否为空。</summary>
    internal static void DiagEnterOwner(bool hasResult, Node? owner)
    {
        if (_diagEnterOwner >= 10)
            return;
        _diagEnterOwner++;
        Diag($"[WasteDiag] enter-owner #{_diagEnterOwner} hasResult={hasResult} owner={Describe(owner)}");
    }

    /// <summary>滚轮补丁的**第一行**埋点（前 10 次）。</summary>
    internal static void DiagEnterWheel(bool registered)
    {
        if (_diagEnterWheel >= 10)
            return;
        _diagEnterWheel++;
        Diag($"[WasteDiag] enter-wheel #{_diagEnterWheel} registered={registered} ourSets={OurSets.Count}");
    }

    /// <summary>登记成功一行：证明 owner 判定通过、悬浮窗已进 OurSets。</summary>
    internal static void DiagRegistered(NHoverTipSet set, Node? owner)
    {
        if (_diagRegister >= 10)
            return;
        _diagRegister++;
        Diag($"[WasteDiag] registered #{_diagRegister} owner={Describe(owner)} set={SafeName(set)} ourSets={OurSets.Count}");
    }

    /// <summary>owner 判定为 false 一行：证明"登记没发生"，并给出 owner 及其父级链。</summary>
    internal static void DiagOwnerMiss(Node? owner)
    {
        if (_diagMiss >= 10)
            return;
        _diagMiss++;
        Diag($"[WasteDiag] owner-miss #{_diagMiss} owner={Describe(owner)} chain={DescribeChain(owner)}");
    }

    /// <summary>滚轮判定一行：hover 到什么控件、命中与否。</summary>
    internal static void DiagHover(Control? hovered, bool hit)
    {
        if (_diagHover >= 10)
            return;
        _diagHover++;
        Diag($"[WasteDiag] hover #{_diagHover} hovered={Describe(hovered)} hit={hit} ourSets={OurSets.Count}");
    }

    private static string Describe(Node? node)
    {
        if (node == null)
            return "<null>";

        return $"{node.GetType().Name}({SafeName(node)})";
    }

    private static string DescribeChain(Node? node)
    {
        var text = string.Empty;
        var cur = node;
        for (var depth = 0; depth < 6 && cur != null; depth++)
        {
            text += (depth == 0 ? string.Empty : "<-") + Describe(cur);
            cur = TryGetParent(cur);
        }

        return text.Length == 0 ? "<null>" : text;
    }

    private static string SafeName(Node node)
    {
        try
        {
            return node.Name.ToString();
        }
        catch (System.Exception)
        {
            return "<freed>";
        }
    }

    private static void Diag(string message)
    {
        try
        {
            Entry.Logger.Info(message);
        }
        catch (System.Exception)
        {
            // 埋点失败无所谓
        }
    }

    /// <summary>
    /// 预热：在**我们自己的初始化上下文**里先把"容易被 MonoMod JIT hook 卡住"的方法各编译一次。
    /// 成功/失败都留日志 ⇒ 实机上一眼能看出 JIT 是否还失败。
    /// </summary>
    internal static void WarmUp()
    {
        try
        {
            _ = IsPreviewOwner(null);
            _ = IsPreviewControl(null);
            _ = IsPreviewReward(null);
            Collect(null!, []);
            Entry.Logger.Info("[WastePreview] warm-up ok: JIT-sensitive methods compiled.");
        }
        catch (System.Exception e)
        {
            Entry.Logger.Warn($"[WastePreview] warm-up failed: {e.Message}");
        }
    }

    /// <summary>把当前屏幕上所有"我们的"废品牌预览换成 <see cref="WastePreview.Current" />（滚轮补丁也会用）。</summary>
    internal static void RefreshVisiblePreviews()
    {
        PruneSets();

        foreach (var set in OurSets)
            ApplyCurrent(PreviewCardsIn(set));
    }

    /// <summary>
    /// 屏幕上是否真的显示着"我们的"废品牌预览卡 —— 悬浮窗只在鼠标停在那三张卡上时才存在，
    /// 所以这个判断等于"玩家正把鼠标放在临时武器/垃圾宝箱/可别浪费上"，且与具体界面无关。
    /// </summary>
    internal static bool HasVisiblePreview()
    {
        PruneSets();

        foreach (var set in OurSets)
        {
            if (PreviewCardsIn(set).Count > 0)
                return true;
        }

        return false;
    }

    /// <summary>在托管侧递归收集"模型是废品牌"的 NCard 预览节点（不依赖 Godot 类名字符串）。</summary>
    internal static List<NCard> PreviewCardsIn(Node root)
    {
        var result = new List<NCard>();
        Collect(root, result);
        return result;
    }

    private static bool IsOurSet(NHoverTipSet set)
    {
        if (!OurSets.Contains(set))
            return false;

        if (GodotObject.IsInstanceValid(set))
            return true;

        OurSets.Remove(set);
        return false;
    }

    private static void PruneSets()
    {
        OurSets.RemoveWhere(set => !GodotObject.IsInstanceValid(set));
    }

    /// <summary>
    /// 缓存仍然有效就直接返回 true（每帧开销 = 几次有效性检查）；
    /// 失效（悬浮窗重建/关闭）才扫一次这个悬浮窗自己的子树。
    /// </summary>
    private static bool EnsurePreviewNodes(NHoverTipSet set)
    {
        if (CachedPreviewNodes.Count > 0)
        {
            var alive = true;
            foreach (var node in CachedPreviewNodes)
            {
                if (GodotObject.IsInstanceValid(node) && node.IsInsideTree())
                    continue;

                alive = false;
                break;
            }

            if (alive)
                return true;
        }

        CachedPreviewNodes.Clear();
        Collect(set, CachedPreviewNodes);
        return CachedPreviewNodes.Count > 0;
    }

    private static void Collect(Node node, List<NCard> result)
    {
        // 同 IsPreviewOwner：已释放的节点不能碰（否则抛 ArgumentException，被 fail-soft 吞掉后轮播静默失效）。
        if (!GodotObject.IsInstanceValid(node))
            return;

        foreach (var child in node.GetChildren())
        {
            if (!GodotObject.IsInstanceValid(child))
                continue;

            if (child is NCard card && card.Model is IWasteCard)
                result.Add(card);

            Collect(child, result);
        }
    }

    private static void ApplyCurrent(List<NCard> previews)
    {
        if (previews.Count == 0 || WastePreview.Current() is not { } next)
            return;

        foreach (var node in previews)
        {
            if (!GodotObject.IsInstanceValid(node))
                continue;

            node.Model = next;
            node.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
        }
    }
}
