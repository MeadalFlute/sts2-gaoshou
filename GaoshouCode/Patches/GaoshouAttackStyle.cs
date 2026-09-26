using Godot;   // Time.GetTicksMsec()（会话兜底超时用）
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 「这一次攻击是 AoE（打全体）」标记 —— 给动画状态机区分"横扫"与"拳"用。
///
/// 判定来源：<see cref="AttackCommand" /> 自己带目标信息（<c>IsMultiTargeted</c> = 目标取自战斗状态、
/// <c>IsRandomlyTargeted</c> = 随机多目标），在 <c>Execute</c> 的 Prefix 里读一次即可 ——
/// 触发动画的 <c>AttackCommand.cs:584 await CreatureCmd.TriggerAnim(...)</c> 就在 <c>Execute</c> 方法体内，
/// 所以 Prefix 一定早于触发器 ✓。
///
/// 【为什么要"会话"而不是每次命令都改写】2026-09-24 实机 bug：
/// 多段 AoE（醉拳 / 蓄力刀这类 <c>WithHitCount</c> 的全体攻击）**第二段开始掉回挥拳** ✗。
/// 原因：一次多段攻击内部还会**嵌套**跑别的攻击命令（反伤、亡语、追击、敌人被打死时的效果……），
/// 旧写法"每个命令都改写风格 + 任何命令结束都清标记"，会让嵌套命令把外层的"横扫"冲掉 ⇒ 后面几段按拳播。
///
/// 现在的规则：
/// <list type="number">
///   <item>**最外层**那次攻击决定风格：进入 Execute 时若已有攻击在跑（= 嵌套），**不改写**；</item>
///   <item>风格**不在结束时清除**，而是在下一次最外层攻击进入时被覆盖 ⇒ 迟到的完成续体不会误清新攻击；</item>
///   <item>会话结束用 <c>ReferenceEquals</c> 判定（只有主人自己能收尾），并有 2 秒兜底，免得续体没跑到时永久占住。</item>
/// </list>
///
/// ⚠️ 另一个坑（同一天踩的）：本模组的补丁**不是自动发现的**，必须在 <c>Entry.cs</c> 里
/// <c>patcher.RegisterPatch&lt;T&gt;()</c> 显式注册 —— 只写 IPatchMethod 类不会挂上，
/// 而且日志里 "Applying N patches" 的 N 也不变（当时 18 → 18，差点没看出来）✗。
/// </summary>
public static class GaoshouAttackStyle
{
    /// <summary>调试日志开关（日志前缀 <c>[Gaoshou][AoE]</c>）：这个 bug 确认修好后置 false 即可。</summary>
    public const bool LogDiagnostics = true;

    // ───────────────────── 时序追踪（临时诊断，确认后删） ─────────────────────
    //
    // 【为什么要这个】2026-09-27 实机反馈：出拳与左右交替都对了，但**打完之后依旧会回 stance**。
    //   怀疑点是"收拳计时器只在 `atk_guard` 的进入事件里起了一次，连击途中没有按命中推后"——
    //   但代码里明明有两处 `AttackHitTriggered` 订阅在做重置，**看不到**所以只能猜。
    //   猜了两轮都猜错（左右交替就是这么被绕进去的），所以这次先**把事件时间线打出来**再改。
    //
    // 【为什么按"帧号"追】Godot 的 `Time.GetTicksMsec()` 在快节奏连击里分辨率不够
    //   （一段 0.33s，但重置点与到点可能同帧或差 1~2 帧）；改成
    //   `Engine.GetProcessFrames()`（引擎已渲染帧数，单调递增）+ 相对会话起点的偏移，
    //   就能看出"重置发生在第几帧、GuardEnd 到点在第几帧、两者差多少帧" ✓。
    //
    // ⚠️ **纯只读旁路**：只往日志写字符串，不碰任何游戏/状态机状态 ✓。
    // ⚠️ 与已删除的"帧号去重门"**完全不同**：那个用帧号做**判定**（会吞事件），
    //    这个只用帧号做**记录**（不参与任何分支）⇒ 不可能造成回归 ✓。
    private static ulong _traceStartFrame;

