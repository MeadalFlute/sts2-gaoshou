using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace Gaoshou.Tutorial;

/// <summary>
/// 新手教程的**播放进度**（模组自己记）+ 设置页「重置新手教程」。
///
/// 为什么不沿用原版 <c>SaveManager.SeenFtue</c> 记进度：
///   原版 <c>SeenFtue(key)</c> 把两件事混在一个返回值里 ——
///     <c>!Progress.EnableFtues</c>（玩家在设置里关掉了教程）**或** 该 key 已完成，都返回 true。
///   于是我们既分不清"玩家关了教程"和"已经播过"，也没法提供"重置"（那要直接改玩家存档的 FtueCompleted）。
///   改成：进度记在模组自己的数据存储里；是否尊重玩家的开关改为直接读 <c>Progress.EnableFtues</c>。
///
/// 语义（2026-09-23 起）：
///   * 默认（没有任何记录）= 全部教程都没播过 → 教程照常触发（<c>EnableFtues</c> 默认也是 true）；
///   * 每条教程只播一次，播过就记一笔；
///   * 当**所有**教程都播过一遍后 <see cref="AllPlayed" /> 变 true → 教程不再触发（即"flag 设为 false"）；
///   * 设置页的「重置新手教程」清空记录 → 回到默认触发状态。
/// </summary>
[RegisterSingleton]
public sealed class GaoshouTutorialSettings : SingletonModel
{
    public const string DataKey = "gaoshou_tutorial_settings";
    private const string DataFile = "settings.json";

    /// <summary>
    /// 教程清单。**新增教程时把它加到这里**：<see cref="AllPlayed" /> 与设置页的进度显示会自动跟着算。
    /// </summary>
    public static readonly string[] AllTutorialKeys =
    [
        GaoshouTutorial.IntroFtueKey,
        GaoshouTutorial.FirstWinFtueKey,
    ];

    public override bool ShouldReceiveCombatHooks => false;

    public GaoshouTutorialSettings()
    {
        RegisterDataStore();

        RitsuLibFramework.RegisterModSettings(
            Entry.ModId,
            page => page
                .WithSortOrder(130)
                .AsChildOf("gaoshou_main")
                .WithTitle(T("新手教程", "Tutorial"))
                .AddSection("tutorial", section =>
                {
                    section.WithTitle(T("新手教程", "Tutorial"));
                    section.AddToggle(
                        "tutorial_enabled",
                        T("启用新手教程", "Enable tutorials"),
                        EnabledBinding());
                    section.AddParagraph("tutorial_intro", T(
                        "高手的教程默认会播放：第一场战斗开场一次、首战胜利后一次。\n" +
                        "所有教程都播过一遍之后就不再弹出；想再看一遍用下面的重置。",
                        "Gaoshou's tutorials play by default: once at the start of your first combat, " +
                        "once after your first victory.\n" +
                        "Once every tutorial has been played they stop showing; use Reset below to watch them again."));
                    section.AddParagraph("tutorial_progress", ModSettingsText.Dynamic(ProgressText));
                    section.AddButton(
                        "reset_tutorial",
                        T("重置新手教程", "Reset Tutorials"),
                        T("重置", "Reset"),
                        host =>
                        {
                            ResetAll();
                            host.RequestRefresh();
                        },
                        ModSettingsButtonTone.Accent,
                        T(
                            "清空已播放记录，教程回到默认状态（下次进入第一场战斗会重新播放）。",
                            "Clears the played records so the tutorials trigger again from your next first combat."));
                }),
            pageId: "gaoshou_tutorial");
    }

    // ---------------- 读取（给 GaoshouTutorial 用） ----------------

