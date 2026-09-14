using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace Gaoshou.Patches;

/// <summary>
/// 高手模组设置里的「事件」页：
///   ① 本模组添加的事件：逐个开关（含总开关）——本模组事件在自己的 IsAllowed 里读这里；
///   ② 原版事件：可逐个禁用——由 <see cref="VanillaEventDisablePatch" /> 在事件抽取前从本章事件池里剔除。
///
/// 值存模组数据存储（<see cref="SaveScope.Global" />，键 <see cref="DataKey" />，文件 settings.json）。
/// 注意 RitsuLib 要求：**先 store.Register 注册数据键，再建 binding**，否则设置项会加载失败。
///
/// 联机：设置是**主机权威**的 —— 由 <see cref="GaoshouEventSettingsSync" /> 用 RitsuLib 的 Sidecar 配置主题
/// （<c>gaoshou.event_settings</c>）广播给客机；客机读取时用主机快照，本地改动在联机里不生效。
/// 单人局 / 主机端的行为与加同步之前完全一致。
/// </summary>
[RegisterSingleton]
public sealed class GaoshouEventSettings : SingletonModel
{
    public const string DataKey = "gaoshou_event_settings";
    private const string DataFile = "settings.json";

    /// <summary>总开关的键（关掉后本模组所有事件都不再出现）。</summary>
    public const string MasterKey = "__master";

    public override bool ShouldReceiveCombatHooks => false;

    // 设置值缓存：补丁每次进事件房都会读，不能每次都走数据存储反序列化。
    private static GaoshouEventSettingsData? _cache;

    /// <summary>本模组添加的事件（默认全部启用）。</summary>
    public static readonly (string Key, string Zh, string En, string HintZh, string HintEn)[] ModEvents =
    [
        ("smite_master", "打击大师", "Smite Master",
            "ACT1/ACT2：教打击的老前辈。", "Act 1/2: an old master who teaches Strikes."),
        ("guard_master", "格挡达人", "Guard Master",
            "ACT2/ACT3：练格挡的大哥。", "Act 2/3: a guy who trains blocks."),
        ("roadside_bench", "路边的工作台", "Roadside Workbench",
            "ACT1：给一张牌附魔。", "Act 1: enchant a card."),
        ("odd_jobs", "万事屋", "Odd Jobs Shop",
            "ACT1：花金币升级牌、打工赚钱或休息回血。", "Act 1: spend gold to upgrade, work for gold, or rest."),
        ("magic_prodigy", "魔法鬼才", "Magic Prodigy",
            "ACT2：从三张模组卡里学一个法术。", "Act 2: learn one of three mod cards."),
        ("grand_library", "大书库", "Grand Library",
            "ACT2/ACT3：借书（选牌入组）或捐赠（删牌换遗物）。", "Act 2/3: borrow cards, or donate one for a relic."),
        ("super_upgrader", "超级强化机", "Super Upgrader",
            "ACT3：花金币或生命抽概率升级牌。", "Act 3: gamble gold or HP to upgrade cards."),
        ("mysterious_cave", "神秘洞穴", "Mysterious Cave",
            "Overgrowth：层层深入的洞窟。", "Overgrowth: a cave that goes deeper."),
        ("mysterious_shop", "神秘商店", "Mysterious Shop",
            "ACT2：至少 100 金币才会遇到。", "Act 2: needs 100+ gold."),
        ("strange_temple", "奇怪神殿", "Strange Temple",
            "Overgrowth：神殿里有钱或有神像。", "Overgrowth: gold, or an idol."),
    ];

