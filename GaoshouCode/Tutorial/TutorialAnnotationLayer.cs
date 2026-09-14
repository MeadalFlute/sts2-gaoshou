using System;
using Godot;

namespace Gaoshou.Tutorial;

/// <summary>
/// 指向"游戏界面上真实控件"的标注参数，坐标全部是**视口比例**（左上角 0,0）：
/// <paramref name="Target" /> = 圆心；<paramref name="Radius" /> = 半径（按视口宽度取比例，保证画出来是正圆）；
/// <paramref name="ArrowFrom" /> = 箭头起点，给 <see cref="Vector2.Zero" /> 时由面板自动放在教程面板左边缘外侧。
/// </summary>
public readonly record struct TutorialAnnotation(
    Vector2 Target,
    float Radius,
    Vector2 ArrowFrom);

/// <summary>
/// 屏幕空间的标注绘制层：淡黄高亮 + 白描边朱红圆圈 + 箭头，用来指示游戏界面上的控件
/// （例如战斗画面左上角的遗物栏）。纯 <see cref="CanvasItem._Draw" /> 绘制，不依赖美术资源，也不吃鼠标事件。
/// 注意：这一层是**屏幕空间**（铺满视口、盖在教程面板之上），不是画在配图上的。
/// </summary>
public sealed partial class TutorialAnnotationLayer : Control
{
    private static readonly Color RingColor = new(0.88f, 0.20f, 0.13f);          // 朱红圈
    private static readonly Color RingHalo = new(1f, 1f, 1f, 0.85f);             // 白色描边，深浅底都看得清
    private static readonly Color FillColor = new(1f, 0.85f, 0.25f, 0.22f);      // 淡黄高亮

    private TutorialAnnotation? _annotation;

    public TutorialAnnotation? Annotation
    {
        get => _annotation;
        set
        {
            _annotation = value;
            QueueRedraw();
        }
    }

    public TutorialAnnotationLayer()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0;
        OffsetTop = 0;
        OffsetRight = 0;
        OffsetBottom = 0;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (_annotation is not { } annotation)
            return;

        var size = Size;
        if (size.X <= 0f || size.Y <= 0f)
            return;

        var center = new Vector2(annotation.Target.X * size.X, annotation.Target.Y * size.Y);
        var radiusPixels = MathF.Max(8f, annotation.Radius * size.X);
        var radius = new Vector2(radiusPixels, radiusPixels);
        var ringWidth = MathF.Max(3f, 0.004f * size.X);

        var ring = EllipsePoints(center, radius, 72);
        DrawColoredPolygon(ring, FillColor);
        // 先画白色粗底再画朱红细线，形成描边效果（深色战斗界面上也能看清）
        var outline = Closed(ring);
        DrawPolyline(outline, RingHalo, ringWidth + MathF.Max(2f, 0.002f * size.X), true);
        DrawPolyline(outline, RingColor, ringWidth, true);

        var from = new Vector2(annotation.ArrowFrom.X * size.X, annotation.ArrowFrom.Y * size.Y);
        var toward = center - from;
        if (toward.LengthSquared() < 1f)
            return;

        var direction = toward.Normalized();
        // 箭头终点落在圆圈边缘上（稍微内缩，明确指向圈内）
        var tip = center - direction * (radiusPixels - radiusPixels * 0.18f);

        DrawLine(from, tip - direction * (ringWidth * 3f), RingHalo, ringWidth * 2.2f, true);
        DrawLine(from, tip - direction * (ringWidth * 3f), RingColor, ringWidth * 1.2f, true);

        var headLength = MathF.Max(12f, 0.020f * size.X);
        var headHalf = headLength * 0.55f;
        var normal = new Vector2(-direction.Y, direction.X);
        var basePoint = tip - direction * headLength;
        DrawColoredPolygon(
            [tip, basePoint + normal * headHalf, basePoint - normal * headHalf],
            RingHalo);
        DrawColoredPolygon(
            [
                tip - direction * (headLength * 0.12f),
                basePoint + normal * (headHalf * 0.62f),
                basePoint - normal * (headHalf * 0.62f),
            ],
            RingColor);
    }

    private static Vector2[] EllipsePoints(Vector2 center, Vector2 radius, int segments)
    {
        var points = new Vector2[segments];
        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            points[i] = center + new Vector2(MathF.Cos(angle) * radius.X, MathF.Sin(angle) * radius.Y);
        }

        return points;
    }

    /// <summary>Godot 这个版本没有 DrawPolylineClosed，手动把首点补到末尾形成闭合折线。</summary>
    private static Vector2[] Closed(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];
        Array.Copy(points, closed, points.Length);
        closed[^1] = points[0];
        return closed;
    }
}
