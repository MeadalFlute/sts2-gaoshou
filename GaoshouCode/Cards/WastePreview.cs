using System.Collections.Generic;
using System.Linq;
using Gaoshou.Keywords;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

/// <summary>
/// 「废品牌」悬浮预览（临时武器 / 垃圾宝箱 / 可别浪费 三张卡共用）。
///
/// 为什么只放一张、而不是一次列全部：悬浮窗里的卡片预览是**完整的 NCard 节点**
/// （<c>NHoverTipCardContainer.Add</c> → 建 <c>card_hover_tip.tscn</c> 再把 <c>%Card</c> 设 Model），
/// 十几张会又高又重；所以这里只放**当前这一张**，换卡交给
/// <see cref="Gaoshou.Patches.WastePreviewPatch" />（兜底定时）与
/// <see cref="Gaoshou.Patches.WastePreviewWheelPatch" />（鼠标滚轮）：
///   * 鼠标滚轮：悬停在卡面上滚动 → 上一张 / 下一张（最理想）；
///   * 否则：每 2 秒自动切到下一张（次理想）；
///   * 两条路都附一行操作说明，也给出"卡牌总览-衍生"这个保底查看入口。
///
/// 性能：废品牌列表**只建一次**并缓存（<c>ModelDb.AllCards</c> + <see cref="IWasteCard" /> 过滤 + 按 Id 排序，
/// 与解锁状态无关，因此各客户端结果一致）；换卡只是把悬浮窗里那个 NCard 的 Model 换掉
/// （就地改 Model + UpdateVisuals），不重建悬浮窗、不新建节点。
/// </summary>
internal static class WastePreview
{
    /// <summary>自动轮播间隔（秒）。</summary>
    internal const double IntervalSeconds = 2.0;

    private static List<CardModel>? _cards;

    private static int _index;

    // 预热后长期持有的资源（丢掉引用就会被原版缓存在边界处 Dispose → 下次同步磁盘加载）。
    private static readonly List<Resource> _keptResources = [];

    private static bool _assetsWarmed;

    /// <summary>上一次换卡的时间（毫秒，Godot Time.GetTicksMsec）。滚轮切过之后自动轮播重新计时。</summary>
    internal static ulong LastAdvanceMs { get; set; }

    /// <summary>当前轮播到的废品牌（没有废品牌时为 null）。</summary>
    internal static CardModel? Current()
    {
        var cards = All();
        if (cards.Count == 0)
            return null;

        var i = _index % cards.Count;
        if (i < 0)
            i += cards.Count;

        return cards[i];
    }

    /// <summary>前进（+1）/ 后退（-1）一张，并记录本次换卡时间。</summary>
    internal static void Step(int delta, ulong nowMs)
    {
        _index += delta;
        LastAdvanceMs = nowMs;
    }

    /// <summary>这张牌是不是"带废品牌预览悬浮"的卡（滚轮补丁用它做第一道闸门）。</summary>
    internal static bool IsPreviewCard(CardModel? card) =>
        card is ImprovisedWeapon or JunkChest or WasteNot;

    /// <summary>卡面悬浮条目：当前这一张废品牌 + 一行操作说明。</summary>
    internal static IEnumerable<IHoverTip> Build()
    {
        WarmAssets();

        if (Current() is { } card)
            yield return HoverTipFactory.FromCard(card);

        yield return new HoverTip(new LocString("cards", "GAOSHOU_WASTE_PREVIEW_HINT"));
    }

    /// <summary>
    /// 预热并**长期持有**预览要用到的资源（只做一次）。
    ///
    /// 背景：原版 <c>AssetCache</c> 对"没进房间预加载表"的资源只做临时缓存（<c>AssetCache.cs:45-60</c>），
    /// 而这些"漏网资源"会在回主菜单等边界被 Dispose（<c>UnloadMissedCacheAssets</c>，<c>AssetCache.cs:96-112</c>），
    /// 下次再用就重新走一遍**同步磁盘加载** —— 在战斗里就是一次肉眼可见的卡顿。
    /// 卡面预览悬浮用的 <c>scenes/ui/card_hover_tip.tscn</c>（连带卡框 SDF 等依赖）正好属于这类
    /// （实测日志：本会话该场景重新加载 8 次、卡框 SDF 6 次，明显与悬停垃圾宝箱成对出现）。
    ///
    /// 做法：我们自己持一份强引用。Godot 的资源缓存只要还有引用就不会真正释放，
    /// 之后原版那次 <c>ResourceLoader.Load(..., CacheMode.Reuse)</c> 只会命中内存（警告照打，但没有磁盘开销）。
    /// </summary>
    private static void WarmAssets()
    {
        if (_assetsWarmed)
            return;
        _assetsWarmed = true;

        Keep("res://scenes/ui/card_hover_tip.tscn");
        Keep($"{Entry.ResPath}/images/characters/energy_text.png");   // 本模组卡面的能量费用贴图（日志里反复重载 12 次）

        // 注意：**不**在这里预加载 13 张废品牌卡面 —— 那是十几个纹理解码，放在"首次悬停"这一刻
        // 反而会造出一次新的卡顿。轮播每次换卡只按需加载 1 张（几十毫秒级、且只在悬停期间），可以接受。
    }

    private static void Keep(string path)
    {
        try
        {
            if (ResourceLoader.Exists(path))
                _keptResources.Add(ResourceLoader.Load<Resource>(path, null, ResourceLoader.CacheMode.Reuse));
        }
        catch (System.Exception ex)
        {
            Entry.Logger.Warn($"[WastePreview] preload failed for '{path}': {ex.Message}");
        }
    }

    private static List<CardModel> All()
    {
        if (_cards is { Count: > 0 })
            return _cards;

        // ModelDb.AllCards 忽略解锁/纪元状态（悬浮释义只是展示，不涉及可抽取性），按 Id 排序保证各端一致。
        _cards = ModelDb.AllCards
            .Where(c => c is IWasteCard)
            .OrderBy(c => c.Id.ToString(), System.StringComparer.Ordinal)
            .ToList();

        return _cards;
    }
}
