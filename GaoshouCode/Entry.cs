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
        // 「连续攻击」循环的收尾计时器要按**每段命中**重置，而"每段"只有 TriggerAnim 能提供
        //（ModifyAttackHitCount 在命中循环之外、每张牌只调一次 ✗）⇒ 额外挂这条纯旁路通知。
        patcher.RegisterPatch<RecordAttackTriggerPatch>();
        // ⚠️ 上面那条只给到"**出拳开始**"（TriggerAnim 是每段开头、且本身 async，Postfix 看不到 await 完成），
        // 于是收拳计时器是**从出拳开始**起算的，比"这一拳打完"早了半段动画 ⇒
        // 实机症状"打完之后依旧会回到 stance"（末段在动画走完前就被 GuardEnd 收拳）。
        // 这条挂在 AddResultsInternal（每段命中循环的最后一句，实参里带着 await CreatureCmd.Damage(...)）
        // ⇒ Postfix 在**伤害已经结算完**之后才触发，正好是"这一拳真的打完了"这一时刻 ✓。
        patcher.RegisterPatch<RecordAttackHitSettledPatch>();
        if (!patcher.PatchAll())
            Logger.Error("Patch application failed!");

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

        Logger.Info("Gaoshou initialized.");
    }
}