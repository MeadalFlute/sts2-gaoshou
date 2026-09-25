using System;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Visuals.Definition;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace Gaoshou.Patches;

/// <summary>
/// 高手模组设置里的「角色形象」页：
///   ① 形象来源 —— 继承原版储君（白模占位）/ 使用高手静态立绘（只有待机·死亡两张静态图）/
///      使用高手 PNG 帧序列（受击·死亡·攻击·横扫全套动画）；
///   ② 人物选择背景 —— 非"继承储君"时，两张立绘背景里选一张；
///   ③ 帧序列选项 —— 多段攻击（拳）与横扫（AoE）的"等待下一段"时长滑条。
///
/// 值存模组数据存储（<see cref="SaveScope.Global" />，键 <see cref="DataKey" />，文件 visual_settings.json）。
/// 注意 RitsuLib 要求：**先 store.Register 注册数据键，再建 binding**，否则设置项会加载失败。
///
/// 读取入口是静态属性 <see cref="UseStaticVisuals" /> / <see cref="UseFrameSequences" /> /
/// <see cref="GuardHoldSeconds" /> / <see cref="AoeHoldSeconds" /> / <see cref="CharacterSelectBgPath" />，
/// 由 <see cref="Characters.GaoshouCharacter.AssetProfile" /> 与状态机在**每次取值/每场战斗**时调用
/// （不是构造时快照），所以改完设置后：新一场战斗 / 下一次进人物选择界面就会用新形象。
/// </summary>
[RegisterSingleton]
public sealed class GaoshouVisualSettings : SingletonModel
{
    public const string DataKey = "gaoshou_visual_settings";

    // ⚠️ 必须和 GaoshouTutorialSettings 的 "settings.json" **分开**：RitsuLib 的持久化条目是
    // (文件名, scope) → 一个模型，两个不同的 DataKey 共用一个文件名会互相覆盖（后写的把前一个的
    // JSON 顶掉，重载时对方只剩默认值）✗。
    private const string DataFile = "visual_settings.json";

    /// <summary>形象来源：继承原版储君（白模占位）。</summary>
    public const string ModePlaceholder = "placeholder";

    /// <summary>形象来源：使用高手静态立绘（只有待机 / 死亡两张静态图，**不做帧动画**）。</summary>
    public const string ModeStatic = "static";

    /// <summary>形象来源：使用高手的 PNG 帧序列（受击 / 死亡 / 攻击 / 横扫全部动画）。</summary>
    public const string ModeFrames = "frames";

    /// <summary>人物选择背景：图一（战斗姿态）。</summary>
    public const string BgBattle = "battle";

    /// <summary>人物选择背景：图二（湖边垂钓）。</summary>
    public const string BgFishing = "fishing";

    // ---- 动画素材集（站姿 / 死亡两套帧序列的来源）----

    /// <summary>动画素材集：**旧版**（H3 之前的静帧版站姿 6 帧 + 官方原版死亡 7 帧）。**默认值**。</summary>
    public const string AnimSetLegacy = "legacy";

    /// <summary>动画素材集：**新版**（H3 视频生成的站姿 39 帧 + 死亡 16 帧）。</summary>
    public const string AnimSetNew = "new";

    // ---- 静态图资源路径（全部在 PCK 的 res://Gaoshou 下）----

    /// <summary>战斗视觉场景（Node2D + Sprite2D，见 scenes/characters/gaoshou_visuals.tscn）。</summary>
    public const string StaticVisualsScenePath = Entry.ResPath + "/scenes/characters/gaoshou_visuals.tscn";

    // ---- 站姿「呼吸」循环帧（MiniMax H3 图生视频 → 抠像 → 对齐裁帧，2026-09-24）----
    //
    // 来源：`_imgwork/video_out/stance-r6-inframe.mp4`（1:1 构图静帧 + "披风绝不出画"提示词，
    // 首尾帧同图 ⇒ 天然无缝），取第 46~84 帧 = **整整一个自然周期**（39 帧）⇒ 首尾同相位、无缝。
    // 管线：gen 侧 `_imgwork/h3_run.py`（提交/轮询/下载）+ `_imgwork/h3_period.py`（测自然周期 +
    //       画布裁切检查）+ `_imgwork/h3_edge_check.py`（查视频有没有把披风甩出画布）
    //       + `_imgwork/h3_make_loop.py`（抠像/对位/裁帧）+ `_imgwork/h3_finalize_frames.py`
    //       （去白边 + 脚部刚性对齐 + 预览）+ `_imgwork/qa_idle_frames.py`（逐帧 QA）。
    // ⚠️ 帧率与幅度都在这里可调：想更缓就把 IdleLoopFrameSeconds 调大（同一套帧、动作摊到更长的时间）；
    //    想更小就得回 gen 侧重挑片段（幅度是**素材属性**，程序化缩放会产生幽灵影 ✗）。
    //    ⚠️ 切窗口必须切**整整一个自然周期**：只切一小段会"闪回"、看着像披风被切掉 ✗（2026-09-25 踩过）。

    /// <summary>站姿循环帧数（Gaoshou_char_idle_1..N.png）。</summary>
    public const int IdleLoopFrameCount = 39;

    /// <summary>站姿循环每帧时长（秒）。
    ///
    /// 素材是 H3 视频（r6）里**整整一个摆动周期**：39 帧、源速 1.62 秒/圈；
    /// 高度极差 1px、披风宽度极差 65px（全程连续摆动）、末→首接缝 0.0011（仅为普通帧步进的 0.21×）✓。
    /// 这里按用户要求**摊到半速 = 3.24 秒/圈**（≈0.083s 一帧、约 12fps）。
    /// 想更缓就继续调大这个数；想更快就调小（帧数不用动）。</summary>
    public const float IdleLoopFrameSeconds = 3.24f / 39f;

    /// <summary>站姿循环帧贴图路径（1 起）。</summary>
    public static string IdleFramePath(int index)
    {
        return Entry.ResPath + "/images/characters/Gaoshou_char_idle_" + index + ".png";
    }

