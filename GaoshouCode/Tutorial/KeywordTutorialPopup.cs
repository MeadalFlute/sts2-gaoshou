using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Ftue;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.addons.mega_text;

namespace Gaoshou.Tutorial;

/// <summary>
/// 关键词教程的"讲解弹窗"（2026-10-11 用户口径：**照原版能力牌 FTUE 的样子做，不要自己画** ✗）。
///
/// 做法 = 直接复用原版那两套东西：
///   * 弹窗：实例化原版场景 <c>res://scenes/ftue/power_card_ftue.tscn</c>（= <see cref="NPowerCardFtue" />）并塞进
///     <c>NModalContainer</c> —— 它自带 <c>NFtue</c> 的**暗色遮罩**、右下角的对话气泡弹窗与确认按钮；
///     它 <c>_Ready</c> 里会把**触发教程的那张牌抬到遮罩之上**（<c>_card.ZIndex++</c>，确认时还原 ✓）
///     ⇒ 正好就是用户要的"触发教程的那张牌以外的区域上一层暗色遮罩" ✓。
///   * 悬浮提示：走**原版那套** <see cref="NHoverTipSet" /> + <c>SetAlignmentForCardHolder</c>
///     —— 这就是原版鼠标悬浮一张牌时走的同一条代码路径（<c>NCardHolder.cs:423</c>），
///     位置、换行、贴边翻转、跟着牌走全部照原版 ✓（自己摆位一定摆不对，2026-10-11 两次截图实证 ✗）。
///
/// ⚠️ 为什么原版直接这么干看不见提示（2026-10-11 查证 <c>scenes/game.tscn</c> 的节点顺序）：
///     <c>HoverTipsContainer</c> 排在 <c>ModalContainer</c> **之前**（同层、同 z）⇒ 遮罩一开就把提示盖住 ✗，
///     所以原版 FTUE 从不显示悬浮提示。我们的办法：弹窗开着的时候把该容器的 <c>ZIndex</c> 临时抬到
///     <see cref="TipZIndex" />（高于被抬起的卡牌 <c>ZIndex == 1</c>），关窗时还原 ✓。
///     这样提示永远画在卡面之上 —— 不会再出现"卡牌遮住了 hovertips" ✗。
///
///   * 我们只改两处文字：标题（气泡上方那行）= 关键词名，正文 = 这个关键词的逐条说明 ✓；
///     外加把气泡**从右下角挪到那张牌左侧、与牌的顶部对齐**（右侧要摆提示，右下角太挤 ✗ 2026-10-12 用户口径，
///     见 <see cref="KeywordTutorialLayout" />：共享定位与避让，版式尺寸照原版）。
///   * 点确认后：原版逻辑会关窗并还原卡牌 ZIndex ✓，我们只在旁边挂一个"还有下一个关键词就接着弹"的回调 ✓。
///   * 关窗（含被别的模态 <c>Clear()</c> 掉）时：提示控件与容器层级一起还原 ✓（见 <see cref="HoverTipsRestoreGuard" />）。
/// </summary>
internal static class KeywordTutorialPopup
{
    /// <summary>
    /// 悬浮提示层要抬到的 z。被抬起的卡牌是 1（原版 <c>_card.ZIndex++</c>），留足余量即可 ✓。
    /// </summary>
    internal const int TipZIndex = 32;

    /// <summary>最多同时展示几条提示（牌的悬浮提示可能有很多条，全放会占满屏幕 ✗）。</summary>
    private const int MaxTips = 3;

