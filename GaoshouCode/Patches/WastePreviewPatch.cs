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
        //（实测症状：hovertip 错位到屏幕左上角）⇒ 整体吞掉，只记一次日志（带完整栈）。
        try
        {
            // ⭐ 所有"识别 owner + 登记"的重活都在**这个上下文**里做 —— 它是**已被实证可用**的：
            //   本方法每帧都在跑 Collect/ApplyCurrent（同样碰游戏类型与节点），日志里从未报错 ✓
            //   而 NHoverTipSet.CreateAndShow 的 detour 是"首次 JIT 我方方法就炸"的脆弱上下文 ✗
            ProcessPendingScan();
            Step(__instance);
        }
        catch (System.Exception e)
        {
            WarnFull(ref _warned, "[WastePreview] auto-cycle skipped after an error", e);
        }
    }

    private static bool _warned;

    // ---- 跨补丁上下文交接：CreateAndShow 的 postfix 里**只做纯字段赋值**（内联，不产生任何方法调用/JIT ✗）----

    /// <summary>CreateAndShow 的 postfix 置位（纯字段写入 ✓）。</summary>
    internal static bool PendingScan;

    /// <summary>待识别的悬浮窗（纯字段写入 ✓）。</summary>
    internal static NHoverTipSet? PendingSet;

    /// <summary>待识别的 owner（纯字段写入 ✓）。</summary>
    internal static Control? PendingOwner;

    /// <summary>
    /// 处理上一帧 CreateAndShow 交接过来的悬浮窗：**识别 + 登记 + 诊断全在这里做** ✓
    /// （只在 _Process 这个安全上下文里调用 ⇒ 不再有"在 CreateAndShow detour 里首次 JIT" ✗）
    /// </summary>
    internal static void ProcessPendingScan()
    {
        if (!PendingScan)
            return;

        PendingScan = false;
        var set = PendingSet;
        var owner = PendingOwner;
        PendingSet = null;
        PendingOwner = null;

        if (set == null)
            return;

        DiagEnterOwner(true, owner);
        if (IsPreviewOwner(owner))
        {
            RecordSet(set, owner);
            LogTipDiag(set, owner);
        }
        else
        {
            DiagOwnerMiss(owner);
        }
    }

    /// <summary>【一次性取证】前 3 次悬浮窗的几何信息（hovertip 错位排查用；现已正常，保留供复核）。</summary>
    private static void LogTipDiag(NHoverTipSet set, Control? owner)
    {
        if (_tipDiagCount >= 3)
            return;
        _tipDiagCount++;
        Diag($"[TipDiag] #{_tipDiagCount} owner={Describe(owner)} set={SafeName(set)} " +
             $"parent={set.GetParent()?.Name ?? "<null>"} size={set.Size} pos={set.Position} " +
             $"anchors=({set.AnchorLeft},{set.AnchorTop},{set.AnchorRight},{set.AnchorBottom}) children={set.GetChildCount()}");
    }

    private static int _tipDiagCount;

    /// <summary>
    /// **强制预编译**（`RuntimeHelpers.PrepareMethod`）并**据此决定能力开关** ✓：
    ///   * 逐个编译"会被补丁回调间接调用"的我方方法；
    ///   * 其中 `IsPreviewNodeByModel`（含游戏类型 `is`/成员读取 ✗）若**编不过**，
    ///     就保持 <see cref="ModelCheckJitOk" /> = false ⇒ **运行时永不调用它** ✗（改由纯字符串判据工作 ✓）
    ///     ⇒ 这样即使 MonoMod 的 JIT hook 拒绝某段 IL，功能也只是"降级"，不会在运行时炸 ✓✓
    ///   * 用户只需**启动一次游戏**看这一行：`force-JIT: N compiled, M failed … (modelCheck=on/off)`
    ///     ⇒ M=0 即全部通过；M&gt;0 会点名是哪几个方法 ✗
    /// </summary>
    internal static void ForceJit()
    {
        var names = new[]
        {
            "IsPreviewNodeItself", "IsPreviewNodeByModel", "IsOurCardNodeName", "IsPreviewOwner",
            "IsPreviewControl", "IsPreviewReward", "IsPreviewCardModel",
            "TryGetParent", "RecordSet", "DiagRegistered", "DiagOwnerMiss", "DiagEnterOwner", "DiagEnterWheel",
            "DiagHover", "Describe", "DescribeChain", "SafeName", "Diag", "WarnFull", "LogTipDiag",
            "ConsumePendingRefresh", "ProcessPendingScan", "Collect", "ApplyCurrent", "EnsurePreviewNodes",
            "IsOurSet", "PruneSets", "PreviewCardsIn", "RefreshVisiblePreviews", "HasVisiblePreview",
            "RequestRefresh", "get_PreviewRegistered", "WarmUp",
        };

        var flags = System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic;

        var ok = 0;
        var failed = 0;
        foreach (var name in names)
        {
            var method = typeof(WastePreviewPatch).GetMethod(name, flags);
            if (method == null)
                continue;

            try
            {
                System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);
                ok++;

                if (name == "IsPreviewNodeByModel")
                    ModelCheckJitOk = true;     // 只有真的编过才允许运行时调用 ✓
            }
            catch (System.Exception e)
            {
                failed++;
                if (failed <= 3)
                    Entry.Logger.Warn($"[WastePreview] PrepareMethod failed for {name}: {e.GetType().Name}: {e.Message}");
            }
        }

        // 轮播数据侧（滚轮补丁会调用）—— 用带 BindingFlags 的查找（internal 方法用默认 flags 找不到 ⇒ NRE ✗）
        var dataFlags = System.Reflection.BindingFlags.Static
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic;
        var step = typeof(WastePreview).GetMethod(nameof(WastePreview.Step), dataFlags);
        var current = typeof(WastePreview).GetMethod(nameof(WastePreview.Current), dataFlags);
        foreach (var entry in new[] { ("WastePreview.Step", step), ("WastePreview.Current", current) })
        {
            if (entry.Item2 == null)
            {
                failed++;
                Entry.Logger.Warn($"[WastePreview] PrepareMethod skipped: {entry.Item1} not found via reflection.");
                continue;
            }

            try
            {
                System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(entry.Item2.MethodHandle);
                ok++;
            }
            catch (System.Exception e)
            {
                failed++;
                Entry.Logger.Warn($"[WastePreview] PrepareMethod failed for {entry.Item1}: {e.GetType().Name}: {e.Message}");
            }
        }

        Entry.Logger.Info(
            $"[WastePreview] force-JIT: {ok} compiled, {failed} failed (of {names.Length}+2); modelCheck={(ModelCheckJitOk ? "on" : "off")}.");
    }

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

    /// <summary>
    /// 单个节点是不是"我们的"废品牌预览相关节点。
    ///
    /// ⚠️ 本方法**只能用"一定能被 MonoMod 的 JIT hook 编译"的 IL** ✗：
    ///   2026-09-30 实证 —— 旧版（对游戏类型做 `is` + 读游戏成员）连 `RuntimeHelpers.PrepareMethod`
    ///   （在 **init 上下文**、与 detour 无关）都编译不了：`ArgumentException: Value does not fall within the expected range`
    ///   ⇒ 说明是**方法本身的 IL 形态**被 hook 拒绝 ✗，而不是上下文问题 ✗。
    ///   而只读 `Node.Name` / `GetParent()` 的方法（`SafeName`/`Describe`/`DiagEnterOwner`/`TryGetParent`）
    ///   全部 PrepareMethod **通过** ✓ ⇒ 这里改成：
    ///     ① **节点名含卡 id**（实证名字形如 `GridCardHolder-CARD_GAOSHOU_CARD_…` ✓）—— 纯 BCL 字符串 ✓
    ///     ② 名字没有就往上找 2 层（同样是名字字符串 ✓）
    ///     ③ 仅当"模型判据版"（<see cref="IsPreviewNodeByModel" />，含游戏类型/成员 ✗）**PrepareMethod 通过**
    ///        时才调用它兜底 ✓（编译不过就永不调用 ⇒ 绝不在运行时炸 ✗）
    /// </summary>
    private static bool IsPreviewNodeItself(Node node)
    {
        if (IsOurCardNodeName(SafeName(node)))
            return true;

        var parent = TryGetParent(node);
        if (parent != null && IsOurCardNodeName(SafeName(parent)))
            return true;

        var grand = parent == null ? null : TryGetParent(parent);
        if (grand != null && IsOurCardNodeName(SafeName(grand)))
            return true;

        return ModelCheckJitOk && IsPreviewNodeByModel(node);
    }

    /// <summary>模型判据版（含游戏类型 `is` 与游戏成员读取 ✗）——只在 PrepareMethod 成功时才使用 ✓。</summary>
    private static bool IsPreviewNodeByModel(Node node)
    {
        if (node is NCardHolder holder && IsPreviewCardModel(holder.CardModel))
            return true;

        if (node is NCard card && IsPreviewCardModel(card.Model))
            return true;

        return false;
    }

    /// <summary>"模型判据版"是否可用（由 <see cref="ForceJit" /> 设置；编不过就保持 false ⇒ 永不调用 ✓）。</summary>
    private static bool ModelCheckJitOk;

    /// <summary>本 mod 三张废品牌的卡 id（节点名里就带这个 ✓ 见 [TipDiag] 实证）。</summary>
    private const string ImprovisedWeaponId = "GAOSHOU_CARD_IMPROVISED_WEAPON";

    private const string JunkChestId = "GAOSHOU_CARD_JUNK_CHEST";

    private const string WasteNotId = "GAOSHOU_CARD_WASTE_NOT";

    /// <summary>节点名里是否含我们三张废品牌的卡 id（**纯 BCL 字符串操作** ✓）。</summary>
    private static bool IsOurCardNodeName(string name)
    {
        if (name.Length == 0)
            return false;

        return name.Contains(ImprovisedWeaponId, System.StringComparison.Ordinal)
            || name.Contains(JunkChestId, System.StringComparison.Ordinal)
            || name.Contains(WasteNotId, System.StringComparison.Ordinal);
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
