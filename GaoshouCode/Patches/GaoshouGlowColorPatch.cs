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
        var cardNode = __instance.CardNode;
        var card = cardNode?.Model;
        if (card == null)
            return;

        // 与卡牌的 ShouldGlowGoldInternal 用同一对判定（都只认"在手牌里"）。
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
