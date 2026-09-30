using STS2RitsuLib.Scaffolding.Content;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using STS2RitsuLib.Scaffolding.Content.Patches;
using Gaoshou.Cards;
using Gaoshou.Characters;
using Gaoshou.Events;
using Gaoshou.Patches;
using Gaoshou.Powers;
using Gaoshou.Relics;
using Gaoshou.Tutorial;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;


namespace Gaoshou;

[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    // ModId 需要和 Gaoshou.json 里的 id 保持一致。
    // res://Gaoshou/... 里的 Gaoshou 是 PCK 资源目录，不是 C# namespace。
    public const string ModId = "Gaoshou";
    public const string ResPath = $"res://{ModId}";

    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    // 心流(DoubleDamagePower) 用的原版「双倍伤害」buff 图标：改用高手自定义图。
    // 尺寸必须对齐原版规格，否则会渲染异常：
    //   * 小图标走图集 atlases/power_atlas.sprites/<id>.png，原版一律 64×64（271 个 sprite 里 194 个是 64×64）；
    //     power.tscn 的 %Icon 是 TextureRect + expand_mode=1 + stretch_mode=4（保持原尺寸比例），
    //     喂 128×128 会画成两倍大小、溢出槽位；悬浮释义行同样被撑高，把容器尺寸算歪。
    //   * 大图标走 images/powers/<id>.png，原版是 256×256，且 NPower 里它是 CPUParticles2D（%PowerFlash）的贴图。
    private const string DoubleDamagePowerIconPath = $"{ResPath}/images/powers/doubledamage.png";
    private const string DoubleDamagePowerBigIconPath = $"{ResPath}/images/powers/doubledamage_big.png";

    private static Godot.Texture2D? _doubleDamagePowerIcon;
    private static Godot.Texture2D? _doubleDamagePowerBigIcon;

    private static Godot.Texture2D? DoubleDamagePowerIcon =>
        _doubleDamagePowerIcon ??= LoadIcon(DoubleDamagePowerIconPath);

    private static Godot.Texture2D? DoubleDamagePowerBigIcon =>
        _doubleDamagePowerBigIcon ??= LoadIcon(DoubleDamagePowerBigIconPath);

    // 载入失败时返回 null（RitsuLib 的取用逻辑会跳过 null 值，自动回落到原版图标），并在日志里留痕。
    private static Godot.Texture2D? LoadIcon(string path)
    {
        var texture = Godot.ResourceLoader.Load<Godot.Texture2D>(path, null, Godot.ResourceLoader.CacheMode.Reuse);
        if (texture == null)
            Logger.Warn($"[Icon] failed to load '{path}'; the vanilla icon will be used instead.");
        else
            Logger.Info($"[Icon] loaded '{path}' ({texture.GetWidth()}x{texture.GetHeight()}).");

        return texture;
    }

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Godot C# 脚本注册只负责让 pck 中的脚本类型能被 Godot 找到。
        // 这一步和 RitsuLib 的内容自动注册不是同一件事，两个都需要保留。
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);

        // 自动注册扫描会读取当前程序集里的 RegisterCard/RegisterRelic/RegisterOwnedCardKeyword 等 attribute。
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 临时力量的回合开始补发已迁移到 GaoshouTemporaryStrengthPower.AfterSideTurnStart（per-owner，多人安全）。

        // 废品/衍生卡已迁入原版 Token 池，不再注册自定义废品池筛选器。

        // 先古牌：让尘封魔典(DUSTY_TOME)奖励时给予「剑舞」（取代原先的「占位」）；古老牙齿(ARCHAIC_TOOTH)的配对由 GunShield 的
        // RegisterArchaicToothTranscendence 属性处理（枪盾 -> 盾斧）。
        RitsuLibFramework.RegisterDustyTomeCard<GaoshouCharacter, SwordDance>();

        // Harmony 补丁：战斗直觉（拦截敌人意图渲染的两条路径——ModPatchInfo 单目标，需分别注册）。
        var patcher = RitsuLibFramework.CreatePatcher(ModId, "gaoshou-patches");
        patcher.RegisterPatch(new ModPatchInfo(
            BattleInstinctPatch.PatchId + "_refresh",
            typeof(NCreature),
            "RefreshIntents",
            typeof(BattleInstinctPatch),
            false,
            "hide enemy intents on turn refresh",
            null,
            true,
            HarmonyLib.MethodType.Normal));
        patcher.RegisterPatch(new ModPatchInfo(
            BattleInstinctPatch.PatchId + "_update",
            typeof(NCreature),
            "UpdateIntent",
            typeof(BattleInstinctPatch),
            false,
            "hide enemy intents on event-driven intent update",
            null,
            true,
            HarmonyLib.MethodType.Normal));
        // 碎纸机火堆行动自定义图标。
        patcher.RegisterPatch(new ModPatchInfo(
            RestIconPatch.PatchId,
            typeof(RestSiteOption),
            "get_Icon",
            typeof(RestIconPatch),
            false,
            "custom rest-site icon for Shredder",
            null,
            true,
            HarmonyLib.MethodType.Normal));
        // 高手卡牌悬浮释义置顶显示 mana 颜色条（第一条）。
        patcher.RegisterPatch(new ModPatchInfo(
            ManaColorHoverPatch.PatchId,
            typeof(ModCardTemplate),
            "get_HoverTips",
            typeof(ManaColorHoverPatch),
            false,
            "mana color tip as first card hover tip",
            null,
            true,
            HarmonyLib.MethodType.Normal));
        // 【幻影复制品单色分配：已撤下，2026-09-22】
        // 原来这里注册 PhantomCloneColorPatch（挂 CombatState.CloneCard），会给**所有**克隆出来的
        // 双色卡随机赋一个单色。但按设计只有「幻影」复制品才该取单色（由 PhantomSingleton 在触发
        // 幻影时用同步 RNG 登记）；其它来源的克隆（DualWield 那类外部复制、遗物、事件、UI 预览）
        // 都应当保持卡牌本来的双色。撤下后：外部克隆查不到实例色 →
        // GaoshouFlowTracker.GetColor 回退到类型色（如杠杆霰弹枪 = RedPurple 双色），行为正确。
        // 代码保留在 GaoshouCode/Cards/PhantomCloneColorPatch.cs（已改为纯注释档，未注册）。
        //
        // patcher.RegisterPatch(new ModPatchInfo(
        //     PhantomCloneColorPatch.PatchId,
        //     typeof(CombatState),
        //     "CloneCard",
        //     typeof(PhantomCloneColorPatch),
        //     false,
        //     "random single color for phantom copies",
        //     null,
        //     true,
        //     HarmonyLib.MethodType.Normal));
        // 事件「色彩哲学家」：把高手卡池并入备选颜色池，让「红蓝双色」与原版颜色等权随机（RitsuLib IPatchMethod 写法）。
        patcher.RegisterPatch<ColorfulPhilosophersPatch>();
        // 设置项「禁用原版事件」：给 RoomSet.EnsureNextEventIsValid 挂前缀，把玩家禁用的原版事件从本局抽取池里移除
        //（**不能**改 EventModel.IsAllowed：13 个目标里 8 个自己重写了该方法且不调 base，基类补丁拦不到）。
        patcher.RegisterPatch<VanillaEventDisablePatch>();
        // 事件插图叠加：10 个事件共用一张 notebook 底图，各自的小插图在这里贴到 %Portrait 左半。
        patcher.RegisterPatch<EventArtOverlayPatch>();
        // 幻影副本按分配到的单色切换卡面（<类名>_R/_B/_P/_G.png，找不到就回落本体卡面）。
        patcher.RegisterPatch<PhantomPortraitPatch>();
        // 事件页「文本没变」时跳过进场动画：超级强化机这类翻页只换计数、文案一模一样的循环事件，
        // 原先每翻一页都要重播 0.5s 延迟 + 1s 淡入 + 1s 逐字（按钮还要再等 index*0.2s 才可点），非常拖沓。
        // 拆成两个类是因为 RitsuLib 按方法名取 "Prefix"，一个类只能对应一个目标方法。
        patcher.RegisterPatch<SkipDuplicateEventDescriptionAnimation>();
        patcher.RegisterPatch<SkipDuplicateEventOptionAnimation>();
        // 手牌高光配色：流转就绪=金橙、奇迹就绪=紫罗兰、两者都就绪=品红。
        // （原版只有金/红两种预设，而红在游戏里是"不可打出"的警告语义，不适合当"奇迹就绪"。）
        patcher.RegisterPatch<GaoshouGlowColorPatch>();
        // 「呼吸」在选牌/弃牌界面里亮**原版金色**（与奇巧同款观感）：postfix 打在
        // NHandCardHolder.ShouldGlowGold 的 getter 上，不注入任何我们自己的委托（2026-09-30 JIT 事故复盘）。
        patcher.RegisterPatch<BreathDiscardGlowPatch>();
        // 流转/奇迹词条的悬浮提示：按当前状态显示 on/off 图标（flow_on 蓝 / miracle_on 橙 / off 白）。
        patcher.RegisterPatch<KeywordStateIconPatch>();
        // 「限制」挡住出牌时的台词：原版对非五种模型的阻挡者取不到名字 → 显示 <Unknown>，这里换成限制自己的台词。
        patcher.RegisterPatch<LimitedDialoguePatch>();
        // 「胆小 Skittish」结算时机：我们的多段卡是"每段一条 AttackCommand"，原版这条"挨打后获得格挡"的反应
        // 会在第一段之后就结算，后面的段数被格挡吃掉 → 压到该牌全部伤害打完之后（只在我们的多段卡结算窗口内生效）。
        // （蜷身 CurlUp 挂在出牌结束上，原版节奏本就正确，不处理。）
        patcher.RegisterPatch<SkittishReactionDelayPatch>();
        // 废品牌悬浮预览：临时武器 / 垃圾宝箱 / 可别浪费 的卡面悬浮里逐个展示所有废品牌。
        // 三条独立补丁（RitsuLib 用 GetMethod("Postfix") 取方法，同名重载会抛歧义异常，不能合成一个类）：
        //   1) 记归属：悬浮窗创建时若挂在三张卡之一上就登记（不能只看"里面有没有废品牌卡片" ——
        //      损失规避的悬浮里就有硬纸板预览，硬纸板也是废品牌，会被误轮换）；
        //   2) 滚轮（常驻的 NCursorManager._Input，用 SetInputAsHandled 独占滚轮）；
        //   3) 2 秒兜底自动切换（NHoverTipSet._Process）。都只就地换 NCard.Model，不重建悬浮窗。
        patcher.RegisterPatch<WastePreviewOwnerPatch>();
        patcher.RegisterPatch<WastePreviewWheelPatch>();
        patcher.RegisterPatch<WastePreviewPatch>();
        // AoE 横扫动画：在 AttackCommand.Execute 前缀里判定"这次打的是不是全体"，
        // 给视觉状态机在「横扫 / 挥拳」之间选路（**本模组补丁必须在这里显式注册，
        // 只写 IPatchMethod 类是不会被挂上的** —— 2026-09-24 就因为这个，横扫一次都没触发过）。
        patcher.RegisterPatch<SetAoeAttackStylePatch>();
        // 「连续攻击」循环动画：把游戏算出的**实际段数**（Hook.ModifyAttackHitCount）抄一份，
        // 让视觉状态机能区分"多段连击"与"单击"，从而决定是否进 atk_loop 视频循环。
        // 同样是 Postfix 纯旁路读取，不改游戏返回值 ✓。
        patcher.RegisterPatch<RecordAttackHitCountPatch>();
        // 【2026-09-29】多人面板里"铁甲战士的阴影"清理：原版 multiplayer_player_state / remote_lobby_player
        // 场景把铁甲战士头像（以及 energy_ironclad）烘进场景当默认值，原版只覆盖头像本体 ⇒ 残留一层。
        // 这条 Postfix 在两个界面构建/刷新后遍历子树，把"路径含原版角色 id"的节点换掉或隐藏（仅限我们自己的角色面板 ✓）。
        patcher.RegisterPatch<VanillaCharacterArtSweepPatch>();
        // 【2026-09-30】百科大全 / 历史记录里"角色图渲染过大"：那两个界面用 IconTexture（我们是 200×200，
        // 而该槽位规范是 88×88），且那些节点没设尺寸/缩放 ⇒ 按贴图原始尺寸渲染 ✗。
        // 这条只在这些界面上把"显示我们头像"的 TextureRect 补上 88×88 + IgnoreSize/KeepAspectCentered ✓
        // （不动共享贴图 ⇒ 顶部栏不受影响 ✓）。
        patcher.RegisterPatch<CharacterIconDisplaySizePatch>();
        // 百科大全角色详情：目标方法只在 beta 0.111.0 存在（正式版没有）⇒ 单独一个补丁类，
        // 挂不上时只影响这一条，绝不连累「历史记录」那条。两条的可选目标缺失都会在下面打日志。
        patcher.RegisterPatch<BestiaryCharacterIconDisplaySizePatch>();
        LogMissingOptionalTargets(CharacterIconDisplaySizePatch.GetTargets(), nameof(CharacterIconDisplaySizePatch));
        LogMissingOptionalTargets(BestiaryCharacterIconDisplaySizePatch.GetTargets(), nameof(BestiaryCharacterIconDisplaySizePatch));
        // 「连续攻击」循环的收尾计时器要按**每段命中**重置，而"每段"只有 TriggerAnim 能提供
        //（ModifyAttackHitCount 在命中循环之外、每张牌只调一次 ✗）⇒ 额外挂这条纯旁路通知。
        patcher.RegisterPatch<RecordAttackTriggerPatch>();
        // ⚠️ 上面那条只给到"**出拳开始**"（TriggerAnim 是每段开头、且本身 async，Postfix 看不到 await 完成），
        // 于是收拳计时器是**从出拳开始**起算的，比"这一拳打完"早了半段动画 ⇒
        // 实机症状"打完之后依旧会回到 stance"（末段在动画走完前就被 GuardEnd 收拳）。
        // 这条挂在 AddResultsInternal（每段命中循环的最后一句，实参里带着 await CreatureCmd.Damage(...)）
        // ⇒ Postfix 在**伤害已经结算完**之后才触发，正好是"这一拳真的打完了"这一时刻 ✓。
        patcher.RegisterPatch<RecordAttackHitSettledPatch>();
        // 假商人事件房（原版 FAKE_MERCHANT）的 %CharacterContainer 是 **scale=1.75**（商店是 1），
        // 因为原版那里塞的是战斗小人（自带 0.29），而 RitsuLib 塞的是商人节点（自带 0.47 基准 +
        // 我们的 ShopStyle 0.68）⇒ 会被再放大 1.75 倍、顶部出屏。这条按容器缩放反算 1/k 兜住。
        patcher.RegisterPatch<FakeMerchantBoothScalePatch>();
        // 游戏结束界面（放弃游戏）里把我们的形象摆回**容器中心**：RitsuLib 的
        // CharacterGameOverScreenCompatibilityPatch 用 Prefix 整体替换了原方法，而它新增的
        // 「假商人」分支只 Reparent 不设位置、营火分支的 null 兜底也不给位置
        // ⇒ 人物与死亡动画停在容器左下角（原版 else 分支是会居中的）。这条 Postfix 跑在它之后覆盖位置
        //（Harmony 的 Postfix 在 Prefix 跳过原方法后仍会执行）。
        patcher.RegisterPatch<GameOverVisualPlacementPatch>();
        if (!patcher.PatchAll())
            Logger.Error("Patch application failed!");

        // 废品牌轮播：把"会被补丁回调间接调用"的我方方法**强制预编译**（RuntimeHelpers.PrepareMethod）
        // ⇒ 补丁回调里不再出现"首次 JIT"。2026-09-30 实证：MonoMod 的 JIT hook 在
        // CreateAndShow / NCursorManager._Input 的 detour 上下文里**首次编译我方方法**时会抛 ArgumentException ✗
        //（`WarmUp()` 那种"用 null 调一次"不够 —— 参数为 null 会提前 return，被调方根本没被编译 ✗，故改用 PrepareMethod ✓）
        WastePreviewPatch.ForceJit();

        // 回合/阶段钩子 JIT 自检（卡死级事故的关键排查手段：编不过的钩子会让引擎调用时抛异常 ⇒ 回合推进停住 ✗）
        VerifyTurnHookJit();

        // 心流：双倍伤害(DoubleDamagePower) 的图标**暂时不做覆盖**（2026-09-20 撤下）。
        // 原因：改成自定义图后实测出现「buff 图标渲染异常（像空的）+ 悬停 buff 时说明条目全挤到屏幕左上角」，
        // 且把尺寸对齐原版规格（小图标 64×64 / 大图 256×256）后依旧复现 —— 说明问题出在"覆盖图集切图"这条路径本身
        // （原版小图标来自 atlases/power_atlas.sprites/*，是图集切出来的 AtlasTexture，不是独立贴图）。
        // 相关资源与代码保留在下面（未注册），将来若要再试可直接启用：
        //   ExternalAssetOverrideRegistry.RegisterPowerIconPathProvider(ModId + ".power_icon.double_damage.path",
        //       power => power is DoubleDamagePower ? DoubleDamagePowerIconPath : null);
        //   ExternalAssetOverrideRegistry.RegisterPowerIconTextureProvider(ModId + ".power_icon.double_damage.texture",
        //       power => power is DoubleDamagePower ? DoubleDamagePowerIcon : null);
        //   ExternalAssetOverrideRegistry.RegisterPowerBigIconTextureProvider(ModId + ".power_icon.double_damage.big_texture",
        //       power => power is DoubleDamagePower ? DoubleDamagePowerBigIcon : null);

        // 新手教程：首次游玩高手时在第一场战斗开场 / 首战胜利后各弹一次指引（RitsuLib 生命周期事件驱动）。
        GaoshouTutorial.Initialize(Logger);

        // 事件运行期标记（事件之间共享，例如「打击大师」→「格挡达人」的隐藏分支解锁）：
        // 开局 / 读档 / 本局结束时清零，避免跨局残留。
        RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(_ => GaoshouEventFlags.Reset());
        RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(_ => GaoshouEventFlags.Reset());
        RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(_ => GaoshouEventFlags.Reset());

        // 构建/游戏版本自检（装错版本会静默出错位，见方法注释）。
        VerifyGameVariant();

        Logger.Info("Gaoshou initialized.");
    }

    /// <summary>本 dll 是按哪个游戏版本编译的（main 分支 = "beta"，release 分支 = "release"）。</summary>
    private const string ExpectedGameVariant = "release";

    /// <summary>
    /// 引擎钩子（回合/阶段钩子等）的**一次性 Warn，带完整类型/消息/栈** ✓
    /// （钩子抛异常 = 回合推进停住 = 游戏卡死 ✗ ⇒ 必须兜住并留证 ✓）
    /// </summary>
    public static void WarnHookOnce(ref bool flag, string tag, System.Exception e)
    {
        if (flag)
            return;
        flag = true;
        Logger.Warn($"{tag}: {e.GetType().FullName}: {e.Message}\n{e.StackTrace}");
    }

    /// <summary>
    /// 回合/阶段钩子的 **JIT 自检 + 预热（含 async 状态机与闭包类）** ✓
    ///
    /// ⚠️ 2026-09-30 卡死事故的**真正原因**（完整栈实证）：
    /// 被 MonoMod 的 JIT hook 拒绝编译的**不是外层钩子方法**，而是编译器为 async 方法生成的
    /// **状态机的 `MoveNext`** ✗（栈里紧跟 `AfterSideTurnEnd` 的帧是 `AsyncMethodBuilderCore.Start[TStateMachine]` ✓）。
    /// 只预热外层方法 ⇒ 自检"0 failed"但运行时首次执行到 `MoveNext` 照样炸 ✗✗（自检盲区 ✓）。
    /// ⇒ 这里把范围扩成三部分，**全部** `PrepareMethod`：
    ///   ① 名字含 Turn / Sande / Participant 的我方方法（钩子链 ✓）
    ///   ② **我方类型里的编译器生成类型**（`<>c` 闭包类 / `<>d__N` 状态机 ✓）的全部方法 —— 这正是反复出事的家族 ✗
    ///   ③ 任何带 `AsyncStateMachineAttribute` 的方法 ⇒ 其 `StateMachineType` 的 `MoveNext` ✓（双保险 ✓）
    /// 并逐条点名失败项（≤12 条 ✓）+ 汇总（含 stateMachines 计数 ✓）。
    /// </summary>
    private static void VerifyTurnHookJit()
    {
        var ok = 0;
        var failed = 0;
        var checkedTypes = 0;
        var stateMachineMethods = 0;
        var failures = new List<string>();

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static
                                       | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsGenericTypeDefinition || type.ContainsGenericParameters)
                    continue;
                if (!IsOurType(type))
                    continue;

                var compilerGenerated = type.Name.Contains("<>", StringComparison.Ordinal);
                checkedTypes++;

                foreach (var method in type.GetMethods(flags))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters)
                        continue;

                    var name = method.Name;
                    var relevant = compilerGenerated
                                   || name.Contains("Turn", StringComparison.Ordinal)
                                   || name.Contains("Sande", StringComparison.Ordinal)
                                   || name.Contains("Participant", StringComparison.Ordinal);
                    if (!relevant)
                        continue;

                    ok += Prepare(method, failures, ref failed);
                    if (name == "MoveNext")
                        stateMachineMethods++;

                    // ③ 双保险：async 方法 ⇒ 顺带预热它生成的状态机类型（含 MoveNext）
                    foreach (var attribute in method.GetCustomAttributes(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), false))
                    {
                        if (attribute is not System.Runtime.CompilerServices.AsyncStateMachineAttribute asyncAttribute)
                            continue;

                        var machineType = asyncAttribute.StateMachineType;
                        if (machineType == null || machineType.ContainsGenericParameters)
                            continue;

                        var moveNext = machineType.GetMethod("MoveNext", flags);
                        if (moveNext == null)
                            continue;

                        stateMachineMethods++;
                        ok += Prepare(moveNext, failures, ref failed);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Logger.Warn($"[HookJit] sweep failed: {e.GetType().Name}: {e.Message}");
        }

        Logger.Info($"[HookJit] hooks+stateMachines: {ok} compiled, {failed} failed " +
                    $"(types={checkedTypes}, MoveNext={stateMachineMethods}).");
        foreach (var failure in failures)
            Logger.Warn($"[HookJit] FAILED: {failure}");
    }

    /// <summary>这个方法是否属于我方（含编译器生成的嵌套类型：其 Namespace 为 null，要靠 DeclaringType 往上找 ✓）。</summary>
    private static bool IsOurType(Type type)
    {
        var current = type;
        for (var depth = 0; current != null && depth < 8; depth++)
        {
            var ns = current.Namespace;
            if (ns != null)
                return ns.StartsWith("Gaoshou", StringComparison.Ordinal);

            current = current.DeclaringType;
        }

        return false;
    }

    /// <summary>预热一个方法：成功返回 1，失败记一条并返回 0 ✓。</summary>
    private static int Prepare(MethodInfo method, List<string> failures, ref int failed)
    {
        try
        {
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);
            return 1;
        }
        catch (System.Exception e)
        {
            failed++;
            if (failures.Count < 12)
                failures.Add($"{method.DeclaringType?.Name}.{method.Name}: {e.GetType().Name}: {e.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 构建/游戏版本自检：本 dll 只能装在它编译时对应的游戏版本上。
    /// 装错版本**不会崩**，但补丁与游戏 API 会整片错位 —— 历史事故症状：
    /// 增幅弃牌无法确认、卡牌被复制、悬浮提示错位（2026-09-30）。
    /// 判据用"只在一个版本存在的 API"，走反射按名查（两个分支都能编译）。
    /// </summary>
    private static void VerifyGameVariant()
    {
        // beta 0.111.0 的 CardModel 才有 CreateCloneForPlayer(player)；正式版 0.107.1 只有 CreateClone()。
        var isBetaGame = typeof(CardModel).GetMethod("CreateCloneForPlayer") != null;
        var actual = isBetaGame ? "beta" : "release";
        if (actual == ExpectedGameVariant)
        {
            Logger.Info($"[Variant] game variant '{actual}' matches this build.");
            return;
        }

        Logger.Error($"[Variant] MISMATCH: this Gaoshou build targets the '{ExpectedGameVariant}' game, but the running game looks like '{actual}'. " +
                     "Patches and game API calls will be misaligned (card selection may not confirm, cards may duplicate, hover tips may break). " +
                     "Install the matching Gaoshou build: beta dll on the beta game, release dll on the release game.");
    }

    /// <summary>
    /// RitsuLib 的"可选目标"在本版本缺失时是**静默跳过**的 ⇒ 这里补一条明确日志，
    /// 避免"以为修了、其实没生效"被埋掉（2026-09-30 事故复盘）。
    /// </summary>
    private static void LogMissingOptionalTargets(ModPatchTarget[] targets, string patchName)
    {
        foreach (var target in targets)
        {
            if (!target.IgnoreIfMissing)
                continue;

            var found = target.TargetType.GetMethod(
                target.MethodName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (found == null)
                Logger.Warn($"[Patch] {patchName}: target {target} is missing in this game version - that part is skipped.");
        }
    }
}