    /// <summary>开始一次新的攻击会话时调用，把帧号基准对齐到会话开头（日志里就能直接读相对帧）。</summary>
    private static void TraceReset()
    {
        _traceStartFrame = Engine.GetProcessFrames();
    }

    /// <summary>写一行时序日志（仅在 <see cref="LogDiagnostics" /> 打开时）。</summary>
    public static void Trace(string what)
    {
        if (!LogDiagnostics)
            return;

        var f = Engine.GetProcessFrames();
        Entry.Logger.Info($"[Gaoshou][trace] +{f - _traceStartFrame,4}f (f={f}) {what}");
    }

    /// <summary>会话最长存活时间（秒）：万一完成续体没跑到，也别让会话永久占住（正常一次攻击远短于它）。</summary>
    private const double StaleSessionSeconds = 2.0;

    private static AttackCommand? _session;
    private static double _sessionStartedAt;
    private static bool _aoe;

    /// <summary>当前这次攻击是否"打全体"（纯读取，可在状态机谓词里用）。</summary>
    public static bool IsAoe => _aoe;

    // ───────────────────────── 「多段(连击)」信号 ─────────────────────────
    //
    // 【信号从哪来 / 为什么不新造概念】游戏侧 `AttackCommand.cs:550` 在真正开打前算了一次
    // **实际段数**：`decimal attackCount = Hook.ModifyAttackHitCount(combatState, this, _hitCount);`，
    // 紧接着 `:551 for (int i = 0; (decimal)i < attackCount; i++)` 就是"打几段"的循环，
    // 而 `_playOnEveryHit` 默认 true ⇒ **每一段都会重发一次 `Attack` 触发器**（见 :564）。
    // 所以"这次是不是多段"唯一权威来源就是 **attackCount** —— 它连遗物改段数
    //（`Hook.ModifyAttackHitCount`）都算进去了 ✓。我们在该 Hook 的 **Postfix** 里把结果抄一份
    //（只读旁路，**不改返回值** ⇒ 对游戏行为零影响 ✓）。
    //
    // ⚠️ 为什么**不用**设置项 `MultiHitWaitSeconds`：那只是设置页给用户调的"等待时长"，
    //    由 `GuardHoldSeconds` 消费（GaoshouVisualSettings.cs:752），**并不是"多段判定"信号** ✗。
    //    真正判定多段的是上面的 attackCount。

    /// <summary>当前这次攻击的**实际段数**（由 <c>Hook.ModifyAttackHitCount</c> 的 Postfix 抄回；未见时为 1）。</summary>
    private static int _hitCount = 1;

    /// <summary>是否已经知道本次攻击的段数（<c>ModifyAttackHitCount</c> 是否被调用过）。
    /// 用来区分"刚进会话、还没算段数"与"已经知道段数了"——避免默认值 1 被误当成判定结果。</summary>
    private static bool _hitCountKnown;

    /// <summary>
    /// 当前这次攻击是否为**多段（连击）**：实际段数 ≥ 2 且不是 AoE。
    ///
    /// 纯读取、可在状态机谓词里用；**不改变** <see cref="Enter" /> / <see cref="Exit" /> /
    /// <see cref="IsAoe" /> 的任何既有语义 ✓。谓词可能被多次求值 ⇒ 这里只读缓存、不做副作用 ✓。
    /// </summary>
    public static bool IsMultihit => _hitCountKnown && _hitCount >= 2 && !_aoe;

    /// <summary>
    /// 当前这次攻击的**实际段数**（AoE 也要用）。
    ///
    /// 【2026-09-27 新增：AoE 补刀改用它，不再依赖命中事件】
    ///   AoE 需要"打几段就砍几刀"，而**命中事件 <see cref="AttackHitTriggered" /> 不可靠**：
    ///   实测（godot.log）一张 2 段 AoE 牌只收到 **1 次** `Attack` 触发器派发，
    ///   于是计数只 +1 ⇒ 欠账永远不够 ⇒ 第 2、3 刀完全没有动画（用户反馈的"闪回"）✗。
    ///   （原因：命中事件挂在 `CreatureCmd.TriggerAnim` 的 Prefix 上，而它在 async 方法里，
    ///    多段之间可能被合并/时序错位；这与之前"帧号去重吞命中"是同一类问题。）
    ///
    ///   而**段数是权威的**：它来自 `Hook.ModifyAttackHitCount` 的 Postfix
    ///   （反编译 `AttackCommand.cs:537`，连遗物改段数都算进去了），日志里稳定输出
    ///   `段数已算定 = 2/3` ✓。所以 AoE 的"该砍几刀"直接用这个值 ✓。
    /// </summary>
    public static int EffectiveHitCount => _hitCountKnown ? _hitCount : 1;

