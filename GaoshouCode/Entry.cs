using STS2RitsuLib.Scaffolding.Content;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Models;
using Gaoshou.Cards;
using Gaoshou.Characters;
using Gaoshou.Powers;
using Gaoshou.Relics;
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
        if (!patcher.PatchAll())
            Logger.Error("Patch application failed!");

        Logger.Info("Gaoshou initialized.");
    }
}