    /// <summary>可被禁用的原版事件（键 = 类名，值 = events 表里的 Id.Entry）。默认全部「不禁用」。</summary>
    public static readonly (string ClassName, string Entry, string Zh, string En)[] VanillaEvents =
    [
        ("LuminousChoir", "LUMINOUS_CHOIR", "冷光合唱团", "Luminous Choir"),
        ("UnrestSite", "UNREST_SITE", "无休之处", "Unrest Site"),
        ("Bugslayer", "BUGSLAYER", "害虫杀手", "Bugslayer"),
        ("InfestedAutomaton", "INFESTED_AUTOMATON", "被寄生的自动机械", "Infested Automaton"),
        ("FieldOfManSizedHoles", "FIELD_OF_MAN_SIZED_HOLES", "人形洞穴之地", "Field of Man-Sized Holes"),
        ("LostWisp", "LOST_WISP", "迷失鬼火", "The Lost Wisp"),
        ("HungryForMushrooms", "HUNGRY_FOR_MUSHROOMS", "蘑菇饥渴", "Hungry for Mushrooms"),
        ("Trial", "TRIAL", "审判", "The Trial"),
        ("DenseVegetation", "DENSE_VEGETATION", "茂密的植被", "Dense Vegetation"),
        ("JungleMazeAdventure", "JUNGLE_MAZE_ADVENTURE", "丛林迷宫奇遇", "Jungle Maze Adventure"),
        ("MorphicGrove", "MORPHIC_GROVE", "变形灵林谷", "Morphic Grove"),
        ("TeaMaster", "TEA_MASTER", "茶艺大师", "Tea Master"),
        ("WelcomeToWongos", "WELCOME_TO_WONGOS", "欢迎来到旺购百货", "Welcome to Wongo's"),
    ];

    public GaoshouEventSettings()
    {
        RegisterDataStore();

        // 联机：把「事件设置」注册成主机权威的同步主题（客机只读主机快照，本地改动不生效）。
        GaoshouEventSettingsSync.Install();

        RitsuLibFramework.RegisterModSettings(
            Entry.ModId,
            page => page
                .WithSortOrder(110)
                .AsChildOf("gaoshou_main")
                .WithTitle(T("事件", "Events"))
                .WithDescription(T(
                    "控制本模组添加的事件，以及可选择性禁用的原版事件。",
                    "Control the events added by this mod, and optionally disable selected vanilla events."))
                .AddSection("mod_events", section =>
                {
                    section.WithTitle(T("本模组事件", "Mod Events"));
                    section.AddToggle(
                        "mod_events_master",
                        T("启用本模组添加的事件", "Enable mod events"),
                        ModEventBinding(MasterKey),
                        T("总开关：关闭后下列事件全部不再出现。", "Master switch: when off, none of the events below will appear."));
                    foreach (var (key, zh, en, hintZh, hintEn) in ModEvents)
                        section.AddToggle("mod_event_" + key, T(zh, en), ModEventBinding(key), T(hintZh, hintEn));
                })
                .AddSection("vanilla_events", section =>
                {
                    section.WithTitle(T("禁用原版事件", "Disable Vanilla Events"));
                    section.AddParagraph(
                        "vanilla_intro",
                        T(
                            "本模组新增事件会稀释原版事件池；如只想保留喜欢的原版事件，可在此逐个禁用。\n" +
                            "改动在进入新的章节/新的一局时生效（本局已生成的事件池不会回滚）。",
                            "Mod events dilute the vanilla event pool. Disable any vanilla events you do not want.\n" +
                            "Changes apply when a new act starts or a new run begins."));
                    foreach (var (className, _entry, zh, en) in VanillaEvents)
                        section.AddToggle(
                            "disable_vanilla_" + className,
                            T(zh, en),
                            VanillaEventBinding(className),
                            T("开启后，该原版事件不再出现。", "When on, this vanilla event will no longer appear."));
                }),
            pageId: "gaoshou_events");
    }

    // ---------------- 读取（给事件与补丁用） ----------------

    /// <summary>
    /// 读取设置时用哪一份：**联机客机用主机广播过来的快照**，单人局 / 主机端用本地设置
    /// （见 <see cref="GaoshouEventSettingsSync" />；后两种情况与加联机同步之前完全一致）。
    /// </summary>
    private static GaoshouEventSettingsData Effective() => GaoshouEventSettingsSync.Resolve();

    /// <summary>同步用的本地设置快照（<see cref="GaoshouEventSettingsSync" /> 广播时读它）。</summary>
    internal static GaoshouEventSettingsData LocalForSync() => Current();