    // ───────────────────── 每段命中的通知（**不去重**） ─────────────────────
    //
    // 【2026-09-27 起：帧号去重已删除】上一轮为了"同一帧内的重复派发只算一次命中"加了
    //   Prefix 置帧号 + Postfix 清帧号的去重门，实机反而出现三个回归 ✗：
    //     * 打小怪时**连续 4 次右拳**（翻转一次都没发生）；
    //     * 打蜂群术士时**右、右、左、右**（首两拳同侧又回来了）；
    //     * 连击途中**突然切回 stance 再继续出拳**（= `atk_guard` 的 GuardEnd 在连击途中到点收拳）。
    //   前两条 = "翻转没发生"，第三条 = "计时器重置没发生"，**两者同时失效** ⇒ 去重把
    //   **合法命中整段吞掉了**（去重门一旦被卡住，后续每一次命中都被 `return` 掉，
    //    既不翻转、也不重置计时器 —— 与症状完全吻合）。
    //
    //   为什么会被卡住：`CreatureCmd.TriggerAnim` 是 **async** 方法
    //   （反编译 `CreatureCmd.cs:967 public static async Task TriggerAnim(...)`），
    //   Harmony 的 **Postfix 在"方法返回 Task 那一刻"就跑**（编译器的桩同步返回到
    //   `<>t__builder` 之后立刻 return；真正的等待在状态机的 MoveNext 里），
    //   **不是 await 完成之后**。于是"置帧号 → 清帧号"发生在**同一瞬间**，
    //   清标记在**本次派发真正落到后端播放之前**就执行了；
    //   而真正触发状态机的 `NCreature.SetAnimationTrigger`（→ `ModAnimStateMachine.SetTrigger`）
    //   反而在**后面**。时序彻底错位 ⇒ 该被去掉的没去掉、该保留的被吞掉 ✗。
    //
    //   现在改为**完全不去重**：让"重复回调"变得**无害**（见 GaoshouCharacter 的两处订阅）：
    //     * 左右交替不再由事件驱动，而是**由状态机在进入出拳状态时自己记账**
    //       （`AnimationStarted` 里进 `atk_video_punch_r/_l` 时翻 `punchSideFlip`）⇒ 调用几次都不影响 ✓；
    //     * 收拳计时器重置是**幂等**的（重建一个同长度计时器，重复重建结果完全相同）✓。
    //   订阅方（状态机）抛异常不能影响游戏结算 ⇒ 这里仍然兜住。

    /// <summary>
    /// 每**命中一段**（= 每段派发一次 `Attack` 触发器）就触发一次。
    ///
    /// 【为什么需要这个事件】游戏侧每段命中都会在 `AttackCommand` 的命中循环体内
    /// `await CreatureCmd.TriggerAnim(..., "Attack", ...)`（反编译 `AttackCommand.cs:584`），
    /// 这是"每段"唯一可靠的信号源；`Hook.ModifyAttackHitCount`（`:550`）在循环**之外**、
    /// 每张牌只调一次，拿它做"每段"会漏 ✗。
    ///
    /// 【2026-09-27 起语义收窄】本事件**只**用于"重置收拳计时器"，**不再**用于决定左右交替
    /// （交替改由状态机自己记账，见 GaoshouCharacter）。这样即使同一段命中让本事件被调用多次，
    /// 也只是**多重置几次计时器**（幂等、无害）✓，不会再把左右顺序搞乱、更不会吞掉命中 ✓。
    ///
    /// ⚠️ 纯旁路：只在"玩家 + Attack 触发器"时发通知，不改游戏侧任何状态 ✓。
    /// </summary>
    public static event Action? AttackHitTriggered;

