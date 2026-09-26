using Godot;
using Gaoshou.Patches;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Characters.Visuals.Definition;
using STS2RitsuLib.Scaffolding.Visuals;
using STS2RitsuLib.Scaffolding.Visuals.Definition;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace Gaoshou.Characters;

[RegisterCharacter]
public sealed class GaoshouCharacter : ModCharacterTemplate<GaoshouCardPool, GaoshouRelicPool, GaoshouPotionPool>
{
    // 高手主题色：沉稳的金色。
    public static readonly Color ThemeColor = new(0.78f, 0.55f, 0.2f);

    public override Color NameColor => ThemeColor;
    public override Color EnergyLabelOutlineColor => new(0.15f, 0.08f, 0.02f);
    public override Color MapDrawingColor => ThemeColor;

    public override CharacterGender Gender => CharacterGender.Masculine;

    public override int StartingHp => 75;
    public override int StartingGold => 99;

    // 白模继承储君：战斗模型、能量表盘、商店/篝火、音效等未覆盖字段都会从储君(regent)补齐。
    public override string? PlaceholderCharacterId => "regent";

    // 只覆盖需要区分身份的 UI 图标；Scenes 留空以继承储君的模型与表盘。
    // IconPath 是"顶部栏左上角角色头像"用的（CharacterModel.Icon → ui/character_icons/<id>_icon 场景）。
    // 之前没给这个字段，RitsuLib 的 Icon 补丁会回退到占位角色（储君）的图标，所以单人左上角显示的是储君。
    // 多人头像走的是 IconTexturePath，本来就是对的，不用动。这里给贴图就行（RitsuLib 会自动转成 Control 图标）。
    //
    // 【形象来源由设置页「角色形象」决定】（GaoshouVisualSettings）：
    //   * 继承储君（占位）：Scenes / VisualCues / CharacterSelectBgPath 全留空，由 PlaceholderCharacterId
    //     从储君补齐 —— 战斗模型、人物选择背景都是储君的；
    //   * 使用高手静态图：战斗视觉换成自建 gaoshou_visuals.tscn（纯 Node2D + Sprite2D，见下），
    //     idle / dead 两张 512×512 立绘走 RitsuLib 的 VisualCueSet 播放，人物选择背景换成两张全屏图之一。
    //
    // AssetProfile 是**每次取值都现场重拼**的属性（RitsuLib 的 ResolvedAssetProfile 会再 Resolve 一次），
    // 所以改完设置不需要重启游戏：下一场战斗 / 下一次进人物选择界面就换过来。
    public override CharacterAssetProfile AssetProfile
    {
        get
        {
            var ui = new CharacterUiAssetSet(
                IconTexturePath: $"{Entry.ResPath}/images/characters/Gaoshou_character_icon.png",
                IconOutlineTexturePath: $"{Entry.ResPath}/images/characters/Gaoshou_character_icon_outline.png",
                IconPath: $"{Entry.ResPath}/images/characters/Gaoshou_top_portrait.png",
                CharacterSelectBgPath: GaoshouVisualSettings.CharacterSelectBgPath,
                CharacterSelectIconPath: $"{Entry.ResPath}/images/characters/Gaoshou_character_select.png",
                CharacterSelectLockedIconPath: $"{Entry.ResPath}/images/characters/Gaoshou_character_select_locked.png",
                MapMarkerPath: $"{Entry.ResPath}/images/characters/Gaoshou_map_marker.png");

            // 多人手势（指人/石头/布/剪刀）：这是角色自己的 UI 素材，**两种形象来源下都生效**
            //（和上面的图标一样属于身份资源，不是"继承储君"要替换的战斗模型）。
            // 四个文件 422×1200，与原版 images/ui/hands/multiplayer_hand_*.png 同尺寸，
            // 应用补丁由 RitsuLib 注册（CharacterArmPointing/Rock/Paper/ScissorsTexturePathPatch）。
            var multiplayer = new CharacterMultiplayerAssetSet(
                ArmPointingTexturePath: GaoshouVisualSettings.HandPointTexturePath,
                ArmRockTexturePath: GaoshouVisualSettings.HandRockTexturePath,
                ArmPaperTexturePath: GaoshouVisualSettings.HandPaperTexturePath,
                ArmScissorsTexturePath: GaoshouVisualSettings.HandScissorsTexturePath);

            // 继承储君：只给 UI 图标 + 多人手势，其余字段留给占位角色补齐。
            if (!GaoshouVisualSettings.UseStaticVisuals)
                return new CharacterAssetProfile(Ui: ui, Multiplayer: multiplayer);

            return new CharacterAssetProfile(
                Scenes: new CharacterSceneAssetSet(
                    VisualsPath: GaoshouVisualSettings.StaticVisualsScenePath),
                Ui: ui,
                Multiplayer: multiplayer,
                // 战斗 cue 见 BuildCombatCues()：静态图模式只给 idle/dead（不做帧动画），
                // 帧序列模式才给受击 / 死亡 / 攻击 / 横扫全套。
                VisualCues: BuildCombatCues(),
                // 商店 / 营火的世界形象：不需要自定义场景，RitsuLib 会在内存里搭好节点壳
                // （见 ModWorldSceneVisualNodeFactory），我们只提供 cue + 显示样式。
                //   * 商店常驻 cue 走 NMerchantCharacter._Ready → PlayAnimation("relaxed_loop")；
                //   * 商店里的「放弃游戏」走 PlayerVisuals → PlayAnimation("die")，所以这里也挂 dead；
                //   * 营火常驻 cue 走 NRestSiteRoom → 按章节名 overgrowth_loop/hive_loop/glory_loop 回落到 relaxed。
                // 营火里的「放弃游戏」走的是战斗视觉 + VisualCues（不是这里），所以营火不用挂 dead。
                WorldProceduralVisuals: CharacterWorldProceduralVisualSetBuilder.Create()
                    .Merchant(cues => cues
                        .Single("relaxed_loop", GaoshouVisualSettings.ShopTexturePath,
                            GaoshouVisualSettings.ShopStyle)
                        .Single("dead", GaoshouVisualSettings.DeadTexturePath,
                            GaoshouVisualSettings.CorpseStyle))
                    .RestSite(cues => cues
                        .Single("relaxed", GaoshouVisualSettings.RestTexturePath,
                            GaoshouVisualSettings.RestStyle))
                    .Build());
        }
    }

    public override bool RequiresEpochAndTimeline => false;

    /// <summary>
    /// 攻击动画的"前摇"时长：游戏在派发 `Attack` 触发器后会等
    /// `CustomScaledWait(min(值*0.5, 0.25), 值)`，然后才结算伤害（CreatureCmd.cs:999）。
    /// 我们的起手段（ready1 0.06 + ready2 0.07 = 0.13s）正好是"收拳握拳 → 出拳架势"，
    /// 所以设 0.13s 让伤害落在拳头打出去那一刻（原版角色普遍是 0.15s；之前是 0 会导致
    /// 伤害先落地、拳后出）。
    /// </summary>
    public override float AttackAnimDelay => 0.13f;

    public override float CastAnimDelay => 0f;

    /// <summary>
    /// 战斗动画状态机（只在「使用高手静态图」时接管；继承储君时返回 null，仍走原版 Spine 动画器）。
    ///
    /// 【为什么必须自己建状态机】RitsuLib 0.5.17 源码里这是两条完全不同的路径：
    ///   * 不建状态机（返回 null）⇒ 走 ModCreatureVisualPlayback 的「触发器 → cue」兜底映射
    ///     （Hit→"hurt"）。这条路径**没有 NextState**：非循环帧序列播完只是「停在最后一帧」，
    ///     Finished 信号没人监听 ⇒ 人物会**卡在受击末帧不回待机**；
    ///   * 建了状态机 ⇒ 每个 hurt_* 状态都用 <c>WithNext</c> 串起来，播完自动进入下一段。
    ///
    /// 状态图（触发器名固定为 Idle/Dead/Hit/Attack/Cast/Relaxed，cue 键 = 这里传的名字）：
    /// <code>
    ///                 Hit(从 idle 触发)
    ///   idle(站姿) ─────────────────▶ hurt_enter(站姿→护姿) ──▶ hurt_body(受击+回位到护姿)
    ///     ▲                                                          │
    ///     └──────────────── hurt_exit(护姿→站姿) ◀────────────────────┘
    /// </code>
    /// **连击的关键**：<c>Hit</c> 先走 any-state 分支，它的谓词是"当前不在 idle"——
    /// 于是在 hurt_enter/hurt_body/hurt_exit 期间再挨打，会**直接重播 hurt_body**（立刻出受击瞬间），
    /// 而不会退回 hurt_enter 的"从站姿抬手"（那会看起来像闪回站姿）；只有在 idle 时才走
    /// idle 自己的分支去播 hurt_enter。RitsuLib 的 <c>SetTrigger</c> 正是"先 any-state、再当前状态"，
    /// 且谓词为 false 时会继续匹配当前状态的分支（ModAnimState.CallTrigger）。
    /// </summary>
    /// <summary>
    /// 战斗视觉的 cue 表（按设置页「角色形象」决定给多少）：
    /// <list type="bullet">
    ///   <item><b>静态图模式</b>（<see cref="GaoshouVisualSettings.UseFrameSequences" /> = false）：只给
    ///     idle / dead 两张静态图 —— 受击、出拳、横扫都不播帧动画，状态机也相应只建"站姿 + 死亡"两个状态；</item>
    ///   <item><b>帧序列模式</b>：idle 常驻；dead 播完定格（RitsuLib 的 dead 状态既不循环也不回 idle）；
    ///     受击三段（hurt_enter 站姿→护姿 / hurt_body 受击+回位到护姿 / hurt_exit 护姿→站姿）；
    ///     死亡两套（die_idle 站姿起手 / die_hurt 受击途中被打死，先垫一帧受击瞬间）；
    ///     攻击 atk_ready→atk_punch_r/_l 左右交替→atk_guard 停在架势→atk_retract_* 收拳；
    ///     AoE 横扫 atk_aoe_*（起手 / 两刀 / 落点停留 / 收刀）；
    ///     连击（anim_set = new 且素材可用）atk_loop_entry（入场一次性，第一记右拳提前）→ atk_loop（稳态循环）。
    ///     cast 没有专门图，不写 cue ⇒ 状态机把它指向 idle，不会出现空帧。</item>
    /// </list>
    /// </summary>
    /// <summary>
    /// 「视频版出拳」多段攻击的 cue / 状态 id（右拳 / 左拳）。
    ///
    /// ⚠️ 提到类级别（而不是各写各的局部 const）是**必需的**：这两个 id 有**两个**消费点
    ///    —— <see cref="BuildCombatCues" /> 注册 cue、<see cref="SetupCustomCombatAnimationStateMachine" />
    ///    声明状态并注册分支，两处必须用**同一对字符串**，否则就会出现"分支注册了、cue 没注册"
    ///    的死状态（<c>EnterState</c> 因 <c>HasAnimation=false</c> 直接 return，画面卡住 ✗）。
    ///    写成同一个常量 = 从语法上杜绝写错 ✓。
    /// </summary>
    private const string VideoPunchRightCue = "atk_video_punch_r";

    /// <summary>「视频版出拳」左拳的 cue / 状态 id。见 <see cref="VideoPunchRightCue" />。</summary>
    private const string VideoPunchLeftCue = "atk_video_punch_l";

