using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An absolutely positioned box that its own <c>top</c> or <c>bottom</c> places stays where its
/// containing block puts it when a margin that collapses through its parent moves the parent.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §10.6.4: such a box is placed against its containing block, the padding box of its nearest
/// positioned ancestor or else the initial containing block, and nothing in the flow moves it. When a
/// later child's top margin collapsed through the parent (CSS2.1 §8.3.1), the engine moved the parent
/// with everything already laid out inside it, the absolutely positioned box too: in a body with no
/// margin holding <c>&lt;a style="position: absolute; top: 40px"&gt;</c> and then a paragraph, the
/// link was drawn 56px down, where Chromium draws it 40px down (measured)
/// -- and a click 40px down missed it.
/// </para>
/// <para>
/// A box at its static position, whose <c>top</c> and <c>bottom</c> are both <c>auto</c>, does go with
/// the flow, and so does one whose containing block is the parent that moves: Chromium draws them 16px
/// down and 56px down.
/// </para>
/// <para>
/// The tree is a page's: the root, the root element's block, and in it the body, a block with no
/// margin holding the absolutely positioned box, 30px tall, and then a paragraph with a 16px top
/// margin around one word, 8×16px. The viewport is 1024×768.
/// </para>
/// </remarks>
public sealed class AbsposAboveCollapsedMarginTests
{
    private static readonly Uri BaseUrl = new("file:///abspos-above-collapsed-margin.html");

    /// <summary>
    /// The box placed by <c>top: 40px</c> is 40px down, with the body and the paragraph 16px down. It
    /// was 56px down.
    /// </summary>
    [Fact]
    public void A_Box_Placed_By_Its_Top_Stays_Where_The_Viewport_Puts_It()
    {
        var page = Lay(box => box.Top = "40px");

        Assert.Equal(16, page.Body.Location.Y, 1);
        Assert.Equal(16, page.Paragraph.Location.Y, 1);
        Assert.Equal(40, page.Box.Location.Y, 1);
    }

    /// <summary>
    /// The box placed by <c>bottom: 0</c> ends at the bottom of the viewport, 738px down. It was 754px
    /// down.
    /// </summary>
    [Fact]
    public void A_Box_Placed_By_Its_Bottom_Stays_Where_The_Viewport_Puts_It()
    {
        var page = Lay(box => box.Bottom = "0");

        Assert.Equal(738, page.Box.Location.Y, 1);
    }

    /// <summary>
    /// The box in a block with a 10px top margin, which the paragraph's 16px collapses with, is 40px
    /// down, with the block 16px down. It was 46px down: the block's own margin had moved the body
    /// before the box was placed, and the paragraph's moved it 6px more.
    /// </summary>
    [Fact]
    public void A_Box_In_A_Block_Whose_Margin_Grows_Stays_Where_The_Viewport_Puts_It()
    {
        var page = Lay(box => box.Top = "40px", wrapperMarginTop: "10px");

        Assert.Equal(16, page.Wrapper!.Location.Y, 1);
        Assert.Equal(40, page.Box.Location.Y, 1);
    }

    /// <summary>
    /// Laid out a second time, the box is 40px down still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Place()
    {
        var page = Build(box => box.Top = "40px", body => { }, wrapperMarginTop: null);
        Layout(page);
        Layout(page);

        Assert.Equal(40, page.Box.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a box at its static position goes with the margin, 16px
    /// down; one whose containing block is the body, which is positioned, goes with the body, 56px down;
    /// one placed by its <c>left</c> alone is at its static position vertically, 16px down; one in a
    /// positioned block with a 10px margin is 40px below the block, 56px down; and one in a block whose
    /// 30px margin is larger than the paragraph's, which moves nothing after the box is placed, is 40px
    /// down.
    /// </summary>
    [Theory]
    [InlineData("static", 16)]
    [InlineData("positioned body", 56)]
    [InlineData("left only", 16)]
    [InlineData("positioned block", 56)]
    [InlineData("larger block margin", 40)]
    public void Control_Boxes_That_Go_With_The_Flow(string kind, float top)
    {
        var page = kind switch
        {
            "static" => Lay(_ => { }),
            "positioned body" => Lay(box => box.Top = "40px", body => body.Position = CssConstants.Relative),
            "left only" => Lay(box => box.Left = "10px"),
            "positioned block" => Lay(box => box.Top = "40px", wrapperMarginTop: "10px", wrapperPositioned: true),
            "larger block margin" => Lay(box => box.Top = "40px", wrapperMarginTop: "30px"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        Assert.Equal(top, page.Box.Location.Y, 1);
    }

    private sealed record Page(CssBox Root, CssBox Body, CssBox? Wrapper, CssBox Box, CssBox Paragraph);

    private static Page Lay(Action<CssBox> box, Action<CssBox>? body = null, string? wrapperMarginTop = null, bool wrapperPositioned = false)
    {
        var page = Build(box, body ?? (_ => { }), wrapperMarginTop, wrapperPositioned);
        Layout(page);
        return page;
    }

    /// <summary>
    /// The root, the root element's block, the body with no margin, and in the body -- or in a block in
    /// it with a <paramref name="wrapperMarginTop"/> top margin -- the absolutely positioned box styled by
    /// <paramref name="style"/>, then the paragraph.
    /// </summary>
    private static Page Build(Action<CssBox> style, Action<CssBox> bodyStyle, string? wrapperMarginTop, bool wrapperPositioned = false)
    {
        var root = new CssBox(null, null, BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var html = new CssBox(root, new HtmlTag("html", false, null), BaseUrl) { Display = "block" };
        var body = new CssBox(html, new HtmlTag("body", false, null), BaseUrl) { Display = "block", MarginTop = "0", MarginBottom = "0" };
        bodyStyle(body);

        CssBox? wrapper = null;
        var parent = body;
        if (wrapperMarginTop != null)
        {
            wrapper = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", MarginTop = wrapperMarginTop };
            if (wrapperPositioned)
                wrapper.Position = CssConstants.Relative;
            parent = wrapper;
        }

        var box = new CssBox(parent, new HtmlTag("a", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Absolute,
            Width = "80px",
            Height = "30px",
        };
        style(box);

        var paragraph = new CssBox(parent, new HtmlTag("p", false, null), BaseUrl)
        {
            Display = "block",
            MarginTop = "16px",
            MarginBottom = "16px",
        };
        var text = new CssBox(paragraph, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();

        return new Page(root, body, wrapper, box, paragraph);
    }

    private static void Layout(Page page) => page.Root.PerformLayout(page.Root.LayoutEnvironment);

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8, 16);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8; }
        public double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 4;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => default;
        public Broiler.Graphics.Color.BColor ParseColor(string value) => default;
        public void RequestRefresh(bool relayout) { }
        public SizeF ViewportSize => new(1024, 768);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1024, 768);
        public int MarginTop => 0;
        public void ReportLayoutError(string message, Exception? exception = null) { }
        public bool AvoidAsyncImagesLoading => true;
        public bool AvoidImagesLateLoading => true;
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => null!;
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class FakeFont : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