    /// <summary>
    /// 每**命中一段并结算完成**（= 该段的 <c>CreatureCmd.Damage</c> 已经 await 返回）就触发一次。
    ///
    /// 【与 <see cref="AttackHitTriggered" /> 的区别 —— 这就是"打完之后回 stance"的根因所在】
    /// 游戏侧一段命中的顺序是（反编译 <c>AttackCommand.Execute</c> 的命中循环体）：
    /// <list type="number">
    ///   <item>触发 `Attack` 触发器：<c>await CreatureCmd.TriggerAnim(...)</c>（本副本 :571）</item>
    ///   <item>结算伤害：<c>AddResultsInternal(await CreatureCmd.Damage(...))</c>（本副本 :653）</item>
    /// </list>
    /// **第 1 步是"出拳动画开始"，第 2 步才是"这一拳打完"。**
    ///
    /// 原来只有第 1 步的通知（<see cref="AttackHitTriggered" />），于是收拳计时器是**从"出拳开始"计时**的；
    /// 而 `TriggerAnim` 内部只等 <c>min(delay*0.5, 0.25)</c>（`delay` = <see cref="GaoshouCharacter" /> 的
    /// 攻击动画延迟）就返回 —— 也就是说"出拳开始"那一刻起，计时器就只剩
    /// <c>GuardHoldSeconds - 半段延迟</c> 的余量，而真正的下一段还要等这一段的伤害结算完才来 ⇒
    /// **末段稍长 / 段间隔稍大就会在最后一段走完之前到点收拳** ✗（实机症状：打完回 stance）。
    ///
    /// 现在补上第 2 步的通知：计时器改为**从"这一拳打完"计时**，语义与注释里承诺的
    /// "最近一次命中之后过了 GuardHoldSeconds 才收拳"终于一致 ✓。
    ///
    /// ⚠️ 纯旁路：<c>AddResultsInternal</c> 的 Postfix 只发通知，不改游戏状态 ✓。
    /// </summary>
    public static event Action? AttackHitSettled;

    /// <summary>由 <see cref="RecordAttackHitSettledPatch" /> 在每段伤害结算完成时调用。</summary>
    public static void NotifyHitSettled()
    {
        // 订阅方（状态机）抛异常不能影响游戏结算 ⇒ 这里兜住。
        try
        {
            AttackHitSettled?.Invoke();
        }
        catch (Exception e)
        {
            Entry.Logger.Error($"[Gaoshou][AoE] AttackHitSettled 订阅方异常（已忽略）: {e.Message}");
        }
    }

    /// <summary>
    /// 由 <see cref="RecordAttackTriggerPatch" />（挂 <c>CreatureCmd.TriggerAnim</c>）在**每段命中**时调用。
    /// 只关心玩家自己的 `Attack` 动画触发 —— 敌人动、玩家受击/Cast 都不该影响我的连击计时 ✓。
    ///
    /// ⚠️ **不去重**（去重的理由与它为何必须删掉，见上方大段注释）：
    ///    重复调用是**无害**的 —— 唯一的订阅方做的是"重建一个同长度的收拳计时器"，幂等 ✓。
    /// </summary>
    public static void NotifyHitTriggered()
    {
        // 订阅方（状态机）抛异常不能影响游戏结算 ⇒ 这里兜住。
        try
        {
            AttackHitTriggered?.Invoke();
        }
        catch (Exception e)
        {
            Entry.Logger.Error($"[Gaoshou][AoE] AttackHitTriggered 订阅方异常（已忽略）: {e.Message}");
        }
    }

    /// <summary>
    /// <c>Hook.ModifyAttackHitCount</c> 的 Postfix 回调：只把游戏算出来的实际段数**抄一份**，
    /// <b>不改写</b>结果（Postfix 不碰 <c>__result</c> ⇒ 对游戏零影响 ✓）。
    /// </summary>
    public static void RecordHitCount(AttackCommand command, int result)
    {
        // 与 Enter 相同的过滤：只关心玩家自己发起的攻击（敌人 / 宠物不该影响我的动画风格）。
        if (command.Attacker is not { IsPlayer: true })
            return;

        _hitCount = result;
        _hitCountKnown = true;
        Trace($"段数已算定 = {result}（多段={result >= 2}，IsMultihit={IsMultihit}）");
        if (LogDiagnostics)
            Entry.Logger.Info($"[Gaoshou][AoE] 实际段数={result}（多段={_hitCount >= 2}）: {Describe(command)}");
    }

