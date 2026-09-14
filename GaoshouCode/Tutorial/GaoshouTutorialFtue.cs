using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Ftue;
using MegaCrit.Sts2.addons.mega_text;

namespace Gaoshou.Tutorial;

/// <summary>
/// 教程的一页：整页底图（左半是配图、右半是便签纸）+ 写在纸上的标题与正文。
/// <paramref name="LeftText" />/<paramref name="LeftTextRect" /> 是作者在**左半页**预留的第二个文字框
/// （坐标 = 底图比例，从 3920×2475 的原始素材上量得），用来放本该标注在配图旁的说明句。
/// </summary>
public readonly record struct GaoshouTutorialPage(
    string Title,
    string Body,
    string? ImagePath = null,
    TutorialAnnotation? Annotation = null,
    string? LeftText = null,
    Rect2? LeftTextRect = null);

/// <summary>
/// 高手教程的模态面板（纯代码构建，无 .tscn）。
/// 继承 <see cref="NFtue" /> 以复用原版 FTUE 的语义：输入阻塞（IScreenContext + NHotkeyManager）、
/// CloseFtue()（= NModalContainer.Clear() + QueueFreeSafely()）；backstop 由 Start() 里 ShowBackstop() 打开，
/// 与原版 NCombatRulesFtue 的做法一致。
///
/// 版式：底图（整页便签）**居中缩放到视口的 78%×84%**，不铺满屏幕——留出四周的游戏画面，
/// 这样第 3 页指向"战斗界面左上角遗物栏"的圈/箭头才看得到实物（底图由作者做好，代码不在图上画任何东西）。
/// 文字按"底图比例"锚定在右半便签纸上：
///   标题 x 54.5%~78%、y 11.5%~19.6%（右上角有装饰贴纸，标题只占左侧）
///   正文 x 54.5%~88.8%、y 28.5%~79.5%
///   页码 x 7.5%~30%、y 88.5%~95.5%（底部深色带，浅色字）
///   按钮 x 58%~95.5%、y 89.2%~96.2%
/// 这些比例是按 3920x2475 的原始底图量出来的（见 _workspace/_imgwork/detail_map.py），底图换了要重新量。
///
/// 富文本要点（踩坑记录）：
///   - RichTextLabel 的 BbcodeEnabled 默认为 false，纯代码创建时必须显式打开，否则 BBCode 会原样显示。
///   - 游戏的自定义特效只由场景序列化或 SetTextAutoSize 触发安装，所以设完文本要再 ParseBbcode 一次。
///   - 纸面是浅色，字色必须显式覆盖（default_color / font_color），否则会继承深色 UI 主题的浅色字。
///   - 标题用 MegaLabel（继承 Label，不支持 BBCode），所以标题文案里不能带标签。
/// </summary>
public sealed partial class GaoshouTutorialFtue : NFtue
{
    // ---- 版式比例（相对整张底图）----
    // 便签内框（作者在 3920×2475 素材上量得）：右内边 x=3480、下内边 y=2020、左内边 x=510。
    // 按钮行/页码都从内框角上再让出一圈间隙（NavGapPixels，素材像素），
    // 「下一页」（或末页的「知道了」）的最右下角 = (3480-间隙, 2020-间隙)。
    private const float MaterialWidth = 3920f;
    private const float MaterialHeight = 2475f;
    private const float NotebookInnerRight = 3480f;
    private const float NotebookInnerBottom = 2020f;
    private const float NotebookInnerLeft = 510f;
    private const float NavGapPixels = 48f;

    private const float ButtonHeightRatio = 0.058f;
    private const float ButtonRowWidthRatio = 0.375f;

    private static readonly float NavRightRatio = (NotebookInnerRight - NavGapPixels) / MaterialWidth;
    private static readonly float NavBottomRatio = (NotebookInnerBottom - NavGapPixels) / MaterialHeight;
    private static readonly float NavTopRatio = NavBottomRatio - ButtonHeightRatio;

    private static readonly Rect2 TitleRect = new(0.545f, 0.123f, 0.235f, 0.081f);
    // 正文下边收到按钮行上方，保证长文案也不会压到按钮
    private static readonly Rect2 BodyRect = new(0.545f, 0.285f, 0.343f, NavTopRatio - 0.010f - 0.285f);
    private static readonly Rect2 ButtonsRect =
        new(NavRightRatio - ButtonRowWidthRatio, NavTopRatio, ButtonRowWidthRatio, ButtonHeightRatio);
    private static readonly Rect2 CounterRect =
        new((NotebookInnerLeft + NavGapPixels) / MaterialWidth, NavTopRatio, 0.225f, ButtonHeightRatio);

