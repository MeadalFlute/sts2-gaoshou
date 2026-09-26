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
///   ③ 帧序列选项 —— 多段攻击（拳）与横扫（AoE）的"等待下一段"时长滑条，
///      以及**连续攻击循环动画的整体快慢**滑条（连击速度）。
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

    // ---- 「视频版出拳序列」（H3 攻击视频抠帧 → 每次命中播一次，2026-09-26 新增）----
    //
    // 【为什么要这一套（用户实机反馈）】`anim_set = new` 时攻击走的是「整段视频循环」
    //   （atk_loop_prehold → atk_loop_entry → atk_loop），在连击速度 1.00× 下实测：
    //     * 多段攻击卡动画、打不出拳；第一下出伤之后左拳才挥出来、第二下出伤时右拳已经缩回去；
    //     * 后续动画顺序变成"左拳、右拳、右拳"。
    //   根因是**循环方案与出伤的因果方向反了**：循环 cue 自主按固定速度播，伤害却是游戏按自己的
    //   节奏结算的，两者只能靠调速度"碰运气对齐"，任何速度下都不可能每一段都对上 ✗。
    //
    // 【做法：照抄原版 PNG 序列的工作机制】原版那条路径（<see cref="AttackPunchRightSequence" /> /
    //   <see cref="AttackPunchLeftSequence" />）是**已经验证与出伤匹配**的：
    //   游戏 `AttackCommand` 每命中一段就派发一次 `Attack` 触发器（`AttackCommand.cs:584`，
    //   `_playOnEveryHit` 默认 true）⇒ 状态机每次命中都切进一个**一次性**出拳 cue
    //   （`CueFrameSequencePlayer.TryStart` 第一句 `StopAndReset()` ⇒ **从头重播**），
    //   由状态机外部记的 `lastWasRight`（GaoshouCharacter）选左/右 ⇒ 一次命中 = 一拳 ✓。
    //   这里**不新造任何机制**，只是把"姿势素材"（ready1/ready2/punchr/punchl）
    //   换成 H3 视频抠出来的帧 —— 接线方式与原版逐条相同 ✓。
    //
    // 素材：`Gaoshou_char_atkloop_fast_1..43.png`（源视频 f1..f43 抠像后落位，512×512、
    //   与攻击家族同画布同锚点）。用户确认的**权威动作分解**：
    //     1~11 Ready｜12~18 左拳出拳｜19~20 左拳收拳｜21~36 右拳出拳｜37~41 收右拳｜41~43 Ready
    //   ⚠️ 贴图文件**保持源编号不变**，下面的序列只是挑其中一段来播，不改名/不重排 ✓。

    /// <summary>「视频版出拳」用的帧贴图路径（复用 <c>Gaoshou_char_atkloop_fast_N.png</c>，**编号 = 源帧号**）。</summary>
    public static string AttackVideoFramePath(int sourceFrame)
    {
        return AttackLoopFramePath("fast", sourceFrame);
    }

    /// <summary>
    /// 左拳（视频版）：源 **f12~f20**，共 <b>9</b> 帧 = "出拳 7 帧（f12~f18）+ 收拳 2 帧（f19~f20）"。
    ///
    /// 【帧号从哪来】上表：12~18 左拳出拳｜19~20 左拳收拳 ⇒ 从"左拳出拳第 1 帧"起、
    /// 到"左拳收拳最后一帧"止，一整招连出带收 ✓。
    ///
    /// 【每帧时长怎么取】**等分** <see cref="AtkVideoPunchSeconds" />（默认 0.33 秒 ≈ 原版一拳）：
    ///   0.33 / 9 = <b>0.036667 秒/帧</b> ⇒ 总时长 = 9 × 0.036667 = <b>0.3300 秒</b>，
    ///   与原版 <see cref="AttackPunchLeftSequence" /> 的 0.06+0.07+0.08+0.05+0.08 = 0.34 秒 差 0.010 秒。
    /// 等分（而不是按原版 5 帧的逐帧比例）的理由：视频帧本身是**连续动作的采样**，
    ///   逐帧不等速会把动作采样成"一顿一顿"的；等分 = 按源 24fps 的**匀速率**播完整招，
    ///   动作幅度自然、且总时长仍与原版对齐 ⇒ 出拳节奏（= 出伤节奏）与原版一致 ✓。
    /// </summary>
    public static (string Path, float Seconds)[] AttackVideoPunchLeftSequence => BuildAttackVideoPunchSequence(12, 20);

    /// <summary>
    /// 右拳（视频版）：源 **f21~f41**，共 <b>21</b> 帧 = "出拳 16 帧（f21~f36）+ 收拳 5 帧（f37~f41）"。
    ///
    /// 【帧号从哪来】上表：21~36 右拳出拳｜37~41 收右拳 ⇒ 从"右拳起手第 1 帧"起、
    /// 到"收右拳最后一帧"止 ✓（f41 同时是"41~43 Ready"的开头，作收拳终点正合适）。
    ///
    /// 【每帧时长怎么取】与左拳**同一套等分逻辑**：21 帧等分 <see cref="AtkVideoPunchSeconds" />
    ///   ⇒ 0.33 / 21 = <b>0.015714 秒/帧</b> ⇒ 总时长 <b>0.3300 秒</b>
    ///   （原版 <see cref="AttackPunchRightSequence" /> = 0.06+0.07+0.07+0.05+0.08 = 0.33 秒）。
    /// 【为什么两侧帧数不同却同总时长】源视频里右拳是"重拳"：出拳段长、收拳段也长（16+5 帧），
    ///   左拳是"刺拳"：出拳短、收拳也短（7+2 帧）。要让**两边都占同样的时间**（= 每次出伤间隔稳定）
    ///   就只能各自等分各自的帧数 ⇒ 右拳每帧更短（动作看起来更快），左拳每帧更长 —— 这正是
    ///   视频里两个拳头的实际速度差异，不是错误 ✓。
    /// </summary>
    public static (string Path, float Seconds)[] AttackVideoPunchRightSequence => BuildAttackVideoPunchSequence(21, 41);

    /// <summary>
    /// 🎚️ <b>「每次出拳的总时长」的唯一调节点</b>（秒；左拳 9 帧与右拳 21 帧**各自等分**这个值）。
    ///
    /// 默认 <c>0.33f</c> 的依据 = **对齐原版姿势序列的总时长**，让出拳节奏与"已对上出伤"的原版一致：
    ///   * 原版右拳 <see cref="AttackPunchRightSequence" />：0.06+0.07+0.07+0.05+0.08 = **0.33 s**；
    ///   * 原版左拳 <see cref="AttackPunchLeftSequence" />：0.06+0.07+0.08+0.05+0.08 = **0.34 s**；
    ///   ⇒ 取两者的代表值 0.33（也正好等于右拳的精确值）。
    ///
    /// 【推导】每帧时长 = 本值 / 帧数；左拳 0.33/9 ≈ 0.036667、右拳 0.33/21 ≈ 0.015714，
    ///   两条序列的总时长**都** = 0.330 秒 ⇒ 单击与多段攻击的"一拳占多久"完全一致 ✓。
    ///
    /// 🎚️ 调大 = 一整拳更慢（动作更舒展）、调小 = 更快（更像快打）。
    ///    ⚠️ 单次命中与下一次命中之间若**短于**本值，新一段会**打断上一拳从头重播**（原版行为，
    ///       见 GaoshouCharacter 的 `punch_r → punch_l` 分支）—— 这是"一次命中一拳"的正常表现，
    ///       嫌断得厉害就把本值调小。
    /// ⚠️ 与「连击速度」（<see cref="ComboSpeedScale" />）**无关**：那条滑条只作用于
    ///    `atk_loop*` 循环素材，而本次改动后攻击路径**已经完全不走循环**（见 GaoshouCharacter），
    ///    所以那条滑条对攻击不再有任何影响（素材与序列**保留未删**，随时可以再挂回去）。
    /// </summary>
    public const float AtkVideoPunchSeconds = 0.33f;

    /// <summary>
    /// 按"源帧号区间 [<paramref name="firstSourceFrame" />, <paramref name="lastSourceFrame" />]（含两端）"
    /// 构造一条视频版出拳序列：帧序 = 源顺序原样，每帧时长 = <see cref="AtkVideoPunchSeconds" /> **等分**。
    ///
    /// ⚠️ 越界（源帧号超出 1..<see cref="AttackLoopFastFrameCount" />）时返回空数组，
    ///    绝不产出指向不存在贴图的帧（`CueFrameSequencePlayer.TryStart` 碰到加载失败会
    ///    直接 `StopAndReset` + 返回 false ⇒ 状态机会卡在"进了等于没进"的死状态 ✗）。
    /// </summary>
    private static (string Path, float Seconds)[] BuildAttackVideoPunchSequence(
        int firstSourceFrame, int lastSourceFrame)
    {
        if (firstSourceFrame < 1 || lastSourceFrame > AttackLoopFastFrameCount ||
            lastSourceFrame < firstSourceFrame)
            return [];

        var count = lastSourceFrame - firstSourceFrame + 1;
        var seconds = AtkVideoPunchSeconds / count;
        var frames = new (string, float)[count];
        for (var i = 0; i < count; i++)
            frames[i] = (AttackVideoFramePath(firstSourceFrame + i), seconds);
        return frames;
    }

    /// <summary>
    /// 视频版出拳素材是否**真的可用**（左/右两条序列都非空且路径非空）。
    ///
    /// 【为什么必须有这个闸门】与 <see cref="AttackLoopMaterialAvailable" /> 完全同一个理由：
    /// <c>ModAnimStateMachine.EnterState</c> 第一句就是
    /// <c>if (!Backend.HasAnimation(state.Id)) { Warn; return; }</c>（ModAnimStateMachine.cs:167-172）
    /// —— cue 不存在时它既不进状态也不播任何 cue，画面停在上一帧且**永远等不到 Completed** ✗。
    /// 所以"要不要走视频版出拳"必须在**注册分支之前**按素材可用性决定。
    /// 素材缺失时 <see cref="UseVideoPunch" /> 自动为 false ⇒ 整体退回原版姿势帧路径（行为与改动前一致 ✓）。
    /// </summary>
    public static bool AttackVideoMaterialAvailable =>
        AttackVideoPunchLeftSequence.Length > 0 &&
        AttackVideoPunchRightSequence.Length > 0 &&
        AttackVideoPunchLeftSequence.All(static frame => !string.IsNullOrWhiteSpace(frame.Path)) &&
        AttackVideoPunchRightSequence.All(static frame => !string.IsNullOrWhiteSpace(frame.Path));

    /// <summary>
    /// 攻击路径是否使用**视频版出拳序列**（<see cref="AttackVideoPunchLeftSequence" /> /
    /// <see cref="AttackVideoPunchRightSequence" />）：**anim_set = new** 且素材可用。
    ///
    /// ⚠️ 与 <see cref="UseAttackLoop" /> 一样，状态机的"cue 注册"与"分支注册"**必须共用这一个判定**
    ///    （两处不一致就会出现"注册了分支、没注册 cue"的死状态 ✗）。
    ///
    /// ⚠️【与 <see cref="UseAttackLoop" /> 的关系】**互斥**：<see cref="UseAttackLoop" /> 现在多了一条
    ///    <c>!UseVideoPunch</c>（见该属性注释）⇒ 两者永远不会同时为 true，
    ///    所以攻击路径只会是"每次命中播一次出拳序列"或"整段循环"之一，不会两条同时挂上 ✓。
    /// </summary>
    public static bool UseVideoPunch =>
        !UseLegacyAnimSet && AttackVideoMaterialAvailable;

    /// <summary>
    /// 喂给 <c>atk_punch_l</c> cue 的**实际序列**：视频版素材可用就用
    /// <see cref="AttackVideoPunchLeftSequence" />（源 f12~f20），否则退回原版姿势帧
    /// <see cref="AttackPunchLeftSequence" />（行为与改动前逐帧一致 ✓）。
    ///
    /// ⚠️ 一定要有这层兜底：状态机**无条件**注册 `atk_punch_l/_r` 分支（那是单击路径，不是新增的），
    ///    若这里直接返回空序列，`CueFrameSequencePlayer.TryStart` 会因为 `sequence.Frames.Count == 0`
    ///    返回 false ⇒ 状态机 `EnterState` 里 `Backend.Play` 播不出东西、`Current` 却已经切过去了 ⇒
    ///    卡在"进了等于没进"的死状态 ✗。有兜底就永远不会出现空 cue ✓。
    /// </summary>
    public static (string Path, float Seconds)[] AttackVideoLeftEffectiveSequence =>
        UseVideoPunch ? AttackVideoPunchLeftSequence : AttackPunchLeftSequence;

    /// <summary>喂给 <c>atk_punch_r</c> cue 的实际序列（视频版素材可用就用源 f21~f41）。见
    /// <see cref="AttackVideoLeftEffectiveSequence" />。</summary>
    public static (string Path, float Seconds)[] AttackVideoRightEffectiveSequence =>
        UseVideoPunch ? AttackVideoPunchRightSequence : AttackPunchRightSequence;

    // 收拳前的"架势停留时长"已改成设置项，见属性 GuardHoldSeconds（设置页：帧序列选项 → 多段攻击动画等待时间（拳））。

    // ---- 战斗「连续攻击」循环帧序列（H3 视频 → Bria Fibo 抠像 → 切环，2026-09-26 新增）----
    //
    // 【为什么要这一套】单击/多段的旧路径是"每命中一段发一次 Attack → 放一次
    // ready→punch→retract"的**姿势帧**（上面那套），单击继续用它、**完全不动** ✓。
    // 但"连续攻击"（真正连着打出去的连段）用姿势帧会变成一招一顿；
    // 这两条循环是**视频生成的连贯动作**，按"打出去就循环播"来接。
    //
    // 素材来源：`_imgwork/stance_motion/final/frames_fibo-atkloop/Gaoshou_char_atk_1..124.png`
    //（H3 生成攻击视频 → Bria Fibo 抠像；fibo_matte 用**第 1 帧的固定锚点**统一变换，
    //  所以全片同一缩放/同一基准 ✓）。用户已逐帧确认动作分解：
    //   * **L1 快速连击** f1..f43：1~11 Ready｜12~18 左拳出拳｜19~20 左拳收拳｜
    //                            21~36 右拳出拳｜37~41 收右拳｜41~43 Ready
    //   * **L2 带等待连击** f57..f106：57~60 Ready｜60~66 左拳出拳｜67~83 **左拳保持击出的等待**｜
    //                            85~86 准备换拳｜87~97 右拳出拳｜98~103 右拳收拳回 Ready｜104~106 Ready
    // 两段的循环点跳变用户已确认"很小、在接受范围内" ⇒ **按上面边界原样切，不做淡化** ✓。
    //
    // ⚠️【贴图编号 ≠ 连击循环的播放顺序】L1 的 1~11 是 Ready，而帧序列播放器进 cue 必从序列第 1 项起播
    //    ⇒ 直接按 f1..f43 排会"先白播 11 帧 Ready"，第一次命中落在 Ready 段上（动画慢半拍 ✗）。
    //    所以 **fast 的序列在代码里循环左移 11 帧**（详见 <see cref="AttackLoopFastStartFrameOffset" />）：
    //    序列 = 源 f12..f43, f1..f11。**贴图文件编号保持源顺序不变**，只有代码构造序列时旋转 ✓。
    //
    // 落位：源 512×512 画布直接沿用（与 atk 家族同画布）；实测视频 Ready 相位帧
    // 双脚底线中点均值 x=255.84、底边 y=457.75，而 atk 家族（ready1/2、retract1/2）均值
    // x=252.50、y=456.75 ⇒ 切帧时**统一平移 (−3, −1)** 一次到位。落位后实测：
    // fast 全片 feet_cx 均值 252.87 / 底边 457.05；wait 250.39 / 457.44（与家族一致 ✓）。
    //
    // ⚠️ 与站姿/死亡帧同理：RitsuLib 帧序列播放器**不做任何尺寸/锚点归一化**，只改
    //    Sprite2D.Texture ⇒ 帧间画布/锚点必须完全一致，否则一定抖。

    /// <summary>连续攻击（快速连击）循环帧数：Gaoshou_char_atkloop_fast_1..N.png。</summary>
    public const int AttackLoopFastFrameCount = 43;

    /// <summary>连续攻击（带等待连击）循环帧数：Gaoshou_char_atkloop_wait_1..N.png。</summary>
    public const int AttackLoopWaitFrameCount = 50;

    /// <summary>
    /// 「快速连击」循环的**入场点偏移**（单位：源序列帧）。
    ///
    /// 【要解决的问题】RitsuLib 的帧序列播放器进入一个 cue 时**一定从序列的第 1 项开始**
    /// （<c>CueFrameSequencePlayer.TryStart</c> 第一句 <c>StopAndReset()</c>、随后 <c>_index = 0</c>
    /// 并 <c>ApplyFrame(0)</c>）。而 fast 素材按上表的动作分解，第 1 帧是 Ready 相位的**开头**：
    ///   1~11 Ready｜**12~18 左拳出拳**｜19~20 左拳收拳｜21~36 右拳出拳｜37~41 收右拳｜41~43 Ready
    /// ⇒ 进场后要先白播 11 帧 Ready（11 × 1/24s ≈ 0.46 秒）才出拳，
    /// 于是"第一次命中"落在纯 Ready 段上，观感就是**动画比命中慢半拍** ✗。
    ///
    /// 【做法】构造序列时把 43 帧**循环左移 11 帧**（方案 b：显式入场偏移常量）⇒ 序列第 1 项变成源
    /// f12 =「左拳出拳」的第 1 帧，进场即出拳 ✓。左移 = 从循环的下标 11 处读起、读满 43 帧绕回开头，
    /// 只是**换了一个循环起点**：序列内容仍是原来那 43 帧、顺序不变、循环仍闭合，
    /// 贴图文件一个都没改名/重排（旋转只发生在代码构造序列时）✓。
    ///
    /// 🎚️ 想改"从哪一拍入场"就改这个数（0 = 退回旧行为：从 Ready 段起播）；
    ///    "连击整体快慢"由设置页滑条 <see cref="ComboSpeedScale" /> 单独控制
    ///    （换算入口 <see cref="AttackLoopLoopFrameSeconds" />），两者互不影响 ✓。
    /// </summary>
    public const int AttackLoopFastStartFrameOffset = 11;

    // ───────────────── 连击速度的**基准**与**缩放**（2026-09-26 改成设置项） ─────────────────
    //
    // 【用户实机反馈（改这一版的起因）】打「完美木棍剑」对蜂群术士时：
    //   第 1 次伤害 ✓ 对上动画第 1 次左拳；但**第 1 次右拳空动画、没有伤害** ✗；
    //   第 2 次伤害落到**第 2 圈的第 1 次左拳**上 ✗
    //   ⇒ **动画出拳频率 ≈ 伤害结算频率的 2 倍**（一轮两拳 1.79 秒，而伤害约每 1.79 秒才结算一次
    //     ⇒ 每 2 拳才 1 次伤害）。
    //
    // 【做法】**不改结构、不改帧数、不改入场相位**，只把"每帧多少秒"整体乘一个可调倍数：
    //   每帧时长 = AttackLoopFrameSeconds / ComboSpeedScale
    //   默认 ComboSpeedScale = 0.5 ⇒ 每帧 = 2/24 = 1/12 秒 ⇒ **一轮两拳（fast 43 帧）≈ 3.58 秒**，
    //   正好让"每一下出拳都咬合一次伤害"（对应上面的观察：伤害约每 1.79 秒一次 ⇒ 一拳一次）✓。
    //   ComboSpeedScale = 1.0 就是改动前的速度（1/24 秒一帧、1.79 秒一圈）✓。
    //
    // ⚠️【入场与稳态必须同缩放】`AttackLoopEntrySequence` 是**照稳态序列构造**的
    //   （见 BuildAttackLoopEntrySequence：先取 BuildAttackLoopSequence("fast") 原样副本，再只覆盖
    //   过渡帧的秒数）⇒ 只要那个构造函数用缩放后的稳态帧时长，入场与稳态就**同步缩放、相对关系不变** ✓。
    //   `AttackLoopEntryBridgeSeconds`（第一记右拳提前那段）**也按同一个 scale 折算**
    //   （见 AttackLoopEntryBridgeFrameSeconds），所以"提前量"与整轮速度成**固定比例** ✓
    //   —— 否则 slow-motion 下入场那 2 帧比例失真、第一轮与第二轮的节奏会脱节 ✗。
    // ⚠️ `AttackLoopPreHoldSeconds`（0.13，对齐"动画 vs 第一次伤害"）**不缩放**：
    //   它是"这次连击什么时候开始"、要和游戏的 AttackAnimDelay 对齐，与连击有多慢无关 ✓（明确保持不动）。

    /// <summary>
    /// 连续攻击循环每帧时长的**基准值**（秒）。源视频 24fps ⇒ 1/24；
    /// **换算成实际每帧时长请用 <see cref="AttackLoopLoopFrameSeconds" />**：
    ///   实际每帧 = 本基准 / <see cref="ComboSpeedScale" />。
    ///
    /// 🎚️【这是基准，不是最终速度】改这里的含义是"源素材的原始节奏"，
    ///    真正给玩家调的是设置页滑条「连击速度」（<see cref="ComboSpeedScale" />）。
    ///    改小基准 = 整体更快；<see cref="ComboSpeedScale" /> = 1.0 时每帧就是本值 ✓。
    ///    （入场点是另一件事、由 <see cref="AttackLoopFastStartFrameOffset" /> 决定，与速度互不影响 ✓。）
    ///    经验：一轮时长应明显**长于**多段牌的命中间隔，否则一轮打不完就该收拳了；
    ///    若嫌一轮太快/太慢，同时也可以微调设置页的「多段攻击动画等待时间（拳）」
    ///    （<see cref="GuardHoldSeconds" />，决定最后一段之后多久收拳）。
    /// </summary>
    public const float AttackLoopFrameSeconds = 1f / 24f;

    /// <summary>连击速度滑条的**最小值**（越小越慢；0.25 ⇒ 每帧 4×1/24，一轮两拳约 7.17 秒）。</summary>
    public const double ComboSpeedScaleMin = 0.25;

    /// <summary>连击速度滑条的**最大值**（1.0 = 改动前的速度；2.0 ⇒ 每帧半速时长，一轮约 0.90 秒）。</summary>
    public const double ComboSpeedScaleMax = 2.0;

    /// <summary>连击速度滑条的**默认值**：0.5 = 比原先（1/24 s/帧）慢一倍。</summary>
    public const double ComboSpeedScaleDefault = 0.5;

    /// <summary>
    /// 🎚️ <b>「连击速度」设置项</b>（设置页滑条，范围 <see cref="ComboSpeedScaleMin" />~<see cref="ComboSpeedScaleMax" />，
    /// 默认 <see cref="ComboSpeedScaleDefault" />）。语义：<b>1.0 = 改动前的速度</b>，越小越慢。
    ///
    /// 【消费者】只有 <see cref="AttackLoopLoopFrameSeconds" /> / <see cref="AttackLoopEntryBridgeFrameSeconds" />
    /// 两个换算入口，也就是**只有连击循环（入场 + 稳态）用**；单击 / legacy 出拳路径
    /// （<see cref="AttackPunchRightSequence" /> 等姿势帧）与 AoE 横扫**完全不经过这里** ✓ 不受影响。
    ///
    /// 【生效时机】与其余设置一致：**下一场战斗生效**。
    ///   值读的是 <see cref="Current" />（已有缓存 + 写入时失效，见 <see cref="InvalidateCache" />），
    ///   序列属性在 <c>GaoshouCharacter.BuildCombatCues()</c> 每场战斗重建 cue 表时读取 ⇒ 拖完滑条
    ///   **不需要重启**，下一场战斗（或重进战斗）就是新速度；正在进行的这一场保持旧速度
    ///   —— 与 <see cref="GuardHoldSeconds" /> / <see cref="AoeHoldSeconds" /> / anim_set 完全同一个语义 ✓。
    /// </summary>
    public static float ComboSpeedScale => (float)Clamp(Current().ComboSpeedScale, ComboSpeedScaleMin, ComboSpeedScaleMax);

    /// <summary>
    /// **稳态**连击循环的实际每帧时长（秒）= <see cref="AttackLoopFrameSeconds" /> / <see cref="ComboSpeedScale" />。
    ///
    /// <see cref="ComboSpeedScale" /> = 1.0 时就是基准 1/24 ✓；默认 0.5 时是 1/12
    /// ⇒ fast 43 帧 = **3.583 秒/圈**（改动前 1.792 秒）、wait 50 帧 = 4.167 秒/圈。
    /// ⚠️ 除法前先兜底（<see cref="ComboSpeedScale" /> 的取值本身已被 clamp 到 0.25 以上 ⇒ 不会除零）✓。
    /// </summary>
    private static float AttackLoopLoopFrameSeconds => AttackLoopFrameSeconds / ComboSpeedScale;

    /// <summary>
    /// **入场**序列里被压缩的过渡帧（"左拳收拳 → 右拳起手"）的实际每帧时长（秒）
    /// = <see cref="AttackLoopEntryBridgeSeconds" /> / <see cref="ComboSpeedScale" />。
    ///
    /// 【为什么也要缩放】这一段的本意是"比稳态那两帧**更短**"（默认 0.010 vs 基准 1/24）。
    ///   若只有稳态缩放、这里不缩放，slow-motion 下这 2 帧几乎瞬间跳过 ⇒
    ///   **入场与稳态的相对关系被改掉了**（等于结构变了）✗。
    ///   按同一个 scale 折算后，"压缩比例"恒定：
    ///     · scale = 1.0（旧速度）：每帧 0.010 s，稳态 1/24 ≈ 0.041667 s，比值 0.24；
    ///     · scale = 0.5（新默认）：每帧 0.020 s，稳态 1/12 ≈ 0.083333 s，比值仍是 0.24 ✓。
    ///   ⇒ **缩放只改整体速度、不改结构** ✓。
    /// </summary>
    private static float AttackLoopEntryBridgeFrameSeconds => AttackLoopEntryBridgeSeconds / ComboSpeedScale;

    /// <summary>连续攻击循环帧贴图路径。<paramref name="name" /> 传 "fast" 或 "wait"。</summary>
    public static string AttackLoopFramePath(string name, int index)
    {
        return Entry.ResPath + "/images/characters/Gaoshou_char_atkloop_" + name + "_" + index + ".png";
    }

    /// <summary>
    /// 连续攻击「快速连击」循环（43 帧；默认连击速度 0.5 下 ≈3.58 秒/圈、1.0 下 ≈1.79 秒/圈，Loop(true)）。
    /// ⚠️ 与 <see cref="IdleLoopSequence" /> 同样是**属性**（每次读取现算），
    /// 因为 cue 表是每场战斗重建的，素材集设置要能即时生效。
    ///
    /// ⚠️ 序列**不是**从源 f1 开始：按 <see cref="AttackLoopFastStartFrameOffset" /> 循环左移后，
    ///    第 1 项 = 源 f12（左拳出拳第 1 帧），末项 = 源 f11（Ready 段末帧），帧数仍为 43 ✓。
    /// </summary>
    public static (string Path, float Seconds)[] AttackLoopFastSequence => BuildAttackLoopSequence("fast");

    /// <summary>
    /// 连续攻击「带等待连击」循环（50 帧；默认连击速度 0.5 下 ≈4.17 秒/圈、1.0 下 ≈2.08 秒/圈，Loop(true)）。
    ///
    /// ⚠️ **素材已就位，但暂未接到任何 cue 上**，原因（2026-09-26 复核）：
    /// 用来判定连击的信号是 <see cref="GaoshouAttackStyle.IsMultihit" />，它来自游戏开打前算出的
    /// **实际段数**（<c>Hook.ModifyAttackHitCount</c>），是**整次攻击的常数** —— 在一次连击全过程中
    /// 恒为 true，**无法区分"正在出招"与"打完了在等下一段"** ✗。而 wait 循环的语义恰恰是
    /// "左拳保持击出的等待"（源视频 f67~f83），只有能区分这两者才有意义。
    /// 按任务要求**不为了区分而硬猜 / 大改连段主干**，故此处只留素材与接口，
    /// 等状态机能拿到"本段之后还有没有下一段"的信号时再接（与 <see cref="AttackLoopFastSequence" /> 同构）。
    /// </summary>
    public static (string Path, float Seconds)[] AttackLoopWaitSequence => BuildAttackLoopSequence("wait");

    private static (string Path, float Seconds)[] BuildAttackLoopSequence(string name)
    {
        var fast = !string.Equals(name, "wait", StringComparison.Ordinal);
        var count = fast ? AttackLoopFastFrameCount : AttackLoopWaitFrameCount;
        // 入场点旋转**只对 fast（快速连击）生效**：wait 循环语义是"左拳保持击出的等待"，
        // 入场点尚未定义（且它还没接到任何 cue 上）⇒ 原样输出，不做旋转 ✓。
        var offset = fast ? AttackLoopFastStartFrameOffset : 0;

        var frames = new (string, float)[count];
        // ⚠️ 每帧时长走缩放后的 AttackLoopLoopFrameSeconds（= 基准 / 连击速度滑条），
        //    不是基准 AttackLoopFrameSeconds —— 入场序列是照这条序列构造的，所以两边同步缩放 ✓。
        var seconds = AttackLoopLoopFrameSeconds;
        for (var i = 0; i < count; i++)
        {
            // 循环左移 offset：序列第 1 项 = 源第 (offset+1) 帧，其余按原顺序环绕；
            // 序列长度、帧顺序、循环闭合性都与旋转前完全一致，只是换了个起点 ✓。
            var sourceIndex = (i + offset) % count;
            frames[i] = (AttackLoopFramePath(name, sourceIndex + 1), seconds);
        }
        return frames;
    }

    // ---- 「连续攻击」**入场一次性序列**（2026-09-26 新增）----
    //
    // 【要解决的问题】用户实机反馈（原话）：
    //   「左拳没问题了，但**第一次右拳也要加快**，才能让右拳也匹配上出伤时机。
    //     第二轮开始，左右拳攻击不用动，已经对上了。」
    // 也就是：游戏的第二段伤害来得**比稳态循环里的右拳更早** ✗ —— 但**只有第一轮**如此，
    // 第二轮及以后的循环节奏用户已确认"对上了"，一个字都不能改 ✗。
    //
    // 【为什么第一轮会偏慢】稳态循环的入场相位是源 f12（左拳出拳第 1 帧），左拳命中后要走
    //   f19、f20（左拳**收拳**，2 帧）才到 f21（右拳起手）。这 2 帧是"招式之间的过渡"，
    //   在视频里为了动作连贯留得比较足；但游戏里第二段伤害是紧跟第一段来的，
    //   于是第一记右拳"慢半拍"。
    // 【做法】不改稳态循环（一个字节都不动 ✓），而是**另做一条只播一次**的入场序列：
    //   帧的顺序/张数/起点与稳态循环**完全相同**（同为 43 帧、同为源 f12 起），
    //   只把"左拳收拳 → 右拳起手"那两帧（源 f19、f20）的每帧时长压短。
    //   播完自动交给稳态 atk_loop（Loop(true)）⇒ 第二轮起与改动前逐帧一致 ✓。
    //
    // ⚠️ 入场序列与稳态序列共用同一批贴图、同一个循环顺序，**没有任何贴图改名/重排** ✓。

    /// <summary>
    /// 🎚️ <b>「第一记右拳提前多少」的唯一调节点</b>（秒/帧，**按当前连击速度折算前的基准值**）。
    ///
    /// 入场序列里，把"左拳收拳 → 右拳起手"的过渡帧（源 f19、f20 这两帧，见
    /// <see cref="AttackLoopEntryBridgeFrameCount" />）的每帧时长**单独**换成这个值；
    /// 其余帧仍然逐帧沿用稳态速度（= <see cref="AttackLoopLoopFrameSeconds" />，未改语义 ✓）。
    ///
    /// ⚠️ 实际写进序列的是 <see cref="AttackLoopEntryBridgeFrameSeconds" /> = 本值 / <see cref="ComboSpeedScale" />
    ///    —— 与稳态**同一个缩放**，保证"这 2 帧比稳态短多少"这个比例恒定（结构不变，只改整体速度）✓。
    ///
    /// 调大 = 过渡更慢、右拳更晚；调小 = 过渡更快、右拳更早。
    /// 想回到旧行为（第一轮与稳态完全相同）就把它设成 <see cref="AttackLoopFrameSeconds" /> 的值。
    ///
    /// 默认 <c>0.010f</c> 的依据（源速 24fps ⇒ 稳态基准 <see cref="AttackLoopFrameSeconds" /> = 1/24 ≈ 0.041667 s/帧；
    /// 下面按 **scale = 1.0（旧速度）** 算，缩放后整段一起等比放大、结论不变）：
    ///   稳态下"左拳起手 → 第一记右拳起手"= 源 f12 → f21 = 前进 9 帧 = 9 × 1/24 = **0.3750 秒**；
    ///   入场序列里源 f21 位于下标 9，其到达时刻 = 前 9 帧时长之和
    ///   = 7 × 1/24 + 2 × 0.010 = 0.291667 + 0.020 = **0.3117 秒**
    ///   ⇒ 提前了 2 × (1/24 − 0.010) = **0.0633 秒**，落在要求的 0.25~0.35 秒区间内 ✓。
    ///   （0.010 时实测 0.3117s；想再快就调小，例如 0.005 ⇒ 0.3017s、0 ⇒ 0.2917s。）
    /// </summary>
    public const float AttackLoopEntryBridgeSeconds = 0.010f;

    /// <summary>
    /// 入场序列里"左拳收拳 → 右拳起手"**被压缩的过渡帧数**（从稳态序列的下标
    /// <see cref="AttackLoopEntryBridgeStartIndex" /> 起，连续这么多帧）。
    ///
    /// 值 2 = 源 f19、f20（用户确认的动作分解里正是"19~20 左拳收拳"整整一段）✓。
    /// 改这个数就等于改"压缩哪几帧"，一般不用动；**要调快慢请改
    /// <see cref="AttackLoopEntryBridgeSeconds" />**。
    /// </summary>
    public const int AttackLoopEntryBridgeFrameCount = 2;

    /// <summary>
    /// 被压缩的过渡帧在**入场序列里的起始下标**（0 起）。
    ///
    /// 入场序列第 1 项 = 源 <see cref="AttackLoopFastStartFrameOffset" />+1 = f12（左拳出拳第 1 帧）
    /// ⇒ 源 f19 落在下标 19 − 12 = <b>7</b>，f20 = 下标 8。
    /// 推导：序列下标 i ⇔ 源帧 (i + 11)。所以这里写 7 = 源 f19 ✓。
    /// ⚠️ 若改了 <see cref="AttackLoopFastStartFrameOffset" />（入场相位），这个下标要一起重算。
    /// </summary>
    public const int AttackLoopEntryBridgeStartIndex = 7;

    /// <summary>
    /// 「连续攻击」**入场一次性序列**（Loop(false)，43 帧，播完自动交给稳态
    /// <see cref="AttackLoopFastSequence" />）。
    ///
    /// 与稳态序列的**唯一差别**：下标
    /// [<see cref="AttackLoopEntryBridgeStartIndex" />,
    ///  +<see cref="AttackLoopEntryBridgeFrameCount" />) 这几帧改用
    /// <see cref="AttackLoopEntryBridgeSeconds" />（按连击速度折算后的
    /// <see cref="AttackLoopEntryBridgeFrameSeconds" />），其余帧逐帧完全相同 ✓。
    ///
    /// ⚠️ 同样是属性（每次读取现算），理由见 <see cref="IdleLoopSequence" />。
    /// </summary>
    public static (string Path, float Seconds)[] AttackLoopEntrySequence => BuildAttackLoopEntrySequence();

    private static (string Path, float Seconds)[] BuildAttackLoopEntrySequence()
    {
        // 先取**稳态序列的原样副本**，再只覆盖过渡帧的秒数 ——
        // 这样"帧顺序、帧数、起点、其余每帧时长"必然与稳态逐帧一致（不重复写一遍循环逻辑）✓。
        // ⚠️ 稳态副本里的每帧时长**已经带缩放**（BuildAttackLoopSequence 用的是
        //    AttackLoopLoopFrameSeconds）⇒ 入场与稳态**同步缩放** ✓；
        //    被覆盖的过渡帧也用同一个 scale 折算（AttackLoopEntryBridgeFrameSeconds）
        //    ⇒ 压缩比例恒定、相对关系不变（见那个属性的推导）✓。
        var frames = BuildAttackLoopSequence("fast");
        var bridgeSeconds = AttackLoopEntryBridgeFrameSeconds;
        for (var i = AttackLoopEntryBridgeStartIndex;
             i < AttackLoopEntryBridgeStartIndex + AttackLoopEntryBridgeFrameCount && i < frames.Length;
             i++)
            frames[i] = (frames[i].Path, bridgeSeconds);
        return frames;
    }

    /// <summary>
    /// 「连续攻击」**入场前的干等**序列：**只 1 帧**（= 入场正文的第 1 帧贴图，即左拳出拳首帧），
    /// 时长为 <see cref="AttackLoopEntryPreHoldSeconds" />，一次性（Loop(false)）。
    ///
    /// 单独做成一条 cue 的原因见 <see cref="AttackLoopEntryPreHoldSeconds" /> 的长注释：
    /// 这样"推迟入场"完全不碰状态机的任何判定，也不碰稳态循环 ✓。
    ///
    /// ⚠️ 贴图直接复用 <see cref="AttackLoopEntrySequence" /> 的第 1 项 ⇒ **没有任何新素材、
    ///    没有改名/重排**，画布与锚点必然一致（同一条序列的同一个元素）✓。
    /// ⚠️ 入场正文为空（素材缺失）时返回空数组 —— 调用方（GaoshouCharacter）会在
    ///    <see cref="AttackLoopMaterialAvailable" /> 里把它一起判定，绝不注册空 cue。
    /// </summary>
    public static (string Path, float Seconds)[] AttackLoopEntryPreHoldSequence
    {
        get
        {
            var entry = AttackLoopEntrySequence;
            if (entry.Length == 0)
                return [];
            // 取正文第 1 帧的贴图（= 源 f12，左拳出拳首帧），换成"定住 AttackLoopEntryPreHoldSeconds 秒"。
            return [(entry[0].Path, AttackLoopEntryPreHoldSeconds)];
        }
    }

    /// <summary>连续攻击循环帧的 cue / 状态 id（快速连击；wait 待状态机可区分时再挂）。</summary>
    public const string AttackLoopCue = "atk_loop";

    /// <summary>
    /// 「连续攻击」**入场**状态的 cue / 状态 id（一次性，Loop(false)，播完转 <see cref="AttackLoopCue" />）。
    /// 与 <see cref="AttackLoopCue" /> **共用同一个 <see cref="UseAttackLoop" /> 门控**（见该属性）。
    /// </summary>
    public const string AttackLoopEntryCue = "atk_loop_entry";

    /// <summary>
    /// 「连续攻击」循环素材是否**真的可用**（帧数 > 0 且路径非空）。
    ///
    /// 【为什么必须有这个闸门】<c>ModAnimStateMachine.EnterState</c> 的第一句就是
    /// <c>if (!Backend.HasAnimation(state.Id)) { Warn(...); return; }</c>（ModAnimStateMachine.cs:167-172）
    /// —— **cue 不存在时它直接 return，既不进入状态、也不播放任何 cue**，画面就停在上一帧（= ready
    /// 出拳准备姿势），且**永远等不到 Completed/NextState** ⇒ 看起来就是"完全不出拳" ✗。
    /// 所以"要不要注册循环分支"必须在**注册分支之前**就按素材可用性决定，不能只看设置项；
    /// 否则一旦 cue 缺失（素材没打进 PCK、ResourcesPath 没引用、用户删了帧文件……），
    /// 多段攻击就会走进一个"进了等于没进、还卡住画面"的死状态。
    /// </summary>
    public static bool AttackLoopMaterialAvailable =>
        AttackLoopFastSequence.Length > 0 &&
        AttackLoopFastSequence.All(static frame => !string.IsNullOrWhiteSpace(frame.Path)) &&
        // 入场序列与稳态序列共用同一批贴图，但它是**独立的一条 cue**：
        // 它若不可用，atk_loop_entry 就会变成"进了等于没进"的死状态 ✗
        // ⇒ 必须一起判定，素材缺一个就整体退化为原出拳路径 ✓（见类注释的"同生共死"）。
        AttackLoopEntrySequence.Length > 0 &&
        AttackLoopEntrySequence.All(static frame => !string.IsNullOrWhiteSpace(frame.Path)) &&
        // 「入场前的干等」cue（atk_loop_prehold）同理：它是 atk_loop_entry 的**前置状态**，
        // 它若没注册，状态机会卡在 atk_loop_entry 上永远等不到（见 GaoshouCharacter 的状态声明）✗
        // ⇒ 必须在**注册分支之前**就把它一起判掉 ✓。
        AttackLoopEntryPreHoldSequence.Length > 0 &&
        AttackLoopEntryPreHoldSequence.All(static frame => !string.IsNullOrWhiteSpace(frame.Path));

    /// <summary>
    /// 「连续攻击」状态（**入场 <see cref="AttackLoopEntryCue" /> + 稳态 <see cref="AttackLoopCue" />**）
    /// 是否启用：**anim_set = new** 且循环素材确实可用，且 **<see cref="UseVideoPunch" /> 没接管攻击路径**。
    /// 状态机的分支注册与 cue 注册**都必须**用这一个判定，两处门控必须严格一致 ✓
    /// —— 入场与稳态是**同一个判定的两个消费点**，绝不允许只注册其中一个 ✗。
    ///
    /// ⚠️【2026-09-26 起：攻击路径已停用循环方案】用户实机反馈（原话）："光靠连击速度滑条解决不了问题"；
    ///    1.00× 下多段攻击卡动画、打不出拳，出伤与左右拳顺序全都对不上。
    ///    根因：循环 cue 按固定速度自主播放，伤害却由游戏按自己的节奏结算，两者只能靠调速度碰运气 ✗。
    ///    现在改为**照抄原版 PNG 序列的工作机制**（一次命中 = 播一次出拳序列，见
    ///    <see cref="AttackVideoPunchLeftSequence" />），循环方案**不再被攻击路径使用**。
    ///    这里加 `!UseVideoPunch` 是为了让"新出拳路径"与"旧循环路径"**互斥、且可各自单独复现**：
    ///      * 正常情况（素材可用）⇒ UseVideoPunch = true ⇒ UseAttackLoop = false ⇒ 只挂出拳序列 ✓；
    ///      * 帧素材缺失 ⇒ UseVideoPunch = false ⇒ 退回循环（若循环素材在）⇒ 至少还有动画，
    ///        不会两条路径都挂不上而"完全不出拳"✗。
    ///    ⚠️ 循环的**素材、序列、状态与分支一个都没删**（<see cref="AttackLoopFastSequence" />、
    ///       <see cref="AttackLoopEntrySequence" />、<see cref="AttackLoopEntryPreHoldSequence" />、
    ///       三个 cue id 常量都原样保留）⇒ 以后想再挂回去，只要去掉这个 `!UseVideoPunch` 即可 ✓。
    /// </summary>
    public static bool UseAttackLoop =>
        !UseLegacyAnimSet && AttackLoopMaterialAvailable && !UseVideoPunch;

    // ───────────── 「入场前的干等」= 动画与伤害对齐的**唯一调节点** ─────────────
    //
    // 【要解决的问题】用户实机反馈（原话）：
    //   「目前第一次的左右出拳与伤害结算不匹配。**目前为：动画左拳 → 动画右拳 → 第一次出伤**」
    // 即：**动画跑在伤害前面** —— 两拳都播完了，第一次伤害才结算 ✗。
    //
    // 【真实时间线】（全部反编译核实，见下面各常量/设置的出处）：
    //   t=0.000  游戏在 `AttackCommand.Execute` 的 for 循环里派发 `Attack` 触发器
    //            （AttackCommand.cs:584 `await CreatureCmd.TriggerAnim(..., "Attack", _attackerAnimDelay)`）
    //            ⇒ RitsuLib 状态机 `SetTrigger("Attack")` → 立即 `EnterState(atk_loop_entry)`
    //            → `Backend.Play(...)` **同一帧**开始播入场序列第 1 帧（= 左拳出拳首帧）✓
    //   t=0.000  ↑ 动画从这里就起跑了，**没有任何前置等待** —— 这就是病根。
    //   t=0.065  `CreatureCmd.TriggerAnim` 内部的等待结束（CreatureCmd.cs:996
    //            `CustomScaledWait(min(delay*0.5, 0.25), delay)`，delay = `AttackAnimDelay` = 0.13
    //            ⇒ Normal 档 = 0.13s，Fast 档 = min(0.065, 0.25) = **0.065s**）
    //   t=0.065  才轮到 `CreatureCmd.Damage(...)`（AttackCommand.cs:669）⇒ **第一次伤害真正结算**
    //   ⇒ 动画比伤害**早了整整一个 `AttackAnimDelay`** ✗
    //
    // 【为什么观感是"两拳都播完才出伤"而不是"早一点点"】
    //   入场序列里"左拳出拳首帧 → 第一记右拳起手"这一段，按 `AttackLoopEntryBridgeSeconds`
    //   的推导是 **0.3117 秒**（源 f12 → f21，7×1/24 + 2×0.010；按连击速度等比缩放，比例不变）。Fast 档下等待只有 0.065s
    //   ⇒ 从"左拳出手"到"伤害到账"之间动画已经走完左拳、并且差一点就走完右拳起手，
    //   叠上伤害飘字的视觉延迟，观感就是"左拳 → 右拳 → 才出伤" ✓（量级吻合）。
    //
    // 【做法（方案 b，最小且可调）】在**入场序列真正开始播之前**插入一段**前置停留**：
    //   先把入场序列**第 1 帧**（左拳出拳首帧）单独显示 `AttackLoopEntryPreHoldSeconds` 秒，
    //   再去播入场序列的其余帧。这一段就是"把动画入场整体往后推"，推的量正好等于
    //   上面那个 `AttackAnimDelay` ⇒ **左拳出手与第一次伤害结算同一时刻** ✓。
    //
    //   ⚠️【为什么是"显示第 1 帧并停留"而不是"什么都不显示"】
    //     什么都不显示 = 白屏/停在上一帧（idle 末帧），反而像卡住；显示第 1 帧 = 左拳出手的准备
    //     姿势先定住一拍，视觉上是"蓄势"，用户上一轮认可的 ready 素材复用同一思路 ✓。
    //
    //   ⚠️【为什么不用 `SceneTreeTimer` 延迟进状态】
    //     那样"触发器已到、状态却还没切"，中途来的第二段命中会因为 `machine.Current` 仍是 idle
    //     而走错分支（甚至被别的分支抢走）；而且 `AttackLoopEntryPreHoldSeconds` 期间
    //     `ResetAttackLoopTimer` 的 `machine.Current` 判定会落空 ⇒ 收尾计时器不重置 ✗。
    //     拆成"前置停留 cue + 入场正文 cue"两条独立 cue 让**状态机在同一帧就切进 atk_loop_entry**，
    //     下面所有既有判定（`machine.Current`、重置计时器、AoE 兜底、Hit 出口）**一个都不用改** ✓。
    //
    //   ⚠️【稳态与第二轮：**逐帧未变** ✓】
    //     前置停留整段都在 `atk_loop_entry` 这一条**一次性** cue 里，而 `atk_loop`（稳态循环）
    //     是**另一条独立的 cue**、内容一个字节都没动；`atk_loop` 又**没有任何回 entry 的边**
    //     （见 GaoshouCharacter 的说明）⇒ 一整串连击只播一次前置停留，
    //     **第二轮及以后的循环节奏与改动前逐帧完全一致** ✓（用户明确要求不许动）。
    //
    // 🎚️【灵敏度】把 `AttackLoopEntryPreHoldSeconds` 改 Δ 秒 ⇒ "左拳出手 + 第一次伤害"这一对
    //    **整体一起**平移 Δ 秒（两者相对关系**不变**，因为它们本来就各自被推迟 Δ）。
    //    换句话说：**这个常量不调"动画 vs 伤害的错位"，那个错位由 `AttackAnimDelay` 决定**；
    //    它调的是"这一次连击**什么时候开始**"。要真正挪动错位就得改 `AttackAnimDelay`（见下）。
    //
    // ⚠️【与设置项 `MultiHitWaitSeconds` 的关系（用户调滑条会怎样）】
    //    它**不参与**这条延迟 —— 它的消费点是 `GuardHoldSeconds`（本文件 :918），
    //    只决定"**最近一次命中之后**多久收拳/是否继续循环"，作用在**入场与稳态的收尾**，
    //    与"入场何时起跑"无关 ⇒ **拖滑条不会改变左拳与第一次伤害的对齐** ✓。
    //    但它会改变**整串连击的总时长**：调大 = 每段之间更能等（不容易被 GuardEnd 提前收拳）；
    //    调小（如 0.1）而段间隔又较长时，可能出现"上一轮还没循环到，就被收拳打断" ✗。

    /// <summary>
    /// 🎚️ <b>「入场前先定住第 1 帧多久」的唯一调节点</b>（秒）。
    ///
    /// 这一段停留发生在**入场序列正文之前**（`atk_loop_entry` 的第一条 cue 里），
    /// 用来把"左拳出手"整体推到与**第一次伤害结算**同一时刻 —— 见上方大段注释的完整时间线。
    ///
    /// 默认 <c>0.13f</c> 的依据：游戏在派发 `Attack` 触发器后固定等
    /// <c>AttackAnimDelay</c>（<see cref="GaoshouCharacter.AttackAnimDelay" /> = 0.13f，
    /// 经 <c>CreatureCmd.cs:996</c> 的 <c>CustomScaledWait(min(delay*0.5,0.25), delay)</c>）
    /// 才结算第一次伤害；入场动画原先在**同一帧**就起跑 ⇒ 早了这一段。
    /// 补上同样长度 ⇒ 左拳出手与第一次伤害**同刻** ✓。
    ///
    /// ⚠️ 若改了 <c>GaoshouCharacter.AttackAnimDelay</c>，**这个值要一起改成同样的数**，
    ///    否则对齐会重新错开（两者是同一件事的两端）。
    /// </summary>
    public const float AttackLoopEntryPreHoldSeconds = 0.13f;

    /// <summary>「入场前干等」的 cue / 状态 id（= <see cref="AttackLoopEntryCue" /> 的第 1 帧，
    /// 一次性、只停 <see cref="AttackLoopEntryPreHoldSeconds" /> 秒，播完由状态机 WithNext 转
    /// <see cref="AttackLoopEntryCue" /> 正文）。它**必须**与 <see cref="UseAttackLoop" /> 同生共死。</summary>
    public const string AttackLoopEntryPreHoldCue = "atk_loop_prehold";

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
    // （hold_r = 刀在右侧 / hold_l = 刀在左侧），计时器同理但更短（AoeHoldSeconds）。

    // 帧拆成"起手 / 两刀 / 收刀"四段 + 两个落点停留姿势，是为了让**单段 AoE 直接收刀**：
    //   单段（1 次命中）：draw → cut_r → hold_r →〔AoeEnd 计时器〕→ sheathe → idle
    //   多段（N 次命中）：每命中一段走下一刀（cut_r ↔ cut_l 交替），最后同样按 AoeEnd 收刀
    // 状态机无法预知"这一段之后还有没有下一段"，所以用"停在刀落点上等 AoeHoldSeconds"来判定
    // —— 与拳那套 GuardEnd + atk_guard 完全同一个套路。
    //
    // ═══════════════════════════════════════════════════════════════════════════
    // 【2026-09-27 起改为**视频逐帧序列**，取代原来的 6 张静态姿势帧】
    //
    // 原来的 6 张（draw1/slashA/slashB/slashC/draw2/sheathe2）是**姿势切换**，
    // 观感生硬（用户反馈"之前的素材生成效果不好"）。现在改为：
    //   * 用 MiniMax H3 一次生成**一整段完整动作视频**（`_imgwork/video_out/aoe-v1.mp4`，
    //     480p/24fps/124 帧），一次成型而不是分段生成 ✓（与拳那套同一方法论）；
    //   * 24fps 切帧后用**本地抠图**（`_imgwork/matte_local.py`，白底去背，$0）得到 124 张 RGBA；
    //   * 按帧号区间切成下面四段，摆位到 AoE 家族约定（768×512 / 底边 y=456 / 双脚中心 x=384，
    //     脚本 `_imgwork/aoe_place.py`，实测自检全部落在约定上 ✓）。
    //
    // 【帧号来源（源视频 1 基）】由 `_imgwork/aoe_measure.py` 按"刀刃像素量/重心"客观测出：
    //   f013 刀出现（取刀完成）｜f033~f044 第 1 次大挥砍｜f055~f064 第 2 次挥砍
    //   ｜f083~f092 第 3 次挥砍｜f107 刀消失（收刀完成）
    //   ⇒ 取刀 f013~f032｜第1刀 f033~f054｜第2刀 f055~f076｜收刀 f093~f107
    //
    // 【为什么每帧时长是"总时长 ÷ 帧数"】源视频是 24fps 的**连续动作采样**，
    //   照 1/24 秒逐帧播 = 还原原始速度 ⇒ 动作自然（与视频版出拳同一理由）✓。
    //   想让横扫更慢/更快，改下面的 SegmentSeconds 即可（整段等比缩放）✓。
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 视频版横扫：每段的**目标总时长**（秒）。逐帧时长 = 本值 ÷ 该段帧数。
    ///
    /// ═══【2026-09-27 提速：修复"cutR 没对上出伤"】═══
    /// 用户实测「cutL、cutR 交替了，但 **cutR 没对上出伤，要稍微加一下速**」。
    /// 日志核算（60fps 帧号）：
    ///     f=2869  会话开始（= 伤害计时起点）
    ///     f=2882  **cut_r 才开始挥**   <== 中间隔了 13 帧 = 0.217s，全被 `draw` 吃掉
    ///     而游戏第 1 段伤害在 `AttackAnimDelay*0.5 = 0.065s`（≈4 帧）就结算了
    ///   ⇒ 伤害比挥刀**早了约 9 帧（0.15s）**，所以"出伤时刀还没动" ✗。
    ///
    /// 元凶就是 `draw`（取刀）本身：它排在第一次挥砍**之前**，
    ///   而游戏的伤害从"会话开始"就起算了，不会等取刀动画 ⇒ 取刀越长，错位越大。
    ///
    /// 修法：把取刀压到 **0.1s**（20 帧 → 5ms/帧，快到接近"一甩手"）。
    ///   ⚠️ 取刀是单段 AoE 时唯一的"拔刀"观感，压太短会看不清；
    ///      但**多段时第 2 段命中会立刻截断它**（`draw --Attack--> cut_r`），
    ///      所以这里主要是在优化"第一刀的出伤对齐" ✓。
    ///   🎚️ 想让刀更早挥出（出伤更准）就调小；想看清取刀就调大。
    /// </summary>
    /// <summary>
    /// 取刀（`draw`）时长（秒）—— 这是**唯一**承担"拔刀补偿"的量。
    ///
    /// 【它决定"第一刀什么时候开始挥"】取刀排在第一次挥砍之前，而游戏的伤害从
    ///   "会话开始"就起算了 ⇒ 取刀越长，第一刀相对出伤越晚。把它压短，
    ///   正是"补偿拔刀花掉的时间" ✓。
    ///
    /// 【但补偿**只能**体现在这里，不能去改单刀时长】
    ///   用户反馈「不再对后续 aoe 应用『弥补拔刀花费的 0.1s』而设置的加速」：
    ///   后续刀（第 2、3 刀…）是由 <c>AoeNextCut</c> 从上一刀**直接接续**的，
    ///   中间**没有取刀动画** ⇒ 它们本就没有拔刀开销，也就不该被加速 ✓。
    ///   所以我上一版"把单刀整体压短"是错的（已恢复 0.33s，见 AoeCutSeconds）。
    ///
    ///   当前值 <b>0.104s</b>：让第一刀尽快挥出、贴近第一次出伤，
    ///   同时仍保留一个可辨认的"拔刀"起势（单段 AoE 时它是唯一的起手观感）。
    ///   🎚️ 第一刀仍偏早就调大；偏晚就调小。
    /// </summary>
    public const float AoeDrawSeconds = 2.5f / 24f;    // 20 帧 → 0.104s（取刀，压到最短可用值）
    /// <summary>
    /// 每一刀的总时长（秒）—— <b>0.27s</b>：由"第 3 刀结束 ≈ 出伤完成"反解出来的值。
    ///
    /// ═══【2026-09-27 定稿：撤销对后续刀的加速】═══
    /// 用户实测+纠正：「**后续刀是动画结束早于出伤，不是晚于。我们需要的是放慢**，
    ///   即不再对后续 aoe 应用『弥补拔刀花费的 0.1s』而设置的加速」。
    ///
    /// 日志实证（3 段牌，60fps，改前单刀 0.22s）：
    ///   * 第 3 刀 f=4257 开始，0.22s(13f) ⇒ f≈4270 播完；
    ///   * "会话结束"（全部伤害结算完）在 **f=4285**
    ///   ⇒ 动画比出伤**早结束约 15 帧（0.25s）** ✗（用户观察正确）。
    ///
    /// 【根因：我把"拔刀补偿"错误地做成了"全局加速"】
    ///   取刀（`draw`）只发生在**第一刀之前**，我却靠**把单刀整体压短**来"弥补"它
    ///   ⇒ 等于让**每一刀**都跟着加速，后续刀自然跑到出伤前面 ✗。
    ///   正确做法：单刀恢复原速，补偿**只保留在 <see cref="AoeDrawSeconds" /> 上** ✓。
    ///   ⚠️ 后续刀天然没有拔刀开销（由 `AoeNextCut` 从上一刀直接接续），本就不该被加速 ✓。
    ///
    /// 【0.27s 是怎么定的】设第 3 刀播完时刻 = draw(6f) + 3 × cut_frames，
    ///   令其等于"伤害完成"的 53f ⇒ cut_frames ≈ 15.7 ⇒ **0.26s**，
    ///   取 **0.27s** 留一点余量（动画收尾略晚于出伤尾巴，观感更自然）✓。
    ///   对照：0.22s → 早 7f；0.33s → 晚 12f；0.27s → 基本对齐 ✓。
    ///
    ///   🎚️ 实测若仍偏早就调大、偏晚就调小（每 ±0.03s ≈ ±2 帧/刀）。
    /// </summary>
    public const float AoeCutSeconds = 0.27f;
    public const float AoeSheatheSeconds = 8f / 24f;    // 15 帧 → 0.333s（收刀）

    /// <summary>起手段（视频版）：手伸向腰后 → 拔刀 → 举刀准备（源 f013~f032，20 帧）。</summary>
    public static (string Path, float Seconds)[] AoeDrawSequence =>
        BuildAoeSegment("draw", 1, 20, AoeDrawSeconds);

    /// <summary>
    /// 第一刀（视频版）：**单程**（右起手 → 挥砍到左 → 停在左），与原版出拳**逐条同构**。
    ///
    /// ═══【2026-09-27 第三次修正：撤回"往返"，改回单程】═══
    /// 上一版把单程素材用"正放+倒放"ping-pong 成往返，想消除重播跳变。
    /// 用户实测反馈：**"回放段对上了第二次出伤，观感很像又砍了一次，实际看上去像砍了 4 次"** ✗
    ///   —— 往返让"一刀"在画面上看起来像"砍了两刀"，直接改变了玩家对"打了几段"的判断，
    ///   观感上不可接受。
    ///
    /// 【为什么原版出拳往返没事、AoE 往返不行】
    ///   原版每拳 0.33s 里"出拳帧"只占 0.12s（punchr 0.07+0.05），收回段是明显的**收势**；
    ///   而 AoE 是**大幅横扫**，原路倒放会被读成"反向又砍一刀" ⇒ 语义被改变 ✗。
    ///
    /// 【现在：单程】一次命中 = 播一次单程序列。
    ///   ⚠️ 重播时的"回位跳变"由**状态机侧**消除：只在刀已落下之后才允许重进（见 GaoshouCharacter）。
    ///   实测单程序列（cutR 输出 1..12）刀重心：427(右) → 561(后摆) → 391 → **188(最左)**，
    ///   **全程只越过中线一次** ✓（`_imgwork/aoe_verify.py` 实测）。
    /// ═══════════════════════════════════════════════════════════════════
    /// </summary>
    public static (string Path, float Seconds)[] AoeCutRightSequence =>
        BuildAoeSegment("cutR", 1, AoeCutRLastSourceFrame, AoeCutSeconds);

    /// <summary>第二刀（视频版）：单程（左起手 → 挥砍到右 → 停在右），理由同 <see cref="AoeCutRightSequence" />。</summary>
    public static (string Path, float Seconds)[] AoeCutLeftSequence =>
        BuildAoeSegment("cutL", 1, AoeCutLLastSourceFrame, AoeCutSeconds);

    /// <summary>收刀（视频版）：刀收回腰间，末帧≈站姿（源 f093~f107，15 帧）。</summary>
    public static (string Path, float Seconds)[] AoeSheatheSequence =>
        BuildAoeSegment("sheathe", 1, 15, AoeSheatheSeconds);

    /// <summary>
    /// 每刀参与往返的**挥砍帧数**（相对该段第 1 帧的序号）—— 取"刀扫到最远处"那一帧。
    ///
    /// 实测（<c>_imgwork/aoe_traj.py</c>，按粉色残影重心追踪切段后各帧）：
    ///   * <b>cutR</b>：f1 重心 385（起手，刀在右）→ f7 **越过中线**（残影峰值 39854）
    ///     → f11-12 重心 110~115（**扫到最左**）
    ///   * <b>cutL</b>：f1 重心 140（起手，刀在左）→ f7 越过中线 → f11-12 重心 ~587（扫到最右）
    /// 所以取 <b>12</b> 帧 = "完整挥完一刀"（起手 → 扫到另一侧）✓。
    /// 往返后总帧数 = 2×12-2 = <b>22</b> 帧。
    /// 调这个值 = 调"这一刀挥多远/挥多久"；配合 <see cref="AoeCutSeconds" /> 调出伤对齐 ✓。
    /// </summary>
    public const int AoeCutRLastSourceFrame = 12;

    /// <inheritdoc cref="AoeCutRLastSourceFrame" />
    public const int AoeCutLLastSourceFrame = 12;

    /// <summary>视频版段落贴图路径（抠图后的 RGBA 帧，832×512）。</summary>
    public static string AoeVideoFramePath(string segment, int index)
    {
        return Entry.ResPath + "/images/characters/Gaoshou_char_aoe_"
               + segment + "_" + index.ToString("00") + ".png";
    }

    /// <summary>
    /// 按"段名 + 帧序号区间（1 基，含两端）+ 目标总时长"构造一条等分序列。
    /// 与出拳那套 <c>BuildAttackVideoPunchSequence</c> 同一思路：帧序原样、时长等分 ✓。
    /// </summary>
    private static (string Path, float Seconds)[] BuildAoeSegment(
        string segment, int firstFrame, int lastFrame, float totalSeconds)
    {
        if (lastFrame < firstFrame || firstFrame < 1)
            return [];

        var count = lastFrame - firstFrame + 1;
        var per = totalSeconds / count;
        var frames = new (string, float)[count];
        for (var i = 0; i < count; i++)
            frames[i] = (AoeVideoFramePath(segment, firstFrame + i), per);
        return frames;
    }

    /// <summary>视频版横扫素材是否可用（四段都非空且路径非空）。</summary>
    public static bool AoeVideoMaterialAvailable =>
        AoeDrawSequence.Length > 0 && AoeCutRightSequence.Length > 0 &&
        AoeCutLeftSequence.Length > 0 && AoeSheatheSequence.Length > 0 &&
        AoeDrawSequence.All(static f => !string.IsNullOrWhiteSpace(f.Path)) &&
        AoeCutRightSequence.All(static f => !string.IsNullOrWhiteSpace(f.Path)) &&
        AoeCutLeftSequence.All(static f => !string.IsNullOrWhiteSpace(f.Path)) &&
        AoeSheatheSequence.All(static f => !string.IsNullOrWhiteSpace(f.Path));

    /// <summary>
    /// 第一刀（cutR）**落点**的停留姿势 = **cutR 自己的末帧**（刀停在左侧）。
    ///
    /// ═══【2026-09-27 修正：以前错误地取了"另一刀的末帧"】═══
    /// 用户实测「打 2 段 AoE，cutL 播放了两次，然后播放 cutR」。
    ///
    /// 根因就在这两个属性：旧写法让 `hold_r` 显示 **cutL 的末帧**。实测刀的位置：
    ///   * `cutR` 播放中：cx 427 → **188**（右起手 → 扫到**左**）；
    ///   * `cutL` 播放中：cx 226 → **614**（左起手 → 扫到**右**）。
    /// 于是 `cutR` 刚在左侧收势（cx=188），`hold_r` 立刻跳到 cx=614（刀在右）
    /// ⇒ 画面上就是"**又闪了一次另一把刀**"，紧接着才播真正的 `cutL` ✗
    /// —— 用户看到的"cutL 播放两次"正是这个"假 cutL 姿势 + 真 cutL 序列"。
    ///
    /// 【正确的语义】`hold_*` 是"**当前这一刀砍完之后停在落点上等下一段**"
    ///   （与出拳的 `atk_guard` 完全同构），所以它必须显示**当前这刀自己的末帧**：
    ///   * `hold_r`（cutR 之后）= cutR 末帧（刀在左）✓；
    ///   * `hold_l`（cutL 之后）= cutL 末帧（刀在右）✓。
    ///   这样 `cutR 末帧 → hold_r` 是**同一个姿势**，视频上完全连续、不跳变 ✓。
    ///
    /// ⚠️ 状态名 `hold_r`/`hold_l` 里的 r/l 指的是"**这一刀是右起手刀**"（即它是 cutR 的落点），
    ///    而不是"刀停在右侧" —— 旧注释把这两件事搞混了，才写出取另一刀末帧的错误实现 ✗。
    /// </summary>
    public static string AoeHoldRightTexturePath =>
        AoeCutRightSequence.Length > 0
            ? AoeCutRightSequence[^1].Path
            : AoeVideoFramePath("cutR", 22);

    /// <summary>第二刀（cutL）落点的停留姿势 = **cutL 自己的末帧**（刀停在右侧），理由同上。</summary>
    public static string AoeHoldLeftTexturePath =>
        AoeCutLeftSequence.Length > 0
            ? AoeCutLeftSequence[^1].Path
            : AoeVideoFramePath("cutL", 22);

    // 收刀前的"落点等待时长"已改成设置项，见属性 AoeHoldSeconds（设置页：帧序列选项 → 横扫落点等待时间）。

    /// <summary>收刀触发器名（由 GaoshouCharacter 用 SceneTree 计时器发出）。</summary>
    public const string AoeEndTrigger = "AoeEnd";

    /// <summary>
    /// 「补刀」触发器名 —— **刻意用独立的触发器，不复用 `"Attack"`**。
    ///
    /// ═══【2026-09-27：这是"第二刀回到出拳"的根因，务必别再改回 "Attack"】═══
    /// 补刀原本用 `machine.SetTrigger("Attack")` 驱动 `hold_* --Attack--> cut_*`，
    /// 但 `"Attack"` 是**游戏自己在用的触发器**，而且其它分支（出拳 / 攻击循环）
    /// 也注册在它上面，RitsuLib 取"**注册顺序里第一个谓词通过的**分支"⇒
    /// 补刀请求被排在后面的**出拳分支抢走**。日志实证：
    ///     ➕ 补刀（命中 3 > 已播 1）
    ///     动画状态 -> atk_video_punch_r      <== 补刀变成了出拳 ✗
    /// 更糟的是我给 `hold_* --Attack-->` 加了 `pending &lt;= cuts` 谓词
    /// （本意是"只让**游戏**的 Attack 走这条"），而补刀时恰恰 `pending &gt; cuts`
    /// ⇒ **我自己的谓词把自己的补刀挡住了**，于是它必然掉到出拳分支 ✗。
    ///
    /// 修法：补刀改用这个**专属触发器** `AoeNextCut`，与 `"Attack"` 完全隔离 ——
    ///   * 它只挂在"补刀"这一条路径上，不会有任何别的分支来抢 ✓；
    ///   * `"Attack"` 上的 `pending &lt;= cuts` 谓词就能安心保留（它现在只服务游戏的命中）✓。
    /// </summary>
    public const string AoeNextCutTrigger = "AoeNextCut";

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
                    // 连击速度：只作用于"连续攻击"的循环动画（入场 + 稳态），
                    // 单击 / legacy 出拳、AoE 横扫、受击 / 死亡都不经过它。
                    section.AddSlider(
                        "combo_speed",
                        T("连击速度（连续攻击动画）", "Combo speed (continuous-attack animation)"),
                        ComboSpeedBinding(),
                        ComboSpeedScaleMin,
                        ComboSpeedScaleMax,
                        0.05,
                        static v => v.ToString("0.00") + "×",
                        T(
                            "连续攻击那套循环动画（入场 + 稳态，例如「完美木棍剑」的多段拳）的整体快慢：\n" +
                            "1.00 = 改动前的原始速度（约 1.79 秒一轮两拳）；越小越慢，越大越快。\n" +
                            "默认 0.50 ⇒ 一轮两拳约 3.58 秒，让每一次出拳都咬合一次伤害结算。\n" +
                            "只改整体速度，不改帧数、入场相位与动作结构；单击 / 横扫 / 受击 / 死亡不受影响。\n" +
                            "改动在下一场战斗生效。",
                            "Overall speed of the continuous-attack loop animation (entry + steady loop, e.g. the " +
                            "multi-hit punches of Perfect Stick Sword):\n" +
                            "1.00 = the original speed (about 1.79 s per two-punch cycle); lower is slower, higher is faster.\n" +
                            "Default 0.50 gives about 3.58 s per two-punch cycle, so every punch lines up with one damage tick.\n" +
                            "Only the overall speed changes; frame count, entry phase and structure are untouched, and " +
                            "single-hit / sweep / hurt / death are unaffected.\n" +
                            "Changes apply in the next combat."));
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

    /// <summary>连击速度（连续攻击循环动画的整体快慢）滑条的绑定。见 <see cref="ComboSpeedScale" />。</summary>
    private static IModSettingsValueBinding<double> ComboSpeedBinding()
    {
        return ModSettingsBindings.Global<GaoshouVisualSettingsData, double>(
            Entry.ModId,
            DataKey,
            static data => data.ComboSpeedScale,
            static (data, value) => data.ComboSpeedScale = value);
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
    /// 连击速度：**连续攻击**循环动画（入场 + 稳态）的整体快慢倍数。
    /// 设置页滑条「连击速度（连续攻击动画）」，范围 0.25~2.0，**默认 0.5**（= 比原先的 1/24 秒一帧慢一倍）。
    /// 语义：1.0 = 改动前的原始速度，越小越慢 ⇒ 每帧时长 = 1/24 / 本值（见 GaoshouVisualSettings.ComboSpeedScale）。
    ///
    /// 与 <see cref="AnimSet" /> 同理，这里就是向后兼容的兜底：老配置文件里没有这个字段时，
    /// System.Text.Json 反序列化不会碰这个属性 ⇒ 保留初始化器的 0.5 ✓（老存档一升级就自动拿到新默认速度）。
    /// </summary>
    public double ComboSpeedScale { get; set; } = GaoshouVisualSettings.ComboSpeedScaleDefault;

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
