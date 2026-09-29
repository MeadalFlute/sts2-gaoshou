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
                // ⚠️⚠️【2026-09-29 修「多人队友头像叠着铁甲战士的阴影」—— 参照 LexNinja2 的做法】
                //   这一槽位**必须是 PackedScene（.tscn），不能给 PNG** ✗✗ 我们原来给的是
                //   `Gaoshou_top_portrait.png` ⇒ 出事了：
                //     * 原版语义（反编译 `CharacterModel.cs:127-129`）：
                //       `IconPath => SceneHelper.GetScenePath("ui/character_icons/<id>_icon")`、
                //       `Icon => PreloadManager.Cache.GetScene(IconPath).Instantiate<Control>()`
                //       ⇒ **它期望一个场景**（实测原版 `res://scenes/ui/character_icons/ironclad_icon.tscn`
                //         就是一个 TextureRect，贴 `images/ui/top_panel/character_icon_ironclad.png`）。
                //     * RitsuLib 侧对这个槽位有**类型校验**（`CharacterAssetOverridePatches.cs:114-131`
                //       `IsLoadableAsAny(..., nameof(PackedScene), typeof(PackedScene), …)`）
                //       ⇒ 我们给的 PNG 校验不过 ⇒ **回退到占位角色的 icon 场景**，
                //         而 RitsuLib 的默认占位是 **ironclad**（`CharacterAssetProfiles.cs:21`
                //         `DefaultPlaceholderCharacterId = "ironclad"`）⇒ 于是 `Icon`（顶部栏/多人队友头像
                //         那条路径用的就是它）变成了**铁甲战士的头像**（原版那张图自带一层阴影 ✓ 正是用户看到的）
                //         ⇒ 多人里就表现为"我们的头像下面/周围压着铁甲战士的阴影" ✗。
                //   修法（= LexNinja2 的做法，见 `LexNinja2Code/Character/LexNinja2.cs:39`
                //   `IconPath: "res://LexNinja2/scenes/Ninja_icon.tscn"` ⇒ 它给的是**自己的场景** ✓）：
                //   我们新建 `res://Gaoshou/scenes/ui/Gaoshou_top_icon.tscn`（结构照抄原版那个 icon 场景：
                //   TextureRect + anchors_preset=15 + expand_mode=1 + stretch_mode=5 + mouse_filter=2），
                //   里面只贴**我们自己的** `Gaoshou_top_portrait.png` ⇒ 类型正确、且场景里没有任何原版图 ✓。
                //   ⚠️ 其余 Ui 槽位本就与"原版期望类型"一致（IconTexture/IconOutline/CharacterSelect*、
                //      MapMarker 原版都是 PNG ✓），所以**只有这一条**需要改 ✓。
                IconPath: $"{Entry.ResPath}/scenes/ui/Gaoshou_top_icon.tscn",
                CharacterSelectBgPath: GaoshouVisualSettings.CharacterSelectBgPath,
                CharacterSelectIconPath: $"{Entry.ResPath}/images/characters/Gaoshou_character_select.png",
                // ⚠️【2026-09-29 止血：原本误用了**原版铁甲战士**的资源】
                //   这两条原来指的是 `Gaoshou_character_select_locked.png`(132×195) 与 `Gaoshou_map_marker.png`(49×64)，
                //   实测它们与 `res://images/packed/character_select/char_select_ironclad_locked.png` /
                //   `res://images/packed/map/icons/map_marker_ironclad.png` **逐字节相同**（MAE = 0.00）
                //   —— 也就是**铁甲战士的锁定态黑色人头剪影 + 铁甲战士的地图标记**被原样拷进了我们的仓库 ✗
                //   ⇒ 玩家在**地图**（NMapMarker.cs:26 用 MapMarker）与**选人界面**（NCharacterSelectButton.cs:145-155
                //      `_isLocked` 时用 CharacterSelectLockedIcon）看到的是铁甲战士的图 ✗。
                //   止血改法：两条都先指向**我们自己的**紫兜帽 `Gaoshou_character_icon.png`（200×200，已确认自有素材 ✓）。
                //   美术正解（以后再做）：单独画一张 49×64 的地图标记 + 一张 132×195 的兜帽黑剪影，
                //   只要**沿用这两个文件名/路径**就行，代码不用再改 ✓。
                //   ⚠️ 那两张铁甲战士拷贝图**已不再被任何路径引用**（可用但未引用；先保留文件、不删）。
                CharacterSelectLockedIconPath: $"{Entry.ResPath}/images/characters/Gaoshou_character_icon.png",
                MapMarkerPath: $"{Entry.ResPath}/images/characters/Gaoshou_character_icon.png");

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
                    .Merchant(cues =>
                    {
                        cues.Single("relaxed_loop", GaoshouVisualSettings.ShopTexturePath,
                                GaoshouVisualSettings.ShopStyle)
                            .Single("dead", GaoshouVisualSettings.DeadTexturePath,
                                GaoshouVisualSettings.CorpseStyle);

                        // 【商店 / 假商人里的「放弃游戏」】走 NMerchantCharacter.PlayAnimation("die")
                        // ⇒ RitsuLib 在写死的别名表 DieCueNames(die/death/dead/Dead) 里找 cue，
                        //   而这里原先只有静态 `dead` ⇒ 只会播静态尸体图（用户报的"回退到了静态图"）✗
                        // 补一条 `die` 帧序列（RitsuLib 先查帧序列、后查单图 ⇒ 会抢在 dead 前面命中）。
                        // ⚠️ 帧序列**必须给样式**：静态尸体图是整张 512 画布，而死亡帧序列的人物只有约 334 高，
                        //    不补 scale 会让"开始倒下"那一帧突然缩水（换算见 MerchantDeathStyle）。
                        if (GaoshouVisualSettings.UseFrameSequences)
                        {
                            cues.Sequence("die", sequence =>
                            {
                                AddFrames(sequence, GaoshouVisualSettings.DieFromIdleSequence);
                                sequence.DefaultStyle(GaoshouVisualSettings.MerchantDeathStyle);
                            });
                        }
                    })
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
    ///                  Hit(从 idle 触发)
    ///   idle(站姿) ─────────────────▶ hurt_enter(站姿→护姿)
    ///      ▲                              │
    ///      │                              ▼
    ///      │                        hurt_impact(护架→后倾峰值)
    ///      │                              │            ▲
    ///      │                              ▼            │ Hit(回正途中挨打 ⇒ 退回后倾)
    ///      │                        hurt_recover(峰值→护架,慢)
    ///      │                              │
    ///      │                              ▼
    ///      │                     hurt_guard(静态护架,等 HurtHoldSeconds)
    ///      │                              │ HurtExit(计时器)
    ///      └──── hurt_exit(护姿→站姿) ◀───┘
    /// </code>
    /// ═══【2026-09-27 受击主体拆成 impact / recover / guard 三段】═══
    /// 旧写法 `hurt_body --WithNext--> hurt_exit`：**每挨一次打就走完整个"举起护架→后仰→回正→放下护架"**。
    /// 用户实测「因为完全回正、高频，看起来很不自然」—— 挨打密集时护架被反复举起放下、身体每次弹回中立位 ✗。
    ///
    /// 改成（**与 AoE 的 `cut_* → hold_*` 完全同构**）：
    ///   * `hurt_impact` 每次挨打都从它起播（首帧 = 护架）；
    ///   * **回正途中再挨打 ⇒ 切回 `hurt_impact`** —— 出程首帧就是护架，观感上等于"从回正半途倒放回后倾状态"，
    ///     然后回正再走一遍 ⇒ 挨打密集时身体停在后倾附近，**不再每次都回到中立位** ✓；
    ///   * 只有回正**完整走完**才进 `hurt_guard`（静态护架）并起 `HurtHoldSeconds` 计时器；
    ///     计时器期间再挨打 ⇒ 又回 `hurt_impact`（重进 `hurt_guard` 时会有新计时器，旧的按代际号丢弃）✓。
    ///
    /// **连击的关键**：<c>Hit</c> 先走 any-state 分支，它的谓词是"当前不在 idle"——
    /// 于是在三个 hurt 状态期间再挨打都会走 any-state（回 `hurt_impact`），而不会退回
    /// `hurt_enter` 的"从站姿抬手"；只有在 idle 时才走 idle 自己的分支去播 `hurt_enter`。
    /// RitsuLib 的 <c>SetTrigger</c> 正是"先 any-state、再当前状态"，且谓词为 false 时会继续匹配
    /// 当前状态的分支（ModAnimState.CallTrigger）。
    /// </summary>
    /// <summary>
    /// 战斗视觉的 cue 表（按设置页「角色形象」决定给多少）：
    /// <list type="bullet">
    ///   <item><b>静态图模式</b>（<see cref="GaoshouVisualSettings.UseFrameSequences" /> = false）：只给
    ///     idle / dead 两张静态图 —— 受击、出拳、横扫都不播帧动画，状态机也相应只建"站姿 + 死亡"两个状态；</item>
    ///   <item><b>帧序列模式</b>：idle 常驻；dead 播完定格（RitsuLib 的 dead 状态既不循环也不回 idle）；
    ///     受击四段（hurt_enter 站姿→护姿 / hurt_impact 护架→后倾峰值 / hurt_recover 峰值→护架 /
    ///     hurt_guard 静态护架等计时器 / hurt_exit 护姿→站姿）；
    ///     死亡两套（die_idle 站姿起手 / die_hurt 受击途中被打死，先垫一帧受击瞬间）；
    ///     攻击 atk_ready→atk_punch_r/_l 左右交替→atk_guard 停在架势→atk_retract_* 收拳；
    ///     AoE 横扫 atk_aoe_*（起手 / 两刀 / 落点停留 / 收刀）；
    ///     连击（anim_set = new 且素材可用）atk_loop_entry（入场一次性，第一记右拳提前）→ atk_loop（稳态循环）。
    ///     双持冲锋枪（anim_set = new 且开火素材可用）smg_u / smg_l：每次命中播一次、上枪/下枪交替，
    ///     播完落到**持枪待机 smg_hold**（整串攻击期间一直保持，只在"最近一次命中之后过了
    ///     GuardHoldSeconds"时才走出口）；
    ///     掏枪 / 收枪 smg_draw / smg_exit（**同一批 4 帧、顺序相反**，各 0.132s）：一次出牌的
    ///     第一段命中且当前在 idle 时先播 smg_draw → smg_hold；出口 = smg_hold --GuardEnd-->
    ///     smg_exit → idle（没有这套素材时才退回 atk_retract_* → idle）
    ///     （只对 DualSMG 这一张牌生效，判定见 <see cref="GaoshouAttackStyle.IsDualSmg" />）。
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

        // ═══【「放弃游戏」的死亡动画：必须在 `die` 这个名字下也挂一条死亡帧序列】═══
        // 战斗之外的死亡（放弃 / 事件房 / 地图上）**根本不经过我们的战斗状态机**：
        //   1) 放弃走 RunManager.Abandon() → CreatureCmd.Kill(force:true)，而那里是
        //      `NCombatRoom.Instance?.GetCreatureNode(...)` ⇒ 房间外拿不到节点 ⇒ **不调用 StartDeathAnim**
        //      ⇒ RitsuLib 的 NCreatureNonSpineDeathAnimationTriggerPatch（挂在 StartDeathAnim 的 Postfix）
        //      永远不会替我们补发 `Dead`（模组旧注释说它会补发，那只对"战斗内死亡"成立 ✗）；
        //   2) 之后游戏结束界面走 RitsuLib 的 CharacterGameOverScreenCompatibilityPatch，
        //      营火/事件/地图分支是 `TryPlayCue(visuals, character, "die")`。
        // 而 RitsuLib 的死亡 cue 别名表**写死**为 ["die", "death", "dead", "Dead"]
        // （ModCreatureVisualPlayback.DieCueNames），且**先查帧序列、再查静态贴图**。
        // 我们原先只把帧序列挂在 `die_idle`/`die_hurt`（那是状态机内部用的名字）、静态图挂在 `dead`
        // ⇒ 别名一个都匹配不上，只剩静态 `dead` 能命中 ⇒ 用户看到的"回退到了静态图" ✓
        //
        // 修法：再挂一条**同名 `die` 的死亡帧序列**，靠"帧序列优先于单图"把它抢在前面。
        // 不设 DefaultStyle ⇒ 沿用本场景 Sprite2D 自带的变换，与战斗里的帧动画完全同构。
        cues = cues.Sequence("die",
            sequence => AddFrames(sequence, GaoshouVisualSettings.DieFromIdleSequence));

        // 「连续攻击」cue（**两条：入场 atk_loop_entry + 稳态 atk_loop**）：**只有 anim_set = new
        // 且循环素材确实可用才挂**（legacy 是默认，行为必须与现在完全一致 ✓）。
        // 没有这两条 cue 时，下面的两个状态也不声明/不建分支 ⇒ 状态图退回原来那张，接不上任何循环素材。
        // ⚠️ 门控用 GaoshouVisualSettings.UseAttackLoop，与下面注册分支时**同一个判定** ——
        //   两处必须严格一致：若分支注册了而 cue 没注册，EnterState 会因为 HasAnimation=false
        //   直接 return（ModAnimStateMachine.cs:167），画面停在上一帧、且永远出不来 ✗。
        //   ⚠️ 入场与稳态是**同一个判定的两个消费点**，绝不允许只挂其中一条 ✗。
        // ⚠️ wait 循环素材已于 2026-09-27 **连同 50 张帧一起删除**（死素材，从未接入）。
        //    理由见 GaoshouVisualSettings.BuildAttackLoopSequence 的注释。
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

        // ── 「双持冲锋枪」上枪 / 下枪开火 + 持枪待机（**只有 anim_set = new 且素材可用才挂**）──
        // 开火是**一次性 2 帧序列**（Loop(false)，由 AddFrames 负责）⇒ 播完走 WithNext 到
        // **持枪待机 smg_hold**（见下面那段；hold 素材不可用时退回 atk_guard），
        // 与「视频版出拳」那条路径**完全同构**（每段命中播一次、由计时器收尾）。
        // ⚠️ DefaultStyle 是**必需的**（640 宽画布、Sprite2D 按贴图中心对齐，且样式播完不会自动还原）：
        //    它把节点钉回战斗场景的规范变换（position=(0,−160) / scale=0.8 / centered=true），
        //    完整实测与"为什么不能用 Offset 微调"见 GaoshouVisualSettings.SmgShotStyle 的注释。
        // ⚠️ 门控必须与下面的**状态声明 + 分支注册**共用同一个判定（UseSmgShots / UseSmgHold）：
        //    注册了分支而 cue 没注册 ⇒ EnterState 因 HasAnimation=false 直接 return，
        //    画面停在上一帧且永远出不来（ModAnimStateMachine.cs:167）✗。
        // legacy / 素材缺失时这几条一条都不挂 ⇒ 状态图与改动前逐条一致 ✓。
        if (GaoshouVisualSettings.UseSmgShots)
        {
            cues = cues
                .Sequence(GaoshouVisualSettings.SmgUpperCue, sequence =>
                {
                    AddFrames(sequence, GaoshouVisualSettings.SmgUpperSequence);
                    sequence.DefaultStyle(GaoshouVisualSettings.SmgShotStyle);
                })
                .Sequence(GaoshouVisualSettings.SmgLowerCue, sequence =>
                {
                    AddFrames(sequence, GaoshouVisualSettings.SmgLowerSequence);
                    sequence.DefaultStyle(GaoshouVisualSettings.SmgShotStyle);
                });

            // ── 持枪待机（smg_hold）：**静态单帧** cue ──
            // 与 atk_guard / hurt_guard / atk_aoe_hold_* 完全同构 —— "不会播完"的静态贴图：
            //   * 用**不带时长的 Single 重载** ⇒ 走 TexturePathByCue 那条路：设一次 Texture，
            //     对非循环静态 cue 只在下一 idle 帧发一次 Completed（DeferCompletion），
            //     而本状态**不设 WithNext** ⇒ 状态机不推进、贴图一直停在这一帧 ✓。
            //     ⚠️ 不要改成"带时长的 Single"（那会走帧序列播放器，机制就与 atk_guard 不是同一条路了）。
            //   * 样式同样写 SmgShotStyle：hold 的贴图与开火帧同画布，从开火序列落下来时节点本来
            //     就是规范变换；但"从别处直接进 smg_hold"这条时序必须同样被钉住 ✓。
            // ⚠️ 门控用 UseSmgHold（= UseSmgShots 且 hold 素材可用），与下面的状态声明/分支注册
            //    严格同生共死 ✓。
            if (GaoshouVisualSettings.UseSmgHold)
            {
                cues = cues.Single(GaoshouVisualSettings.SmgHoldCue,
                    GaoshouVisualSettings.SmgHoldTexturePath,
                    GaoshouVisualSettings.SmgShotStyle);
            }

            // ── 掏枪 / 收枪（smg_draw / smg_exit）：两条**一次性**序列 ──
            // 4 帧 × 33ms = 0.132s（= 出拳起手预算，见 SmgDrawFrameSeconds）；
            // **同一批 4 帧、顺序相反**（收枪 = 掏枪倒放）⇒ 两端对齐、零额外素材 ✓。
            // 落点/起点都是 smg_hold（D_04 = hold 姿态）⇒ 切进切出都零跳变 ✓。
            // ⚠️ 门控用 UseSmgDraw（= UseSmgHold 且这两条序列的素材可用），与下面的状态声明/分支注册
            //    严格同生共死；为 false 时一条都不挂，行为精确退回"加掏枪之前" ✓。
            if (GaoshouVisualSettings.UseSmgDraw)
            {
                cues = cues
                    .Sequence(GaoshouVisualSettings.SmgDrawCue, sequence =>
                    {
                        AddFrames(sequence, GaoshouVisualSettings.SmgDrawSequence);
                        sequence.DefaultStyle(GaoshouVisualSettings.SmgShotStyle);
                    })
                    .Sequence(GaoshouVisualSettings.SmgExitCue, sequence =>
                    {
                        AddFrames(sequence, GaoshouVisualSettings.SmgExitSequence);
                        sequence.DefaultStyle(GaoshouVisualSettings.SmgShotStyle);
                    });
            }
        }

        return cues
            // 攻击后的"停在架势"：静态贴图 cue（不会播完 ⇒ 一直保持到计时器发 GuardEnd）
            .Single("atk_guard", GaoshouVisualSettings.AttackGuardTexturePath)
            .Sequence("hurt_enter", sequence => AddFrames(sequence, GaoshouVisualSettings.HurtEnterSequence))
            // ⚠️ 受击主体**拆成三段**（2026-09-27，见 SetupCustomCombatAnimationStateMachine 里 hurt 的注释）：
            //   hurt_impact（护架→后倾峰值）/ hurt_recover（峰值→护架）/ hurt_guard（静态护架，等计时器）
            .Sequence("hurt_impact", sequence => AddFrames(sequence, GaoshouVisualSettings.HurtImpactSequence))
            .Sequence("hurt_recover", sequence => AddFrames(sequence, GaoshouVisualSettings.HurtRecoverSequence))
            .Single("hurt_guard", GaoshouVisualSettings.HurtGuardTexturePath)
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
        const string cueHurtImpact = "hurt_impact";
        const string cueHurtRecover = "hurt_recover";
        const string cueHurtGuard = "hurt_guard";
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

        // ── 「双持冲锋枪」（DualSMG）上/下枪交替开火状态（anim_set = new + 开火素材可用；
        //    legacy 或素材缺失时恒为 false，下面的状态/分支一条都不建 ⇒ 状态图逐条不变 ✓）──
        // ⚠️ 与 BuildCombatCues 里注册 smg_u / smg_l 两条 cue 用的是**同一个**
        //    GaoshouVisualSettings.UseSmgShots（两处不一致 ⇒ "注册了分支、没注册 cue"的死状态 ✗）。
        var useSmgShots = GaoshouVisualSettings.UseSmgShots;
        // 开火 / 持枪待机的 cue id 走类级别常量（与 BuildCombatCues 注册 cue 时**是同一批字符串** ✓）。
        const string cueSmgU = GaoshouVisualSettings.SmgUpperCue;
        const string cueSmgL = GaoshouVisualSettings.SmgLowerCue;
        const string cueSmgHold = GaoshouVisualSettings.SmgHoldCue;
        // 「持枪待机」是否启用（= useSmgShots 且 hold 素材可用）—— 与 BuildCombatCues 里注册那条 cue
        // 用的是**同一个** GaoshouVisualSettings.UseSmgHold ✓（三处同生共死，见该属性注释）。
        var useSmgHold = GaoshouVisualSettings.UseSmgHold;
        // 掏枪 / 收枪的 cue id 与门控（= useSmgHold 且这两条序列的素材可用；**第三个独立闸门**）。
        const string cueSmgDraw = GaoshouVisualSettings.SmgDrawCue;
        const string cueSmgExit = GaoshouVisualSettings.SmgExitCue;
        var useSmgDraw = GaoshouVisualSettings.UseSmgDraw;

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

        // ── 「受击保持护架」计时器的代际号（同 guardTimerGeneration 那套，**互不干扰**）──
        // 每次进入 hurt_guard 就 +1；到点时若代际落后 ⇒ 说明中途又挨过打、已重进过 hurt_guard
        // ⇒ 本计时器是僵尸，丢弃、不发 HurtExit ✓。
        var hurtGuardTimerGeneration = 0;

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
            .AddState(cueHurtEnter, false).WithNext(cueHurtImpact).Done()
            .AddState(cueHurtImpact, false).WithNext(cueHurtRecover).Done()
            .AddState(cueHurtRecover, false).WithNext(cueHurtGuard).Done()
            .AddState(cueHurtGuard, false).Done()                 // 静态 cue，不会播完 ⇒ 一直保持护架
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
            .AddAnyState("Hit", cueHurtImpact,                    // 受击过程中再挨打 → 退回后倾（出程首帧=护架）
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
            .AddBranch(cueHurtImpact, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueHurtRecover, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
            .AddBranch(cueHurtGuard, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe)
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
                .AddBranch(cueHurtImpact, "Attack", cueVideoPunchR, () =>
                    GaoshouAttackStyle.IsMultihit && !punchSideFlip)
                .AddBranch(cueHurtImpact, "Attack", cueVideoPunchL, () =>
                    GaoshouAttackStyle.IsMultihit && punchSideFlip)
                .AddBranch(cueHurtRecover, "Attack", cueVideoPunchR, () =>
                    GaoshouAttackStyle.IsMultihit && !punchSideFlip)
                .AddBranch(cueHurtRecover, "Attack", cueVideoPunchL, () =>
                    GaoshouAttackStyle.IsMultihit && punchSideFlip)
                .AddBranch(cueHurtGuard, "Attack", cueVideoPunchR, () =>
                    GaoshouAttackStyle.IsMultihit && !punchSideFlip)
                .AddBranch(cueHurtGuard, "Attack", cueVideoPunchL, () =>
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

        // ── 「双持冲锋枪」上枪 / 下枪交替开火（anim_set = new + 开火素材可用才声明）──
        //
        // 【结构与「视频版出拳」同构】一次性 2 帧序列 → `WithNext(smg_hold)` 停在**持枪待机** →
        //   收尾靠 `GuardEnd`（计时器）→ atk_retract_* 收拳 → idle。
        //   关键的差别只有"**中途停在哪**"：视频版出拳停在 `atk_guard`（**徒手**架势），
        //   而这一档必须停在 `smg_hold`（**持枪**待机）—— 否则打完之后画面是徒手姿势 ✗。
        //
        // 【为什么中途绝不能走 guard → retract → idle】(2026-09-28 实机两个症状的根因)
        //   上一版 `WithNext(atk_guard)` 把"两次命中之间的停留"也交给了收尾链：
        //     ① 停在 atk_guard ⇒ 看到的是**徒手架势**；
        //     ② 落进 guard 后若间隔稍长就走了 retract → idle ⇒ 下一发**再进一次攻击**
        //        ⇒ "普通目标每次攻击之间插入一段攻击等待 / 入场动画"。
        //   现在 smg_u/l 的落点是 smg_hold（静态、无 WithNext）⇒ 状态**停在攻击这条链上不动**，
        //   后续命中由 any-state 直接换成另一把枪的火光、播完再落回 smg_hold
        //   ⇒ 连续命中之间只是交替冒火光，**不回徒手、也没有任何入场/等待过渡** ✓。
        //
        // 【这一发是上枪还是下枪】**不由状态自己记账**，而是每次命中由外部发一个**专属触发器**
        //   （见下面 FireSmgShot 的订阅）：用户要求的语义就是"按命中事件交替"，
        //   而 DualSMG 的段间隔（≈0.13s，见 SmgShotTotalSeconds）与本序列等长 ⇒
        //   命中到来时状态**可能还在 smg_\***（走 any-state 直接换另一把枪），
        //   **也可能已经停在 smg_hold**（同样靠 any-state 拉回来）—— 两种时序都覆盖得到 ✓。
        //
        // 【为什么用 any-state】`ModAnimStateMachine.SetTrigger` 是"**先 any-state、再当前状态**"
        //   （ModAnimStateMachine.cs:142）⇒ 只要命中到来就一定切得进开火状态，不管此刻停在哪
        //   （idle / smg_hold / 另一个开火状态 / 受击 / 横扫链中途）✓。
        //   触发器名是**专属**的（SmgShotUpper / SmgShotLower）⇒ 不存在像 "Attack" 那样
        //   被出拳 / 横扫 / 连击分支按注册顺序抢走的风险（那个坑见 AoeNextCutTrigger）✓。
        //
        // ⚠️【"Attack" 分支：smg_u / smg_l **故意不注册**，smg_hold **必须注册**】两件事别搞混：
        //   * `smg_u` / `smg_l`：**不注册**。这张牌自己的后续段走的是**专属触发器**（any-state 优先级更高），
        //     而引擎在**同一个调用栈**里紧接着派发的 `"Attack"`（`CreatureCmd.cs:995`；我们的事件是它的
        //     Prefix）到达时，状态已经切进 smg_u / smg_l 了 ⇒ 这里没有分支 ⇒ `SetTrigger` 直接 return、
        //     状态与 cue 都不动 ✓（注册了反而会把刚起播的火光序列从头重播 ✗ —— 与 atk_loop 故意
        //     不注册自指边是同一个道理）。
        //     顺带一个好处：`DualSMG` 的命中**绝不会**掉进"多段 → atk_loop_prehold"那条分支
        //     （它注册在无条件的拳分支之前）✓。
        //   * `smg_hold`：**必须注册**（见下面那组"接走别的牌"的边）。它不是 0.13s 就走的过路状态，
        //     而是会**一直停到 GuardHoldSeconds 到点**（默认 1.0s / 本机 0.7s）⇒ 玩家完全来得及
        //     在这段时间里再打一张攻击牌；不注册就会把那张牌的攻击**整个吞掉**（人物还举着枪）✗。
        //   * ⚠️ 旧注释曾写"轮到玩家再出下一张牌时状态早已是 idle" —— 那是**加 hold 之前**的时序，
        //     现在**不成立**（hold 会停留整个 GuardHoldSeconds），照着它做就会漏掉上面那条边 ✗。
        //
        // ⚠️ legacy / 开火素材缺失时 useSmgShots = false ⇒ AddState / AddAnyState **一条都不调用**，
        //    `smg_u` / `smg_l`（以及 hold 那条）这些 id 在整个图里不存在
        //    （AddBranch 对未声明的源状态会直接抛，所以它们的分支也绝不能写到无条件区）✓。
        // ⚠️ hold 那条边由**第二个门控** UseSmgHold 决定（= useSmgShots 且 hold 素材可用）：
        //    它只影响 `smg_u/l` 的 `WithNext` **指向谁**，不影响"要不要声明 smg_u/l"
        //    ⇒ hold 素材缺失时这里就是**上一版的样子**（WithNext(cueGuard)），不会造出
        //      "指向不存在 cue 的边"（那会让 EnterState 直接 return、卡在火光第 2 帧 ✗）✓。
        if (useSmgShots)
        {
            // 有 hold ⇒ 中途停在持枪待机；没有 ⇒ 退回上一版的 atk_guard（徒手架势）。
            var smgRestCue = useSmgHold ? cueSmgHold : cueGuard;

            builder
                .AddState(cueSmgU, false).WithNext(smgRestCue).Done()
                .AddState(cueSmgL, false).WithNext(smgRestCue).Done()
                .AddAnyState(GaoshouVisualSettings.SmgUpperTrigger, cueSmgU)
                .AddAnyState(GaoshouVisualSettings.SmgLowerTrigger, cueSmgL);

            if (useSmgHold)
            {
                // 静态 cue、**不设 WithNext** ⇒ 不会播完推进，一直停在持枪待机 ✓
                //（与 atk_guard / hurt_guard / atk_aoe_hold_* 的声明方式逐字同构）。
                builder.AddState(cueSmgHold, false).Done();
            }

            if (useSmgDraw)
            {
                // 掏枪：一次性 4 帧 → `WithNext(smg_hold)`（末帧 D_04 就是 hold 姿态 ⇒ 落点零跳变）✓
                // 收枪：一次性 4 帧（掏枪倒放）→ `WithNext(idle)`（首帧 D_04 = hold 姿态 ⇒ 起点零跳变）✓
                builder
                    .AddState(cueSmgDraw, false).WithNext(cueSmgHold).Done()
                    .AddState(cueSmgExit, false).WithNext(cueIdle).Done()
                    // 入口**只注册在 idle 上**（分支的源状态 = idle ⇒ 只有"当前停在 idle"时才吃这个触发器）：
                    //   * 4 帧掏枪的**首帧就是徒手站姿**（D_01）⇒ 只有从 idle 切进去才不跳变 ✗→✓；
                    //   * 从 smg_hold 起（例如连打两张 DualSMG、枪已在手上）**不该**再掏一次枪，
                    //     那条时序走的是 any-state 的开火触发器 ⇒ 直接开火 ✓（见 FireSmgShot 的判据）。
                    .AddBranch(cueIdle, GaoshouVisualSettings.SmgDrawTrigger, cueSmgDraw);
            }
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
            // 护架保持结束（计时器）→ 放下护架回站姿。语义与上面正好同构（见 hurt 状态图注释）
            .AddBranch(cueHurtGuard, GaoshouVisualSettings.HurtExitTrigger, cueHurtExit)
            // 受击途中出拳（少见，例如反伤牌）：同样按"上一拳"选边，别让攻击动画被吞掉
            .AddBranch(cueHurtEnter, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueHurtEnter, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueHurtImpact, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueHurtImpact, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueHurtRecover, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueHurtRecover, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueHurtGuard, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueHurtGuard, "Attack", cuePunchL, () => lastWasRight)
            .AddBranch(cueHurtExit, "Attack", cuePunchR, () => !lastWasRight)
            .AddBranch(cueHurtExit, "Attack", cuePunchL, () => lastWasRight)
            // 死亡：先匹配"当前在待机"（走站姿起手的死亡），否则（受击/出拳途中被打死）走受击版。
            // 游戏侧顺序已查证：伤害结算派发 Hit 后**不等**受击动画播完就 Kill ⇒ 受击版是常见路径。
            .AddAnyState("Dead", cueDieIdle, () => machine?.Current?.Id == cueIdle)
            .AddAnyState("Dead", cueDieHurt, () => machine?.Current?.Id != cueIdle)
            .AddAnyState("Idle", cueIdle)
            .AddAnyState("Cast", cueIdle)
            .AddAnyState("Relaxed", cueIdle);

        // ── 小工具：给"停在**持枪系**状态上、又不该吞掉别人攻击"的那些状态注册三条同构兜底边 ──
        //
        // 用在哪里：`smg_hold`（会一直停到 GuardHoldSeconds 到点，默认 1.0s / 本机用户 0.7s）与
        //   `smg_exit`（收枪 0.132s）。玩家完全来得及在这些窗口里再打一张攻击牌；若那些状态上没有
        //   `"Attack"` 分支，引擎派发的 `"Attack"` 在 any-state 与当前状态上都匹配不到 ⇒
        //   `SetTrigger` 直接 return ⇒ **那张牌完全没有攻击动画**（人物还举着枪 / 还在收枪），
        //   比"多一段过渡"严重得多 ✗。
        // ⚠️ **不要**给 `smg_u` / `smg_l` / `smg_draw` 用这个函数：它们都是**由"这一段命中"本身**
        //   触发的，而引擎紧接着会在同一个调用栈里派发 `"Attack"`（见 `FireSmgShot` 那段的推演）
        //   ⇒ 在那三个状态上注册 `"Attack"` 会把刚起播的开火 / 掏枪**劫持成出拳** ✗。
        //
        // 三条与 `atk_guard` 上那组**逐条同构**（横扫 → 视频版拳 → 原版拳），**顺序也必须一致**：
        //   RitsuLib 取"注册顺序里第一个谓词通过的"（ModAnimState.cs:117-123）⇒
        //   横扫必须排在拳之前、视频版必须排在原版之前，否则后者永远轮不到 ✗。
        // ⚠️ 抽成函数是为了"几处必须永远一起改"（漏一处就会出现上面那个吞攻击的洞）。
        void AddOtherCardAttackBranches(string fromCue)
        {
            builder.AddBranch(fromCue, "Attack", cueAoeDraw, () => GaoshouAttackStyle.IsAoe);
            if (useVideoPunch)
            {
                builder
                    .AddBranch(fromCue, "Attack", cueVideoPunchR,
                        () => !GaoshouAttackStyle.IsAoe && !punchSideFlip)
                    .AddBranch(fromCue, "Attack", cueVideoPunchL,
                        () => !GaoshouAttackStyle.IsAoe && punchSideFlip);
            }

            builder
                .AddBranch(fromCue, "Attack", cuePunchR,
                    () => !GaoshouAttackStyle.IsAoe && !lastWasRight)
                .AddBranch(fromCue, "Attack", cuePunchL,
                    () => !GaoshouAttackStyle.IsAoe && lastWasRight);
        }

        // ── 「持枪待机」的**唯一出口**：GuardEnd（计时器）→ 收枪 / 收拳 → idle ──
        //
        // 写在这里（而不是上面那段连续的 builder 链里）是**必须的**：
        //   `AddBranch` 对**未声明的源状态**会直接抛 `InvalidOperationException`
        //   （ModAnimStateMachineBuilder.cs:77-78）⇒ `cueSmgHold` 在 legacy / 素材缺失时根本不存在，
        //   那几条边会让**整个状态机构建失败** ✗（与 useAttackLoop / useVideoPunch 那两组同一个纪律）。
        //
        // 【出口分两档】
        //   * 有掏枪 / 收枪素材（`useSmgDraw`）⇒ `smg_hold --GuardEnd--> smg_exit --WithNext--> idle`：
        //     收枪是掏枪的倒放、首帧就是 hold 姿态 ⇒ 从持枪待机切进去零跳变，打完把枪收回去 ✓；
        //   * 没有那套素材 ⇒ **精确退回**上一层的 `→ atk_retract_r/_l`（收拳链），
        //     与"加掏枪之前"逐条一致 ✓。
        //
        // 【方向（只在退回收拳链时用得上）】复用 `lastWasRight` 选 atk_retract_r / _l —— 与 atk_guard
        //   的收拳**逐字同构**：smg 链一路不改 lastWasRight（它只被原版拳 / 视频版拳的 AnimationStarted 写），
        //   所以这里选出来的是"进入 smg 之前最后那一拳是哪边"，只影响收拳用哪只手，无副作用 ✓。
        //   （反过来，**不能**在 smg 的 AnimationStarted 里改 lastWasRight：
        //    那会把"上一拳"污染掉，下一张徒手牌的第一拳方向就错了 ✗。）
        //
        // 【为什么停在这里不会被"连续命中"打断】见 AnimationStarted 里 smg 分支的注释：
        //   每次命中都会重置这个计时器的代际号（作废旧表 + 重起新表）⇒ 只要还有下一段命中，
        //   计时器就永远到不了点；只有"最近一次命中之后安静了 GuardHoldSeconds"才真的收枪/收拳 ✓。
        if (useSmgHold)
        {
            if (useSmgDraw)
            {
                builder.AddBranch(cueSmgHold, GaoshouVisualSettings.GuardEndTrigger, cueSmgExit);
            }
            else
            {
                builder
                    .AddBranch(cueSmgHold, GaoshouVisualSettings.GuardEndTrigger, cueRetractR,
                        () => lastWasRight)
                    .AddBranch(cueSmgHold, GaoshouVisualSettings.GuardEndTrigger, cueRetractL,
                        () => !lastWasRight);
            }

            AddOtherCardAttackBranches(cueSmgHold);
        }

        if (useSmgDraw)
        {
            // 收枪途中（0.132s 窗口）来了别的牌的攻击 ⇒ 同样别吞掉。
            AddOtherCardAttackBranches(cueSmgExit);

            // ⚠️【`smg_draw` **故意不注册** "Attack" 分支 —— 与 `smg_u` / `smg_l` 同一个理由】
            //   掏枪是**由"这一段的命中"本身触发的**：我们的专属触发器跑在 `CreatureCmd.TriggerAnim`
            //   的 Prefix（`CreatureCmd.cs:964-995`），而引擎紧接着在**同一个调用栈**里派发 `"Attack"`
            //   ⇒ 那一刻当前状态已经是 `smg_draw`。若这里注册了 `"Attack"` 分支，那条边会立刻把掏枪
            //   **劫持成一次出拳** ✗（4 帧掏枪等于白播，而且每一串连击的第一发都会这样）。
            //   代价：掏枪这 0.132s 内**嵌套**出来的攻击（反伤 / 亡语 / 追击）会没有动画 —— 窗口极短，
            //   且这张牌自己后续段的命中走的是专属触发器（any-state 优先级更高，照样切得进开火）✓。
            //   ⇒ 这是刻意取舍，**别"顺手补上"**（补上必然打断掏枪）✗。
        }

        machine = builder.BuildForVisualsRoot(visualsRoot, character);

        // 进入某一拳状态时记录该边（用事件而不是在谓词里写副作用：谓词可能被多次求值）；
        // 进入架势时起一个一次性计时器，到点发 GuardEnd ⇒ 只有"这一串攻击确实结束了"才收拳。
        // AoE 同理：每次停在刀落点上起一个更短的计时器，到点发 AoeEnd ⇒ 单段 AoE 直接收刀，
        // 多段则在下一次 Attack 到来时被接走（离开停留态后那个计时器到点自然落空，无害）。
        var tree = visualsRoot.GetTree();

        // ── 「收拳计时器」的统一装配（架势 atk_guard 与持枪待机 smg_hold **共用**）──
        //
        // 【为什么要抽成本地函数】smg_hold 与 atk_guard 的退出条件是**逐字相同**的：
        //   "进入时起一个 GuardHoldSeconds 的一次性计时器、到点发 GuardEnd、代际号作废僵尸"。
        //   抄一份不难，但**两份必须永远一起改**（改时长 / 改触发器 / 改代际规则时漏一处就会出现
        //   "一个走新逻辑、一个走旧逻辑"的暗坑）⇒ 抽成一个函数从语法上杜绝 ✓。
        //
        // ⚠️ 声明在**方法体这一层**（不在任何 if 里）：除了 `AnimationStarted` 的两个分支
        //    （架势 / smg 三态），下面 `FireSmgShot` 里"停在 smg_hold 收到新命中"时也要用它重置
        //    ⇒ 作用域必须覆盖到那里 ✓。
        // ⚠️ 代际号在**调用时**就 +1（而不是到点才判断）：这样"后一次进入/命中"会立刻让前一个
        //    计时器到点即被丢弃 ✓（等价于取消旧计时器，Godot 的 SceneTreeTimer 没有取消接口）。
        void ArmGuardEndTimer()
        {
            // `tree == null`（视觉节点还没进场景树）时无处起表 ⇒ 直接放弃。
            // 这种时序下收尾会退回"没有任何计时器"的旧行为（与 `tree == null` 时原版
            // `AnimationStarted` 里那条 `&& tree != null` 守卫完全一致）✓。
            if (tree is not { } t)
                return;

            var myGeneration = ++guardTimerGeneration;
            var guardStart = t.CreateTimer(GaoshouVisualSettings.GuardHoldSeconds);
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
                    return;
                }

                m.SetTrigger(GaoshouVisualSettings.GuardEndTrigger);
            };
        }

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
            }
            else if (string.Equals(state.Id, cueVideoPunchL, StringComparison.Ordinal))
            {
                lastWasRight = false;    // 刚才出的是左拳
                punchSideFlip = false;   // 下一拳出右
                guardTimerGeneration++;  // 作废上一拳留下的收拳计时器（见代际号声明处）
            }
            else if (string.Equals(state.Id, cueGuard, StringComparison.Ordinal))
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
                //
                // ⚠️ 具体装配已抽到 `ArmGuardEndTimer`（smg 链与它**共用同一个函数**）✓。
                // ⚠️ 原写法还带了 `&& tree != null` 守卫；现在由 `ArmGuardEndTimer` 内部
                //    自己判 `tree`（等价，且不会漏掉 smg 那条新路径）✓。
                ArmGuardEndTimer();
            }
            // ── 「双持冲锋枪」持枪待机 / 开火：收尾与 atk_guard **共用同一个计时器装配** ──
            //
            // 【进入开火状态 / 持枪待机时都要重新起表！】这是"连续命中不会中途退出"的关键：
            //   开火序列只有 0.13s，若只在"进入 smg_hold"时起表，那么**在 smg_u/l 播放途中**
            //   到达的下一段命中（这正是 DualSMG 的常态，段间隔≈0.13s）就**不会**重置计时器
            //   ⇒ 从"进入 smg_u 那一刻"起算的旧表会在连射中途到点，把人物从火光里拽去收拳 ✗
            //   （与 `atk_guard` 只在进入时起表、必须靠外部事件重置是**同一个坑**）。
            //   现在"每次进入 smg_u / smg_l / smg_hold 都重起"⇒ 每一段命中都天然把到点时刻推后，
            //   语义正好是"**最近一次命中**之后过了 GuardHoldSeconds 才收枪/收拳" ✓。
            // ⚠️ `FireSmgShot` 里还额外重置一次（见那里的注释）：从 smg_hold 被 any-state 拉去换枪
            //   本来就是一次"进入"、这里已经覆盖；那一处是**幂等双保险**，并覆盖"状态进了、但
            //   后端的 Started 没发出来 ⇒ 这里整个没跑"的时序 ✓。
            else if (useSmgHold &&
                     (string.Equals(state.Id, cueSmgU, StringComparison.Ordinal) ||
                      string.Equals(state.Id, cueSmgL, StringComparison.Ordinal) ||
                      string.Equals(state.Id, cueSmgHold, StringComparison.Ordinal) ||
                      // 掏枪也一起起表（收枪 smg_exit 不需要：它 `WithNext(idle)` 自己会走完）：
                      // 覆盖"这一串只有一段、掏完枪就没有下文"的兜底；正常时序下它随即被
                      // 进入 smg_hold 那一下的代际号作废，等于没起过 ✓。
                      (useSmgDraw && string.Equals(state.Id, cueSmgDraw, StringComparison.Ordinal))))
            {
                ArmGuardEndTimer();
            }
            else if (string.Equals(state.Id, cueHurtGuard, StringComparison.Ordinal) && tree != null)
            {
                // 【2026-09-27 新增：受击后的「保持护架」计时器】
                // 语义与上面的架势计时器**完全同构**，只是换了一个代际号与一个触发词：
                //   * 进入 hurt_guard（= 受击回正**完整走完**）时起表；
                //   * 期间再挨打 ⇒ any-state Hit 把状态切到 hurt_impact（不再进 hurt_guard），
                //     回正走完时会**再次**进入本状态 ⇒ 代际 +1、旧计时器到点即被丢弃 ✓；
                //   * 到点时当前仍是 hurt_guard ⇒ 发 HurtExit ⇒ hurt_exit 放下护架回站姿 ✓。
                var myGeneration = ++hurtGuardTimerGeneration;
                var hold = tree.CreateTimer(GaoshouVisualSettings.HurtHoldSeconds);
                hold.Timeout += () =>
                {
                    if (disposed || machine is not { } m)
                        return;

                    if (myGeneration != hurtGuardTimerGeneration)
                    {
                        return;
                    }

                    m.SetTrigger(GaoshouVisualSettings.HurtExitTrigger);
                };
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
                    mm.SetTrigger(GaoshouVisualSettings.AoeNextCutTrigger);
                }
                else if (tree != null)
                {
                    // 不欠账 ⇒ 停在落点上等一会儿，到点收刀回站姿 ✓。
                    // ⚠️【2026-09-27】必须用 **ScaledAoeHoldSeconds**（已按游戏加速模式折算），
                    //    不能用 AoeHoldSeconds —— SceneTreeTimer 只受 Engine.time_scale 影响，
                    //    而游戏的加速模式**不是 time_scale**（见 GaoshouVisualSettings.GameFastModeScale）✗。
                    var timer = tree.CreateTimer(GaoshouVisualSettings.ScaledAoeHoldSeconds);
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
                }
            }

            GaoshouAttackStyle.AttackHitTriggered += NoteAoeHit;
            visualsRoot.TreeExiting += () => GaoshouAttackStyle.AttackHitTriggered -= NoteAoeHit;
        }

        // ── 「双持冲锋枪」：**每命中一段就开一枪**，上枪 / 下枪交替 ──
        //
        // 【接线方式】与上面 AoE 的 NoteAoeHit **完全同构**（同一个事件、同样在 TreeExiting 里解绑），
        //   但这里**不记账、直接驱动状态机**：每次命中取一次序号
        //   （<see cref="GaoshouAttackStyle.NextSmgShotIsUpper" />，奇偶交替），
        //   再 `SetTrigger` 对应的**专属触发器**（SmgShotUpper / SmgShotLower）——
        //   这两个触发器各有 any-state 分支（见上面的状态声明），所以无论此刻停在哪都能切进去 ✓。
        //
        // 【序号在哪、为什么不归零】序号是 GaoshouAttackStyle 里的静态 `_smgShotIndex`：
        //   * **按"一次出牌"（CardPlay 实例）归零**，所以 DualSMG 的**逐敌循环**与**风暴第二轮**
        //     （同一个 OnPlay 里再 PlayOnce 一次）都不会把它打回"上枪" ✓（用户明确要求）；
        //   * 只有玩家**再打一张** DualSMG 才会换 CardPlay ⇒ 那时才重新从"上枪"开始 ✓。
        //
        // ⚠️【只对「双持冲锋枪」生效】本事件是"玩家**任何**一张牌的 Attack 触发器都会发"，
        //    所以这里必须再用 <see cref="GaoshouAttackStyle.IsDualSmg" /> 过滤一次
        //    （那一档风格只按**卡牌类型**认 DualSMG，其它卡恒为 false）✓
        //    —— 忘了过滤的后果是"所有多段牌都会开枪"，而其它卡的状态机分支一条都没变、只是被这个触发器抢走 ✗。
        // ⚠️【命中事件是"不去重"的】见 GaoshouAttackStyle 的类注释：TriggerAnim 是 async、
        //    去重门曾把合法命中整段吞掉，现在故意不去重。重复派发的结果只是"交替多翻一格"
        //    （顺序整体错位一位），不会漏枪、也不会卡住 —— 这是本方案（按命中事件交替）的固有上限。
        // ⚠️ 必须解绑：状态机随每场战斗重建，旧闭包若一直挂在静态事件上会累积（泄漏 + 误触发）✗。
        if (useSmgShots && tree != null)
        {
            // 「掏枪」起播时刻（毫秒）。只服务下面那条**重复派发**护栏，见那里的说明。
            // 用**时间窗**而不是"状态是不是 smg_draw"来识别重复，是因为下一段**真实**命中到来时
            // （间隔 ≈0.13s；加速档 0.065s）状态**也可能还停在** smg_draw 里 ——
            // 那种命中必须照常开火，绝不能当成重复被吞掉 ✗。
            // 窗口取 25ms：重复派发是同一调用栈里的微秒级重入，而真实命中间隔最快也有 65ms（加速档）
            // ⇒ 两边都留了余量 ✓。
            const double smgDrawDuplicateGuardMs = 25;
            double smgDrawStartedMs = -1;

            void FireSmgShot()
            {
                // 节点已离树 ⇒ 不推进动画、也不碰 machine（见 disposed 闩锁声明处）。
                if (disposed || machine is not { } m)
                    return;

                // 只认「双持冲锋枪」这一次出牌（其它任何牌的命中一律不碰开火状态）✓。
                if (!GaoshouAttackStyle.IsDualSmg)
                    return;

                // ── 【2026-09-29 新增】一次出牌的**第一段命中**先掏枪（`smg_draw`），然后再开火 ──
                //
                // 判据有两半：
                //   ① `IsFirstSmgShotOfPlay`（= 这一次出牌还没开过火，读 `_smgShotIndex == 0`，纯读取）；
                //   ② **此刻正停在 `idle`** —— 4 帧掏枪的**首帧就是徒手站姿**（D_01），从别的姿势切进去
                //      会看到"啪地蹦回站姿再掏枪"的跳变 ✗；而 `idle` 正是 D_01 的姿态 ⇒ 零跳变 ✓。
                //      （这条也自动处理了"枪已经在手上"的时序：连打两张 DualSMG 时状态是 `smg_hold`
                //        而不是 idle ⇒ 不重播掏枪，直接开火 ✓ —— 用户明确要求"不要每次都重播 draw"。）
                //
                // ⚠️ 掏枪**不消耗序号**（这里刻意**不**调 `NextSmgShotIsUpper`）⇒ 第一发实弹仍然是"上枪"，
                //    交替序列与加掏枪之前**逐段一致** ✓。
                // ⚠️ 掏枪用的是**专属触发器** `SmgDraw`，而它的边只注册在 `cueIdle` 上
                //    （分支的源状态 = idle ⇒ 只有"当前确实停在 idle"时才会吃它）
                //    ⇒ 与开火那两条 any-state 分支**互不抢占**：从 idle 起就掏枪、从 smg_hold 起就开火 ✓。
                if (useSmgDraw && GaoshouAttackStyle.IsFirstSmgShotOfPlay &&
                    string.Equals(m.Current?.Id, cueIdle, StringComparison.Ordinal))
                {
                    smgDrawStartedMs = Time.GetTicksMsec();
                    m.SetTrigger(GaoshouVisualSettings.SmgDrawTrigger);
                    return;
                }

                // ── 【重复派发护栏】同一次命中被本事件通知两遍时，第二遍会走到这里 ──
                // 它看到的状态已经是 `smg_draw`（掏枪不消耗序号 ⇒ `IsFirstSmgShotOfPlay` 仍为真，
                // 但 Current 不再是 idle），于是会**开一枪把刚起播的掏枪立刻切断** ✗
                // （掏枪那条 cue 没有 "Attack" 分支，但开火触发器是 **any-state**，照样能切走）。
                // 这里把"掏枪刚起播的一瞬间"的重复通知丢掉即可（幂等、上限 25ms，见声明处）✓。
                // 真实命中（≥65ms 之后）不会被这条挡住，照常开火 ✓。
                if (useSmgDraw && smgDrawStartedMs >= 0 &&
                    string.Equals(m.Current?.Id, cueSmgDraw, StringComparison.Ordinal) &&
                    Time.GetTicksMsec() - smgDrawStartedMs < smgDrawDuplicateGuardMs)
                {
                    return;
                }

                // 取一个序号：上、下、上、下……（序号本身跨敌人 / 跨风暴第二轮连续递增）。
                var upper = GaoshouAttackStyle.NextSmgShotIsUpper();
                m.SetTrigger(upper
                    ? GaoshouVisualSettings.SmgUpperTrigger
                    : GaoshouVisualSettings.SmgLowerTrigger);

                // ── 【2026-09-28 新增】把"收拳计时器"按**每一段命中**再推后一次（只在 smg 链上）──
                //
                // 【为什么这里还要再重置一次】`AnimationStarted` 那边已经覆盖了"**进入** smg_u / smg_l /
                //   smg_hold"的情形 —— 包括"停在 smg_hold 时被 any-state 拉去换另一把枪"这一下
                //   （那也是一次**进入**，所以正常时序下每次命中都会顺延到点时刻）。
                //   这里再加一道**按命中**的重置，是为了把语义钉死成"**最近一次命中**之后过了
                //   GuardHoldSeconds 才收拳"，并覆盖"状态进了、但后端的 Started 没发出来
                //   （例如 cue 加载失败 / Play 提前 return）⇒ AnimationStarted 整个没跑"这种时序
                //   ⇒ 那时计时器也会被推后，而不是把连射从中间拽去收拳 ✗。
                //   （与 `atk_guard` 必须靠外部事件重置是同一个道理，只是那边**只能**靠事件。）
                //
                // 【幂等 ⇒ 重复回调无害】与 atk_loop 的 `ResetAttackLoopTimer` 完全同一个论证：
                //   本事件不去重（见 GaoshouAttackStyle 的类注释），但这里做的是
                //   "作废旧表 + 重起一张同长度的表" ⇒ 调用 1 次还是 N 次，结果都一样 ✓。
                //
                // ⚠️ 只在**确实停在 smg 链上**时才重置（与 `ResetAttackLoopTimer` 的 `Current` 判定同构）：
                //   别的状态（idle / atk_guard / 受击 / 横扫）自有收尾逻辑，乱重置只会把
                //   "该收的拳"推迟 GuardHoldSeconds ⇒ 无谓的观感延迟 ✗。
                //   ⚠️ 这里读的是**发触发器之后**的 `Current`：第一段命中是 any-state 从 idle
                //     切进 smg_u ⇒ 读到的就是 smg_u（属于链上，重置正确 ✓）；若这次命中没能切进去
                //     （理论上不会：any-state 必中），读到的还是原状态、不在链上 ⇒ 不重置，也不会误伤 ✓。
                var nowId = machine?.Current?.Id;
                if (useSmgHold &&
                    (string.Equals(nowId, cueSmgU, StringComparison.Ordinal) ||
                     string.Equals(nowId, cueSmgL, StringComparison.Ordinal) ||
                     string.Equals(nowId, cueSmgHold, StringComparison.Ordinal)))
                {
                    ArmGuardEndTimer();
                }
            }

            GaoshouAttackStyle.AttackHitTriggered += FireSmgShot;
            visualsRoot.TreeExiting += () => GaoshouAttackStyle.AttackHitTriggered -= FireSmgShot;
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