    /// <summary>当前动画素材集是否为**旧版**（<see cref="AnimSetLegacy" />）。</summary>
    public static bool UseLegacyAnimSet =>
        !string.Equals(Current().AnimSet, AnimSetNew, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 新版站姿循环帧序列（idle 状态是 loop ⇒ 靠 RitsuLib 的 Loop(true) 播放）。
    ///
    /// ⚠️ 这里是**属性**（每次读取现算）而不是 static readonly 字段：素材集是设置项，
    /// 字段只在类加载时算一次，改完设置就再也不会变了 ✗。消费方
    /// <c>GaoshouCharacter.BuildCombatCues()</c> 在**每场战斗**重建 cue 表时读取，
    /// 所以改完设置后新一场战斗就会用新素材（与 <see cref="UseStaticVisuals" /> 等的语义一致）。
    /// </summary>
    public static (string Path, float Seconds)[] IdleLoopSequence => BuildIdleLoopSequence();

    private static (string Path, float Seconds)[] BuildIdleLoopSequence()
    {
        var frames = new (string, float)[IdleLoopFrameCount];
        for (var i = 0; i < IdleLoopFrameCount; i++)
            frames[i] = (IdleFramePath(i + 1), IdleLoopFrameSeconds);
        return frames;
    }

    /// <summary>待机帧贴图（512×512，人物高 330，脚底落在 y=456）。</summary>
    public const string IdleTexturePath = Entry.ResPath + "/images/characters/Gaoshou_char_idle.png";

    /// <summary>死亡帧贴图（512×512，贴地边对齐 y=456）。</summary>
    public const string DeadTexturePath = Entry.ResPath + "/images/characters/Gaoshou_char_dead.png";

    // ---- 战斗「受击」帧序列（RitsuLib 帧序列 cue；**分三段**，见 GaoshouCharacter 的状态图）----
    //
    // 贴图由 _workspace 的 _imgwork 产线生成（同一张基准图 img2img，脚底锚点/缩放共用 char_frame_geom）：
    //   gen_hurt_frames.py + assemble_hurt_anim.py + make_hurt_game_frames.py        → Gaoshou_char_hurt_1..8
    //   gen_transition_frames.py + make_transition_game_frames.py                     → Gaoshou_char_hurt_{enter,exit}_1..3
    //
    // 受击主体帧号 → 画面：
    //   1 直立高位防御（基准，也是回位终点）  2 受击瞬间（后仰 6~8°）  3 后仰最深（10~12°，披风惯性最大）
    //   4 回弹（拳略前顶，披风滞后仍上扬）    5 受击段收尾（护姿基本恢复）  6 回位 1  7 回位 2（轻微向前过冲）  8 回位 3
    //
    // 三段序列（逐帧时长，秒）：
    //   * <see cref="HurtEnterSequence" />：站姿 → 护姿 的过渡（+末尾接护姿帧）——只在"从待机挨第一下"时播；
    //   * <see cref="HurtBodySequence" />：受击瞬间 → 后仰 → 回位到护姿 ——**从第 2 帧起播**（立刻有反应），
    //     连续挨打时每次都从这里重放（RitsuLib 的 CueFrameSequencePlayer.TryStart 第一句就是 StopAndReset）；
    //   * <see cref="HurtExitSequence" />：护姿 → 站姿 的过渡 —— 收招用，播完由状态机切回待机贴图。
    //
    /// <summary>进入段：站姿→护姿（1~3 为 Gaoshou_char_hurt_enter_N.png，末尾接护姿帧）。</summary>
    public static readonly (string Path, float Seconds)[] HurtEnterSequence =
    [
        (HurtTransitionFramePath("enter", 1), 0.05f),
        (HurtTransitionFramePath("enter", 2), 0.05f),
        (HurtTransitionFramePath("enter", 3), 0.06f),
        (HurtFramePath(1), 0.06f),
    ];

    /// <summary>
    /// 受击主体：从"受击瞬间"起播，回位到护姿（护姿帧 = 第 1 帧）。
    ///
    /// ⚠️ 2026-09-24 实机反馈"衔接不自然、像是回正时过倾"⇒ **去掉了第 7 帧（回位2 的向前过冲）**。
    /// 量化依据（头顶相对腰带扣的横向偏移，正值=前倾；站姿基线 +2.7、护姿 +2.4）：
    ///   6 = +1.9 → **7 = +13.1** → 8 = +0.2，在 0.10s/帧 里来回甩 12px ⇒ 短时长下就是"顿一下"。
    /// 去掉后回位变成 6(+1.9) → 8(+0.2) → 1(+2.4)，单调平滑；同时把回位段放慢（0.10/0.12/0.12）。
    /// 第 7 帧贴图仍然保留（Gaoshou_char_hurt_7.png），需要"更冲"的版本时可以再挂回来。
    /// </summary>
    public static readonly (string Path, float Seconds)[] HurtBodySequence =
    [
        (HurtFramePath(2), 0.05f),   // 受击瞬间（立刻有反应）
        (HurtFramePath(3), 0.06f),   // 后仰最深
        (HurtFramePath(4), 0.07f),   // 回弹（披风滞后）
        (HurtFramePath(5), 0.08f),   // 受击段收尾
        (HurtFramePath(6), 0.10f),   // 回位 1
        (HurtFramePath(8), 0.12f),   // 回位 3（跳过会过冲的第 7 帧）
        (HurtFramePath(1), 0.12f),   // 回到护姿，交给 hurt_exit
    ];

    /// <summary>退出段：护姿→站姿（起点为护姿帧，之后 1~3 为 Gaoshou_char_hurt_exit_N.png）。</summary>
    public static readonly (string Path, float Seconds)[] HurtExitSequence =
    [
        (HurtFramePath(1), 0.06f),
        (HurtTransitionFramePath("exit", 1), 0.08f),
        (HurtTransitionFramePath("exit", 2), 0.09f),
        (HurtTransitionFramePath("exit", 3), 0.10f),
    ];

    /// <summary>
    /// 受击主体帧贴图路径。规格与 <see cref="IdleTexturePath" /> 完全一致（512×512 透明画布、
    /// 脚底落在 y=456、水平锚点 x=256）—— RitsuLib 的帧序列播放器**不做任何尺寸/锚点归一化**，
    /// 只改 Sprite2D.Texture，所以帧与帧之间只要画布或锚点不一致就一定抖。
    /// </summary>
    public static string HurtFramePath(int frame)
    {
        return Entry.ResPath + "/images/characters/Gaoshou_char_hurt_" + frame + ".png";
    }

    /// <summary>
    /// 过渡帧贴图路径：<paramref name="phase" /> 传 "enter" 或 "exit"，<paramref name="index" /> 为 1~3。
    /// 规格与受击帧共用（同一套换算），所以可以混在同一段帧序列里播。
    /// </summary>
    public static string HurtTransitionFramePath(string phase, int index)
    {
        return Entry.ResPath + "/images/characters/Gaoshou_char_hurt_" + phase + "_" + index + ".png";
    }

    // ---- 战斗「死亡」帧序列（站立 → 跪地 → 趴地，2026-09-24 新增）----
    //
    // 贴图 640×512（**加宽**：躺姿统一缩放后最宽 499px，512 只剩 6px 余量；高度仍 512、地面仍 y=456，
    // Sprite2D 居中绘制 ⇒ 多出的透明边不会让画面位移，场景与代码都不用改）。
    //
    // 两个入口（游戏侧查证过调用顺序，见 _workspace\sts2spy-test 反编译）：
    //   * `CreatureCmd` 在伤害结算里派发 `Hit`（CreatureCmd.cs:347，等待时间 waitTime=0 ⇒ **不等我们的
    //     受击动画播完**），紧接着 `await Kill(...)`（:431）→ `NCreature.StartDeathAnim`（:546）
    //     ⇒ **被打死时是从受击动画切入的**（人物还停在第 2~4 帧的受击姿势）；
    //   * 中毒/失血/放弃游戏等非攻击死亡则是从**待机**切入。
    //   * 注意：`StartDeathAnim` 里的 `SetAnimationTrigger("Dead")` 被包在 `if (_spineAnimator != null)`
    //     里（NCreature.cs:956），我们是静态图本收不到 —— 由 RitsuLib 的
    //     `NCreatureNonSpineDeathAnimationTriggerPatches`（patch StartDeathAnim）补发 ✓。
    // 所以死亡分两段：idle 版从站姿起、受击版先垫一帧"受击瞬间"（复用 hurt 第 2 帧，无需新素材）。
    // 状态图的 dead 状态**不设 NextState**（终止态）⇒ 播完定格在最后一帧 = 尸体姿势 ✓。

    // 素材：H3 视频抠像的 **16 帧**（站立 → 脱力 → 跪地 → 折身 → 前扑 → 触地 → 趴地）。
    // 落位约定：640×512 画布 / 地面线 y=456 / 人物中心 x=320（与官方死亡家族一致）。
    // 时长：16 × 0.11s = 1.76 秒。想更缓就调大 DieFrameSeconds（帧数不用动）。
    // ⚠️ 16 帧静止图本由 RitsuLib 帧序列播放器逐帧换 Sprite2D.Texture，**不做任何尺寸/锚点归一化**
    //    ⇒ 帧间画布/锚点必须完全一致（统一 640×512 + 底边 456 + 中心 x 320），否则一定抖。

    /// <summary>新版死亡帧数（Gaoshou_char_die_h3_1..N.png）。</summary>
    public const int DieFrameCount = 16;

    /// <summary>新版死亡每帧时长（秒）。16 × 0.11s = 1.76 秒。</summary>
    public const float DieFrameSeconds = 0.11f;

    /// <summary>新版死亡帧贴图路径（640×512，底边 y=456，中心 x=320）。</summary>
    public static string DieFramePath(int frame)
    {
        return Entry.ResPath + "/images/characters/Gaoshou_char_die_h3_" + frame + ".png";
    }

    // ─────────────────────── 旧版（legacy）死亡帧 ───────────────────────
    //
    // 素材来源：官方原版 7 帧，备份在 `_imgwork\stance_motion\final\die-backup-official\`
    //   （2026-09-26 复制回 Gaoshou_char_die_1..7.png）。
    // 每帧时长是**原始节奏**（不是均分）：合计 1.41 秒。

    /// <summary>旧版（官方原版）死亡帧数。</summary>
    public const int LegacyDieFrameCount = 7;

    /// <summary>旧版死亡帧贴图路径（Gaoshou_char_die_1..7.png，官方原版规格）。</summary>
    public static string LegacyDieFramePath(int frame)
    {
        return Entry.ResPath + "/images/characters/Gaoshou_char_die_" + frame + ".png";
    }

    /// <summary>
    /// 旧版死亡 7 帧的**原始逐帧时长**（秒），合计 1.41 秒。
    /// 索引 0 = 第 1 帧（0.15s）……索引 6 = 第 7 帧（0.35s，收尾定格帧最久）。
    /// </summary>
    private static readonly float[] LegacyDieFrameSeconds =
        [0.15f, 0.18f, 0.15f, 0.20f, 0.18f, 0.20f, 0.35f];

    /// <summary>旧版死亡第 N 帧的时长（1 起；越界回落到 0.11s 兜底，正常不会走到）。</summary>
    private static float LegacyDieSecondsOf(int frame)
    {
        return frame >= 1 && frame <= LegacyDieFrameSeconds.Length
            ? LegacyDieFrameSeconds[frame - 1]
            : DieFrameSeconds;
    }

    /// <summary>
    /// 死亡（从待机切入）：新版 = die_h3_1..16（站姿 → 脱力 → 跪地 → 折身 → 前扑 → 触地 → 趴地）；
    /// 旧版 = die_1..7（官方原版 7 帧 + 原始逐帧节奏）。
    /// ⚠️ 同样用属性（每次读取现算）而不是 static readonly 字段，理由见 <see cref="IdleLoopSequence" />。
    /// </summary>
    public static (string Path, float Seconds)[] DieFromIdleSequence => BuildDieFromIdleSequence();

    private static (string Path, float Seconds)[] BuildDieFromIdleSequence()
    {
        if (UseLegacyAnimSet)
        {
            var legacy = new (string, float)[LegacyDieFrameCount];
            for (var i = 0; i < LegacyDieFrameCount; i++)
                legacy[i] = (LegacyDieFramePath(i + 1), LegacyDieSecondsOf(i + 1));
            return legacy;
        }

        var frames = new (string, float)[DieFrameCount];
        for (var i = 0; i < DieFrameCount; i++)
            frames[i] = (DieFramePath(i + 1), DieFrameSeconds);
        return frames;
    }

    /// <summary>死亡（从受击切入）：先垫一帧"受击瞬间"（复用 hurt 第 2 帧），再接第 2..N 帧，
    /// 末尾再补一次末帧作收尾定格帧（保持原语义"跳过第一帧、其余相同"，总时长不变）。
    /// 新版 = 垫帧 + h3_2..h3_16 + h3_16 收尾；旧版 = 垫帧 + die_2..die_7 + die_7 收尾（逐帧沿用各自节奏）。</summary>
    public static (string Path, float Seconds)[] DieFromHurtSequence => BuildDieFromHurtSequence();

    private static (string Path, float Seconds)[] BuildDieFromHurtSequence()
    {
        var count = UseLegacyAnimSet ? LegacyDieFrameCount : DieFrameCount;
        Func<int, string> pathOf = UseLegacyAnimSet ? LegacyDieFramePath : DieFramePath;
        Func<int, float> secondsOf = UseLegacyAnimSet
            ? LegacyDieSecondsOf
            : static _ => DieFrameSeconds;

        // 1 帧受击垫帧 + die_2..die_N（count - 1 帧）+ 1 帧 die_N 收尾
        var frames = new (string, float)[1 + (count - 1) + 1];
        frames[0] = (HurtFramePath(2), 0.06f);
        for (var i = 1; i < count; i++)
            frames[i] = (pathOf(i + 1), secondsOf(i + 1));
        frames[^1] = (pathOf(count), secondsOf(count));
        return frames;
    }

    // ---- 战斗「攻击」帧序列（架势 → 左/右拳交替 → 收拳回站姿，2026-09-24 新增）----
    //
    // 游戏侧行为（已按反编译核实，反编译在 _workspace\sts2spy-betaTest）：
    //   * `AttackCommand.cs:582-585` 每次命中前 `await TriggerAnim(Attacker,"Attack",_attackerAnimDelay)`；
    //   * `:85 _playOnEveryHit = true`（默认）⇒ **多段攻击每命中一次都重发 `Attack`**，
    //     我们的序列会一段一段重放 ⇒ 正好用来做"左右拳交替"；
    //   * 等待时长 = `CreatureCmd.cs:999 CustomScaledWait(min(delay*0.5,0.25), delay)`，delay 就是角色
    //     的 `AttackAnimDelay`（由 GaoshouCharacter.AttackAnimDelay 提供，已设为 0.13s）；
    //   * **攻击结束后游戏不发任何 `Idle` 触发器**（全库只有 CreatureCmd.cs:768 的治疗路径发），
    //     所以"收拳回站姿"必须靠状态图 `WithNext` 自己串。
    //
    // 帧（同一套统一缩放、脚底锚定）：ready1 收拳握拳 / ready2 出拳架势（兼任左右拳之间的回弹落点）
    // / punchr 右直拳 / punchl 后手转身横击（姿势来自用户交付的 GPT 帧，配色已按材质对齐到 punchr）
    // / retract1·2 收拳回站姿。

    /// <summary>攻击帧贴图路径。</summary>
    public static string AttackFramePath(string name)
    {
        return Entry.ResPath + "/images/characters/Gaoshou_char_atk_" + name + ".png";
    }

    /// <summary>
    /// 右直拳：**起手 2 帧（ready1/ready2）+ 出拳 + 回到架势**。
    ///
    /// 为什么把起手并进每一拳里（而不是单独做一个 `atk_ready` 状态）：
    /// 交替必须能跨越"两次命中之间状态已经回到 idle"的情况 —— 例如蜂群术士每段受击会插入额外流程，
    /// 把两段命中的间隔拉到超过整套序列（≈0.33s），状态回到 idle 后再挨打就又是"第一拳"✗。
    /// 现在每拳自带起手，`idle → 哪只拳` 由状态机外部记录的"上一拳是哪边"决定（见 GaoshouCharacter），
    /// 所以交替不再依赖"上一次的状态还停在拳上"。
    /// 每拳 0.06+0.07+0.07/0.08+0.05+0.08 ≈ 0.33s，与 `AttackAnimDelay = 0.13s`（起手长度）配合，
    /// 伤害正好落在出拳那一刻。
    /// </summary>
    public static readonly (string Path, float Seconds)[] AttackPunchRightSequence =
    [
        (AttackFramePath("ready1"), 0.06f),
        (AttackFramePath("ready2"), 0.07f),
        (AttackFramePath("punchr"), 0.07f),
        (AttackFramePath("punchr"), 0.05f),
        (AttackFramePath("ready2"), 0.08f),
    ];

    /// <summary>后手转身横击：与右拳同一结构（起手 + 出拳 + 回架势），只换出拳帧与时长。</summary>
    public static readonly (string Path, float Seconds)[] AttackPunchLeftSequence =
    [
        (AttackFramePath("ready1"), 0.06f),
        (AttackFramePath("ready2"), 0.07f),
        (AttackFramePath("punchl"), 0.08f),
        (AttackFramePath("punchl"), 0.05f),
        (AttackFramePath("ready2"), 0.08f),
    ];

    /// <summary>
    /// 收拳回站姿（左右共用同一组贴图，只是状态 id 不同，用于交替判定）。
    /// ⚠️ 只在**攻击结束后**才播：出拳后先进 `atk_guard` 停在架势，隔 `GuardHoldSeconds`
    /// 没有新命中才收到 `GuardEnd` 触发器走这里（否则多段攻击会变成"每拳都松手回站姿"✗）。
    /// </summary>
    public static readonly (string Path, float Seconds)[] AttackRetractSequence =
    [
        (AttackFramePath("retract1"), 0.08f),
        (AttackFramePath("retract2"), 0.10f),
    ];

    /// <summary>出拳后停留的"架势"姿势（用 ready2 的**静态贴图**：静态 cue 不会播完 ⇒ 一直保持）。</summary>
    public static string AttackGuardTexturePath => AttackFramePath("ready2");

    // 收拳前的"架势停留时长"已改成设置项，见属性 GuardHoldSeconds（设置页：帧序列选项 → 多段攻击动画等待时间（拳））。

    /// <summary>收拳触发器名（由 GaoshouCharacter 用 SceneTree 计时器发出）。</summary>
    public const string GuardEndTrigger = "GuardEnd";

    // ─────────────────────────── AoE 横扫（斩马长刀） ───────────────────────────
    //
    // 【为什么要单独一套】游戏打单体/打全体都只发同一个 `Attack` 触发器、不带目标信息，
    // 所以"这次是横扫还是拳"由 GaoshouAttackStyle.IsAoe 判定 ——
    // 在 `AttackCommand.Execute` 的 Prefix 里读命令自身的 `IsMultiTargeted && !IsRandomlyTargeted`（见那个文件）。
    //
    // 帧规格：**768×512**（而不是 512×512）—— 斜劈残影弧横向很长，512 画布会把弧梢裁掉 ✗。
    // 高度仍 512、地面仍 y=456、双脚在中线上（x=384），Sprite2D 居中绘制 ⇒ 加宽的透明边不会让画面位移。
    // 换算用 from_raw_wide(horizontal='feet')：先剔除粉色残影再取双脚底线中点 —— 按"整体外框居中"会让
    // 人物在 slashB 上横向跳 95px ✗（刀伸向左下时外框被刀拉偏）。
    //
    // 顺序（用户 2026-09-24 指定，几何上连贯）：
    //   draw1    手伸到腰后起手（无刀）
    //   slashB   拔刀即向左下横砍（刀停在左下）
    //   slashA   反手撩到右上（残影 左下→右上）
    //   draw2    刀收在右下（= slashA 的落点附近）
    //   slashC   回砍到左下（残影 右上→左下，正好接上 slashB 的刀位）
    //   slashB   再停一次左下，作为收刀前的落点
    //   sheathe2 刀收回腰间（≈站姿，无缝回 idle）
    // 也就是"拔刀 → 右砍 → 左砍 → 收刀"；两刀之间是**按命中段数**走的，见下面的分段说明。
    //
    // 与拳套的关系：AoE 不走 `atk_guard` 那套"停在架势"，而是各自停在**本刀的落点**上
    // （hold_r = 刀右下 / hold_l = 刀左下），计时器同理但更短（AoeHoldSeconds）。

    /// <summary>AoE 横扫帧贴图路径（768×512）。</summary>
    public static string AoeFramePath(string name)
    {
        return Entry.ResPath + "/images/characters/Gaoshou_char_aoe_" + name + ".png";
    }

    // 帧拆成"起手 / 两刀 / 收刀"四段 + 两个落点停留姿势，是为了让**单段 AoE 直接收刀**：
    //   单段（1 次命中）：draw(0.15) → cut_r(0.20) → hold_r →〔AoeEnd 计时器〕→ sheathe(0.12) → idle
    //   多段（N 次命中）：每命中一段走下一刀（cut_r ↔ cut_l 交替），最后同样按 AoeEnd 收刀
    // 状态机无法预知"这一段之后还有没有下一段"，所以用"停在刀落点上等 AoeHoldSeconds"来判定
    // —— 与拳那套 GuardEnd + atk_guard 完全同一个套路。

    /// <summary>起手段：手伸向腰后 → 拔刀即左下横砍（刀停在左下）。</summary>
    public static readonly (string Path, float Seconds)[] AoeDrawSequence =
    [
        (AoeFramePath("draw1"), 0.10f),
        (AoeFramePath("slashB"), 0.05f),
    ];

    /// <summary>第一刀：从左下撩到右上（残影 左下→右上），收在右下。</summary>
    public static readonly (string Path, float Seconds)[] AoeCutRightSequence =
    [
        (AoeFramePath("slashA"), 0.08f),
        (AoeFramePath("draw2"), 0.12f),
    ];

    /// <summary>第二刀：从右上回砍到左下（残影 右上→左下），停在左下。</summary>
    public static readonly (string Path, float Seconds)[] AoeCutLeftSequence =
    [
        (AoeFramePath("slashC"), 0.08f),
        (AoeFramePath("slashB"), 0.05f),
    ];

    /// <summary>收刀：刀收回腰间（≈站姿，接 idle 不跳）。</summary>
    public static readonly (string Path, float Seconds)[] AoeSheatheSequence =
    [
        (AoeFramePath("sheathe2"), 0.12f),
    ];

    /// <summary>第一刀落点的停留姿势（刀在右下）—— 静态 cue 不会播完 ⇒ 一直保持到下一段或 AoeEnd。</summary>
    public static string AoeHoldRightTexturePath => AoeFramePath("draw2");

    /// <summary>第二刀落点的停留姿势（刀在左下）。</summary>
    public static string AoeHoldLeftTexturePath => AoeFramePath("slashB");

    // 收刀前的"落点等待时长"已改成设置项，见属性 AoeHoldSeconds（设置页：帧序列选项 → 横扫落点等待时间）。

    /// <summary>收刀触发器名（由 GaoshouCharacter 用 SceneTree 计时器发出）。</summary>
    public const string AoeEndTrigger = "AoeEnd";

    /// <summary>AoE 各段 cue / 状态 id。</summary>
    public const string AoeDrawCue = "atk_aoe_draw";

    public const string AoeCutRightCue = "atk_aoe_cut_r";
    public const string AoeCutLeftCue = "atk_aoe_cut_l";
    public const string AoeHoldRightCue = "atk_aoe_hold_r";
    public const string AoeHoldLeftCue = "atk_aoe_hold_l";
    public const string AoeSheatheCue = "atk_aoe_sheathe";


    /// <summary>商店形象贴图（1024×768，人物在画布中央偏左）。</summary>
    public const string ShopTexturePath = Entry.ResPath + "/images/characters/Gaoshou_char_shop.png";

    /// <summary>营火休息形象贴图（1024×1024，坐姿，人物底边 y=967，坐面 y≈735）。</summary>
    public const string RestTexturePath = Entry.ResPath + "/images/characters/Gaoshou_char_rest.png";

    // 多人游戏手势贴图（422×1200，与原版 images/ui/hands/multiplayer_hand_*.png 同尺寸）。
    // 四个文件对应 RitsuLib 的 CharacterMultiplayerAssetSet：
    //   ArmPointing（指人）/ ArmRock（石头）/ ArmPaper（布）/ ArmScissors（剪刀）。
    //
    // 【对齐要点，重新生成手势图后必须重做】判定点固定在**贴图像素**上，和节点/画布无关：
    //   * 指人手势：pivot 见 scenes/ui/hand_image.tscn 的 TextureRect.pivot_offset，
    //     代码里是 NHandImage._pointingPivot = (163, 10)；贴图 1:1 绘制
    //     （expand_mode=1 让控件最小尺寸被贴图撑到 422×1200，stretch_mode=5 正好 1:1），
    //     所以**指尖画在哪、玩家就得点哪** —— 指尖顶点必须落在 (163, 10) 附近。
    //   * 石头/布/剪刀：pivot 是 NHandImage._fightingPivot = (197, 600)（掌心），
    //     按"内容 bbox 中心对齐该点"来对，谁也不是靠指尖。
    //
    // 2026-09-22 修过一次：AI 出的原图指尖顶点在 (169.5, 106)，比原版 (127.5, 22)
    // 低了 84px、偏右 42px，导致"看着点在按钮上其实点不到"。已整体平移对齐：
    //   point (-42,-84) 指人对齐到原版像素；rock (-7,-110) / paper (0,-110) / scissors (0,-90)
    //   （这三张受画布上沿限制，没能拉到理想的 -113/-125/-118，差 15~28px）。
    public const string HandPointTexturePath = Entry.ResPath + "/images/hands/Gaoshou_hand_point.png";
    public const string HandRockTexturePath = Entry.ResPath + "/images/hands/Gaoshou_hand_rock.png";
    public const string HandPaperTexturePath = Entry.ResPath + "/images/hands/Gaoshou_hand_paper.png";
    public const string HandScissorsTexturePath = Entry.ResPath + "/images/hands/Gaoshou_hand_scissors.png";

    // ---- 世界形象（商店 / 营火）的显示样式 ----
    //
    // 商店和营火不需要自定义场景：RitsuLib 的 ModWorldSceneVisualNodeFactory 会在内存里搭好
    // NMerchantCharacter / NRestSiteCharacter（含 ControlRoot、%Hitbox、%SelectionReticle、
    // %ThoughtBubbleLeft|Right 和 Visuals(Sprite2D)），并把这里的 cue 贴到那个 Sprite2D 上，
    // 所以尺寸/位置只能靠 VisualNodeStyle 调。
    //
    // 基准：让世界形象在 1080p 下约 265px 高（与原版休息处小人同量级）。
    //   营火容器缩放 0.5：0.6 × 880(人物高) × 0.5 ≈ 264px
    //   商店容器缩放 1  ：0.68 × 729(人物高)      ≈ 496px（2026-09-22 实测原尺寸偏小，放大到 1.89 倍）
    // position 的作用是把"人物底边 + 水平中心"挪到节点原点（原版的世界形象原点在脚底），
    // 营火则是把"坐面"对准左侧圆木的顶面。

    /// <summary>
    /// 营火休息（坐姿）的显示样式。
    ///
    /// 2026-09-22 微调记录（每次都按"屏幕上人物位置不变"来反算）：
    ///   ① 原图 1024×1024（人物 622×880）→ scale 0.5 / pos (−41, −20)：人物在屏幕上占 x545..700、y600..820；
    ///   ② 斗篷重画后 1254×1254（人物 753×1082，斗篷改成搭在腿上而不是直上直下）：
    ///      按高度归一 → scale = 0.5 × 880 / 1082 = 0.407；
    ///      bbox 底边中点仍对齐屏幕 (622.5, 820) → pos (−43, −19)。**屏幕上位置与 ① 完全一致**。
    ///
    /// 换算（重要，别再算错）：
    ///   * 立绘中心屏幕位置 = (651 + 0.5·x, 716 + 0.5·y)（651/716 = Character_1 节点原点）
    ///   * 人物屏幕高度 = 画布人物高 × scale × 0.5（休息处容器缩放 0.5）
    ///   * position 改 1 = 游戏内 0.5px = 2560×1440 截图上 0.667px
    ///   * **想在 2560 截图上动 N px → position 增减 1.5·N；想在游戏内动 N px → 增减 2·N**
    ///
    /// 参考：原版左侧圆木顶面是斜的（屏幕 x=572→y726、x=680→y683、x=788→y627）。
    /// </summary>
    public static readonly VisualNodeStyle RestStyle = VisualNodeStyle.Create(
        position: new Vector2(-43f, -19f), scale: new Vector2(0.407f, 0.407f));

    /// <summary>商店（思考姿态）的显示样式：人物底边 y=754 / 水平中心 x=482.5 对齐节点原点。</summary>
    public static readonly VisualNodeStyle ShopStyle = VisualNodeStyle.Create(
        position: new Vector2(20f, -252f), scale: new Vector2(0.68f, 0.68f));

    /// <summary>
    /// 商店/营火里「放弃游戏」时切到的尸体图样式：512×512 画布的贴地边在 y=456，
    /// 居中贴图时它在中心下方 200px，所以上移 200 让贴地边落到节点原点。
    /// </summary>
    public static readonly VisualNodeStyle CorpseStyle = VisualNodeStyle.Create(
        position: new Vector2(0f, -200f));

    /// <summary>人物选择背景场景：战斗姿态（2560×1440 全屏图）。</summary>
    public const string SelectBgBattleScenePath = Entry.ResPath + "/scenes/characters/gaoshou_select_bg_a.tscn";

    /// <summary>人物选择背景场景：湖边垂钓（2560×1440 全屏图）。</summary>
    public const string SelectBgFishingScenePath = Entry.ResPath + "/scenes/characters/gaoshou_select_bg_b.tscn";

    public override bool ShouldReceiveCombatHooks => false;

    // 设置值缓存：AssetProfile 每次取值都会读到，不能每次都走数据存储反序列化。
    private static GaoshouVisualSettingsData? _cache;

    public GaoshouVisualSettings()
    {
        RegisterDataStore();
        MigrateLegacyVisualMode();

        RitsuLibFramework.RegisterModSettings(
            Entry.ModId,
            page => page
                .WithSortOrder(120)
                .AsChildOf("gaoshou_main")
                .WithTitle(T("角色形象", "Character Visuals"))
                .WithDescription(T(
                    "选择高手的战斗形象，以及人物选择界面的背景。",
                    "Choose Gaoshou's combat visuals and the character-select background."))
                .AddSection("visual_mode", section =>
                {
                    section.WithTitle(T("形象来源", "Visual Source"));
                    section.AddChoice(
                        "visual_mode",
                        T("角色形象", "Character visuals"),
                        VisualModeBinding(),
                        [
                            new(ModePlaceholder, T("继承储君（占位）", "Regent placeholder")),
                            new(ModeStatic, T("使用高手静态图", "Gaoshou static images")),
                            new(ModeFrames, T("使用高手 PNG 帧序列", "Gaoshou PNG frame sequences")),
                        ],
                        T(
                            "影响范围：战斗、商店、火堆、宝箱、死亡。\n" +
                            "「静态图」只有待机 / 死亡两张静态立绘（不做帧动画）；" +
                            "「PNG 帧序列」才是受击、死亡、出拳、横扫全部动画。",
                            "Affects: combat, shop, campfire, treasure, death.\n" +
                            "Static images = idle/dead stills only (no frame animation); " +
                            "PNG frame sequences = the full hurt/death/attack/sweep animations."),
                        ModSettingsChoicePresentation.Dropdown);
                })
                .AddSection("anim_set", section =>
                {
                    section.WithTitle(T("动画素材集", "Animation Asset Set"));
                    section.AddChoice(
                        "anim_set",
                        T("动画素材版本", "Animation asset version"),
                        AnimSetBinding(),
                        [
                            new(AnimSetLegacy, T("旧版（默认）", "Legacy (default)")),
                            new(AnimSetNew, T("新版（H3 视频）", "New (H3 video)")),
                        ],
                        T(
                            "只影响「PNG 帧序列」模式下的站姿与死亡动画，其它动作（受击 / 出拳 / 横扫）不受影响：\n" +
                            "· 旧版 = 静态立绘站姿（单张 Gaoshou_char_idle.png，不做帧动画）+ 官方原版死亡 7 帧；\n" +
                            "· 新版 = H3 视频生成的站姿 39 帧（3.24 秒一圈）+ 死亡 16 帧。\n" +
                            "改动在下一场战斗生效。",
                            "Only affects the idle stance and death animation in PNG frame-sequence mode; " +
                            "hurt / punch / sweep are unaffected:\n" +
                            "- Legacy = still-image stance (single Gaoshou_char_idle.png, no frame animation) " +
                            "+ the original 7-frame death animation.\n" +
                            "- New = H3 video-generated 39-frame stance (3.24 s per loop) + 16-frame death " +
                            "animation.\n" +
                            "Changes apply in the next combat."),
                        ModSettingsChoicePresentation.Dropdown);
                })
                .AddSection("frame_sequence", section =>
                {
                    section.WithTitle(T("帧序列选项", "Frame-Sequence Options"));
                    section.AddParagraph(
                        "frame_sequence_note",
                        T(
                            "多段攻击（拳）与横扫（AoE）都是「每命中一段发一次 Attack」，动画靠" +
                            "停在落点上等一小会儿来判定后面还有没有下一段：\n" +
                            "· 等得到 → 接着挥下一刀（拳左右交替 / 横扫左右横砍）；\n" +
                            "· 等不到 → 收招回站姿（所以这两个值也是单段攻击收招前的延迟）。\n" +
                            "段间隔比设定值大时会出现「中途先收招再重新起手」，把这个值调大即可。",
                            "Multi-hit attacks and AoE sweeps fire one Attack trigger per hit; the animation " +
                            "waits at the recovery pose to see whether another segment follows:\n" +
                            "- another segment arrives -> swing the next one (alternating);\n" +
                            "- nothing arrives -> return to the idle stance.\n" +
                            "If the gap between segments is longer than this value you will see the character " +
                            "recover and wind up again mid-attack; raise the value to fix it."));
                    section.AddSlider(
                        "multihit_wait",
                        T("多段攻击动画等待时间（拳）", "Multi-hit attack wait (punches)"),
                        MultiHitWaitBinding(),
                        0.1,
                        2.0,
                        0.05,
                        static v => v.ToString("0.00") + " s",
                        T(
                            "出拳后停在架势里等下一次命中的时间；到点没有新命中就收拳回站姿。",
                            "How long the character holds the guard pose after a punch before retracting."));
                    section.AddSlider(
                        "sweep_wait",
                        T("横扫落点等待时间（收刀前）", "AoE sweep wait (before sheathing)"),
                        SweepHoldBinding(),
                        0.05,
                        1.0,
                        0.05,
                        static v => v.ToString("0.00") + " s",
                        T(
                            "横扫每刀砍完后停在落点上等下一段的时间；单段 AoE 就是" +
                            "「砍完这一刀 → 等这么久 → 收刀」的等待。",
                            "How long the sweep waits at the blade's landing pose for the next segment; " +
                            "for a single-segment AoE this is the delay before sheathing."));
                })
                .AddSection("select_bg", section =>
                {
                    section.WithTitle(T("人物选择背景", "Character-Select Background"));
                    section.AddParagraph(
                        "select_bg_note",
                        T(
                            "只要不是「继承储君」就生效；选「继承储君」时沿用原版的储君背景。\n" +
                            "改动在重新进入人物选择界面（或重新选择该角色）时生效。",
                            "Applies whenever the Regent placeholder is not selected; the placeholder keeps the vanilla background.\n" +
                            "Changes apply when the character-select screen is opened again."));
                    section.AddChoice(
                        "select_bg",
                        T("人物选择立绘", "Character-select art"),
                        SelectBgBinding(),
                        [
                            new(BgBattle, T("图一：战斗姿态", "Art 1: battle stance")),
                            new(BgFishing, T("图二：湖边垂钓", "Art 2: fishing by the pond")),
                        ],
                        presentation: ModSettingsChoicePresentation.Dropdown);
                }),
            pageId: "gaoshou_visuals");
    }

    // ---------------- 读取（给角色 AssetProfile 用） ----------------

    /// <summary>是否使用高手自己的形象（静态图或帧序列都算）；false = 继承原版储君白模。</summary>
    public static bool UseStaticVisuals =>
        string.Equals(Current().VisualMode, ModeStatic, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Current().VisualMode, ModeFrames, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 是否使用 **PNG 帧序列**（受击 / 死亡 / 攻击 / 横扫）。false（纯静态图模式）时只播待机与死亡两张静态图，
    /// 出拳 / 横扫 / 受击都停在待机上 —— 给"只想换张皮、不想有帧动画"或排查动画问题用。
    /// </summary>
    public static bool UseFrameSequences =>
        string.Equals(Current().VisualMode, ModeFrames, StringComparison.OrdinalIgnoreCase);

    /// <summary>拳：出拳后停在架势里等下一次命中的时间（秒）。设置页「多段攻击动画等待时间（拳）」。</summary>
    public static float GuardHoldSeconds => (float)Clamp(Current().MultiHitWaitSeconds, 0.1, 2.0);

    /// <summary>横扫：每刀砍完后停在落点上等下一段的时间（秒）。设置页「横扫落点等待时间（收刀前）」。</summary>
    public static float AoeHoldSeconds => (float)Clamp(Current().SweepHoldSeconds, 0.05, 1.0);

    /// <summary>手改 JSON 也別让值跑到滑条范围外（滑条本身会约束，这里只是兜底）。</summary>
    private static double Clamp(double value, double min, double max)
    {
        return double.IsFinite(value) ? Math.Clamp(value, min, max) : min;
    }

    /// <summary>人物选择背景场景路径；继承储君时返回 null，交给占位角色补齐。</summary>
    public static string? CharacterSelectBgPath => !UseStaticVisuals
        ? null
        : string.Equals(Current().SelectBg, BgFishing, StringComparison.OrdinalIgnoreCase)
            ? SelectBgFishingScenePath
            : SelectBgBattleScenePath;

    /// <summary>写设置时清缓存（RitsuLib 写完值后会广播）。</summary>
    internal static void InvalidateCache()
    {
        _cache = null;
    }

    // ---------------- 内部 ----------------

    private static void RegisterDataStore()
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId, false))
        {
            store.Register(DataKey, DataFile, SaveScope.Global,
                static () => new GaoshouVisualSettingsData(), true);
        }