    /// <summary>进入 <c>AttackCommand.Execute</c>：只有最外层那次攻击才决定风格。</summary>
    public static void Enter(AttackCommand command)
    {
        // 只认玩家自己发起的攻击：敌人攻击、宠物攻击不该改写玩家动画的风格，
        // 也不该占住会话（否则玩家下一张牌会被误判成"嵌套"）。
        if (command.Attacker is not { IsPlayer: true })
            return;

        var now = Time.GetTicksMsec() / 1000.0;
        var stale = _session == null || now - _sessionStartedAt > StaleSessionSeconds;
        // 同一张牌的后续段（完美棍剑：先单体多段、再全体一段）**要**能改写风格；
        // 而出处不同的命令（反伤 / 亡语 / 追击打出来的攻击）是嵌套，不能改写。
        var samePlay = !stale && command.CardPlay != null &&
                       ReferenceEquals(command.CardPlay, _session!.CardPlay);

        if (!stale && !samePlay)
        {
            if (LogDiagnostics)
                Entry.Logger.Info(
                    $"[Gaoshou][AoE] 嵌套攻击（沿用外层风格 aoe={_aoe}）: {Describe(command)}");
            return;
        }

        _session = command;
        _sessionStartedAt = now;
        TraceReset();
        Trace($"── 攻击会话开始 ──（段数待算，IsAoe 暂 {command.IsMultiTargeted && !command.IsRandomlyTargeted}）");
        _aoe = command.IsMultiTargeted && !command.IsRandomlyTargeted;
        // 新会话 = 段数还没算出来 ⇒ 先把"已知段数"清掉，免得上一张牌的多段判定渗到这一张
        //（ModifyAttackHitCount 紧随其后就会把它设成真实值 ✓）。
        _hitCount = 1;
        _hitCountKnown = false;
        if (LogDiagnostics)
            Entry.Logger.Info($"[Gaoshou][AoE] 新攻击会话 aoe={_aoe}: {Describe(command)}");
    }

    /// <summary>该命令执行完（挂在 <c>__result.ContinueWith</c>）：只有会话主人能收尾，且不清风格。</summary>
    public static void Exit(AttackCommand command)
    {
        if (!ReferenceEquals(_session, command))
            return;

        _session = null;
        Trace("── 攻击会话结束（Execute 的 Task 已完成）──");
        if (LogDiagnostics)
            Entry.Logger.Info($"[Gaoshou][AoE] 攻击会话结束（风格保留到下一次攻击开始）: {Describe(command)}");
    }

    /// <summary>
    /// 视觉回到站姿（状态机进入 idle）时收尾 —— 比 <c>ContinueWith</c> 更贴近"这一次攻击真的结束了"，
    /// 于是紧接着打出的下一张牌不会被误判成嵌套。由 GaoshouCharacter 的状态机回调。
    /// </summary>
    public static void CloseSession()
    {
        _session = null;
        // 攻击真的结束了 ⇒ 段数判定也一起作废，避免"上一串连击是多段"渗给下一张单击牌。
        _hitCount = 1;
        _hitCountKnown = false;
    }

    private static string Describe(AttackCommand c)
    {
        return $"multi={c.IsMultiTargeted} single={c.IsSingleTargeted} random={c.IsRandomlyTargeted} "
               + $"card={c.ModelSource?.GetType().Name ?? "null"} id={c.GetHashCode()}";
    }
}

/// <summary>
/// 挂 <c>AttackCommand.Execute</c>：Prefix 判定风格，Postfix 用 <c>__result.ContinueWith</c> 收尾
/// （⚠️ <c>Execute</c> 是 async，Harmony 的 Postfix 在方法**返回 Task 时**就跑，必须挂到 Task 上）。
/// </summary>
public sealed class SetAoeAttackStylePatch : IPatchMethod
{
    public static string PatchId => "gaoshou_set_aoe_attack_style";