    private static readonly Color InkColor = new(0.20f, 0.16f, 0.13f);           // 纸上的深墨色

    // 整页底图占视口的比例（居中，不铺满：留出游戏画面，便于指向界面上的控件）
    private const float StageViewportWidthRatio = 0.78f;
    private const float StageViewportHeightRatio = 0.84f;

    private const int FallbackImageWidth = 1920;
    private const int FallbackImageHeight = 1212;

    private readonly List<GaoshouTutorialPage> _pages = [];

    private string _confirmText = "知道了";
    private string _prevText = "上一页";
    private string _nextText = "下一页";

    private Control _stage = null!;
    private TextureRect _background = null!;
    private TutorialAnnotationLayer _pointer = null!;
    private MegaLabel _title = null!;
    private MegaRichTextLabel _body = null!;
    private MegaRichTextLabel _leftText = null!;
    private MegaLabel _pageCounter = null!;
    private HBoxContainer _buttons = null!;
    private Button _prevButton = null!;
    private Button _nextButton = null!;
    private Button _confirmButton = null!;

    private int _index;
    private TaskCompletionSource<bool>? _confirmed;

    /// <summary>Godot 需要一个无参构造（脚本实例化路径）；内容由 <see cref="Create" /> 填充。</summary>
    public GaoshouTutorialFtue()
    {
        BuildUi();
    }

    /// <summary>构建面板；失败返回 null（调用方据此选择"不标记、下次再试"）。</summary>
    public static GaoshouTutorialFtue? Create(
        List<GaoshouTutorialPage> pages, string confirmText, string prevText, string nextText)
    {
        try
        {
            var panel = new GaoshouTutorialFtue();
            panel._pages.Clear();
            panel._pages.AddRange(pages);
            panel._confirmText = confirmText;
            panel._prevText = prevText;
            panel._nextText = nextText;
            panel._index = 0;
            panel.ApplyButtonTexts();
            // 渲染放到 Start()（节点已加入树之后）再做，避免在树外调用 CallDeferred/自动字号。
            return panel;
        }
        catch (Exception e)
        {
            GaoshouTutorial.LogError($"[GaoshouTutorialFtue] build failed: {e}");
            return null;
        }
    }

    /// <summary>开始展示：打开压暗遮罩并渲染第一页（对应原版 FTUE 的 Start()）。</summary>
    public void Start()
    {
        NModalContainer.Instance?.ShowBackstop();
        Render();
        _confirmButton.GrabFocus();
    }