    /// <summary>
    /// 弹出讲解。返回 false 表示没弹成（调用方据此选择"不标记、下次再试" ✓）。
    /// </summary>
    public static bool TryShow(CardModel card, Control holder, CardKeyword keyword, string title, string body,
        Action onConfirmed)
    {
        try
        {
            var container = NModalContainer.Instance;
            var popup = NPowerCardFtue.Create(holder);
            if (container == null || popup == null)
                return false;

            // 入树 ⇒ _Ready 跑完（原版文案已设、那张牌已抬到遮罩之上 ✓）⇒ 现在覆盖成我们的文案 ✓
            // ⚠️ 第二参 `showBackstop` 一定要用默认的 true：**暗色遮罩就是它开的** ✗
            //    （本模组自己的教程面板传的是 false，那是因为它自己在 Start() 里调了 ShowBackstop() ✓；
            //      这里漏了 ⇒ 2026-10-11 实机"完全没有暗色遮罩" ✗）
            container.Add(popup);

            // The vanilla scene has a fixed-position pointer for its original layout.
            if (popup.GetNodeOrNull<CanvasItem>("Arrow") is { } arrow)
                arrow.Hide();

            if (popup.GetNodeOrNull<MegaLabel>("FtuePopup/Header") is { } header)
                header.SetTextAutoSize(title);

            if (popup.GetNodeOrNull<MegaRichTextLabel>("FtuePopup/DescriptionContainer/Description") is { } description)
            {
                description.Text = body;
                // MegaRichTextLabel 的富文本特效由 SetTextAutoSize 安装；这里文字是直接赋值的，补一次解析 ✓
                description.ParseBbcode(description.Text);
            }

            AttachTips(popup, holder, card, keyword);
            HookConfirm(popup, onConfirmed);
            return true;
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[KeywordTutorial] popup failed: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 用**原版那套悬浮提示**把那张牌的提示显示出来（"像是鼠标放上去一样" ✓）。
    ///
    /// 教学关键词那条排最前（最相关 ✓），其余按原顺序补齐，最多 <see cref="MaxTips" /> 条。
    /// 位置完全交给原版 <c>SetAlignmentForCardHolder</c>：贴牌右侧、放不下改左侧、跟着牌走 ✓。
    ///
    /// ⚠️ 调用前必须确认奖励牌行**已经摆好版**（卡位不是 0）—— 本方法算出来的是绝对坐标，
    ///    布局没跑完就算错（见 <see cref="GaoshouTutorial" /> 里等布局的那几帧 ✓）。
    /// </summary>
    private static void AttachTips(Control popup, Control holder, CardModel card, CardKeyword keyword)
    {
        Control? owner = null;
        Control? layer = null;
        var previousZ = 0;
        try
        {
            // 原版贴牌定位只认 NCardHolder（要用它的 Hitbox）⇒ 不是的话宁可不显示，也不要乱摆 ✗
            if (holder is not NCardHolder cardHolder)
                return;

            var tips = new List<IHoverTip>(MaxTips);
            tips.Add(HoverTipFactory.FromKeyword(keyword));
            foreach (var tip in card.HoverTips)
            {
                if (tips.Count >= MaxTips)
                    break;
                tips.Add(tip);
            }

            // A reward card may already have mouse-hover tips registered under this owner.
            NHoverTipSet.Remove(cardHolder);
            // Keep tutorial tips alive when the pointer leaves the reward card.
            var tipsOwner = popup;
            var set = NHoverTipSet.CreateAndShow(tipsOwner, tips);
            if (set == null)
                return;

            owner = tipsOwner;
            set.SetAlignmentForCardHolder(cardHolder);

            layer = RaiseHoverTipsLayer(out previousZ);
            popup.AddChild(new KeywordTutorialLayout
            {
                Holder = cardHolder,
                Tips = set,
                Bubble = popup.GetNode<Control>("FtuePopup"),
                Bounds = popup,
            });

            popup.AddChild(new HoverTipsRestoreGuard
            {
                TipsOwner = owner,
                Layer = layer,
                LayerPreviousZ = previousZ,
            });
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[KeywordTutorial] tips failed: {e.Message}");
            HoverTipsRestoreGuard.Restore(owner, layer, previousZ);
        }
    }

    /// <summary>
    /// 把全局提示容器 <c>%HoverTipsContainer</c> 抬到模态容器之上（否则被暗色遮罩盖住 ✗）。
    /// 返回被抬起的容器，<paramref name="previousZ" /> 传出它原来的 z（还原用 ✓）。
    /// </summary>
    private static Control? RaiseHoverTipsLayer(out int previousZ)
    {
        previousZ = 0;
        if (NGame.Instance?.HoverTipsContainer is not Control container)
            return null;

        previousZ = container.ZIndex;
        container.ZIndex = Mathf.Max(previousZ + 1, TipZIndex);
        return container;
    }

    /// <summary>
    /// 挂确认回调：原版自己的处理（关窗 + 还原卡牌 ZIndex）照常跑 ✓，我们再接着弹下一个关键词 ✓。
    /// （用 lambda 是安全的：这条路径不是被 MonoMod hook 的补丁方法；本模组既有 FTUE 面板同样用 lambda ✓）
    /// </summary>
    private static void HookConfirm(Node popup, Action onConfirmed)
    {
        if (popup.GetNodeOrNull<NButton>("FtuePopup/FtueConfirmButton") is not { } button)
            return;

        button.Connect(NClickableControl.SignalName.Released, Callable.From((Action<NButton>)(_ => onConfirmed())));
    }
}

internal sealed partial class KeywordTutorialLayout : Node
{
    public NCardHolder Holder = null!;
    public NHoverTipSet Tips = null!;
    public Control Bubble = null!;
    public Control Bounds = null!;

    public override void _Ready()
    {
        // Run after the native tip follower and container layout have updated.
        ProcessPriority = 100;
        Bubble.SetAnchorsPreset(Control.LayoutPreset.TopLeft, true);
    }

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(Holder) || !Holder.IsInsideTree()
            || !GodotObject.IsInstanceValid(Tips) || Tips.IsQueuedForDeletion())
            return;

        var column = Tips.GetNodeOrNull<Control>("textHoverTipContainer");
        if (column == null || column.Size.X <= 0 || column.Size.Y <= 0)
            return;

        // All measurements below are global rectangles, including scaled UI bounds.
        var bounds = Bounds.GetGlobalRect();
        var card = Holder.Hitbox.GetGlobalRect();
        var bubbleSize = Bubble.GetGlobalRect().Size;
        var scale = Bounds.GetGlobalTransform().Scale.Abs();
        var margin = 24f * scale;
        var gap = 12f * scale;
        if (bounds.Size.X <= 0 || bounds.Size.Y <= 0 || card.Size.Y <= 0)
            return;

        Tips.SetExtraFollowOffset(Vector2.Zero);
        Tips.SetAlignmentForCardHolder(Holder);
        var tips = column.GetGlobalRect();
        var left = card.Position.X - gap.X - bubbleSize.X;
        var right = card.End.X + gap.X;
        var minX = bounds.Position.X + margin.X;
        var maxX = Mathf.Max(minX, bounds.End.X - margin.X - bubbleSize.X);
        var minY = bounds.Position.Y + margin.Y;
        var maxY = Mathf.Max(minY, bounds.End.Y - margin.Y - bubbleSize.Y);

        // Prefer the left side, but follow the native tooltip's actual side.
        var x = tips.GetCenter().X < card.GetCenter().X ? right : left;
        var y = Mathf.Clamp(card.Position.Y, minY, maxY);
        var proposed = new Rect2(new Vector2(x, y), bubbleSize);
        if (x < minX || x > maxX || proposed.Intersects(tips))
        {
            var alternateX = x == left ? right : left;
            var alternate = new Rect2(new Vector2(alternateX, y), bubbleSize);
            if (alternateX >= minX && alternateX <= maxX && !alternate.Intersects(tips))
            {
                x = alternateX;
            }
            else
            {
                // Narrow layouts use free space below or above, never clamp over the card.
                x = Mathf.Clamp(card.GetCenter().X - bubbleSize.X * 0.5f, minX, maxX);
                y = card.End.Y + gap.Y;
                if (y > maxY)
                    y = card.Position.Y - gap.Y - bubbleSize.Y;
                y = Mathf.Clamp(y, minY, maxY);
            }
        }

        Bubble.GlobalPosition = new Vector2(x, y);
        var bubble = Bubble.GetGlobalRect();
        if (!bubble.Intersects(tips))
            return;

        var up = bubble.Position.Y - gap.Y - tips.End.Y;
        var down = bubble.End.Y + gap.Y - tips.Position.Y;
        var shift = tips.Position.Y + up >= bounds.Position.Y + margin.Y
            ? up
            : tips.End.Y + down <= bounds.End.Y - margin.Y ? down : 0f;
        if (!Mathf.IsZeroApprox(shift))
        {
            // The native follower applies offsets in global coordinates.
            var offset = new Vector2(0, shift);
            Tips.SetExtraFollowOffset(offset);
            Tips.GlobalPosition += offset;
        }
    }
}

/// <summary>
/// 挂在弹窗下的收尾节点：弹窗一离开场景树（原版确认按钮、或被别的模态 <c>Clear()</c> 掉）就还原提示 ✓。
/// 用节点（而不是委托）是为了不引入闭包，且生命周期天然跟着弹窗走 ✓。
/// </summary>
internal sealed partial class HoverTipsRestoreGuard : Node
{
    public Control? TipsOwner;
    public Control? Layer;
    public int LayerPreviousZ;

    public override void _ExitTree()
    {
        Restore(TipsOwner, Layer, LayerPreviousZ);
        TipsOwner = null;
        Layer = null;
    }

    /// <summary>移除提示控件 + 还原提示容器层级（关窗、或中途出错时都要跑 ✓）。</summary>
    public static void Restore(Control? owner, Control? layer, int previousZ)
    {
        if (owner != null && GodotObject.IsInstanceValid(owner))
            NHoverTipSet.Remove(owner);

        if (layer != null && GodotObject.IsInstanceValid(layer) && layer.ZIndex == KeywordTutorialPopup.TipZIndex)
            layer.ZIndex = previousZ;
    }
}
