using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Gaoshou.Events;

/// <summary>
/// 事件插图叠加层。
///
/// 设计：10 个事件**共用同一张 notebook 底图**（<c>images/events/_base_notebook.png</c>，2560×1200，
/// 已含"notebook 84% 居中 + 整体下移 1/30 + 右半压暗"），每个事件的插图只是一张**小尺寸透明 PNG**
/// （<c>images/events/art/&lt;事件键&gt;.png</c>，高度固定为画布高的 34%）。运行时把插图作为
/// <c>%Portrait</c> 的子节点贴在左半中心即可 —— 加事件只需一张小图，神秘洞穴换场景也只是换贴图，
/// 不必为每个场景各烘焙一张 2560×1200 整图。
///
/// 坐标空间就是 <c>%Portrait</c> 自己的框：2560×1200 逻辑像素（节点 scale 1.04 会带着子节点一起缩放）。
/// </summary>
internal static class EventArtOverlay
{
    /// <summary>叠加节点名（同一个事件重进/换页时复用同一节点）。</summary>
    private const string NodeName = "GaoshouEventArt";

    /// <summary>插图中心 X：notebook 左半（notebook 实测框 481..2078）的中心。</summary>
    private const float CenterX = 880f;

    /// <summary>插图中心 Y：画布中心 600 + 底图整体下移的 40px。</summary>
    private const float CenterY = 640f;

    /// <summary>按事件 id 推出插图名：<c>GAOSHOU_EVENT_MYSTERIOUS_CAVE</c> → <c>mysterious_cave</c>。</summary>
    public static string ArtNameForEntry(string entry)
        => entry.StartsWith("GAOSHOU_EVENT_", System.StringComparison.Ordinal)
            ? entry["GAOSHOU_EVENT_".Length..].ToLowerInvariant()
            : entry.ToLowerInvariant();

    /// <summary>把插图贴到指定宿主（一般是 <c>NEventLayout</c>）的 <c>%Portrait</c> 上。</summary>
    public static void Show(Node host, string artName)
    {
        if (!GodotObject.IsInstanceValid(host))
            return;

        if (host.GetNodeOrNull<TextureRect>("%Portrait") is not { } portrait)
        {
            Entry.Logger.Warn("[EventArt] %Portrait not found; skipping illustration.");
            return;
        }

        ShowOnPortrait(portrait, artName);
    }

    /// <summary>玩家正在看的事件房间（事件代码里换场景用）。</summary>
    public static void ShowInRoom(string artName)
    {
        if (NEventRoom.Instance?.Layout is not { } layout)
            return;

        Show(layout, artName);
    }

    private static void ShowOnPortrait(TextureRect portrait, string artName)
    {
        var texture = ResourceLoader.Load<Texture2D>(
            $"{Entry.ResPath}/images/events/art/{artName}.png", null, ResourceLoader.CacheMode.Reuse);

        if (texture == null)
        {
            // 少一张插图不该让事件崩：只警告并清掉旧图，避免张冠李戴。
            Entry.Logger.Warn($"[EventArt] illustration '{artName}' missing; clearing overlay.");
            portrait.GetNodeOrNull<TextureRect>(NodeName)?.QueueFree();
            return;
        }

        var node = portrait.GetNodeOrNull<TextureRect>(NodeName);
        if (node == null)
        {
            node = new TextureRect
            {
                Name = NodeName,
                // 纯展示：不吃鼠标事件，别挡住选项按钮的点击。
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            portrait.AddChild(node);
        }

        node.Texture = texture;
        var size = texture.GetSize();
        node.Size = size;
        node.Position = new Vector2(CenterX - size.X / 2f, CenterY - size.Y / 2f);
    }
}