    private static VisualCueSet BuildCombatCues()
    {
        // idle：帧序列模式下用 H3 生成的「呼吸」循环帧（idle 状态本身是 loop ⇒ Loop(true) ✓）；
        // 纯静态图模式仍是单张立绘（保持"完全不动的静态图"这个档位不变 ✓）。
        //
        // 【动画素材集 = legacy（默认）】站姿走**静态立绘** Gaoshou_char_idle.png，不做帧动画。
        //   早期那套静帧序列用户实测"效果太差、没有被采用"，真正上线的旧版就是这张静态图
        //   —— 正好复用"static"档位已有的单张 cue 路径，不新造机制。
        // 【= new】用现有的 H3 39 帧循环（IdleLoopSequence）。
        var useIdleFrames = GaoshouVisualSettings.UseFrameSequences
                            && !GaoshouVisualSettings.UseLegacyAnimSet;

        var cues = useIdleFrames
            ? ModVisualCues.CueSet()
                .Sequence("idle", sequence => AddLoopFrames(sequence, GaoshouVisualSettings.IdleLoopSequence))
            : ModVisualCues.CueSet()
                .Single("idle", GaoshouVisualSettings.IdleTexturePath);
        cues = cues.Single("dead", GaoshouVisualSettings.DeadTexturePath);

        if (!GaoshouVisualSettings.UseFrameSequences)
            return cues.Build();

        // 「连续攻击」cue（**两条：入场 atk_loop_entry + 稳态 atk_loop**）：**只有 anim_set = new
        // 且循环素材确实可用才挂**（legacy 是默认，行为必须与现在完全一致 ✓）。
        // 没有这两条 cue 时，下面的两个状态也不声明/不建分支 ⇒ 状态图退回原来那张，接不上任何循环素材。
        // ⚠️ 门控用 GaoshouVisualSettings.UseAttackLoop，与下面注册分支时**同一个判定** ——
        //   两处必须严格一致：若分支注册了而 cue 没注册，EnterState 会因为 HasAnimation=false
        //   直接 return（ModAnimStateMachine.cs:167），画面停在上一帧、且永远出不来 ✗。
        //   ⚠️ 入场与稳态是**同一个判定的两个消费点**，绝不允许只挂其中一条 ✗。
        // ⚠️ wait 循环素材已就位但暂未接入，理由见 GaoshouVisualSettings.AttackLoopWaitSequence。
        if (GaoshouVisualSettings.UseAttackLoop)
        {
            // **入场前的干等**（一次性，1 帧）：把"左拳出手"整体推迟 AttackLoopEntryPreHoldSeconds 秒，
            // 对上游戏的第一次伤害结算时刻（完整时间线见该常量的注释）。
            // 它必须与下面两条 cue **同生共死**（同一个 UseAttackLoop），否则会出现
            // "状态声明了、分支注册了、cue 却没注册"的死状态 ✗。
            cues = cues.Sequence(GaoshouVisualSettings.AttackLoopEntryPreHoldCue,
                sequence => AddFrames(sequence, GaoshouVisualSettings.AttackLoopEntryPreHoldSequence));
            // **入场一次性序列**（Loop(false)）：只压短"左拳收拳 → 右拳起手"那两帧的时长，
            // 让**第一记右拳**提前、对上游戏第二段的出伤时机；帧顺序/张数/起点与稳态完全相同。
            // 它必须与稳态 cue **同生共死**（同一个 UseAttackLoop）—— 否则会出现
            // "状态声明了、分支注册了、cue 却没注册"的死状态 ✗（原因见 UseAttackLoop 注释）。
            cues = cues.Sequence(GaoshouVisualSettings.AttackLoopEntryCue,
                sequence => AddFrames(sequence, GaoshouVisualSettings.AttackLoopEntrySequence));
            cues = cues.Sequence(GaoshouVisualSettings.AttackLoopCue,
                sequence => AddLoopFrames(sequence, GaoshouVisualSettings.AttackLoopFastSequence));
        }

        // ── 「视频版出拳」多段攻击用的两条 cue（**只有 anim_set = new 且视频素材可用才挂**）──
        // 内容与 atk_punch_l/_r 在 new 档下完全相同（同一批 H3 视频帧），独立成 cue 只为让状态机
        // 能把"多段"的左右交替记账与原版单击路径分开（见 SetupCustomCombatAnimationStateMachine）。
        // ⚠️ 门控必须与状态声明/分支注册用**同一个** GaoshouVisualSettings.UseVideoPunch：
        //    注册了分支而 cue 没注册 ⇒ EnterState 因 HasAnimation=false 直接 return，
        //    画面停在上一帧且永远出不来（ModAnimStateMachine.cs:167）✗。
        // legacy / 素材缺失时这两条一条都不挂 ⇒ 状态图与改动前逐条一致 ✓。
        // ⚠️ 这里要自己声明一份：它是 `BuildCombatCues()` 的局部，与下面的
        //    `SetupCustomCombatAnimationStateMachine()` **是两个方法**，局部量不共享
        //    （但两边判定都取 `GaoshouVisualSettings.UseVideoPunch` 这一个来源 ⇒ 严格等价 ✓）。
        const string cueVideoPunchR = VideoPunchRightCue;
        const string cueVideoPunchL = VideoPunchLeftCue;
        if (GaoshouVisualSettings.UseVideoPunch)
        {
            cues = cues
                .Sequence(cueVideoPunchR, sequence => AddFrames(sequence,
                    GaoshouVisualSettings.AttackVideoPunchRightSequence))
                .Sequence(cueVideoPunchL, sequence => AddFrames(sequence,
                    GaoshouVisualSettings.AttackVideoPunchLeftSequence));
        }

        return cues
            // 攻击后的"停在架势"：静态贴图 cue（不会播完 ⇒ 一直保持到计时器发 GuardEnd）
            .Single("atk_guard", GaoshouVisualSettings.AttackGuardTexturePath)
            .Sequence("hurt_enter", sequence => AddFrames(sequence, GaoshouVisualSettings.HurtEnterSequence))
            .Sequence("hurt_body", sequence => AddFrames(sequence, GaoshouVisualSettings.HurtBodySequence))
            .Sequence("hurt_exit", sequence => AddFrames(sequence, GaoshouVisualSettings.HurtExitSequence))
            .Sequence("die_idle", sequence => AddFrames(sequence, GaoshouVisualSettings.DieFromIdleSequence))
            .Sequence("die_hurt", sequence => AddFrames(sequence, GaoshouVisualSettings.DieFromHurtSequence))
            .Sequence("atk_punch_r", sequence => AddFrames(sequence,
                GaoshouVisualSettings.AttackVideoRightEffectiveSequence))
            .Sequence("atk_punch_l", sequence => AddFrames(sequence,
                GaoshouVisualSettings.AttackVideoLeftEffectiveSequence))
            .Sequence("atk_retract_r", sequence => AddFrames(sequence, GaoshouVisualSettings.AttackRetractSequence))
            .Sequence("atk_retract_l", sequence => AddFrames(sequence, GaoshouVisualSettings.AttackRetractSequence))
            // AoE 横扫：起手 / 两刀 / 收刀 四段 + 两个落点停留（静态 cue）
            .Sequence(GaoshouVisualSettings.AoeDrawCue,
                sequence => AddFrames(sequence, GaoshouVisualSettings.AoeDrawSequence))
            .Sequence(GaoshouVisualSettings.AoeCutRightCue,
                sequence => AddFrames(sequence, GaoshouVisualSettings.AoeCutRightSequence))
            .Sequence(GaoshouVisualSettings.AoeCutLeftCue,
                sequence => AddFrames(sequence, GaoshouVisualSettings.AoeCutLeftSequence))
            .Single(GaoshouVisualSettings.AoeHoldRightCue,
                GaoshouVisualSettings.AoeHoldRightTexturePath)
            .Single(GaoshouVisualSettings.AoeHoldLeftCue,
                GaoshouVisualSettings.AoeHoldLeftTexturePath)
            .Sequence(GaoshouVisualSettings.AoeSheatheCue,
                sequence => AddFrames(sequence, GaoshouVisualSettings.AoeSheatheSequence))
            .Build();
    }