    /// <summary>本模组事件是否启用（未设置过默认启用；总开关关闭则一律不启用）。</summary>
    public static bool IsModEventEnabled(string key)
    {
        var data = Effective();
        if (data.ModEvents.TryGetValue(MasterKey, out var master) && !master)
            return false;
        return !data.ModEvents.TryGetValue(key, out var value) || value;
    }

    /// <summary>玩家禁用掉的**原版事件 Id.Entry** 集合（补丁用；空集合代表不用过滤）。</summary>
    public static IReadOnlyCollection<string> DisabledVanillaEventEntries()
    {
        var data = Effective();
        if (data.DisabledVanillaEvents.Count == 0)
            return [];

        var entries = new List<string>(data.DisabledVanillaEvents.Count);
        foreach (var (className, entry, _zh, _en) in VanillaEvents)
        {
            if (data.DisabledVanillaEvents.TryGetValue(className, out var disabled) && disabled)
                entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// 抽取事件池前调用（<see cref="VanillaEventDisablePatch" /> 用）：确认本次能不能按设置剔除原版事件，
    /// 顺带把主机端的最新设置广播给客机。返回 false ⇒ 本次**不要**动事件池
    /// （拿不到主机设置时保守不筛，保证各端事件池 / 已访问记录不会分歧）。
    /// </summary>
    public static bool TryAlignVanillaEventPoolFilter(RunState runState)
        => GaoshouEventSettingsSync.TryAlignEventPoolFilter(runState);

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
                static () => new GaoshouEventSettingsData(), true);
        }

        // 任何设置写入后让缓存失效，保证补丁下一次读到最新值。
        // 本模组事件页的写入还要通知联机同步：房主把新快照广播出去，客机的本地改动被忽略。
        ModSettingsBindingWriteEvents.ValueWritten += binding =>
        {
            if (!string.Equals(binding.ModId, Entry.ModId, StringComparison.Ordinal) ||
                !string.Equals(binding.DataKey, DataKey, StringComparison.Ordinal))
                return;

            InvalidateCache();
            GaoshouEventSettingsSync.OnLocalSettingsChanged();
        };
    }

    private static GaoshouEventSettingsData Current()
    {
        try
        {
            return _cache ??= RitsuLibFramework.GetDataStore(Entry.ModId)
                .Get<GaoshouEventSettingsData>(DataKey);
        }
        catch (Exception e)
        {
            Entry.Logger.Error($"[GaoshouEventSettings] read failed: {e.Message}");
            return new GaoshouEventSettingsData();
        }
    }

    private static IModSettingsValueBinding<bool> ModEventBinding(string key)
    {
        return ModSettingsBindings.Global<GaoshouEventSettingsData, bool>(
            Entry.ModId,
            DataKey,
            data => !data.ModEvents.TryGetValue(key, out var value) || value,
            (data, value) => data.ModEvents[key] = value);
    }

    private static IModSettingsValueBinding<bool> VanillaEventBinding(string className)
    {
        return ModSettingsBindings.Global<GaoshouEventSettingsData, bool>(
            Entry.ModId,
            DataKey,
            data => data.DisabledVanillaEvents.TryGetValue(className, out var value) && value,
            (data, value) => data.DisabledVanillaEvents[className] = value);
    }

    private static ModSettingsText T(string zh, string en)
    {
        var isZh = string.Equals(LocManager.Instance.Language, "zhs", StringComparison.OrdinalIgnoreCase);
        return ModSettingsText.Literal(isZh ? zh : en);
    }
}

/// <summary>设置项的数据模型（JSON 存在模组数据存储里）。</summary>
public sealed class GaoshouEventSettingsData
{
    /// <summary>本模组事件开关：键 → 是否启用。</summary>
    public Dictionary<string, bool> ModEvents { get; set; } = [];

    /// <summary>被禁用的原版事件：类名 → 是否禁用。</summary>
    public Dictionary<string, bool> DisabledVanillaEvents { get; set; } = [];
}
