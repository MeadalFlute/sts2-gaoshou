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
    ///     AoE 横扫 atk_aoe_*（起手 / 两刀 / 落点停留 / 收刀）。
    ///     cast 没有专门图，不写 cue ⇒ 状态机把它指向 idle，不会出现空帧。</item>
    /// </list>
    /// </summary>
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

        return cues
            // 攻击后的"停在架势"：静态贴图 cue（不会播完 ⇒ 一直保持到计时器发 GuardEnd）
            .Single("atk_guard", GaoshouVisualSettings.AttackGuardTexturePath)
            .Sequence("hurt_enter", sequence => AddFrames(sequence, GaoshouVisualSettings.HurtEnterSequence))
            .Sequence("hurt_body", sequence => AddFrames(sequence, GaoshouVisualSettings.HurtBodySequence))
            .Sequence("hurt_exit", sequence => AddFrames(sequence, GaoshouVisualSettings.HurtExitSequence))
            .Sequence("die_idle", sequence => AddFrames(sequence, GaoshouVisualSettings.DieFromIdleSequence))
            .Sequence("die_hurt", sequence => AddFrames(sequence, GaoshouVisualSettings.DieFromHurtSequence))
            .Sequence("atk_punch_r", sequence => AddFrames(sequence, GaoshouVisualSettings.AttackPunchRightSequence))
            .Sequence("atk_punch_l", sequence => AddFrames(sequence, GaoshouVisualSettings.AttackPunchLeftSequence))
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

        // 谓词要读"当前状态"，而状态机是 Build 之后才存在的 ⇒ 用一个可变引用兜住。
        ModAnimStateMachine? machine = null;

        var builder = ModAnimStateMachineBuilder.Create()
            .AddState(cueIdle, true).AsInitial().Done()
            .AddState(cueHurtEnter, false).WithNext(cueHurtBody).Done()
            .AddState(cueHurtBody, false).WithNext(cueHurtExit).Done()
            .AddState(cueHurtExit, false).WithNext(cueIdle).Done()
            .AddState(cueDead, false).Done()                      // 终止状态：不设 Next，播完定格
            .AddState(cueDieIdle, false).Done()                   // 死亡（从待机切入），同样终止
            .AddState(cueDieHurt, false).Done()                   // 死亡（从受击切入），同样终止
            // 出拳 → **停在架势**（不立刻收拳！）；架势里靠计时器发 GuardEnd 才收拳回站姿。
            .AddState(cuePunchR, false).WithNext(cueGuard).Done()
            .AddState(cuePunchL, false).WithNext(cueGuard).Done()
            .AddState(cueGuard, false).Done()                     // 静态 cue，不会播完 ⇒ 一直保持
            .AddState(cueRetractR, false).WithNext(cueIdle).Done()
            .AddState(cueRetractL, false).WithNext(cueIdle).Done()
            // AoE 横扫（打全体）：起手 → 第一刀 → 停在刀落点（等下一段 / 等 AoeEnd 收刀）。
            // 单段 AoE 就是 起手→第一刀→收刀（"直接收刀"），多段才会在落点上被下一次 Attack 接走。
            .AddState(cueAoeDraw, false).WithNext(cueAoeCutR).Done()
            .AddState(cueAoeCutR, false).WithNext(cueAoeHoldR).Done()
            .AddState(cueAoeCutL, false).WithNext(cueAoeHoldL).Done()
            .AddState(cueAoeHoldR, false).Done()                   // 静态 cue，不会播完 ⇒ 一直保持
            .AddState(cueAoeHoldL, false).Done()
            .AddState(cueAoeSheathe, false).WithNext(cueIdle).Done()
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
            // 横扫链内部再来一段 ⇒ 立刻接另一刀（左右交替）—— 这就是 loop 的来源
            .AddBranch(cueAoeDraw, "Attack", cueAoeCutR, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueAoeCutR, "Attack", cueAoeCutL, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueAoeCutL, "Attack", cueAoeCutR, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueAoeHoldR, "Attack", cueAoeCutL, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueAoeHoldL, "Attack", cueAoeCutR, () => GaoshouAttackStyle.IsAoe)
            // 落点上等不到下一段（计时器）⇒ 收刀回站姿
            .AddBranch(cueAoeHoldR, GaoshouVisualSettings.AoeEndTrigger, cueAoeSheathe)
            .AddBranch(cueAoeHoldL, GaoshouVisualSettings.AoeEndTrigger, cueAoeSheathe)
            // 横扫途中挨了一次**单体**攻击 ⇒ 兜底切拳（否则这次 Attack 被吞掉、拳头完全不出现 ✗）
            .AddBranch(cueAoeDraw, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueAoeDraw, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueAoeCutR, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueAoeCutR, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueAoeCutL, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueAoeCutL, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueAoeHoldR, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueAoeHoldR, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueAoeHoldL, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueAoeHoldL, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueAoeSheathe, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueAoeSheathe, "Attack", cuePunchL, () => lastWasRight)
            // 攻击：每命中一次重发 Attack（_playOnEveryHit 默认 true）。交替靠"外部记录的上一拳是
            // 哪边"来选下一拳 —— 这样即使两次命中之间状态已经回到 idle，也仍然是左右交替 ✓。
            // 注意**不能**给 Attack 挂 any-state → idle，否则每次攻击都会被拉回待机 ✗。
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
                lastWasRight = true;
            else if (string.Equals(state.Id, cuePunchL, StringComparison.Ordinal))
                lastWasRight = false;
            else if (string.Equals(state.Id, cueGuard, StringComparison.Ordinal) && tree != null)
            {
                // 中途再来命中会离开架势 ⇒ 这个计时器到点在别的状态上触发，找不到对应分支，无害。
                var timer = tree.CreateTimer(GaoshouVisualSettings.GuardHoldSeconds);
                timer.Timeout += () => machine.SetTrigger(GaoshouVisualSettings.GuardEndTrigger);
            }
            else if ((string.Equals(state.Id, cueAoeHoldR, StringComparison.Ordinal) ||
                      string.Equals(state.Id, cueAoeHoldL, StringComparison.Ordinal)) && tree != null)
            {
                var timer = tree.CreateTimer(GaoshouVisualSettings.AoeHoldSeconds);
                timer.Timeout += () => machine.SetTrigger(GaoshouVisualSettings.AoeEndTrigger);
            }
        };
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