        // 写入后让缓存失效，保证下一次拼 AssetProfile 时读到最新值。
        ModSettingsBindingWriteEvents.ValueWritten += binding =>
        {
            if (!string.Equals(binding.ModId, Entry.ModId, StringComparison.Ordinal) ||
                !string.Equals(binding.DataKey, DataKey, StringComparison.Ordinal))
                return;

            InvalidateCache();
            Entry.Logger.Info(
                $"[GaoshouVisualSettings] visuals = {Current().VisualMode}, select bg = {Current().SelectBg}, " +
                $"anim set = {Current().AnimSet} (legacy = {UseLegacyAnimSet})");
        };
    }

    /// <summary>
    /// 一次性迁移：老版本的形象来源只有 placeholder / static 两项，而当时的 static **本身就是**
    /// "静态立绘 + 帧动画"。现在拆成 static（纯静态立绘）与 frames（PNG 帧序列）之后，第一次读到
    /// 老的 static 必须按 frames 处理并写回，否则老存档会突然一点战斗动画都没有 ✗。
    /// 见 <see cref="GaoshouVisualSettingsData.VisualModeSplitMigrated" />。
    /// </summary>
    private static void MigrateLegacyVisualMode()
    {
        try
        {
            var store = RitsuLibFramework.GetDataStore(Entry.ModId);
            if (store.Get<GaoshouVisualSettingsData>(DataKey).VisualModeSplitMigrated)
                return;

            store.Modify<GaoshouVisualSettingsData>(DataKey, data =>
            {
                if (!data.VisualModeSplitMigrated &&
                    string.Equals(data.VisualMode, ModeStatic, StringComparison.OrdinalIgnoreCase))
                {
                    data.VisualMode = ModeFrames;
                }

                data.VisualModeSplitMigrated = true;
            });
            store.Save(DataKey);          // Modify 只改内存 + 广播，落盘要显式 Save
            InvalidateCache();
            Entry.Logger.Info(
                $"[GaoshouVisualSettings] legacy visual mode migrated -> {Current().VisualMode}");
        }
        catch (Exception e)
        {
            Entry.Logger.Error($"[GaoshouVisualSettings] legacy visual mode migration failed: {e.Message}");
        }
    }

    private static GaoshouVisualSettingsData Current()
    {
        try
        {
            return _cache ??= RitsuLibFramework.GetDataStore(Entry.ModId)
                .Get<GaoshouVisualSettingsData>(DataKey);
        }
        catch (Exception e)
        {
            Entry.Logger.Error($"[GaoshouVisualSettings] read failed: {e.Message}");
            return new GaoshouVisualSettingsData();
        }
    }

    private static IModSettingsValueBinding<string> VisualModeBinding()
    {
        return ModSettingsBindings.Global<GaoshouVisualSettingsData, string>(
            Entry.ModId,
            DataKey,
            static data => data.VisualMode,
            static (data, value) => data.VisualMode = value);
    }

    private static IModSettingsValueBinding<string> SelectBgBinding()
    {
        return ModSettingsBindings.Global<GaoshouVisualSettingsData, string>(
            Entry.ModId,
            DataKey,
            static data => data.SelectBg,
            static (data, value) => data.SelectBg = value);
    }

    private static IModSettingsValueBinding<string> AnimSetBinding()
    {
        return ModSettingsBindings.Global<GaoshouVisualSettingsData, string>(
            Entry.ModId,
            DataKey,
            static data => data.AnimSet,
            static (data, value) => data.AnimSet = value);
    }

    private static IModSettingsValueBinding<double> MultiHitWaitBinding()
    {
        return ModSettingsBindings.Global<GaoshouVisualSettingsData, double>(
            Entry.ModId,
            DataKey,
            static data => data.MultiHitWaitSeconds,
            static (data, value) => data.MultiHitWaitSeconds = value);
    }

    private static IModSettingsValueBinding<double> SweepHoldBinding()
    {
        return ModSettingsBindings.Global<GaoshouVisualSettingsData, double>(
            Entry.ModId,
            DataKey,
            static data => data.SweepHoldSeconds,
            static (data, value) => data.SweepHoldSeconds = value);
    }

    private static ModSettingsText T(string zh, string en)
    {
        // 随游戏当前语言出文案：zhs 用中文，其余用英文。
        var isZh = string.Equals(LocManager.Instance.Language, "zhs", StringComparison.OrdinalIgnoreCase);
        return ModSettingsText.Literal(isZh ? zh : en);
    }
}

