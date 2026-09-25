using System.Collections.Generic;
using System.Linq;
using Gaoshou.Keywords;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 「流转」「奇迹」这两个**带状态**的词条，悬浮提示按"这张牌此刻是否就绪"显示 on / off 图标。
///
/// 图标来自原作（纯白蒙版），已在 images/keywords/ 里染好色：
///   * 就绪（on）→ 流转蓝 (93,94,253) / 奇迹橙 (253,180,45)
///   * 未就绪（off）→ 白
///
/// 为什么要补丁：
///   * 词条提示由 RitsuLib 的 <c>ModKeywordRegistry.CreateHoverTip</c> 生成，图标取
///     <c>ModKeywordDefinition.IconPath</c> —— 那是**每个词条一份**的静态配置，不知道具体这张牌的状态 ✗；
///   * 但 <c>CardModel.HoverTips</c> 是**每次访问现算**的（ExtraHoverTips + 词条 + 附魔…，见 CardModel.cs:953），
///     所以在这里挂 postfix 就能拿到实时状态 ✓。
///
/// 实现细节：HoverTip 是 record struct 且 Icon 是私有 set，所以只能**重建**那一条提示
/// （标题/正文原样沿用，按标题文本识别、跨语言安全）。纯本地 UI，不影响游戏状态。
/// </summary>
public sealed class KeywordStateIconPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_keyword_state_icon";

    public static string Description => "flow/miracle keyword hover tips show on/off icons by current state";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(CardModel), "get_HoverTips"),
    ];

    private sealed class KeywordIcons
    {
        internal CardKeyword Keyword;
        internal LocString Title = null!;
        internal string OnName = "";
        internal string OffName = "";
        internal Texture2D? On;
        internal Texture2D? Off;
        internal bool Loaded;
    }

    private static readonly KeywordIcons[] Entries =
    [
        new()
        {
            Keyword = GaoshouKeyword.Flow,
            Title = new LocString("card_keywords", "GAOSHOU_KEYWORD_FLOW.title"),
            OnName = "flow_on",
            OffName = "flow_off",
        },
        new()
        {
            Keyword = GaoshouKeyword.Miracle,
            Title = new LocString("card_keywords", "GAOSHOU_KEYWORD_MIRACLE.title"),
            OnName = "miracle_on",
            OffName = "miracle_off",
        },
    ];

    public static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__result == null || __instance.Keywords.Count == 0)
            return;

        List<IHoverTip>? list = null;
        var changed = false;

        foreach (var entry in Entries)
        {
            if (!__instance.Keywords.Contains(entry.Keyword))
                continue;

            EnsureLoaded(entry);
            if (entry.On == null || entry.Off == null)
                continue; // 图缺失：保持 RitsuLib 的默认（on）

            // 就绪 = on。判定与手牌高光用的是同一对（都只认"在手牌里"）。
            var want = IsReady(entry.Keyword, __instance) ? entry.On : entry.Off;
            var title = entry.Title.GetFormattedText();

            list ??= __result.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not HoverTip tip)
                    continue;

                // 按标题文本认出这一条（跨语言安全：两边都是同一份本地化标题）。
                if (tip.Title != title || ReferenceEquals(tip.Icon, want))
                    continue;

                list[i] = new HoverTip(entry.Title, tip.Description, want);
                changed = true;
            }
        }

        if (changed && list != null)
            __result = list;
    }

    private static bool IsReady(CardKeyword keyword, CardModel card)
    {
        return keyword == GaoshouKeyword.Flow
            ? GaoshouFlowTracker.IsFlowGlowReady(card)
            : MiracleCounter.IsMiracleGlowReady(card);
    }

    private static void EnsureLoaded(KeywordIcons entry)
    {
        if (entry.Loaded)
            return;
        entry.Loaded = true;
        entry.On = Load(entry.OnName);
        entry.Off = Load(entry.OffName);
    }

    private static Texture2D? Load(string name)
    {
        var path = $"{Entry.ResPath}/images/keywords/{name}.png";
        try
        {
            return ResourceLoader.Exists(path)
                ? ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse)
                : null;
        }
        catch (System.Exception e)
        {
            Entry.Logger.Warn($"[KeywordStateIcon] load '{path}' failed: {e.Message}");
            return null;
        }
    }
}
