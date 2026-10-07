using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A <c>&lt;button&gt;</c>, <c>&lt;input&gt;</c>, <c>&lt;select&gt;</c> or <c>&lt;textarea&gt;</c>
/// laid out as a block keeps its fit-content width, as HTML's button layout says of a button and as
/// Chromium does for all four. An ordinary block still fills its container.
/// </summary>
/// <remarks>
/// It stretched across its container like any block: reCAPTCHA's demo form gives its "Submit"
/// button <c>display: block</c>, and Chromium draws it 57px wide where it filled the form here.
/// Words are 8px wide.
/// </remarks>
public sealed class BlockFormControlWidthTests
{
    private static readonly Uri BaseUrl = new("file:///block-controls.html");

    /// <summary>
    /// A block button with one 8px word, 6px of padding and a 2px border on each side is 24px wide
    /// in a 300px container, where a block div around the same word is 300px.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("button", 24)]
    [InlineData("div", 300)]
    public void A_Block_Button_Fits_Its_Content(string tag, float width)
    {
        var (root, box) = Block(tag, null);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(width, box.Size.Width, 1);
    }

    /// <summary>A stated width still holds: 50% of the 300px container, and the 16px of edges.</summary>
    [Fact(Timeout = 600000)]
    public void A_Stated_Width_Still_Holds()
    {
        var (root, box) = Block("button", "50%");

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(150 + 16, box.Size.Width, 1);
    }

    /// <summary>
    /// A block text input, which has no words of its own, is as wide as its minimum width and edges
    /// say: 173px, as Broiler.HTML's default sheet gives it, plus 2px of padding and a 1px border on
    /// each side.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Block_Input_Keeps_Its_Own_Width()
    {
        var root = Root(out var container);
        var input = new CssBox(container, new HtmlTag("input", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            MinWidth = "173px",
            PaddingLeft = "2px",
            PaddingRight = "2px",
            BorderLeftWidth = "1px",
            BorderRightWidth = "1px",
            BorderLeftStyle = "solid",
            BorderRightStyle = "solid",
        };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(179, input.Size.Width, 1);
    }

    private static (CssBox Root, CssBox Box) Block(string tag, string? width)
    {
        var root = Root(out var container);
        var box = new CssBox(container, new HtmlTag(tag, false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            PaddingLeft = "6px",
            PaddingRight = "6px",
            BorderLeftWidth = "2px",
            BorderRightWidth = "2px",
            BorderLeftStyle = "solid",
            BorderRightStyle = "solid",
        };
        if (width is not null)
            box.Width = width;

        var word = new CssBox(box, null, BaseUrl) { Display = "inline", Text = "Submit".AsMemory() };
        word.ParseToWords();
        return (root, box);
    }

    private static CssBox Root(out CssBox container)
    {
        var root = new CssBox(null, new HtmlTag("html", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };
        container = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block, Width = "300px" };
        return root;
    }

    // Minimal ILayoutEnvironment: every word 8px wide and 16px tall, a space 4px, a 1024×768 viewport.
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