    /// <summary>
    /// 模组自己的教程开关（默认 true）。
    /// 刻意**不**跟随原版 <c>Progress.EnableFtues</c>：那是原版教程的开关，很多玩家/开发者早就关了，
    /// 跟着它会导致模组教程永远不弹（2026-09-23 实测踩到）。
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            try
            {
                return Current().Enabled;
            }
            catch (Exception e)
            {
                Entry.Logger.Error($"[GaoshouTutorialSettings] read enabled failed: {e.Message}");
                return true;   // 读不到就按"开着"处理，符合"默认触发"
            }
        }
    }

    /// <summary>该条教程是否已经播放过。读不到数据时按"没播过"处理（教程照常触发）。</summary>
    public static bool IsPlayed(string key)
    {
        try
        {
            return Current().Played.TryGetValue(key, out var played) && played;
        }
        catch (Exception e)
        {
            Entry.Logger.Error($"[GaoshouTutorialSettings] read failed: {e.Message}");
            return false;
        }
    }

    /// <summary>是否所有教程都播放过一遍（= 用户说的那个 flag）。true 之后教程不再触发。</summary>
    public static bool AllPlayed => AllTutorialKeys.All(IsPlayed);

    /// <summary>已播放条数 / 总条数。</summary>
    public static (int Played, int Total) Progress =>
        (AllTutorialKeys.Count(IsPlayed), AllTutorialKeys.Length);

    /// <summary>记一笔：该条教程播放过了。刚好集满时打一条日志（方便排查"怎么不弹了"）。</summary>
    public static void MarkPlayed(string key)
    {
        try
        {
            var wasAll = AllPlayed;
            var store = RitsuLibFramework.GetDataStore(Entry.ModId);
            store.Modify<GaoshouTutorialSettingsData>(DataKey, data => data.Played[key] = true);
            store.Save(DataKey);

            if (!wasAll && AllPlayed)
                Entry.Logger.Info(
                    "[GaoshouTutorial] all tutorials have been played; they will not trigger again " +
                    "(reset from 设置 → 高手 → 新手教程).");
        }
        catch (Exception e)
        {
            Entry.Logger.Error($"[GaoshouTutorialSettings] mark '{key}' failed: {e.Message}");
        }
    }

    /// <summary>重置：清空所有播放记录（设置页「重置新手教程」按钮）。</summary>
    public static void ResetAll()
    {
        try
        {
            var store = RitsuLibFramework.GetDataStore(Entry.ModId);
            store.Modify<GaoshouTutorialSettingsData>(DataKey, data => data.Played.Clear());
            store.Save(DataKey);
            Entry.Logger.Info("[GaoshouTutorial] tutorial progress reset; tutorials will trigger again.");
        }
        catch (Exception e)
        {
            Entry.Logger.Error($"[GaoshouTutorialSettings] reset failed: {e.Message}");
        }
    }

    // ---------------- 内部 ----------------

    private static void RegisterDataStore()
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId, false))
        {
            store.Register(DataKey, DataFile, SaveScope.Global,
                static () => new GaoshouTutorialSettingsData(), true);
        }
    }

    private static GaoshouTutorialSettingsData Current()
    {
        return RitsuLibFramework.GetDataStore(Entry.ModId).Get<GaoshouTutorialSettingsData>(DataKey);
    }

    private static IModSettingsValueBinding<bool> EnabledBinding()
    {
        return ModSettingsBindings.Global<GaoshouTutorialSettingsData, bool>(
            Entry.ModId,
            DataKey,
            static data => data.Enabled,
            static (data, value) => data.Enabled = value);
    }

    /// <summary>设置页里那行动态进度文本。</summary>
    private static string ProgressText()
    {
        var isZh = string.Equals(LocManager.Instance.Language, "zhs", StringComparison.OrdinalIgnoreCase);
        var (played, total) = Progress;

        var parts = string.Join(isZh ? "、" : ", ",
            AllTutorialKeys.Select(key =>
                $"{(isZh ? LabelZh(key) : LabelEn(key))} {(IsPlayed(key) ? "✓" : "✗")}"));

        var head = isZh ? $"进度：{played}/{total}" : $"Progress: {played}/{total}";
        var tail = played >= total
            ? (isZh ? " —— 已全部播放，教程不再弹出。" : " — all played; tutorials no longer show.")
            : (isZh ? " —— 还有没播过的，仍会触发。" : " — not finished yet; tutorials still trigger.");

        return $"{head}　{parts}{tail}";
    }

    private static string LabelZh(string key) => key switch
    {
        GaoshouTutorial.IntroFtueKey => "第一场战斗开场",
        GaoshouTutorial.FirstWinFtueKey => "首战胜利后",
        _ => key,
    };

    private static string LabelEn(string key) => key switch
    {
        GaoshouTutorial.IntroFtueKey => "first combat",
        GaoshouTutorial.FirstWinFtueKey => "after first win",
        _ => key,
    };

    private static ModSettingsText T(string zh, string en)
    {
        var isZh = string.Equals(LocManager.Instance.Language, "zhs", StringComparison.OrdinalIgnoreCase);
        return ModSettingsText.Literal(isZh ? zh : en);
    }
}

/// <summary>设置项的数据模型（JSON 存在模组数据存储里）。</summary>
public sealed class GaoshouTutorialSettingsData
{
    /// <summary>模组自己的教程开关（默认开；不跟随原版的教程开关）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>教程 key → 是否播放过。缺失即"没播过"。</summary>
    public Dictionary<string, bool> Played { get; set; } = [];
}
