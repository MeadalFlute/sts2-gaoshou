using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Nodes.Events;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

// 「文本没变就别重播进场动画」。
//
// 背景（读原版源码得到的确切链路）：
//   NEventRoom.RefreshEventState 每次收到 StateChanged 都会**无条件**调
//     SetDescription(...) → NEventLayout.SetDescription → AnimateIn()
//     SetOptions(...)     → NEventLayout.AddOptions → AnimateButtonsIn() → 每个按钮 AnimateIn()
//   而这两段动画都很长：
//     * 描述：TweenInterval(0.5s) + 1.0s 淡入 + 1.0s visible_ratio 逐字显现（还有 0.25s 延迟）
//     * 按钮：0.5s + index*0.2s 延迟 + 0.5s 滑入，而且**动画结束前按钮是禁用的**（Finished += EnableButton）
//   对「超级强化机」这种翻页只换计数、文案一模一样的循环事件，这纯粹是拖沓。
//
// 做法：只对**本模组的事件**（Id 以 GAOSHOU_EVENT 开头）生效，且仅当新描述与上次设置的描述
// **完全一致**时跳过。跳过按钮动画时补调 EnableButton()，保证按钮立刻可用（原版要等动画完）。
// 首次展示、以及文案真的变了的页，动画照旧。
//
// 作用域刻意收窄到自家事件：原版也有几个重复文案的循环事件（滑脚木桥的 LOOP、巨型花等），
// 但不擅自改原版观感。想对所有事件放开，把 IsOurEvent 的两个判断删掉即可。
internal static class EventPageAnimationState
{
    internal sealed class Page
    {
        /// <summary>上一次设置给这个布局的描述文本。</summary>
        internal string? LastText;

        /// <summary>上一次设置是否属于「文本没变」（供按钮动画跳过判断）。</summary>
        internal bool LastWasDuplicate;
    }

    private static readonly ConditionalWeakTable<NEventLayout, Page> Table = new();

    internal static Page For(NEventLayout layout) => Table.GetOrCreateValue(layout);

    /// <summary>这个布局是不是本模组的事件（只对自家事件动手）。</summary>
    internal static bool IsOurEvent(NEventLayout layout)
    {
        // _event 是 protected 字段，靠 csproj 里的 Krafs.Publicizer（Publicize Include="sts2"）可访问。
        var entry = layout._event?.Id?.Entry;
        return entry != null && entry.StartsWith("GAOSHOU_EVENT", System.StringComparison.Ordinal);
    }
}

/// <summary>事件描述：与上次完全相同时跳过整个 SetDescription（不重设文本、不播淡入与逐字显现）。</summary>
public sealed class SkipDuplicateEventDescriptionAnimation : IPatchMethod
{
    public static string PatchId => "gaoshou_skip_dup_event_description_animation";

    public static string Description => "skip the event description fade-in when the text is unchanged";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(NEventLayout), "SetDescription"),
    ];

    /// <summary>返回 false = 跳过原方法。</summary>
    public static bool Prefix(NEventLayout __instance, string description)
    {
        if (!EventPageAnimationState.IsOurEvent(__instance))
            return true;

        var page = EventPageAnimationState.For(__instance);
        var duplicate = page.LastText == description;
        page.LastText = description;
        page.LastWasDuplicate = duplicate;
        return !duplicate;
    }
}

/// <summary>事件选项按钮：若刚才那段描述没变，就不播滑入/淡入，直接启用按钮。</summary>
public sealed class SkipDuplicateEventOptionAnimation : IPatchMethod
{
    public static string PatchId => "gaoshou_skip_dup_event_option_animation";

    public static string Description => "skip the event option slide-in when the description was unchanged";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(NEventLayout), "AnimateButtonsIn"),
    ];

    /// <summary>返回 false = 跳过原方法；跳过前必须自己把按钮启用（原版靠动画结束回调启用）。</summary>
    public static bool Prefix(NEventLayout __instance)
    {
        if (!EventPageAnimationState.IsOurEvent(__instance))
            return true;

        if (!EventPageAnimationState.For(__instance).LastWasDuplicate)
            return true;

        foreach (var button in __instance.OptionButtons)
            button.EnableButton();

        return false;
    }
}