/// <summary>设置项的数据模型（JSON 存在模组数据存储里）。</summary>
public sealed class GaoshouVisualSettingsData
{
    /// <summary>
    /// 形象来源：<see cref="GaoshouVisualSettings.ModePlaceholder" />（继承储君）/
    /// <see cref="GaoshouVisualSettings.ModeStatic" />（只有静态立绘）/
    /// <see cref="GaoshouVisualSettings.ModeFrames" />（PNG 帧序列，默认）。
    /// 注意：这里只是**默认值**，已经存过设置的老存档会沿用自己存的值 ——
    /// 老存档里的 "static" 由 GaoshouVisualSettings 启动时的一次性迁移改成 frames。
    /// </summary>
    public string VisualMode { get; set; } = GaoshouVisualSettings.ModeFrames;

    /// <summary>拳：出拳后停在架势里等下一次命中的时间（秒）。设置页滑条「多段攻击动画等待时间（拳）」，范围 0.1~2.0。</summary>
    public double MultiHitWaitSeconds { get; set; } = 1.0;

    /// <summary>横扫：每刀砍完后停在落点上等下一段的时间（秒）。设置页滑条「横扫落点等待时间（收刀前）」，范围 0.05~1.0。</summary>
    public double SweepHoldSeconds { get; set; } = 0.35;