    protected override ModAnimStateMachine? SetupCustomCombatAnimationStateMachine(
        Node visualsRoot, CharacterModel character)
    {
        if (!GaoshouVisualSettings.UseStaticVisuals)
            return null;   // 继承储君：把动画交回原版 Spine 动画器

        const string cueIdle = "idle";
        const string cueDead = "dead";
        const string cueHurtEnter = "hurt_enter";
        const string cueHurtBody = "hurt_body";
        const string cueHurtExit = "hurt_exit";
        const string cueDieIdle = "die_idle";
        const string cueDieHurt = "die_hurt";
        const string cuePunchR = "atk_punch_r";
        const string cuePunchL = "atk_punch_l";
        const string cueGuard = "atk_guard";
        const string cueRetractR = "atk_retract_r";
        const string cueRetractL = "atk_retract_l";
        const string cueAoeDraw = GaoshouVisualSettings.AoeDrawCue;
        const string cueAoeCutR = GaoshouVisualSettings.AoeCutRightCue;
        const string cueAoeCutL = GaoshouVisualSettings.AoeCutLeftCue;
        const string cueAoeHoldR = GaoshouVisualSettings.AoeHoldRightCue;
        const string cueAoeHoldL = GaoshouVisualSettings.AoeHoldLeftCue;
        const string cueAoeSheathe = GaoshouVisualSettings.AoeSheatheCue;
        // 「连续攻击」循环状态 id（**只有 anim_set = new 且循环素材可用才存在**）。
        // ⚠️ 与 BuildCombatCues() 里注册 atk_loop cue 用的是**同一个** GaoshouVisualSettings.UseAttackLoop，
        //    保证"注册了分支 ⇔ 注册了 cue"严格等价（这是本 bug 的修复要点，见该属性注释）。
        const string cueAtkLoop = GaoshouVisualSettings.AttackLoopCue;
        // 「连续攻击」**入场**状态 id（与稳态共用同一个 UseAttackLoop 门控）。
        const string cueAtkLoopEntry = GaoshouVisualSettings.AttackLoopEntryCue;
        // 「连续攻击」**入场前的干等**状态 id（= 把左拳出手推迟 AttackLoopEntryPreHoldSeconds 秒）。
        const string cueAtkLoopPreHold = GaoshouVisualSettings.AttackLoopEntryPreHoldCue;
        var useAttackLoop = GaoshouVisualSettings.UseAttackLoop;

        // ── 「视频版出拳」多段攻击状态（anim_set = new + 视频素材可用；legacy 时恒为 false，路径逐条不变）──
        //
        // 【2026-09-26 的本次改动】多段攻击原先走"整段视频循环"（prehold → entry → atk_loop），
        // 实测在 1.00× 下卡动画、左右拳顺序错乱、与出伤对不上。
        // 现在改为**照抄原版 PNG 序列的工作机制**：每段命中播一次对应的出拳序列、左右交替
        //（谓词与分支写法与原版 `cuePunchR/cuePunchL` 那几条**逐字相同**，只是目标换成了 video 状态）。
        //
        // 【为什么要专门两个状态，而不是直接复用 atk_punch_r/_l】
        //   复用会**破坏单击**：`AnimationStarted` 里"进入 cuePunchR ⇒ lastWasRight = true"这条
        //   会在**每段命中**时把交替指针打回"右拳"，于是第二拳又出右拳（用户实测的
        //   "左拳、右拳、右拳"就是这一类现象 ✗）。原版单击路径恰好没暴露这个问题，是因为
        //   单击只会命中一次、函数每场战斗只建一次状态机。
        //   所以这里**不动原版那条路径**（单击逐帧等价 ✓），另建两个状态：
        //     * 它们用**自己的交替指针 `punchSideFlip`**（不复用原版的 `lastWasRight`），
        //       从根上杜绝上面那个坑 ✓（记账仍在 `AnimationStarted` 里，但走独立分支）；
        //     * 它们与 atk_punch_* 一样是**一次性**、WithNext 到 atk_guard、由 GuardEnd 收拳 ✓；
        //     * 它们的 cue 内容 = 视频序列（<see cref="GaoshouVisualSettings.UseVideoPunch" />）,
        //       legacy 或素材缺失时这两条 cue/状态/分支**一条都不建**，状态图与改动前逐条一致 ✓。
        var useVideoPunch = GaoshouVisualSettings.UseVideoPunch;
        // cue / 状态 id 走类级别常量（与 BuildCombatCues 注册 cue 时**是同一对字符串** ✓）。
        const string cueVideoPunchR = VideoPunchRightCue;
        const string cueVideoPunchL = VideoPunchLeftCue;

        // 静态图模式（设置页选「使用高手静态图」）：只有 idle / dead 两个 cue，
        // 所以建一台"只认站姿与死亡"的最简状态机 —— 攻击 / 受击触发器全部落在站姿上（不做帧动画）。
        // ⚠️ 这里**不能**返回 null：返回 null 会走 RitsuLib 的"触发器→cue"兜底映射（Hit→hurt 之类），
        // 而静态图模式下根本没有那些 cue，容易出现空帧/告警。
        if (!GaoshouVisualSettings.UseFrameSequences)
            return ModAnimStateMachineBuilder.Create()
                .AddState(cueIdle, true).AsInitial().Done()
                .AddState(cueDead, false).Done()                  // 静态 cue，播完定格
                .AddAnyState("Dead", cueDead)
                .AddAnyState("Attack", cueIdle)
                .AddAnyState("Hit", cueIdle)
                .AddAnyState("Idle", cueIdle)
                .AddAnyState("Cast", cueIdle)
                .AddAnyState("Relaxed", cueIdle)
                .BuildForVisualsRoot(visualsRoot, character);

        // "上一拳出的是哪边" —— **必须记在状态机外面**。若只靠状态图的边（punch_r → punch_l）来交替，
        // 那么两次命中之间只要状态回到了 idle（例如打蜂群术士时每段受击会插入额外流程、把间隔拉长到
        // 整套序列播完），下一拳又会从"第一拳 = 右拳"开始，表现为**一直用右拳** ✗。
        // 这里用局部变量 + 事件更新：进入某一拳状态时记录该边，Attack 触发时按它选下一拳。
        var lastWasRight = false;

        // ── 收拳计时器的「代际号」（用来作废僵尸计时器，见 AnimationStarted 里 cueGuard 分支）──
        // Godot 的 `SceneTreeTimer` 建出来就**没法取消**，只能让它到点后自己判断"该不该生效"。
        // 每次"该由计时器收拳"的状态被进入（架势 / 出拳 / 攻击循环）就 +1；
        // 计时器到点时若代际落后 ⇒ 它已被后续进入顶替 ⇒ 丢进垃圾桶、不发 GuardEnd ✓。
        // 这样"旧计时器穿越到下一拳里到点收拳"（= 闪回 stance）从根上不可能再发生 ✓。
        var guardTimerGeneration = 0;

        // ── AoE 横扫的「待办刀数」（见分支处的三轮排查注释）──
        // 游戏的命中流（每 ~0.065s 一段）与动画进度（每刀 0.33s）是**两个时间尺度**，
        // 用"边"无法表达"来过几次命中、还欠几刀" ⇒ 用显式计数器：
        //   * `aoePendingHits`：**这一串连击共该砍几刀**（= 游戏的**实际段数**，权威来源）；
        //   * `aoeCutsPlayed` ：**已经播出的刀数**（每进入一次 cut_* +1）；
        //   * 欠账 = pending − cutsPlayed；落到 `hold_*` 时若还欠 ⇒ 立刻补一刀 ✓。
        //
        // ⚠️⚠️【2026-09-27 关键修正：pending 不再靠"命中事件"累加，改用「实际段数」】
        //   用户反馈「第二、三刀完全没有中间动画」= 补刀没触发。
        //   日志实证（godot.log，醉拳 2 段）：
        //       段数已算定 = 2
        //       🦡 出刀 atk_aoe_cut_r（已播 1 / 命中 0）
        //       📌 记一笔欠刀（欠账 1，当前 atk_aoe_cut_r）      <== 只记了 1 次！
        //       hold_r → sheathe                                  <== 直接收刀，第 2 刀没了
        //   即：**一张 2 段牌只收到 1 次 `Attack` 触发器派发**（`AttackHitTriggered` 挂的
        //   `CreatureCmd.TriggerAnim` 是 async 方法，多段之间的派发会被合并/时序错位 ——
        //   与之前"帧号去重吞命中"是同一类病因）⇒ 计数永远只 +1 ⇒ 欠账永远不够 ✗。
        //
        //   改成用 **`GaoshouAttackStyle.EffectiveHitCount`**（= `Hook.ModifyAttackHitCount`
        //   的 Postfix 抄回来的**实际段数**，日志里稳定输出 `段数已算定 = 2/3`），
        //   它是**权威**的"该砍几刀" ✓。于是不再依赖任何"每段"事件。
        //
        // 【为什么仍保留 NoteAoeHit（命中事件）】作为**兜底**：万一实际段数偏小
        //   （例如某些遗物在 Hook 之后又改段数），命中事件多记的那一笔能让刀数不至于偏少 ✓。
        //   多记一刀只是多挥一下（小瑕疵），漏记则直接少一刀（用户明确反馈的问题）。
        var aoePendingHits = 0;
        var aoeCutsPlayed = 0;

        // ── 「视频版出拳」的左右交替指针（**由状态机自己记账，不依赖任何外部事件**）──
        //
        // 【2026-09-27 实机回归：右右右右 / 右右左右】上一轮改成"由 `AttackHitTriggered`
        //   按当前状态推进到下一侧"（即命中事件驱动翻转）。实机反而出现：
        //     * 打小怪：**连续 4 次右拳**（一次都没换边）；
        //     * 打蜂群术士：**右、右、左、右**（首两拳同侧又回来了）。
        //   根因不在翻转算法，而在**事件源被吞了**：那套翻转依赖 `NotifyHitTriggered()` 的
        //   "帧号去重"，而 `CreatureCmd.TriggerAnim` 是 **async** 方法
        //   （反编译 `CreatureCmd.cs:967`）—— Harmony 的 Prefix/Postfix 都只在"方法返回 Task 那一刻"
        //   跑（真正的 await 在状态机 MoveNext 里），Postfix 的"清帧号"与 Prefix 的"置帧号"
        //   几乎同一瞬间发生，去重门因此卡死 ⇒ **后续每一次合法命中都被 return 掉**，
        //   翻转一次都不发生（症状 1、2），同时也**不重置收拳计时器** ⇒ 连击途中 `atk_guard`
        //   的 GuardEnd 到点收拳、人物突然切回 stance（症状 3）。三个症状同一个根因 ✓。
        //
        // 【现在：谁进状态谁记账】交替指针改由**状态机自己在进入出拳状态时**翻转：
        //     * 第 1 拳：`punchSideFlip` 初值 false ⇒ 进 `atk_video_punch_r`（右），
        //       进状态时翻成 true ⇒ 第 2 拳按 `punchSideFlip` 选左 ✓；
        //     * 第 2 拳：进 `atk_video_punch_l` ⇒ 翻成 false ⇒ 第 3 拳回右 ✓ ……严格交替。
        //   ⚠️ **为什么这样不怕重复回调**：交替完全不再读任何事件、也不读 `Current` 的时序，
        //      只由"状态机真的切进了哪个出拳状态"这一件**已经发生的事实**驱动
        //      （`AnimationStarted` 是后端 `Backend.Started` 的回调，切进去就会发一次）。
        //      命中事件被重复通知几次都无所谓 —— 它只负责重置计时器（幂等）✓。
        //   ⚠️ **记账位置**：`punchSideFlip` 与 `lastWasRight` 两者在**同一次进入**里一起写，
        //      顺序固定为"先按旧值选边（谓词已经求值完毕），再由 AnimationStarted 更新"——
        //      谓词在 `SetTrigger` 里同步求值，`AnimationStarted` 在后端 `Play()` 内**同步**回调
        //      （`CueAnimationBackend.Play` → `Started?.Invoke(id)`，同一次调用栈内；
        //        ModAnimStateMachine.cs:174 `Current = state` → :177 `Backend.Play` → :111 `Started`），
        //      所以"第 N 拳用到的 punchSideFlip"一定是"第 N-1 拳留下的值" ✓。
        //      万一后端是异步发 Started（Spine 走 Godot 信号），最坏也只是**少翻一次**
        //      （退化成"两拳同侧"），**绝不会**像旧方案那样"一次都不翻" ⇒ 风险面小得多 ✓。
        var punchSideFlip = false;

        // 谓词要读"当前状态"，而状态机是 Build 之后才存在的 ⇒ 用一个可变引用兜住。
        ModAnimStateMachine? machine = null;

        // ── 「视觉被回收」闩锁：节点离树后，所有计时器 λ 都要能**停止引用 machine** ──
        //
        // 【为什么需要它】Godot 的 `SceneTreeTimer` 属于 `SceneTree`、**不是**本节点的子节点
        //   ⇒ 节点离树（战斗结束 / 视觉被回收 / 换角色）时，这些计时器**不会**自动销毁，
        //   它们仍然挂在场景树上等着到点。而每个 `Timeout` 委托的 λ 都捕获了 `machine`，
        //   `machine` 又持有整棵视觉节点树 ⇒ 只要有一个计时器还没到点，
        //   **这一整场战斗的视觉树就无法被 GC 回收**（整棵树级的内存滞留，不是几字节的计时器）。
        //
        //   正常情况下这些计时器很短（0.3~2.0s）、很快就会到点并释放；但
        //     * 战斗恰好在这几秒内结束；
        //     * 或场景树被 `Paused`（Godot 的 `SceneTreeTimer` 默认跟随暂停，除非 `processAlways`）
        //   ⇒ 计时器可能一直不触发，滞留就变成"到下次 GC 也回收不掉"。
        //
        // 【修法（B 方案：离树时主动释放）】设一个闩锁 `disposed`：
        //   * 节点 `TreeExiting`（离树）时置 true，并**顺手把 machine 置空** ⇒ 断掉 λ → machine 引用；
        //   * 每个计时器 λ 开头先查闩锁，已释放就直接 return（不碰 machine，也不发任何触发器）。
        //
        //   ⚠️ **不改任何时序**：闩锁只在"节点已经离树"时才为 true，而那种情况下
        //      本来也不该再推进动画（节点都没了）⇒ 对正常战斗流程零影响 ✓。
        //   ⚠️ 与代际号（`guardTimerGeneration`）**是两回事、互不干扰**：
        //      代际号管"连击途中旧计时器作废"，闩锁管"节点销毁后 λ 别再握着引用" ✓。
        var disposed = false;

        // 「连续攻击」循环的**收尾计时器**：进状态时起一次，之后**每次命中都重置**（见下）。
        // 必须是"可重置的引用"而不是局部变量 —— 重置发生在命中事件里，跨多次调用 ✓。
        SceneTreeTimer? attackLoopTimer = null;

        var builder = ModAnimStateMachineBuilder.Create()
            .AddState(cueIdle, true).AsInitial().Done()
            .AddState(cueHurtEnter, false).WithNext(cueHurtBody).Done()
            .AddState(cueHurtBody, false).WithNext(cueHurtExit).Done()
            .AddState(cueHurtExit, false).WithNext(cueIdle).Done()
            .AddState(cueDead, false).Done()                      // 终止状态：不设 Next，播完定格
            .AddState(cueDieIdle, false).Done()                   // 死亡（从待机切入），同样终止
            .AddState(cueDieHurt, false).Done()                   // 死亡（从受击切入），同样终止
            // 出拳 → **停在架势**（不立刻收拳！）；架势里靠计时器发 GuardEnd 才收拳回站姿。
            // ⚠️ 这三条（punch_r / punch_l / guard）是**原版单击路径**，本次改动一个字都没动 ✓。
            .AddState(cuePunchR, false).WithNext(cueGuard).Done()
            .AddState(cuePunchL, false).WithNext(cueGuard).Done()
            .AddState(cueGuard, false).Done()                     // 静态 cue，不会播完 ⇒ 一直保持
            .AddState(cueRetractR, false).WithNext(cueIdle).Done()
            .AddState(cueRetractL, false).WithNext(cueIdle).Done()
            // ⚠️【2026-09-27 修正：draw 之后**直接**进 cutR，且 cutR 不再等 draw 播完】
            //   用户实测「在第一刀挥出过程中，就已经结算两次伤害」。
            //   原因：`draw`(0.417s) + `cutR` 前半（挥砍段）期间，游戏已经把第 2 段伤害结算完了
            //   （每段命中只间隔 ~0.065s）。也就是说**取刀动画本身就吃掉了两次出伤** ✗。
            //
            //   现在的处理：
            //     * `draw` 仍是 `WithNext(cutR)`（单段 AoE 时取刀 → 挥刀 → 收刀，观感完整）✓；
            //     * 但**多段时**，第 2 段命中会从 `draw` 分支直接接走（见下面的
            //       `draw --Attack--> cutR`）⇒ 取刀被**截断**，不会拖到伤害都结算完 ✓；
            //     * 并且 `cut` 序列已改成**往返**结构（首尾都回到起手位）⇒ 重播不跳变 ✓。
            //   ⚠️ 取刀时长已从 0.833s 压到 0.417s（见 AoeDrawSeconds），
            //      是"既让单段 AoE 看得到取刀、又不太拖累多段"的折中值 ✓。
            .AddState(cueAoeDraw, false).WithNext(cueAoeCutR).Done()
            .AddState(cueAoeCutR, false).WithNext(cueAoeHoldR).Done()
            .AddState(cueAoeCutL, false).WithNext(cueAoeHoldL).Done()
            .AddState(cueAoeHoldR, false).Done()                   // 静态 cue，不会播完 ⇒ 一直保持
            .AddState(cueAoeHoldL, false).Done()
            .AddState(cueAoeSheathe, false).WithNext(cueIdle).Done();
        // ↑ 状态声明到此为止（下面开始注册分支）。
        // ── 「视频版出拳」多段攻击状态（anim_set = new + 视频素材可用才声明）──
        // 与 atk_punch_r/_l **完全同构**（一次性出拳序列 → WithNext 到 atk_guard 停在架势 →
        // GuardEnd 收拳），唯一区别是"左右交替不由本状态记账"（见 AnimationStarted 与下面的订阅）。
        // 不声明时下面那几条分支也一条都不注册（AddBranch 对未声明的源状态会直接抛）⇒ legacy 状态图逐条不变 ✓。
        if (useVideoPunch)
        {
            builder.AddState(cueVideoPunchR, false).WithNext(cueGuard).Done();
            builder.AddState(cueVideoPunchL, false).WithNext(cueGuard).Done();
        }

        // ── 「连续攻击」循环状态（anim_set = new 且循环素材可用时才声明）──
        // 循环 cue 是 Loop(true) 永不播完 ⇒ **无 WithNext**，一直转到被接走；
        // 收尾靠计时器发 GuardEnd → 收拳回站姿（与 atk_guard 同一套路，复用现有 GuardHoldSeconds）。
        //
        // ⚠️ 【本 bug 的修复要点】这个状态必须与"循环分支 + 循环 cue"**同生共死**：
        //    * 声明了但它指向的分支/cue 没注册 ⇒ 永远进不去（无害，但白占一个状态）；
        //    * **注册了分支但 cue 没注册** ⇒ 最糟：`ModAnimStateMachine.EnterState` 第一句
        //      `if (!Backend.HasAnimation(state.Id)) { Warn; return; }`（ModAnimStateMachine.cs:167）
        //      会**既不进状态、也不播任何 cue**，画面停在上一帧（ready 出拳姿势）且**永远等不到
        //      Completed / NextState** ⇒ 实机表现就是"多段牌完全不出拳、卡在 ready" ✗。
        //    所以这里和上面的 cue 注册共用 GaoshouVisualSettings.UseAttackLoop 这一个判定。
        if (useAttackLoop)
        {
            // 入场序列是**一次性**（Loop(false)）⇒ 播完由 WithNext 交给稳态 atk_loop。
            // ⚠️ 用 WithNext 而不是"分支"：非循环序列播完时 `CueFrameSequencePlayer` 发 Finished
            //    ⇒ `CueAnimationBackend.OnSequenceFinished` 拉起 Completed ⇒ 状态机 `OnBackendCompleted`
            //    把 Current 推进到 NextState，并由后端的 ConsumeQueue() 真正开始播下一条
            //    （CueFrameSequencePlayer.cs:149-160 / CueAnimationBackend.cs:211-218、249-257）✓。
            //    稳态 atk_loop 仍按原样 Loop(true)、不设 WithNext ✓。
            // 干等（1 帧，AttackLoopEntryPreHoldSeconds）⇒ 播完由 WithNext 交给入场正文。
            // 用 WithNext 的理由与下面 entry→loop 完全一样（非循环序列播完发 Finished ⇒ 状态机推进）✓。
            builder.AddState(cueAtkLoopPreHold, false).WithNext(cueAtkLoopEntry).Done();
            builder.AddState(cueAtkLoopEntry, false).WithNext(cueAtkLoop).Done();
            builder.AddState(cueAtkLoop, false).Done();
        }

        builder
            // 受击
            .AddBranch(cueIdle, "Hit", cueHurtEnter)              // 待机时挨打 → 先播进入段
            .AddAnyState("Hit", cueHurtBody,                      // 受击过程中再挨打 → 直接重播主体
                () => machine?.Current?.Id != cueIdle)
            // ── AoE 横扫（打全体）：优先级高于拳 ──
            // RitsuLib 的 ModAnimState.CallTrigger 是"**按注册顺序**取第一个谓词通过的分支"
            // （ModAnimState.cs:117-123），所以带 IsAoe 谓词的分支必须注册在无条件的拳分支**之前**，
            // 否则横扫永远轮不到 ✗。拳分支各自都带谓词（!lastWasRight / lastWasRight），互斥不受影响。
            //
            // ⚠️ 2026-09-24 实机 bug（多段 AoE 第二段掉回挥拳）：命中间隔（≈0.25s）**比一整刀还短**
            //（draw 0.15 + cut 0.20），所以第 2 段往往在 draw/cut **途中**就发过来了；那时若只有
            // "非横扫 → 拳"的兜底，就会直接掉进拳 ✗（日志实测：atk_aoe_cut_r → atk_punch_l，IsAoe 全程 True）。
            // 现在横扫链**每个状态**都接"再来一段 ⇒ 立刻换另一刀"，做到"一次命中 = 一刀" ✓。
            .AddBranch(cueIdle, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueGuard, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cuePunchR, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cuePunchL, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueRetractR, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueRetractL, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueHurtEnter, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueHurtBody, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueHurtExit, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueAoeSheathe, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            // 横扫链内部再来一段 ⇒ 由**待办计数器**（aoePendingHits / aoeCutInFlight）决定
            //
            // ⚠️【2026-09-27 三轮排查的结论（务必读完再改）】
            //   * 第 1 轮 `cut_r --Attack--> cut_l`：命中到来就**打断**当前刀从头播
            //     （`TryStart` → `StopAndReset`）；命中间隔仅 0.065s ⇒ 每段都重进 ⇒ 刀数 ≫ 段数 ✗
            //   * 第 2 轮**删掉** `cut_* --Attack-->`：第 2 段掉进下面的兜底**出拳**分支
            //     （日志 `cut_r → punch_l → guard`），因为 RitsuLib 取"注册顺序里第一个谓词通过的" ✗
            //   * 第 3 轮 `cut_* --Attack--> hold_*`（不打断、只提前收尾）：**仍有漏洞** ——
            //     这条分支会**消耗掉**那次 Attack 触发器，等刀落到 `hold_*` 时触发器已没了
            //     ⇒ **第 2 刀丢失** ✗
            //
            //   ⇒ 根本矛盾：「**命中次数**」（游戏给的，快）与「**动画进度**」（状态机，慢）
            //     是两个时间尺度，"边"表达不了"来过几次、还欠几刀"。
            //     所以改为**显式计数器**（见 aoePendingHits 声明处）：
            //       * 每段命中：pending +1（由 AnimationStarted 侧的记账完成）；
            //       * 每播完一刀：pending -1；
            //       * 落到 `hold_*` 时若 pending > 0 ⇒ 立刻接下一刀 ✓。
            //   ⚠️ 谓词保持**纯读取**（只读 aoeCutInFlight，不做副作用）✓。
            .AddBranch(cueAoeDraw, "Attack", cueAoeCutR, () => GaoshouAttackStyle.IsAoe)
            // ⚠️【2026-09-27 关键修正：这两条分支**不能要** —— 它们会打断正在播的刀】
            //   用户实测「打 2 段 AoE，两次都是 cutL，没有播放 cutR」。
            //   日志实证（60fps 帧号）：
            //       f=4210 进 cut_r
            //       f=4215 进 hold_r      <== 只隔 5 帧（0.083s）！
            //   而 cut_r 序列是 12 帧 / 0.33s（≈20 帧 @60fps）⇒ **只播了约 15% 就被切走** ✗。
            //   于是画面上 cut_r 一闪而过、几乎看不见，只剩完整的 cut_l
            //   ⇒ 观感就是"两次都是 cutL" ✓（与用户描述完全吻合）。
            //
            //   罪魁就是 `cut_r --Attack--> hold_r`：第 2 段命中的 `Attack` 在 cut_r
            //   **播放途中**到达，这条分支立刻把状态切到 hold_r ⇒ **打断了当前刀** ✗。
            //   我加它的本意是"别让第 2 段命中丢掉"，但正确做法是
            //   **让命中被记下来、刀照常播完**（`NoteAoeHit` 已经在做记账），
            //   刀播完自然走 `WithNext → hold_*`，再由 `hold_*` 的补刀接下一刀 ✓。
            //
            //   ⇒ 所以这里**删掉** `cut_* --Attack--> hold_*` 两条分支。
            //     命中不会丢：`NoteAoeHit` 已把"该砍几刀"记在 `aoePendingHits` 里，
            //     而它的权威来源是**实际段数**（见声明处），与这条边无关 ✓。
            //
            // `hold_*` 上的接刀，分两条路径（**用不同触发器，互不抢占**）：
            //
            // ① 游戏的命中（`"Attack"`）：只在**不欠账**时走。
            //    * 欠账（命中 > 已播）时**不要**走这条 —— 那会让"欠的那一刀"被提前消费且不计数；
            //      此时正确做法是等 `AnimationStarted(hold_*)` 里补刀（那里能顺手 +1 已播数）✓；
            //    * 不欠账时走这条 = 正常的"游戏又来了一段" ⇒ 立刻出下一刀 ✓。
            .AddBranch(cueAoeHoldR, "Attack", cueAoeCutL,
                () => GaoshouAttackStyle.IsAoe && aoePendingHits <= aoeCutsPlayed)
            .AddBranch(cueAoeHoldL, "Attack", cueAoeCutR,
                () => GaoshouAttackStyle.IsAoe && aoePendingHits <= aoeCutsPlayed)
            // ② **补刀**（专属触发器 `AoeNextCut`）：无条件走 —— 它只可能由"补刀"发出，
            //    语义就是"还欠一刀，现在补上" ⇒ 不需要任何谓词 ✓。
            //
            // ⚠️【为什么必须用专属触发器，而不能复用 "Attack"】（2026-09-27 实机 bug）
            //   原来补刀发的是 `SetTrigger("Attack")`，结果被**出拳分支抢走**：
            //       日志：➕ 补刀（命中 3 > 已播 1） → 动画状态 -> atk_video_punch_r ✗
            //   两个原因叠加：① `"Attack"` 上有其它分支（出拳等）也在匹配，RitsuLib 取
            //   "注册顺序里第一个谓词通过的"；② 我给 `hold_* --Attack-->` 加的
            //   `pending <= cuts` 谓词**恰好把补刀自己挡住了**（补刀时必然 pending > cuts）✗。
            //   换成专属触发器后，补刀路径与 `"Attack"` 完全隔离、不会再被抢占 ✓。
            .AddBranch(cueAoeHoldR, GaoshouVisualSettings.AoeNextCutTrigger, cueAoeCutL)
            .AddBranch(cueAoeHoldL, GaoshouVisualSettings.AoeNextCutTrigger, cueAoeCutR)
            // 落点上等不到下一段（计时器）⇒ 收刀回站姿
            .AddBranch(cueAoeHoldR, GaoshouVisualSettings.AoeEndTrigger, cueAoeSheathe)
            .AddBranch(cueAoeHoldL, GaoshouVisualSettings.AoeEndTrigger, cueAoeSheathe)
            // 横扫途中挨了一次**单体**攻击 ⇒ 兜底切拳（否则这次 Attack 被吞掉、拳头完全不出现 ✗）
            //
            // ⚠️⚠️【2026-09-27 重大坑：这组"无条件"拳分支会**抢走 AoE 的第 2 段** ✗】
            //   日志实测（醉拳 2 段）：
            //     draw → cut_r → **punch_l** → guard → retract_l → idle
            //   第 2 段命中**掉进了出拳**，而不是接第二刀。
            //
            //   原因：这些分支的谓词是 `!lastWasRight`（**无条件**，与 IsAoe 无关），
            //   而 RitsuLib 的 `CallTrigger` 取"**注册顺序里第一个谓词通过**的分支"
            //   （ModAnimState.cs:117-123）。我上一轮把 `cut_r --Attack--> cut_l` 删掉后，
            //   `cut_r` 上就只剩这些拳分支能匹配 ⇒ 第 2 段命中被它们接走 ✗。
            //
            //   修法：给这组兜底拳分支**加上 `!IsAoe` 谓词** —— 它们本来就只该服务
            //   "横扫途中挨了一次**单体**攻击"这种场景（见上面的注释），
            //   加谓词后 AoE 的段数不会再被抢走，而"横扫中途来单体攻击"依然有兜底 ✓。
            //   ⚠️ 谓词里读 `GaoshouAttackStyle.IsAoe` 是**纯读取**、无副作用 ✓。
            //
            // ⚠️【2026-09-27 二次修正：`draw` / `cut_*` 上的这几条已一并删除】
            //   它们是"横扫途中挨了一次单体攻击"的兜底，但**在 AoE 自己的连击里会抢戏**：
            //   `IsAoe` 在**攻击会话结束后会变假**（`AttackCommand.Execute` 完成即 `Exit`），
            //   而多段 AoE 的第 2、3 段恰恰是在第 1 段之后才派发的 ⇒ 那时 `IsAoe` 可能已假
            //   ⇒ 这些分支（以及上面同类的 `cueVideoPunch*` 分支）就会把后续段**抢去出拳** ✗
            //   （用户实测：2 段 AoE「两次都是 cutL、没播放 cutR」，日志里 cut_r 只播 5 帧就被切走）。
            //   ⇒ 现在只保留 `hold_*` / `sheathe` 上的兜底（那时横扫链已近尾声，
            //     被单体攻击接走才是合理行为），**`draw` / `cut_*` 上一条都不留** ✓。
            .AddBranch(cueAoeHoldR, "Attack", cuePunchR, () => !GaoshouAttackStyle.IsAoe && !lastWasRight)
            .AddBranch(cueAoeHoldR, "Attack", cuePunchL, () => !GaoshouAttackStyle.IsAoe && lastWasRight)
            .AddBranch(cueAoeHoldL, "Attack", cuePunchR, () => !GaoshouAttackStyle.IsAoe && !lastWasRight)
            .AddBranch(cueAoeHoldL, "Attack", cuePunchL, () => !GaoshouAttackStyle.IsAoe && lastWasRight)
            .AddBranch(cueAoeSheathe, "Attack", cuePunchR, () => !GaoshouAttackStyle.IsAoe && !lastWasRight)
            .AddBranch(cueAoeSheathe, "Attack", cuePunchL, () => !GaoshouAttackStyle.IsAoe && lastWasRight);
        // ↑ 上面这些（含无条件拳分支）必须先注册完；下面的循环入口要插在"攻击"分支组之前。

        // ── 「连续攻击」循环入口（anim_set = new 且**多段**才走这里）──
        // ⚠️ 必须注册在下面那些**无条件**的拳分支**之前**：RitsuLib 按注册顺序取第一个谓词
        //    通过的分支（ModAnimState.cs:117-123），放到后面就会被 `idle→Attack→punch_r` 抢先 ✗。
        //    谓词严格限定为"new 素材集 + 多段（段数≥2 且非 AoE）" ⇒ 单击与 legacy 自然落到
        //    后面的原分支，路径逐条不变 ✓（与上面 IsAoe 分支"必须排在拳之前"是同一个道理）。
        // legacy 时 useAttackLoop=false ⇒ 这几条一条都不注册，状态图与改动前**逐条完全一致** ✓。
        if (useAttackLoop)
        {
            builder
                // 【首次进入 ⇒ 先播**入场前的干等**】只有"多段攻击的第一次"走这里：先定住入场第 1 帧
                // AttackLoopEntryPreHoldSeconds 秒（把左拳出手推到与**第一次伤害结算**同刻），
                // 播完自动交给入场正文 atk_loop_entry。
                // ⚠️ 这里**不需要**额外记"是不是第一次"：干等状态只能从 idle / atk_guard 进入，
                //    而一旦进了就沿 WithNext 转 entry → loop；loop 自己**不注册任何回 entry / 回干等的边**，
                //    后续命中在 atk_loop 里被吞掉（见下面的说明）⇒ 干等 + 入场序列**一整串连击只会播一次** ✓
                //    ⇒ **第二轮及以后的循环节奏逐帧未变** ✓（用户明确要求）。
                .AddBranch(cueIdle, "Attack", cueAtkLoopPreHold, () => GaoshouAttackStyle.IsMultihit)
                .AddBranch(cueGuard, "Attack", cueAtkLoopPreHold, () => GaoshouAttackStyle.IsMultihit)
                // ⚠️ 【顺序要点】横扫分支必须排在下面那条 `IsMultihit → atk_loop` **之前**：
                //    CallTrigger 取的是**注册顺序里第一个**谓词通过的分支（ModAnimState.cs:117-123），
                //    "多段横扫"会同时满足 IsAoe 与 IsMultihit ⇒ 放后面就会被连击分支抢走、播成拳头 ✗。
                //    这两条**只在这里注册**（而不是上面那组共用的 AoE 分支里）：`atk_loop_entry` /
                //    `atk_loop` 只在 useAttackLoop 时才声明，而 `AddBranch` 在**源状态未声明时会直接抛**
                //    `InvalidOperationException("Source state '...' not declared.")`
                //    （ModAnimStateMachineBuilder.cs:77-78）⇒ 写到上面会让 legacy 直接构建失败 ✗。
                //    没有这两条时，横扫会被后面无条件的拳分支接走、播成出拳 ✗
                //    （与 2026-09-24 修过的"多段 AoE 第二段掉回挥拳"是同一类问题）。
                .AddBranch(cueAtkLoopEntry, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
                .AddBranch(cueAtkLoop, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
                // ⚠️【顺序要点·干等状态】它也必须**排在无条件的拳分支之前**，且横扫优先于多段兜底
                //    （理由与上面 entry 那两条完全一样）。干等只有 AttackLoopEntryPreHoldSeconds
                //    （默认 0.13s）这么短，但连击节奏快时"干等还没播完第二段就来了"确有可能：
                //      * 来了**横扫** ⇒ 切 cueAoeDraw（必须排在下面多段兜底之前）✓；
                //      * 又一段**多段** ⇒ 跳过入场正文、直接进稳态 atk_loop（因为它本来就要去那里，
                //        而干等已经完成了"推迟左拳出手"的使命）✓；
                //      * 单击 ⇒ 本状态不注册对应分支 ⇒ SetTrigger 返回 null、状态与 cue 都不动 ✓
                //        （与 atk_loop / entry 同一语义）。
                .AddBranch(cueAtkLoopPreHold, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
                .AddBranch(cueAtkLoopPreHold, "Attack", cueAtkLoop, () => GaoshouAttackStyle.IsMultihit)
                // 干等途中挨打 / 收到 Idle ⇒ 出去（与 entry / atk_loop 完全同样的出口）
                .AddBranch(cueAtkLoopPreHold, "Hit", cueHurtEnter)
                .AddBranch(cueAtkLoopPreHold, "Idle", cueIdle)
                .AddBranch(cueAtkLoopPreHold, GaoshouVisualSettings.GuardEndTrigger, cueRetractR, () => lastWasRight)
                .AddBranch(cueAtkLoopPreHold, GaoshouVisualSettings.GuardEndTrigger, cueRetractL, () => !lastWasRight)
                // 入场**播完 ⇒ 转稳态循环**（真正驱动它的是 WithNext，见状态声明处；
                // 这条分支只是给"入场途中又挨一发多段 Attack"留的兜底：直接跳到稳态，
                // 免得被下面无条件的拳分支抢走、把连击打断成普通出拳 ✗）。
                .AddBranch(cueAtkLoopEntry, "Attack", cueAtkLoop, () => GaoshouAttackStyle.IsMultihit)
                // 入场途中挨打 / 收到 Idle ⇒ 出去（与 atk_loop 完全同样的出口）
                .AddBranch(cueAtkLoopEntry, "Hit", cueHurtEnter)
                .AddBranch(cueAtkLoopEntry, "Idle", cueIdle)
                // 收尾：GuardEnd（计时器，见 AnimationStarted）→ 收拳回站姿，仍按"上一拳"选方向
                .AddBranch(cueAtkLoop, GaoshouVisualSettings.GuardEndTrigger, cueRetractR, () => lastWasRight)
                .AddBranch(cueAtkLoop, GaoshouVisualSettings.GuardEndTrigger, cueRetractL, () => !lastWasRight)
                // 循环中挨打 / 收到 Idle ⇒ 出去
                .AddBranch(cueAtkLoop, "Hit", cueHurtEnter)
                .AddBranch(cueAtkLoop, "Idle", cueIdle);
            // ⚠️ 【这里**故意不注册** `atk_loop + Attack → atk_loop`】（2026-09-26 实机 bug 修复）：
            //    后续每一段命中都会重发 `Attack` 触发器，若让它重进本状态，
            //    `ModAnimStateMachine.EnterState` → `Backend.Play(...)` → `CueFrameSequencePlayer.TryStart`
            //    的第一句就是 `StopAndReset()`（CueFrameSequencePlayer.cs:86）⇒ **循环 cue 每次都从头重播** ✗。
            //    43 帧 × 1/24s ≈ 1.79 秒一轮，而多段命中间隔通常只有 0.2~0.4 秒 ⇒ 永远播不到右拳那一段，
            //    观感就是"动画一直被打断、出不了完整一拳" ✗（用户实机反馈的原话）。
            //    不注册这条分支时 `SetTrigger("Attack")` 在本状态找不到任何目标
            //    （`ModAnimState.CallTrigger` 返回 null，`ModAnimStateMachine.SetTrigger` 直接 return，
            //      ModAnimStateMachine.cs:142-146）⇒ **当前状态与 cue 都不动，循环动画自己按 24fps 连续播下去** ✓，
            //    这正是"连续攻击"要的观感：一次进入、一直循环、靠计时器收尾 ✓。
            //    ⚠️ 别改成"自指分支"（atk_loop→atk_loop）：那仍然会走 EnterState 把 cue 重置掉 ✗。
            //    ⚠️ 同理**别**给 atk_loop 加回 entry 的边：那会让每次连击都重播入场序列，
            //       第二轮起就不再与"已对上"的稳态节奏一致了 ✗（用户明确要求第二轮不许动）。
            //
            // ⚠️ 【入场状态也只注册上面那几条 Attack】
            //    入场序列里第一记右拳**已经在序列内部**，后续命中不需要（也不应该）重进状态：
            //      * 又一段**多段** ⇒ 直接跳稳态 atk_loop（上面的兜底，覆盖"入场还没播完第二段就来了"）✓；
            //      * 来了**横扫** ⇒ 切 cueAoeDraw（必须排在多段兜底之前，见上面的顺序要点）✓；
            //      * 单击 ⇒ 本状态没有对应分支 ⇒ `CallTrigger` 返回 null、`SetTrigger` 直接 return，
            //        状态与 cue 都不动（与 atk_loop 同一语义）✓。
        }

        // ── 「视频版出拳」多段攻击（anim_set = new + 视频素材可用）──
        // 【2026-09-26 的接线】把"多段 = 整段视频循环"换成"**每段命中播一次出拳序列、左右交替**"，
        // 接线方式**照抄原版 PNG 序列那套**（下面那组 `cueIdle/cueGuard/... → Attack → cuePunchR/L`）：
        //   * 谓词形状与原版**同构**（`() => <条件> && !side` / `() => <条件> && side`）✓；
        //   * 区别有两个：目标换成 `cueVideoPunchR/L`，且**选边读的是专用指针 `punchSideFlip`**
        //     （原版那组读的是 `lastWasRight`；两条路径各有各的指针 ⇒ 互不干扰 ✓，
        //       理由与推演见 punchSideFlip 的声明处）。
        //
        // ⚠️【顺序要点】这几条**必须注册在下面那些无条件的拳分支之前**：RitsuLib 的
        //    `ModAnimState.CallTrigger` 取的是"注册顺序里第一个谓词通过的分支"（ModAnimState.cs:117-123），
        //    放到后面就会被 `idle → Attack → punch_r` 抢先 ✗。
        //    谓词严格限定 IsMultihit（段数 ≥ 2 且非 AoE）⇒ 单击与 legacy 自然落到后面的原分支 ✓。
        // ⚠️【为什么不像循环方案那样注册 "Hit / Idle" 出口】本状态是**一次性**序列（Loop(false)、
        //    有 WithNext），播完会自己走 Completed → atk_guard；中途挨打/收到 Idle 也由下面那组
        //    通用分支（`cueHurt* / cueIdle / any-state Idle|Dead`）接走 —— 与 atk_punch_r/_l 的
        //    待遇**完全一样**（原版单击路径同样没给 punch 状态注册 Hit 出口）✓。
        //    ⇒ 既少写一堆重复边，又保证"与原版单击同构"。
        if (useVideoPunch)
        {
            builder
                // 待机/架势/受击途中来了**多段**攻击的下一段 ⇒ 按交替播对应那一拳。
                // 受击那三条是兜底（反伤牌等），与原版单击路径的写法一致。
                //
                // ⚠️【选边读的是 `punchSideFlip`，不是 `lastWasRight`】两者在同一份记账里一起写，
                //    但 `punchSideFlip` 是**专供视频版**的指针，语义更直白：
                //      false ⇒ 本拳出**右**（R）；true ⇒ 本拳出**左**（L）。
                //    进 R 状态后翻成 true、进 L 状态后翻成 false ⇒ 下一拳必然换边 ✓。
                //    第 1 拳初值 false ⇒ 右拳（与原版第一拳一致）✓。
                //    用独立指针（而不是复用 lastWasRight）还能保证：**视频版与单击路径互不干扰**
                //    —— 原版 `cuePunchR/L` 分支仍只读 `lastWasRight`，本次一字未改 ✓。
                .AddBranch(cueIdle, "Attack", cueVideoPunchR, () =>
                    GaoshouAttackStyle.IsMultihit && !punchSideFlip)
                .AddBranch(cueIdle, "Attack", cueVideoPunchL, () =>
                    GaoshouAttackStyle.IsMultihit && punchSideFlip)
                .AddBranch(cueGuard, "Attack", cueVideoPunchR, () =>
                    GaoshouAttackStyle.IsMultihit && !punchSideFlip)
                .AddBranch(cueGuard, "Attack", cueVideoPunchL, () =>
                    GaoshouAttackStyle.IsMultihit && punchSideFlip)
                .AddBranch(cueHurtEnter, "Attack", cueVideoPunchR, () =>
                    GaoshouAttackStyle.IsMultihit && !punchSideFlip)
                .AddBranch(cueHurtEnter, "Attack", cueVideoPunchL, () =>
                    GaoshouAttackStyle.IsMultihit && punchSideFlip)
                .AddBranch(cueHurtBody, "Attack", cueVideoPunchR, () =>
                    GaoshouAttackStyle.IsMultihit && !punchSideFlip)
                .AddBranch(cueHurtBody, "Attack", cueVideoPunchL, () =>
                    GaoshouAttackStyle.IsMultihit && punchSideFlip)
                .AddBranch(cueHurtExit, "Attack", cueVideoPunchR, () =>
                    GaoshouAttackStyle.IsMultihit && !punchSideFlip)
                .AddBranch(cueHurtExit, "Attack", cueVideoPunchL, () =>
                    GaoshouAttackStyle.IsMultihit && punchSideFlip)
                // 出拳途中又来一段 ⇒ 接另一只（**与原版 punch_r↔punch_l 完全同一写法**：
                // 新一段会 StopAndReset 从头播，抢在上一拳播完之前接上，正是"连续出拳"的观感）✓。
                // ⚠️ 这两条是**无谓词**的硬连接（R→L、L→R），本身就是"必然换边"，
                //    与 punchSideFlip 的记账**同向**、不会互相打架 ✓。
                .AddBranch(cueVideoPunchR, "Attack", cueVideoPunchL)
                .AddBranch(cueVideoPunchL, "Attack", cueVideoPunchR)
                // 架势停留结束（计时器）→ 收拳回站姿，方向仍按"上一拳"选（与原版 atk_guard 一致）。
                .AddBranch(cueVideoPunchR, GaoshouVisualSettings.GuardEndTrigger, cueRetractR,
                    () => lastWasRight)
                .AddBranch(cueVideoPunchR, GaoshouVisualSettings.GuardEndTrigger, cueRetractL,
                    () => !lastWasRight)
                .AddBranch(cueVideoPunchL, GaoshouVisualSettings.GuardEndTrigger, cueRetractR,
                    () => lastWasRight)
                .AddBranch(cueVideoPunchL, GaoshouVisualSettings.GuardEndTrigger, cueRetractL,
                    () => !lastWasRight)
                // 【横扫链途中来了"多段单体"的下一段 ⇒ 切视频版出拳】与上面那条
                // `cueAoe* → Attack → cuePunchR/L`（原版兜底）**完全同构**，只是目标换成视频状态：
                // 横扫链的每个状态原本只注册了"非横扫 ⇒ 原版拳"的兜底，不加这几条就会在
                // `IsAoe` 变假、`IsMultihit` 为真的交界处掉回**原版姿势帧**（画面突然换画风）✗。
                // 谓词只带 punchSideFlip（不带 IsMultihit）：能走到这里就说明上面的 IsAoe 分支
                // 都没通过 ⇒ 这次是单体攻击，而 useVideoPunch 时单体多段本来就该走视频版 ✓。
                // ⚠️ 顺序：这几条排在 atk_punch_* 兜底**之前**（同一状态内先注册者优先），
                //    否则又会被原版拳抢走 ✗。
                // ⚠️⚠️【2026-09-27 重大坑：`cut_*` 上这几条必须带 `!IsAoe` 谓词】
                //   用户实测「2 段 AoE 两次都是 cutL、没播 cutR」。日志实证：
                //       f=4210 进 cut_r → f=4215 进 hold_r  ⇒ **只播 5 帧(0.083s)**
                //   而 cut_r 是 12 帧/0.33s（≈20 帧 @60fps）⇒ 只播了 15% 就被切走 ✗。
                //
                //   抢走它的就是下面这几条 `cut_* --Attack--> cueVideoPunch*`：它们的谓词只有
                //   `!punchSideFlip`（**与 IsAoe 无关**），而**攻击会话结束后 `IsAoe` 会变假**
                //   （`AttackCommand.Execute` 完成 → `Exit` 里风格不再保证，见日志
                //    `会话结束` 紧跟在 cut_r 之后）⇒ 第 2 段的 `Attack` 一旦在 cut_r 播放途中到达，
                //   就被这几条无条件接走、切成了**出拳** ✗。
                //   （旧注释说"能走到这里就说明上面 IsAoe 分支都没通过 ⇒ 是单体攻击"——
                //     这个推断**不成立**：`IsAoe` 可能在连击途中就已变假。）
                //
                //   修法：给 `cut_*` 与 `draw` 上的这几条**都加 `!IsAoe`** ——
                //   它们本就只该服务"横扫途中挨了一次**单体**攻击"（见上一条注释），
                //   加谓词后 AoE 的段数绝不会被抢走 ✓。
                //   `hold_*` / `sheathe` 上那几条保留（那时横扫链已近尾声，
                //   被单体攻击接走是合理兜底；但同样加 `!IsAoe` 更严谨 ✓）。
                .AddBranch(cueAoeDraw, "Attack", cueVideoPunchR,
                    () => !GaoshouAttackStyle.IsAoe && !punchSideFlip)
                .AddBranch(cueAoeDraw, "Attack", cueVideoPunchL,
                    () => !GaoshouAttackStyle.IsAoe && punchSideFlip)
                .AddBranch(cueAoeCutR, "Attack", cueVideoPunchR,
                    () => !GaoshouAttackStyle.IsAoe && !punchSideFlip)
                .AddBranch(cueAoeCutR, "Attack", cueVideoPunchL,
                    () => !GaoshouAttackStyle.IsAoe && punchSideFlip)
                .AddBranch(cueAoeCutL, "Attack", cueVideoPunchR,
                    () => !GaoshouAttackStyle.IsAoe && !punchSideFlip)
                .AddBranch(cueAoeCutL, "Attack", cueVideoPunchL,
                    () => !GaoshouAttackStyle.IsAoe && punchSideFlip)
                .AddBranch(cueAoeHoldR, "Attack", cueVideoPunchR,
                    () => !GaoshouAttackStyle.IsAoe && !punchSideFlip)
                .AddBranch(cueAoeHoldR, "Attack", cueVideoPunchL,
                    () => !GaoshouAttackStyle.IsAoe && punchSideFlip)
                .AddBranch(cueAoeHoldL, "Attack", cueVideoPunchR,
                    () => !GaoshouAttackStyle.IsAoe && !punchSideFlip)
                .AddBranch(cueAoeHoldL, "Attack", cueVideoPunchL,
                    () => !GaoshouAttackStyle.IsAoe && punchSideFlip)
                .AddBranch(cueAoeSheathe, "Attack", cueVideoPunchR,
                    () => !GaoshouAttackStyle.IsAoe && !punchSideFlip)
                .AddBranch(cueAoeSheathe, "Attack", cueVideoPunchL,
                    () => !GaoshouAttackStyle.IsAoe && punchSideFlip);
        }

        builder
            // 攻击：每命中一次重发 Attack（_playOnEveryHit 默认 true）。交替靠"外部记录的上一拳是
            // 哪边"来选下一拳 —— 这样即使两次命中之间状态已经回到 idle，也仍然是左右交替 ✓。
            // 注意**不能**给 Attack 挂 any-state → idle，否则每次攻击都会被拉回待机 ✗。
            // ⚠️ 这整组是**原版路径**：单击、legacy 与"视频素材缺失时的兜底"都走这里，本次未改动 ✓。
            .AddBranch(cueIdle, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueIdle, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueGuard, "Attack", cuePunchR, () => !lastWasRight)   // 架势里又挨一发 → 按交替直接出拳
            .AddBranch(cueGuard, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cuePunchR, "Attack", cuePunchL)            // 出拳途中又来一发 → 接另一只
            .AddBranch(cuePunchL, "Attack", cuePunchR)
            .AddBranch(cueRetractR, "Attack", cuePunchL)          // 收拳途中来一发 → 接另一只
            .AddBranch(cueRetractL, "Attack", cuePunchR)
            // 架势停留结束（计时器）→ 收拳回站姿；仍按"上一拳"选收拳方向
            .AddBranch(cueGuard, GaoshouVisualSettings.GuardEndTrigger, cueRetractR, () => lastWasRight)
            .AddBranch(cueGuard, GaoshouVisualSettings.GuardEndTrigger, cueRetractL, () => !lastWasRight)
            // 受击途中出拳（少见，例如反伤牌）：同样按"上一拳"选边，别让攻击动画被吞掉
            .AddBranch(cueHurtEnter, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueHurtEnter, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueHurtBody, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueHurtBody, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueHurtExit, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueHurtExit, "Attack", cuePunchL, () => lastWasRight)
            // 死亡：先匹配"当前在待机"（走站姿起手的死亡），否则（受击/出拳途中被打死）走受击版。
            // 游戏侧顺序已查证：伤害结算派发 Hit 后**不等**受击动画播完就 Kill ⇒ 受击版是常见路径。
            .AddAnyState("Dead", cueDieIdle, () => machine?.Current?.Id == cueIdle)
            .AddAnyState("Dead", cueDieHurt, () => machine?.Current?.Id != cueIdle)
            .AddAnyState("Idle", cueIdle)
            .AddAnyState("Cast", cueIdle)
            .AddAnyState("Relaxed", cueIdle);

        machine = builder.BuildForVisualsRoot(visualsRoot, character);

        // 进入某一拳状态时记录该边（用事件而不是在谓词里写副作用：谓词可能被多次求值）；
        // 进入架势时起一个一次性计时器，到点发 GuardEnd ⇒ 只有"这一串攻击确实结束了"才收拳。
        // AoE 同理：每次停在刀落点上起一个更短的计时器，到点发 AoeEnd ⇒ 单段 AoE 直接收刀，
        // 多段则在下一次 Attack 到来时被接走（离开停留态后那个计时器到点自然落空，无害）。
        var tree = visualsRoot.GetTree();

        // ── 节点离树 ⇒ 置闩锁 + 把 `machine` 置空，**断掉所有计时器 λ → machine 的引用链** ──
        //
        // 这是 B 方案的核心一步。计时器 λ 捕获的是**变量** `machine`（不是它当时的取值），
        // 所以这里把它置空之后，那些还没到点的计时器再也拿不到状态机
        // ⇒ 整棵视觉节点树不再被场景树上的僵尸计时器间接引用 ⇒ **可被 GC 回收** ✓。
        //
        // ⚠️ `machine` 必须保持为**局部变量**（不能改成字段/属性），否则这里的置空
        //    不会反映到已经建好的 λ 里，等于白做 ✓。
        // ⚠️ 置空后 `machine.AnimationStarted` 等订阅仍挂在**旧对象**上，但旧对象已无人引用
        //    ⇒ 一并可回收；`visualsRoot.TreeExiting` 上挂的那几个解绑回调也是同样道理 ✓。
        visualsRoot.TreeExiting += () =>
        {
            disposed = true;
            machine = null;
            // 顺手放掉循环计时器引用（它同样持有 λ；虽然它会随场景树到点释放，但早放早干净）。
            attackLoopTimer = null;
        };
        machine.AnimationStarted += state =>
        {
            // 临时诊断（GaoshouAttackStyle.LogDiagnostics）：看清每次触发落在哪个状态、当时的风格判定值。
            if (GaoshouAttackStyle.LogDiagnostics)
                Entry.Logger.Info(
                    $"[Gaoshou][AoE] 动画状态 -> {state.Id}（IsAoe={GaoshouAttackStyle.IsAoe}）");

            // 回到站姿 = 这一次攻击的动画真的结束了 ⇒ 关掉"攻击会话"，
            // 免得紧接着打出的下一张牌被当成上一次攻击的嵌套命令（沿用了错的一档风格）。
            if (string.Equals(state.Id, cueIdle, StringComparison.Ordinal))
                GaoshouAttackStyle.CloseSession();

            if (string.Equals(state.Id, cuePunchR, StringComparison.Ordinal))
            {
                lastWasRight = true;
                guardTimerGeneration++;   // 原版单击路径同样作废旧计时器
            }
            else if (string.Equals(state.Id, cuePunchL, StringComparison.Ordinal))
            {
                lastWasRight = false;
                guardTimerGeneration++;   // 原版单击路径同样作废旧计时器
            }
            // 「视频版出拳」两个状态**自己记账**（见 punchSideFlip 的声明处大段注释）。
            // 语义（`punchSideFlip` = "**下一拳**该出左"）：
            //   * 进 R（这一拳出了右）⇒ 把指针打到"下一拳出左"：punchSideFlip = true；
            //   * 进 L（这一拳出了左）⇒ 把指针打到"下一拳出右"：punchSideFlip = false。
            // 于是下一段命中求值 `!punchSideFlip → R` / `punchSideFlip → L` 时**必然换边** ✓。
            // 逐段推演（4 段牌，初值 punchSideFlip = false）：
            //   段1 求值 !false ⇒ **R**；进 R ⇒ punchSideFlip = true
            //   段2 求值  true ⇒ **L**；进 L ⇒ punchSideFlip = false
            //   段3 求值 !false ⇒ **R**；进 R ⇒ punchSideFlip = true
            //   段4 求值  true ⇒ **L**  ⇒ 右、左、右、左 ✓
            // ⚠️ 这里写 lastWasRight 是**为了收拳方向**（atk_guard 的 GuardEnd 分支读它），
            //    语义 = "刚才那一拳出的是哪边"，也是**本状态自己的事实**，
            //    与"按 Current 反推"那种依赖外部时序的旧写法完全不同 ✓。
            else if (string.Equals(state.Id, cueVideoPunchR, StringComparison.Ordinal))
            {
                lastWasRight = true;     // 刚才出的是右拳 ⇒ 收拳用右手的收拳帧
                punchSideFlip = true;    // 下一拳出左
                guardTimerGeneration++;  // 作废上一拳留下的收拳计时器（见代际号声明处）
                GaoshouAttackStyle.Trace("  👊 出拳 → 右");
            }
            else if (string.Equals(state.Id, cueVideoPunchL, StringComparison.Ordinal))
            {
                lastWasRight = false;    // 刚才出的是左拳
                punchSideFlip = false;   // 下一拳出右
                guardTimerGeneration++;  // 作废上一拳留下的收拳计时器（见代际号声明处）
                GaoshouAttackStyle.Trace("  👊 出拳 → 左");
            }
            else if (string.Equals(state.Id, cueGuard, StringComparison.Ordinal) && tree != null)
            {
                // 【2026-09-27 重写：旧计时器必须被取消，否则它会"穿越"到下一拳里到点收拳】
                // 实机日志（godot.log，0.3s 设置）：
                //   +19f ⏳ 进入架势 ⇒ 起 0.15s 收拳计时器
                //   +20f 👊 出拳 → 左                ← 下一拳打断了架势（几乎是立刻）
                //   +28f ⏰ 架势计时器到点 ⇒ GuardEnd（当前状态 atk_video_punch_l）✗
                // 也就是说：**上一条"进入架势"起的计时器，在下一拳已经开打之后才到点**，
                // 它照样发 GuardEnd ⇒ 从出拳状态走 `GuardEnd → atk_retract_*` ⇒ **收拳回 stance**。
                // 这就是用户看到的"第二拳之后闪回 stance"✓（等待时间越短越容易撞上 ⇒ 0.3s 必现、
                // 2.0s 因为每拳间隔只有 0.33s、计时器每次都被下一次进架势覆盖而不易到点 ⇒ 看起来"没事"）。
                //
                // 旧写法每次进架势都新建一个计时器、却**从没人取消**上一个（Godot 的 SceneTreeTimer
                // 一旦建出来就无法取消，只能靠"到点后自己判断该不该生效"）。等待时间越长，
                // 同时挂着的僵尸计时器越多 ⇒ 日志里那串 `GuardEnd（当前状态 idle）` 就是它们到点的回声。
                //
                // 修法：给这套计时器一个**代际号**（generation）。每次进入"该由计时器收拳"的状态
                // 都让代际 +1，并让新计时器记住自己的代际；到点时若代际已经不是最新的，
                // **说明中途又发生过一次进入 ⇒ 本计时器是僵尸，直接丢弃、不发 GuardEnd** ✓。
                // 这等价于"取消旧计时器"，但不需要 Godot 提供取消接口 ✓。
                var myGeneration = ++guardTimerGeneration;
                var guardStart = tree.CreateTimer(GaoshouVisualSettings.GuardHoldSeconds);
                guardStart.Timeout += () =>
                {
                    // 节点已离树 ⇒ 不推进动画、也不碰 machine（见 disposed 闩锁声明处）。
                    // ⚠️ 用 `is not { } m` 取出非空引用（而不是裸 `machine.`）：
                    //    `disposed` 与 `machine = null` 是**同一次** TreeExiting 回调里一起设置的，
                    //    编译器无法证明这个不变式 ⇒ 裸写会报 CS8602；这里顺手把不变式写明 ✓。
                    if (disposed || machine is not { } m)
                        return;

                    if (myGeneration != guardTimerGeneration)
                    {
                        GaoshouAttackStyle.Trace(
                            $"  🗑 僵尸架势计时器到点，丢弃（代际 {myGeneration} < {guardTimerGeneration}）");
                        return;
                    }

                    GaoshouAttackStyle.Trace(
                        $"  ⏰ 架势计时器到点 ⇒ GuardEnd（当前状态 {m.Current?.Id}，"
                        + $"IsMultihit={GaoshouAttackStyle.IsMultihit}）");
                    m.SetTrigger(GaoshouVisualSettings.GuardEndTrigger);
                };
                GaoshouAttackStyle.Trace(
                    $"  ⏳ 进入架势 ⇒ 起 {GaoshouVisualSettings.GuardHoldSeconds:F2}s 收拳计时器（代际 {myGeneration}）");
            }
            // ── AoE 横扫的「待办刀数」记账（见 aoePendingHits 声明处）──
            //
            // 【为什么要在这里记账，而不是在分支谓词里】RitsuLib 的谓词可能被**多次求值**
            //   （`CallTrigger` 逐个试分支），所以谓词必须是纯读取、不能改状态 ✗；
            //   而"欠几刀"必须**恰好记一次**，所以只能放在"进入状态"这个**恰好发生一次**的事件里 ✓
            //   （与 `punchSideFlip` 的记账同一个设计原则）。
            if (string.Equals(state.Id, cueAoeCutR, StringComparison.Ordinal) ||
                string.Equals(state.Id, cueAoeCutL, StringComparison.Ordinal))
            {
                // 又播出一刀 ⇒ 已播刀数 +1。欠账 = pending − cutsPlayed（见声明处）✓。
                aoeCutsPlayed++;
                GaoshouAttackStyle.Trace(
                    $"  🗡 出刀 {state.Id}（已播 {aoeCutsPlayed} / 命中 {aoePendingHits}）");

                // 【关键：**进刀即预约下一刀**，不等这刀播完】
                //   用户实测「第三刀结束早于出伤」—— 根因是"伤害并行、动画串行"，
                //   越靠后的刀越晚。这里把"还欠的刀"**立刻入队**（`AoeNextCut`），
                //   当前刀播完（发 Finished）后队列会被立刻消费 ⇒ 下一刀**无缝接上**，
                //   省掉 `hold_*` 的停留时间，从而让后续刀尽可能早地开挥 ✓。
                //   ⚠️ 入队**不会打断**当前刀：状态机要等 cue 播完才推进 ✓。
                //   ⚠️ 每刀只预约 **1** 次（若还欠多刀，下一刀进入时会再预约一次）⇒ 不会堆积 ✓。
                var stillOwed = aoePendingHits - aoeCutsPlayed;
                if (stillOwed > 0 && machine is { } m2)
                {
                    GaoshouAttackStyle.Trace($"  ⏭ 预约下一刀（还欠 {stillOwed}）");
                    m2.SetTrigger(GaoshouVisualSettings.AoeNextCutTrigger);
                }
            }
            else if (string.Equals(state.Id, cueAoeDraw, StringComparison.Ordinal))
            {
                // 进入取刀 ⇒ 一串新的横扫连击开始 ⇒ 两个计数都重置
                //（上一串的残留绝不能带进来）✓。
                // ⚠️ pending **直接取"实际段数"**（权威），不再靠命中事件累加 ——
                //    见声明处的日志实证（2 段牌只收到 1 次命中派发）✓。
                aoeCutsPlayed = 0;
                aoePendingHits = Math.Max(1, GaoshouAttackStyle.EffectiveHitCount);
                GaoshouAttackStyle.Trace(
                    $"  🎬 取刀：横扫连击开始（该砍 {aoePendingHits} 刀）");
            }
            else if (string.Equals(state.Id, cueAoeHoldR, StringComparison.Ordinal) ||
                     string.Equals(state.Id, cueAoeHoldL, StringComparison.Ordinal))
            {
                // 刀已落下。**欠账 = 命中数 − 已播刀数**（见声明处）。
                //   还有欠账 ⇒ 立刻补上那一刀 ✓ —— 这就是"刀数 = 段数"的保证。
                //   ⚠️ 补刀时**不**再起 AoeEnd 计时器：马上要出下一刀，起了也只会到点发一个
                //      没有分支匹配的 AoeEnd（无害但多余）。
                //
                // 【2026-09-27：不再"等到 hold 才补" —— 改成**在出刀途中就预约补刀**】
                //   用户实测「**第三刀结束早于出伤**。拔刀的加速补偿只需要作用于前两段」。
                //   根因是"伤害并行、动画串行"：
                //     * 3 段伤害都在会话开始后 ≈0.065~0.18s 内结算完；
                //     * 而刀是串行播的：第 N 刀开始时刻 = 取刀 + (N-1) × 单刀时长。
                //   所以越靠后的刀必然越晚 —— 靠"缩短单刀"是错的方向
                //   （那会让**所有**刀一起提前，前两刀反而过早，用户观察完全正确）。
                //
                //   现在改为**让后续刀提前排队**：每次进入 `cut_*`（= 一刀已开播）
                //   就把"还欠的刀"立刻预约到队列里（`AoeNextCut`），而不是等这一刀播完。
                //   队列化的触发器会在当前 cue 播完后立刻被消费 ⇒ 下一刀**无缝接上**，
                //   省掉 `hold_*` 的停留，从而让第 2/3 刀尽可能早地开始挥 ✓。
                //   ⚠️ 这不会打断当前刀：`SetTrigger` 只是入队，状态机要等
                //      `CueFrameSequencePlayer` 播完（发 Finished）才推进 ✓。
                var owed = aoePendingHits - aoeCutsPlayed;
                if (owed > 0 && machine is { } mm)
                {
                    // ⚠️ 必须用**专属触发器**（见 GaoshouVisualSettings.AoeNextCutTrigger 的注释）：
                    //    以前这里发 "Attack"，会被出拳分支抢走 ⇒ 补刀变成出拳 ✗（实机 bug）。
                    //
                    // ⚠️【为什么这里通常**不会**再触发一次】正常节奏下，上一刀**进入时**
                    //    （见 `cut_*` 分支里的"预约下一刀"）就已经把这次补刀排进队列了，
                    //    队列在当前刀播完时被消费 ⇒ 状态直接 `cut_* → cut_*`，
                    //    根本不会经过 `hold_*` ⇒ 本分支只在**异常时序**下兜底
                    //    （例如刀播完时预约还没被消费、或段数在中途变大）。
                    //    因此这里的重复 SetTrigger 是**幂等安全**的：
                    //    多入队一次最多让下一刀早一帧开始，不会多砍一刀
                    //    （刀数由 `aoeCutsPlayed` 计数决定，与入队次数无关）✓。
                    GaoshouAttackStyle.Trace($"  ➕ 补刀（命中 {aoePendingHits} > 已播 {aoeCutsPlayed}）");
                    mm.SetTrigger(GaoshouVisualSettings.AoeNextCutTrigger);
                }
                else if (tree != null)
                {
                    // 不欠账 ⇒ 停在落点上等 AoeHoldSeconds，到点收刀回站姿 ✓。
                    var timer = tree.CreateTimer(GaoshouVisualSettings.AoeHoldSeconds);
                    timer.Timeout += () =>
                    {
                        // 节点已离树 ⇒ 不推进动画、也不碰 machine（见 disposed 闩锁声明处）。
                        if (disposed || machine is not { } m)
                            return;

                        m.SetTrigger(GaoshouVisualSettings.AoeEndTrigger);
                    };
                }
            }
            else if (useAttackLoop && (string.Equals(state.Id, cueAtkLoop, StringComparison.Ordinal) ||
                                       string.Equals(state.Id, cueAtkLoopEntry, StringComparison.Ordinal) ||
                                       string.Equals(state.Id, cueAtkLoopPreHold, StringComparison.Ordinal))
                                    && tree != null)
            {
                // 【连续攻击（入场 + 稳态循环）的**退出条件**（关键，别让它卡在循环里）】
                // 稳态 cue 是 Loop(true) 永不播完，所以**不能靠播完退出**，只能靠计时器；
                // 入场 cue 虽是一次性（会转稳态），但**同样**要起这个计时器，两个理由：
                //   ① 语义统一：入场期间命中的重置逻辑与稳态共用一套，不会出现"入场播太久被漏掉"；
                //   ② 兜底：万一 IsMultihit 提前变假 / 命中不再来，GuardHoldSeconds 之后仍能收拳回站姿 ✓。
                // 规则：
                //   * 进入本状态时起一个 GuardHoldSeconds 的一次性计时器；
                //   * **后续每段命中**（Attack 触发器）都会把它**重置/重起**（见下面的订阅）；
                //   * 最后一段打完、GuardHoldSeconds 内没有新命中 ⇒ 到点发 GuardEnd ⇒
                //     上面的 `atk_loop + GuardEnd → atk_retract_r/_l` 分支收拳回站姿 ✓。
                // 因此退出**只取决于"最近一次命中之后过了多久"**，与 IsMultihit 此刻是否仍为 true 无关 ✓ ——
                // 这一点很重要：`IsMultihit` 读的是"游戏已经算出的段数"，它在一整串连击期间**一直为真**，
                // 若把它当退出条件就会永远退不出去 ✗。改用"命中后计时"后语义正好：
                //   * 还有下一段（段间隔 < GuardHoldSeconds，默认 1.0s）⇒ 计时器被重置，继续循环 ✓；
                //   * 最后一段打完、GuardHoldSeconds 内没有新命中 ⇒ 到点收拳 ✓。
                // 与 atk_guard 完全同一套路（复用同一个多段等待设置，语义一致）。
                //
                // ⚠️ 【为什么必须在"命中"上重置，而不是"进入状态"上重起】
                //    这两个状态**都不因后续命中重进**（重进会把 cue 从第 1 帧重播 ⇒ 永远放不完一整轮 ✗），
                //    所以"进入事件"整串连击里只发生**一次**；若只靠它起计时器，循环播到一半就会被 GuardEnd
                //    打断收拳 ✗。正确做法是：进状态起一次 + 之后**每次命中都重置** ✓（见下面的订阅）。
                attackLoopTimer = tree.CreateTimer(GaoshouVisualSettings.GuardHoldSeconds);
                attackLoopTimer.Timeout += () =>
                {
                    // 节点已离树 ⇒ 不推进动画、也不碰 machine（见 disposed 闩锁声明处）。
                    if (disposed || machine is not { } m)
                        return;

                    m.SetTrigger(GaoshouVisualSettings.GuardEndTrigger);
                };
            }
        };

        // ── 「连续攻击」循环：**每次命中都重置收尾计时器** ──
        // 每段命中都会让 AttackCommand 在循环体内派发一次 `Attack` ⇒ 触发 AttackHitTriggered
        // （挂在 CreatureCmd.TriggerAnim 上，**每段都跑**；不能挂 ModifyAttackHitCount，那个每张牌只调一次 ✗）；
        // 在这里把计时器重起（Godot 的 SceneTreeTimer 不能改时长，只能重建）。
        // 于是语义正是"**最近一次命中之后**过了 GuardHoldSeconds 才收拳"：
        //   * 还有下一段（间隔 < GuardHoldSeconds）⇒ 计时器不断被推后，循环一直播 ✓；
        //   * 最后一段之后无新命中 ⇒ 到点 ⇒ GuardEnd ⇒ `atk_loop + GuardEnd → retract_*` 收拳回站姿 ✓。
        // ⚠️ 只在**确实停在 atk_loop / atk_loop_entry 里**时才重置（`machine.Current` 判定）：
        //    此时循环 cue 正在播，重置才有意义；别的状态自有收尾逻辑，乱重置也无害，但不必。
        //    入场状态也要一起认 ✓ —— 否则"入场还没播完就来第二段"时计时器不会被推后，
        //    可能出现"连击还在打、却被 GuardEnd 提前收拳" ✗。
        // ⚠️ 必须解绑：状态机随每场战斗重建，旧闭包若一直挂在静态事件上会累积（泄漏 + 误触发）✗。
        if (useAttackLoop && tree != null)
        {
            void ResetAttackLoopTimer()
            {
                // 节点已离树 ⇒ 连计时器都不必再建（这条订阅本身也会在 TreeExiting 里解绑）。
                if (disposed)
                    return;

                var currentId = machine?.Current?.Id;
                if (!string.Equals(currentId, cueAtkLoop, StringComparison.Ordinal) &&
                    !string.Equals(currentId, cueAtkLoopEntry, StringComparison.Ordinal) &&
                    !string.Equals(currentId, cueAtkLoopPreHold, StringComparison.Ordinal))
                    return;

                attackLoopTimer = tree.CreateTimer(GaoshouVisualSettings.GuardHoldSeconds);
                attackLoopTimer.Timeout += () =>
                {
                    // 节点已离树 ⇒ 不推进动画、也不碰 machine（见 disposed 闩锁声明处）。
                    if (disposed || machine is not { } m)
                        return;

                    m.SetTrigger(GaoshouVisualSettings.GuardEndTrigger);
                };
            }

            GaoshouAttackStyle.AttackHitTriggered += ResetAttackLoopTimer;
            // 节点离树（战斗结束 / 视觉被回收）时解绑，避免旧闭包累积。
            visualsRoot.TreeExiting += () => GaoshouAttackStyle.AttackHitTriggered -= ResetAttackLoopTimer;
        }

        // ── 「视频版出拳」多段攻击：**每次命中重置收拳计时器**（幂等） ──
        //
        // 【2026-09-27 起本订阅**只做计时器重置**，不再决定左右交替】（旧写法在这里改 lastWasRight，
        //   因为依赖被吞掉的命中事件，实机出现"连续 4 次右拳 / 右右左右"；交替已改由
        //   `AnimationStarted` 里状态机**自己记账**，见 punchSideFlip 的声明处）。
        //
        // 【为什么视频版路径**也**必须重置】视频版出拳是"一次性序列 → WithNext 到 atk_guard 停在架势"。
        //   `atk_guard` 是静态 cue（永不播完）⇒ 只能靠 `AnimationStarted` 里起的那个
        //   `GuardHoldSeconds` 计时器发 GuardEnd 收拳。而那个计时器**只在"进入 atk_guard"时起一次**，
        //   连击途中不会再进 atk_guard ⇒ 若不在这里按命中推后，**最后一段还没打完，计时器就到点收拳** ✗
        //   —— 这正是用户反馈的"连击途中突然切回 stance、然后继续出拳"（症状 3）。
        //   在每次命中重建计时器后，语义变成"**最近一次命中之后**过了 GuardHoldSeconds 才收拳" ✓。
        //
        // 【幂等 ⇒ 重复回调无害】Godot 的 `SceneTreeTimer` 不能改时长，只能重建：
        //   这里每次都 `CreateTimer(GuardHoldSeconds)` 并重新挂 Timeout ⇒ 无论本回调被调用 1 次还是
        //   N 次（同一段命中可能让 `CreatureCmd.TriggerAnim` 的 Prefix 被 Harmony 触发多次、
        //   或同一帧内多次派发），**结果都完全相同**（只是把到点时刻推后到"最后一次调用 + 0.33/0.6 秒"）✓。
        //   所以这里**不需要也不应该**做任何去重（去重的教训见 GaoshouAttackStyle 的类注释）。
        //
        // ⚠️ 命中间隔若**大于** GuardHoldSeconds，仍会被 GuardEnd 提前收拳（这是设置项的语义，
        //    不是 bug）。用户的 0.6s 对 0.33s/拳的节奏足够 ✓；万一某张牌段间隔更长，
        //    把设置页「多段攻击动画等待时间（拳）」调大即可（不要改这里的常量）✓。
        //
        // ⚠️【2026-09-27：这里原来挂着"每次命中重置收拳计时器"的订阅，现已整体删除】
        //   历史：
        //     * 第一版挂 `AttackHitTriggered`（TriggerAnim 的 Prefix）→ 因 TriggerAnim 是 async，
        //       时刻不准，且配合已删除的帧号去重门会把命中整段吞掉 ✗；
        //     * 第二版挂 `AttackHitSettled`（AddResultsInternal 的 Postfix）→ **实机日志证明一次都没触发**
        //       （godot.log 里没有任何 `↻ 拳计时器重置` 行；该方法大概率被 JIT 内联，Harmony 挂不上）✗。
        //   两版的共同错误假设是"计时器没被重置"。日志证明**根本不是**：
        //   真正的问题是**僵尸计时器没被作废** —— 旧计时器在下一拳已经开打之后到点，
        //   照样发 GuardEnd ⇒ 收拳回 stance（详细推演见 `cueGuard` 分支的代际号注释）✓。
        //   该问题用"多重置几次"治不好（重置只会再造一个将来的僵尸），必须靠代际号作废 ✓。
        //   所以收拳现在**完全不依赖外部事件**：进入架势时起一个计时器 + 代际号决定它是否有效 ✓。

        // ── AoE 横扫：**每段命中记一笔"欠刀"**（见 aoePendingHits 声明处）──
        //
        // 【为什么需要它】游戏的命中流（每 ~0.065s 一段）比动画（每刀 0.33s）快得多，
        //   所以在"正在播一刀"期间到达的命中必须**被记下来**，等刀落下再补上，
        //   否则那一刀就丢了（用户实测：状态机吃掉第 2 段，掉回出拳/少砍一刀）✗。
        //
        // 【为什么不做任何去重】去重的教训见 GaoshouAttackStyle 的类注释（帧号去重门曾
        //   把合法命中整段吞掉、引发三个回归）。这里**宁可多记也不要漏记**：
        //   多记一刀的后果是"多挥一下"（观感小瑕疵），漏记的后果是"少一刀"（用户明确反馈的问题）。
        //   ⚠️ 记账只在 `IsAoe` 时生效，不影响出拳路径 ✓。
        //   ⚠️ 必须解绑（状态机随每场战斗重建，旧闭包会累积）。
        if (tree != null)
        {
            void NoteAoeHit()
            {
                // 只在"横扫链正在跑"时记账（其它状态自有逻辑，记了也没人消费）。
                var id = machine?.Current?.Id;
                var inAoeChain =
                    string.Equals(id, cueAoeDraw, StringComparison.Ordinal) ||
                    string.Equals(id, cueAoeCutR, StringComparison.Ordinal) ||
                    string.Equals(id, cueAoeCutL, StringComparison.Ordinal) ||
                    string.Equals(id, cueAoeHoldR, StringComparison.Ordinal) ||
                    string.Equals(id, cueAoeHoldL, StringComparison.Ordinal);
                if (!inAoeChain)
                    return;

                // 【只作兜底：不超过"该砍的刀数"】见 aoePendingHits 声明处：
                //   pending 的**权威来源是实际段数**（`EffectiveHitCount`），
                //   这个命中事件只用于"段数偏小时补一点"，所以**封顶**在段数上，
                //   否则会像实机日志那样把 2 段牌记成 `欠账 3`（多砍一刀）✗。
                var cap = Math.Max(1, GaoshouAttackStyle.EffectiveHitCount);
                if (aoePendingHits < cap)
                {
                    aoePendingHits++;
                    GaoshouAttackStyle.Trace(
                        $"  📌 命中兜底 +1（欠账 {aoePendingHits}/{cap}，当前 {id}）");
                }
            }

            GaoshouAttackStyle.AttackHitTriggered += NoteAoeHit;
            visualsRoot.TreeExiting += () => GaoshouAttackStyle.AttackHitTriggered -= NoteAoeHit;
        }

        // ── 「视频版出拳」的左右交替记账**不在这里** ──
        // 已移到 `machine.AnimationStarted` 里（进 atk_video_punch_r/_l 时自己翻 `punchSideFlip`）。
        //
        // 【历史】2026-09-26 与 2026-09-27 两轮都试图用"命中事件"来定侧，两轮都失败：
        //   * 第一轮按 `Current` **反推**"上一拳是哪边" —— `Current` 只在 `SetTrigger` 内部推进，
        //     而事件排在它前面 ⇒ 第 2 段读到过期值 ⇒ 首两拳同侧 ✗；
        //   * 第二轮改成按 `Current` **推进到下一侧**，并给事件加"帧号去重" —— 去重门因
        //     `TriggerAnim` 是 async（Postfix 在返回 Task 时即触发、看不到 await 完成）而卡死，
        //     命中事件被整段吞掉 ⇒ 翻转一次都不发生（连续同侧）+ 计时器不重置（途中收拳）✗。
        // 结论：**只要"定侧"依赖外部事件，就受事件送达时机/重复/丢失的影响**。
        // 现在的方案让状态机**只依赖自己进入状态这一事实**，从根上免疫这些问题 ✓。
        //   * 第 1 拳：初值 lastWasRight = false ⇒ `!lastWasRight` ⇒ **右拳**（与原版第一拳一致）✓；
        //   * 之后每一拳都由上一拳的 AnimationStarted 把指针推向另一侧 ⇒ 右/左/右/左 严格交替 ✓；
        //   * 单击（IsMultihit = false）走原版 `cuePunchR/L` 路径，不碰 punchSideFlip，
        //     原版那条路径的记账（`AnimationStarted` 里 cuePunchR/L 两条）**一个字都没动** ✓。

        return machine;
    }

    /// <summary>把 (贴图路径, 时长) 列表塞进帧序列（不循环：靠状态机 WithNext 串场）。</summary>
    private static void AddFrames(
        VisualFrameSequenceBuilder sequence, (string Path, float Seconds)[] frames)
    {
        foreach (var (path, seconds) in frames)
            sequence.Frame(path, seconds);
        sequence.Loop(false);
    }

    /// <summary>循环帧序列（站姿呼吸用）：与 <see cref="AddFrames" /> 相同，但显式 <c>Loop(true)</c>。</summary>
    private static void AddLoopFrames(
        VisualFrameSequenceBuilder sequence, (string Path, float Seconds)[] frames)
    {
        foreach (var (path, seconds) in frames)
            sequence.Frame(path, seconds);
        sequence.Loop(true);
    }

    // 不覆写 TryCreateCreatureVisuals()：基类返回 null 表示"使用已配置的场景路径"，
    // 继承储君时用储君的战斗模型，静态图时用 GaoshouVisualSettings.StaticVisualsScenePath。
    // 也不覆写 SetupCustomCombatAnimationStateMachine()：基类返回 null 表示"用标准 Spine 动画器或
    // 视觉提示播放"，静态图的 VisualCues 会由 RitsuLib 的 cue 播放路径接管。
    //
    // 【历史】2026-09-20 曾接入一套 GPT 生成的 6 帧待机帧，因逐帧一致性不足回退到储君占位；
    // 现改为**单张静态图**（不再有多帧漂移问题）。旧的 6 帧素材与生成脚本已挪到
    // _workspace\backups\character_anim_20260920_185003\ 与 _workspace\_imgwork\make_character_idle_frames.py。
    // 帧规格（仍然适用）：512×512 透明画布、脚底落在 y=456、水平居中 x=256；站姿人物高约 330px
    // （原版玩家小人的真实基准是储君场景里的 %Bounds = 230×335）；
    // 场景结构：纯 Node2D 根 + %Visuals(Sprite2D)/%Bounds/%CenterPos/%IntentPos/%TalkPos，
    // 根节点**不要**挂脚本（那是游戏工程内的路径，mod 工程没有），Sprite2D 上移 200px 使"脚底=节点原点"，
    // .tscn 里也不能写 ';' 注释。

    public override List<string> GetArchitectAttackVfx()
    {
        return
        [
            "vfx/vfx_attack_blunt",
            "vfx/vfx_heavy_blunt",
            "vfx/vfx_attack_slash",
            "vfx/vfx_bloody_impact",
            "vfx/vfx_rock_shatter"
        ];
    }
}
