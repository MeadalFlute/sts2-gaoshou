using Gaoshou.Cards;
using Gaoshou.Keywords;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 手牌高光的模组配色（**单一颜色**，不改材质、不写着色器）：
///   * **流转**就绪 → 蓝 (93, 94, 253)（与流转图标同色）
///   * **奇迹**就绪 → 橙 (253, 180, 45)（与奇迹图标同色）
///   * 两者都就绪 → **品红** (255, 115, 217)：与上面两色都拉得开，一眼能看出"两个都齐了"
///
/// 机制：手牌高光是 <c>NCardHighlight</c>（TextureRect）+ ShaderMaterial，原版
/// <c>NHandCardHolder.UpdateCard</c> 只把节点的 <c>Modulate</c> 设成 gold / red / playableColor 三个预设；
/// 而那个着色器（materials/outline_texturerect.gdshader）的数学效果**就是"整块高光 = Modulate 的颜色"**
/// （贴图只提供形状），所以换颜色只需要改 Modulate。
///
/// 历史：曾试过换自定义着色器做"红蓝转圈"，两轮都在实机上表现为"一坨白色"（Modulate 与着色器的上色叠加
/// 难以稳定控制），2026-09-24 应作者要求改回单色方案，相关着色器与材质切换代码已删除。
///
/// 纯本地 UI：不碰任何游戏状态，联机各端各画各的。
/// </summary>
public sealed class GaoshouGlowColorPatch : IPatchMethod
{
    // ---- 配色（前两个取自各自的词条图标；要调直接改这三行）----

    /// <summary>流转就绪：蓝。</summary>
    public static readonly Color FlowColor = new(93 / 255f, 94 / 255f, 253 / 255f, 0.98f);

    /// <summary>奇迹就绪：橙。</summary>
    public static readonly Color MiracleColor = new(253 / 255f, 180 / 255f, 45 / 255f, 0.98f);

    /// <summary>流转与奇迹**同时**就绪：品红（与蓝/橙都拉得开，辨识度最高）。</summary>
    public static readonly Color BothColor = new(1f, 115 / 255f, 217 / 255f, 0.98f);

    public static string PatchId => "gaoshou_glow_color";

    public static string Description => "flow/miracle ready hand highlights use the mod's own single colors";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(NHandCardHolder), "UpdateCard"),
    ];

    /// <summary>
    /// 原版逻辑跑完后覆盖颜色（同一帧内，不会先闪一下金色）。
    /// 两种都没就绪时**直接返回**，把配色交还给原版（可打出=青、不可打出=红）——
    /// 因此也不需要"还原"逻辑：下一次 UpdateCard 会重新写成原版色。
    /// </summary>
    public static void Postfix(NHandCardHolder __instance)
    {
        // 同 KeywordStateIconPatch：这是原版 UI 回调，抛异常会打断手牌渲染/布局 ⇒ 失败一律吞掉。
        try
        {
            Apply(__instance);
        }
        catch (System.Exception e)
        {
            if (_warned)
                return;
            _warned = true;
            Entry.Logger.Warn($"[GlowColor] recoloring skipped after an error: {e.Message}");
        }
    }

    private static bool _warned;

    private static void Apply(NHandCardHolder __instance)
    {
        var cardNode = __instance.CardNode;
        var card = cardNode?.Model;
        if (card == null)
            return;

        // 与卡牌的 ShouldGlowGoldInternal 用同一对判定（都只认"在手牌里"）。
        // 但弃牌/选牌界面下手牌仍在 Hand 堆里，光靠牌堆判定挡不住（原版此时由
        // CardSelectorPrefs.ShouldGlowGold 接管泛光）⇒ 再用界面闸门挡一层，避免改色照到选牌界面。
        if (!GaoshouKeywordMechanics.IsHandGlowAllowed())
            return;

        var flow = GaoshouFlowTracker.IsFlowGlowReady(card);
        var miracle = MiracleCounter.IsMiracleGlowReady(card);
        if (!flow && !miracle)
            return;

        var highlight = cardNode!.CardHighlight;
        if (highlight == null)
            return;

        // 注意：不要在这里再调 AnimShow() —— 原版同一次 UpdateCard 里已经因为它 ShouldGlowGold 为真而
        // AnimShow 过了，重复调用会 kill+重启 tween，看起来会抖一下。
        highlight.Modulate = (flow, miracle) switch
        {
            (true, true) => BothColor,
            (true, false) => FlowColor,
            _ => MiracleColor,
        };
    }
}

/// <summary>
/// 「呼吸」在**选牌/弃牌界面**里获得与奇巧(Sly)完全相同的**原版金色高光**。
///
/// 为什么不沿用"给 <c>CardSelectorPrefs.ShouldGlowGold</c> 赋我们的委托"：2026-09-30 正式版事故 ——
/// MonoMod 的 JIT hook 在编译 <c>AmplifyAsync</c> 里那个 lambda（编译器生成的 <c>&lt;&gt;c.b__0_0</c>）时
/// 抛 <c>ArgumentException</c> ⇒ 首次进入被补丁化的 <c>NHandCardHolder.UpdateCard</c> 时整条链中断
/// （确认键不 Enable / 幽灵卡位 / hovertip 错位）。
/// ⇒ 现在改为 Harmony **postfix 打在原版"要不要金色高光"的判定**上
///   （<c>NHandCardHolder.ShouldGlowGold</c> 的 getter）⇒ 原版自己走它自己的金色分支
///   （<c>AnimShow()</c> + <c>Modulate = NCardHighlight.gold</c>）⇒ 观感与奇巧 **100% 一致** ✓
///   而且我们的 postfix 是**普通静态方法**（与其它正常工作的补丁同形，**不是**编译器生成的 lambda ✗），
///   不引入任何委托到 UI 回调里 ⇒ 不会再踩 MonoMod 的 JIT 失败 ✓
///
/// 纯观感：该 getter 只被 <c>NHandCardHolder.UpdateCard</c> 使用（已核对两版反编译），不参与玩法判定 ✓
/// </summary>
public sealed class BreathDiscardGlowPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_breath_discard_glow";

    public static string Description => "Breath glows gold in hand-selection screens, exactly like Sly";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        // 私有属性 getter ⇒ 按名定位（Krafs.Publicizer 已把 sts2 的私有成员公开化）
        PatchTarget.Getter(typeof(NHandCardHolder), "ShouldGlowGold"),
    ];

    public static void Postfix(NHandCardHolder __instance, ref bool __result)
    {
        if (__result)
            return;   // 原版已判为金色（奇巧等）⇒ 不覆盖它自己的判定

        try
        {
            if (__instance.CardNode?.Model is not Breath)
                return;

            // 只在"手牌选择模式"（弃牌/选牌界面）里补这层金色，与奇巧口径一致；正常出牌手牌不亮。
            var hand = MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance?.Ui?.Hand;
            if (hand == null || !hand.IsInCardSelection)
                return;

            __result = true;
        }
        catch (System.Exception e)
        {
            if (_warned)
                return;
            _warned = true;
            Entry.Logger.Warn($"[BreathGlow] skipped after an error: {e.Message}");
        }
    }

    private static bool _warned;
}
