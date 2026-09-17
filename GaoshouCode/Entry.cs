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
    private const string DoubleDamagePowerIconPath = $"{ResPath}/images/powers/doubledamage.png";

    private static Godot.Texture2D? _doubleDamagePowerIcon;

    private static Godot.Texture2D? DoubleDamagePowerIcon =>
        _doubleDamagePowerIcon ??=
            Godot.ResourceLoader.Load<Godot.Texture2D>(DoubleDamagePowerIconPath, null,
                Godot.ResourceLoader.CacheMode.Reuse);

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
        // 幻影复制品单色分配（所有 CloneCard 复制的双色卡随机一个主色）。
        patcher.RegisterPatch(new ModPatchInfo(
            PhantomCloneColorPatch.PatchId,
            typeof(CombatState),
            "CloneCard",
            typeof(PhantomCloneColorPatch),
            false,
            "random single color for phantom copies",
            null,
            true,
            HarmonyLib.MethodType.Normal));
        // 事件「色彩哲学家」：把高手卡池并入备选颜色池，让「红蓝双色」与原版颜色等权随机（RitsuLib IPatchMethod 写法）。
        patcher.RegisterPatch<ColorfulPhilosophersPatch>();
        // 设置项「禁用原版事件」：给 RoomSet.EnsureNextEventIsValid 挂前缀，把玩家禁用的原版事件从本局抽取池里移除
        //（**不能**改 EventModel.IsAllowed：13 个目标里 8 个自己重写了该方法且不调 base，基类补丁拦不到）。
        patcher.RegisterPatch<VanillaEventDisablePatch>();
        // 事件插图叠加：10 个事件共用一张 notebook 底图，各自的小插图在这里贴到 %Portrait 左半。
        patcher.RegisterPatch<EventArtOverlayPatch>();
        // 幻影副本按分配到的单色切换卡面（<类名>_R/_B/_P/_G.png，找不到就回落本体卡面）。
        patcher.RegisterPatch<PhantomPortraitPatch>();
        if (!patcher.PatchAll())
            Logger.Error("Patch application failed!");

        // 心流：把原版「双倍伤害」buff 的图标换成高手自定义图（其余原版 buff 图标不动）。
        // 原版能力走的是模型自带图标，这里用 RitsuLib 的外部资源覆盖注册表按模型类型过滤替换；
        // IconPath/PackedIconPath、Icon(小图标)、BigIcon(悬浮释义大图) 三条读取路径都要覆盖。
        ExternalAssetOverrideRegistry.RegisterPowerIconPathProvider(
            ModId + ".power_icon.double_damage.path",
            power => power is DoubleDamagePower ? DoubleDamagePowerIconPath : null);
        ExternalAssetOverrideRegistry.RegisterPowerIconTextureProvider(
            ModId + ".power_icon.double_damage.texture",
            power => power is DoubleDamagePower ? DoubleDamagePowerIcon : null);
        ExternalAssetOverrideRegistry.RegisterPowerBigIconTextureProvider(
            ModId + ".power_icon.double_damage.big_texture",
            power => power is DoubleDamagePower ? DoubleDamagePowerIcon : null);

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