    /// <summary>
    /// 形象来源拆分的**一次性迁移**标记：老版本的 static 就是"静态立绘 + 帧动画"，
    /// 现在 static 变成"纯静态立绘"，所以老值必须迁成 frames（见 MigrateLegacyVisualMode）。
    /// 迁移过一次就置 true，之后 static 才是字面意思。
    /// </summary>
    public bool VisualModeSplitMigrated { get; set; }

    /// <summary>人物选择背景：<see cref="GaoshouVisualSettings.BgBattle" /> 或 <see cref="GaoshouVisualSettings.BgFishing" />。</summary>
    public string SelectBg { get; set; } = GaoshouVisualSettings.BgBattle;

    /// <summary>
    /// 动画素材集：<see cref="GaoshouVisualSettings.AnimSetLegacy" />（**默认**：站姿用静态立绘
    /// Gaoshou_char_idle.png 不做帧动画 + 官方原版死亡 7 帧）
    /// 或 <see cref="GaoshouVisualSettings.AnimSetNew" />（H3 站姿 39 帧 + 死亡 16 帧）。
    ///
    /// **默认 legacy**，并且这里就是向后兼容的兜底：老配置文件里没有 `AnimSet` 这个字段时，
    /// System.Text.Json 反序列化不会碰这个属性 ⇒ 保留初始化器的 "legacy" ✓（已核实 RitsuLib 的
    /// ModDataStore 用的是标准 JsonSerializerOptions，没有 MissingMemberHandling.Error）。
    /// </summary>
    public string AnimSet { get; set; } = GaoshouVisualSettings.AnimSetLegacy;
}