    public static string Description =>
        "decide whether this attack's visuals are the AoE sweep (outermost command wins, nested commands keep the style)";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(AttackCommand), nameof(AttackCommand.Execute)),
    ];

    public static void Prefix(AttackCommand __instance) => GaoshouAttackStyle.Enter(__instance);

    public static void Postfix(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        __result?.ContinueWith(_ => GaoshouAttackStyle.Exit(__instance));
    }
}

/// <summary>
/// 挂 <c>Hook.ModifyAttackHitCount</c>：把游戏算出来的**实际段数**抄一份给状态机用
///（见 <see cref="GaoshouAttackStyle.IsMultihit" />）。
///
/// ⚠️ 这是 **Postfix 且不碰 <c>__result</c>** ⇒ 纯旁路读取，对游戏行为零影响 ✓。
/// ⚠️ 必须签名完全匹配：游戏的 <c>Hook.ModifyAttackHitCount</c> 返回的是 <c>decimal</c>
///    （`Hook.cs:1309`），不是 int —— 写成 int 会让 Harmony 匹配不上而静默失效 ✗。
/// </summary>
public sealed class RecordAttackHitCountPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_record_attack_hit_count";

    public static string Description =>
        "record the effective attack hit count so the state machine can tell multihit (combo) attacks apart";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(Hook), nameof(Hook.ModifyAttackHitCount)),
    ];

    public static void Postfix(AttackCommand attackCommand, decimal __result) =>
        GaoshouAttackStyle.RecordHitCount(attackCommand, (int)__result);
}

/// <summary>
/// 挂 <c>CreatureCmd.TriggerAnim</c>：**每段命中**都会在这里派发一次 <c>"Attack"</c>
/// （反编译 `AttackCommand.cs:584`，在 `for (int i...)` 循环体内，`_playOnEveryHit` 默认 true）。
///
/// 用途：给两手路径的**收尾计时器**在"每段命中"时重置 ——
///   * 「视频版出拳」：每次命中播一次出拳序列，`atk_guard` 的 GuardEnd 计时器要在命中时推后；
///   * 「连续攻击」循环（`UseAttackLoop` 时才会走到）：循环 cue 自主播，同样靠它推后收尾。
/// 为什么不能挂 <c>ModifyAttackHitCount</c>：那个在 `AttackCommand.cs:550`、**命中循环之外**，
/// 一张 4 段牌只调一次 ⇒ 只会重置一次计时器，连击中途就被 GuardEnd 打断收拳 ✗。
///
/// ⚠️ 纯旁路：Prefix 只在"玩家 + Attack 触发器"时发一个通知，**不改参数、不拦原方法** ✓。
/// ⚠️ **只有 Prefix，没有 Postfix**（Postfix 已删，理由见类尾注释）：目标是 async 方法，
///    Postfix 在返回 Task 时即触发、看不到 await 完成，任何"等它跑完"的收尾逻辑都是错的 ✗。
/// </summary>
public sealed class RecordAttackTriggerPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_record_attack_trigger";

    public static string Description =>
        "notify per-hit 'Attack' animation triggers so the combo loop can reset its retract timer";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        // ⚠️ 显式写全参数类型：保证只挂在 `TriggerAnim(Creature, string, float)` 这一个重载上
        //    （只写名字的话，将来游戏多出一个重载就可能被匹配到别的签名上 ✗）。
        PatchTarget.Method(
            typeof(CreatureCmd),
            nameof(CreatureCmd.TriggerAnim),
            typeof(Creature),
            typeof(string),
            typeof(float)),
    ];

    public static void Prefix(Creature creature, string triggerName)
    {
        // 只认玩家自己的出拳；敌人动作、玩家受击/Cast 一律不参与连击计时 ✓。
        if (creature is not { IsPlayer: true })
            return;

        if (!string.Equals(triggerName, "Attack", StringComparison.Ordinal))
            return;

        GaoshouAttackStyle.NotifyHitTriggered();
    }

    // ⚠️【2026-09-27 起本补丁**只有 Prefix**，Postfix 已删除】
    //    它原来的唯一职责是清掉上面那次派发留下的"去重帧号"。而 `CreatureCmd.TriggerAnim` 是
    //    **async** 方法，Harmony 的 Postfix 在"方法返回 Task 那一刻"就跑（真正的 await 在状态机
    //    MoveNext 里）⇒ 清帧号发生在"本次派发真正落到后端播放"**之前**，
    //    Prefix/Postfix 的时间点几乎重合，整个去重门既拦不住重复、又会吞掉后续合法命中 ✗
    //    （实机三个回归的根因）。现在改为**完全不去重**：重复回调无害
    //    （交替由状态机自己记账、计时器重置幂等）⇒ 这个 Postfix 已无存在意义，一并删掉 ✓。
    //    ⚠️ 别再把它加回来做"收尾"用途：async 方法的 Postfix **看不到 await 完成**，
    //       任何"等这个方法真正跑完再做事"的写法都是错的 ✗。
}