    /// <summary>等待玩家点"确认"关闭（本次两处都只是"确认即关"，不阻塞战斗流程）。</summary>
    public Task WaitForPlayerToConfirm()
    {
        _confirmed ??= new TaskCompletionSource<bool>();
        return _confirmed.Task;
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // 不用容器包一层：直接算出整页尺寸并居中摆放，尺寸在 ApplyStageLayout 里按底图比例算，
        // 这样文字框（锚定在比例上）能立刻拿到正确的 rect，字号自适应不会算错。
        _stage = new Control();
        AddChild(_stage);

        Resized += OnPanelResized;

        _background = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        FillParent(_background);
        _stage.AddChild(_background);

        _title = new MegaLabel
        {
            AutoSizeEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _title.AddThemeColorOverride("font_color", InkColor);
        ApplyRatioRect(_title, TitleRect);
        _stage.AddChild(_title);

        _body = new MegaRichTextLabel
        {
            AutoSizeEnabled = true,
            ScrollActive = false,
            BbcodeEnabled = true,
        };
        _body.AddThemeColorOverride("default_color", InkColor);
        ApplyRatioRect(_body, BodyRect);
        _stage.AddChild(_body);

        // 左半页预留的说明文字框（不是每页都有，位置由 page.LeftTextRect 给）
        _leftText = new MegaRichTextLabel
        {
            AutoSizeEnabled = true,
            ScrollActive = false,
            BbcodeEnabled = true,
            Visible = false,
        };
        _leftText.AddThemeColorOverride("default_color", InkColor);
        ApplyRatioRect(_leftText, BodyRect);
        _stage.AddChild(_leftText);

        _pageCounter = new MegaLabel
        {
            AutoSizeEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _pageCounter.AddThemeColorOverride("font_color", InkColor);
        ApplyRatioRect(_pageCounter, CounterRect);
        _stage.AddChild(_pageCounter);

        _buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _buttons.AddThemeConstantOverride("separation", 16);
        ApplyRatioRect(_buttons, ButtonsRect);
        _stage.AddChild(_buttons);

        _prevButton = MakeButton();
        _prevButton.Pressed += () => GoTo(_index - 1);
        _buttons.AddChild(_prevButton);

        _nextButton = MakeButton();
        _nextButton.Pressed += () => GoTo(_index + 1);
        _buttons.AddChild(_nextButton);

        _confirmButton = MakeButton();
        _confirmButton.Pressed += Close;
        _buttons.AddChild(_confirmButton);

        // 指针层是屏幕空间的，加在最后 → 盖在整页底图之上，箭头/圈不会被底图挡住。
        _pointer = new TutorialAnnotationLayer();
        AddChild(_pointer);
    }

    private static Button MakeButton()
    {
        return new Button();
    }

    private static void FillParent(Control control)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = 0;
        control.OffsetTop = 0;
        control.OffsetRight = 0;
        control.OffsetBottom = 0;
    }

    /// <summary>把控件按"父节点尺寸的比例"摆放（比例相对底图，缩放后依然对齐）。</summary>
    private static void ApplyRatioRect(Control control, Rect2 rect)
    {
        control.AnchorLeft = rect.Position.X;
        control.AnchorTop = rect.Position.Y;
        control.AnchorRight = rect.Position.X + rect.Size.X;
        control.AnchorBottom = rect.Position.Y + rect.Size.Y;
        control.OffsetLeft = 0;
        control.OffsetTop = 0;
        control.OffsetRight = 0;
        control.OffsetBottom = 0;
    }

    private void ApplyButtonTexts()
    {
        _prevButton.Text = _prevText;
        _nextButton.Text = _nextText;
        _confirmButton.Text = _confirmText;
    }

    private void GoTo(int index)
    {
        if (_pages.Count == 0)
            return;
        _index = Math.Clamp(index, 0, _pages.Count - 1);
        Render();
    }

    private void Render()
    {
        if (_pages.Count == 0)
            return;

        var page = _pages[_index];
        var texture = LoadTexture(page.ImagePath);

        ApplyStageLayout(texture);
        _background.Texture = texture;
        _background.Visible = texture != null;
        _pointer.Annotation = ResolveAnnotation(page.Annotation);

        _title.SetTextAutoSize(page.Title);
        _body.SetTextAutoSize(page.Body);
        // SetTextAutoSize 内部才会安装游戏的富文本特效，装完必须再解析一次。
        _body.ParseBbcode(_body.Text);
        RenderLeftText(page);
        _pageCounter.SetTextAutoSize($"{_index + 1} / {_pages.Count}");

        _prevButton.Visible = _index > 0;
        _nextButton.Visible = _index < _pages.Count - 1;
        _confirmButton.Visible = _index >= _pages.Count - 1;

        var focus = _confirmButton.Visible ? _confirmButton : _nextButton;
        if (focus.Visible)
            focus.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>左半页的预留文字框：没有文案或没给位置就隐藏（内容清空，避免残留上一页的字）。</summary>
    private void RenderLeftText(GaoshouTutorialPage page)
    {
        var hasLeftText = !string.IsNullOrEmpty(page.LeftText) && page.LeftTextRect is { } rect;
        _leftText.Visible = hasLeftText;
        if (!hasLeftText)
        {
            _leftText.SetTextAutoSize(string.Empty);
            return;
        }

        ApplyRatioRect(_leftText, page.LeftTextRect!.Value);
        _leftText.SetTextAutoSize(page.LeftText!);
        _leftText.ParseBbcode(_leftText.Text);
    }

    /// <summary>
    /// 把"指向界面控件"的标注解析成可直接绘制的屏幕比例：箭头起点没给的话，
    /// 自动放在教程面板左边缘外侧、目标略下方，这样箭头总是从页面指向目标控件。
    /// </summary>
    private TutorialAnnotation? ResolveAnnotation(TutorialAnnotation? annotation)
    {
        if (annotation is not { } spec)
            return null;

        if (spec.ArrowFrom != Vector2.Zero)
            return spec;

        var viewportWidth = MathF.Max(1f, Size.X);
        var panelLeftRatio = _stage.Position.X / viewportWidth;
        return spec with { ArrowFrom = new Vector2(panelLeftRatio - 0.008f, spec.Target.Y + 0.055f) };
    }

    /// <summary>按底图比例把整页缩小居中到视口内（四周留出游戏画面），同时同步字号/按钮尺寸。</summary>
    private void ApplyStageLayout(Texture2D? texture)
    {
        var imageWidth = texture != null ? texture.GetWidth() : FallbackImageWidth;
        var imageHeight = texture != null ? texture.GetHeight() : FallbackImageHeight;

        var viewport = GetViewportRect().Size;
        var scale = MathF.Min(
            viewport.X * StageViewportWidthRatio / imageWidth,
            viewport.Y * StageViewportHeightRatio / imageHeight);
        if (scale <= 0f)
            scale = 1f;

        var stageWidth = imageWidth * scale;
        var stageHeight = imageHeight * scale;
        _stage.Position = new Vector2((viewport.X - stageWidth) * 0.5f, (viewport.Y - stageHeight) * 0.5f);
        _stage.Size = new Vector2(stageWidth, stageHeight);

        _title.MinFontSize = Mathf.RoundToInt(stageHeight * 0.026f);
        _title.MaxFontSize = Mathf.RoundToInt(stageHeight * 0.045f);
        _body.MinFontSize = Mathf.RoundToInt(stageHeight * 0.024f);
        _body.MaxFontSize = Mathf.RoundToInt(stageHeight * 0.037f);
        _leftText.MinFontSize = Mathf.RoundToInt(stageHeight * 0.020f);
        _leftText.MaxFontSize = Mathf.RoundToInt(stageHeight * 0.032f);
        _pageCounter.MinFontSize = Mathf.RoundToInt(stageHeight * 0.018f);
        _pageCounter.MaxFontSize = Mathf.RoundToInt(stageHeight * 0.024f);

        var buttonSize = new Vector2(stageWidth * 0.115f, stageHeight * ButtonHeightRatio);
        var buttonFont = Mathf.RoundToInt(stageHeight * 0.026f);
        _buttons.AddThemeConstantOverride("separation", Mathf.RoundToInt(stageWidth * 0.010f));
        foreach (var button in new[] { _prevButton, _nextButton, _confirmButton })
        {
            button.CustomMinimumSize = buttonSize;
            button.AddThemeFontSizeOverride("font_size", buttonFont);
        }
    }

    /// <summary>窗口尺寸变化时重新按比例摆放（教程展示期间玩家改分辨率/缩放也不会错位）。</summary>
    private void OnPanelResized()
    {
        if (_pages.Count == 0)
            return;

        ApplyStageLayout(_background.Texture);
        // 文本没变时 SetTextAutoSize 不会重算字号，先清空再写回，让自适应按新尺寸重新跑一次。
        var page = _pages[_index];
        _pointer.Annotation = ResolveAnnotation(page.Annotation);
        _title.SetTextAutoSize(string.Empty);
        _title.SetTextAutoSize(page.Title);
        _body.SetTextAutoSize(string.Empty);
        _body.SetTextAutoSize(page.Body);
        _body.ParseBbcode(_body.Text);
        RenderLeftText(page);
    }

    // 缺图只报一次（每页 Render 都会走这里，避免刷日志）。
    private static readonly Dictionary<string, Texture2D?> TextureCache = new(StringComparer.Ordinal);

    private static Texture2D? LoadTexture(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        if (TextureCache.TryGetValue(path, out var cached))
            return cached;

        // 与模组其它图片一致用 GD.Load：pck 里只有 .import + ctex，靠重映射按原始 res:// 路径加载。
        Texture2D? texture = null;
        try
        {
            texture = GD.Load<Texture2D>(path);
        }
        catch (Exception e)
        {
            GaoshouTutorial.LogError($"[GaoshouTutorialFtue] image load failed '{path}': {e.Message}");
        }

        if (texture == null)
            GaoshouTutorial.LogError($"[GaoshouTutorialFtue] image missing or not a texture: {path}");

        TextureCache[path] = texture;
        return texture;
    }

    private void Close()
    {
        _confirmed?.TrySetResult(true);
        CloseFtue();
    }
}