/// <summary>
/// 挂 <c>AttackCommand.AddResultsInternal</c>：**每一段命中结算完成**时发通知
/// （反编译 <c>AttackCommand.Execute</c> 命中循环体的最后一句：
/// <c>AddResultsInternal(await CreatureCmd.Damage(...))</c>，本副本 :653）。
///
/// 【为什么必须是这个方法，而不是别的】
/// <list type="bullet">
///   <item><c>Hook.ModifyAttackHitCount</c>（:537）在命中循环**之外**，一张牌只调一次 ⇒ 拿不到"每段" ✗；</item>
///   <item><c>CreatureCmd.TriggerAnim</c>（:571）在**每段**都调 ✓，但它是"**出拳开始**"，
///         且本身是 async（Harmony 的 Postfix 看不到 await 完成）⇒ 只凭它，计时器会从"出拳开始"起算，
///         比"这一拳打完"早了半段动画，末段就会在动画走完前到点收拳 ✗（实机症状：打完回 stance）；</item>
///   <item><c>AddResultsInternal</c>（:653）在每段都调 ✓，**且它的实参里带着 `await CreatureCmd.Damage(...)`**
///         ⇒ Harmony 的 Prefix 在"Damage 还没跑"时触发、**Postfix 在"Damage 已经 await 返回之后"才触发** ✓。
///         这正是"**这一拳真的打完了**"这一时刻，收拳计时器应该从这里起算 ✓。</item>
/// </list>
///
/// ⚠️ **为什么这个 Postfix 是安全的**（与 <see cref="RecordAttackTriggerPatch" /> 的 Postfix 不同）：
///    那个方法本身是 `async`，Postfix 挂在**桩方法**上，早于真正的 await；
///    而 `AddResultsInternal` 是**普通同步方法**，await 写在**调用方的实参**里 ⇒
///    Postfix 触发时实参**已经求值完毕**（即 Damage 已结算完）⇒ 时刻是准的 ✓。
///
/// ⚠️ 纯旁路：Postfix 只发通知，**不碰参数、不碰任何游戏状态** ✓。
/// </summary>
public sealed class RecordAttackHitSettledPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_record_attack_hit_settled";

    public static string Description =>
        "notify when each hit's damage has settled so the combo timer counts from the end of the punch";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        // ⚠️ 显式写全参数类型：只挂在 `AddResultsInternal(IEnumerable<DamageResult>)` 上。
        //    本机 sts2.dll（0.1.0+41cef1ea / 2026-08-29）里它是 **1 个参数** ✓
        //    （更新的 betaTest 源码里是 2 个参数，带 cardPlay；将来游戏更新要跟着改这里）。
        PatchTarget.Method(
            typeof(AttackCommand),
            nameof(AttackCommand.AddResultsInternal),
            typeof(IEnumerable<DamageResult>)),
    ];

    public static void Postfix(AttackCommand __instance)
    {
        // 只认玩家自己发起的攻击：敌人打人 / 反伤 / 亡语不该影响玩家的连击收拳计时 ✓。
        if (__instance.Attacker is not { IsPlayer: true })
            return;

        GaoshouAttackStyle.NotifyHitSettled();
    }
}
