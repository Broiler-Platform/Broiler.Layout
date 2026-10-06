using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A fixed box with all four insets 0, auto margins and a <c>fit-content</c> size -- what a popover is by
/// HTML's rendering rules, and a modal dialog by the scripting host's -- is as tall as its content and
/// centred in the viewport on both axes, whether the engine places anchors natively or not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two things kept it from that.</b> A height keyword such as <c>fit-content</c> counted as an explicit
/// height where an inline formatting context sizes its block, so a scroll container -- held there to its
/// explicit height -- was as tall as its padding and border: a popover, <c>overflow: auto</c> by those
/// rules, lost its text. And the block-axis centring of a box whose height is known only after layout ran
/// only with native anchor placement on, which the window never turns on: a modal dialog and a popover
/// stood at the top of the viewport. Chromium centres both (measured).
/// </para>
/// <para>
/// The tree is a page's: the root, the root element's block, the body, and in it the fixed box holding one
/// word, 8×16px, with 4px of padding all round. The viewport is 1024×768.
/// </para>
/// </remarks>
public sealed class CentredOutOfFlowBoxTests
{
    private static readonly Uri BaseUrl = new("file:///centred-out-of-flow-box.html");

    /// <summary>A scroll container sized <c>fit-content</c> is as tall as one sized to its content with no overflow clipping. It was 8px tall.</summary>
    [Fact]
    public void A_Scroll_Container_Sized_To_Fit_Its_Content_Keeps_Its_Content()
    {
        var clipping = Lay(box => box.Overflow = "auto");
        var visible = Lay(_ => { });

        Assert.True(visible.Size.Height > 8, $"The box is {visible.Size.Height}px tall: it has a line of text in it.");
        Assert.Equal(visible.Size.Height, clipping.Size.Height, 1);
    }

    /// <summary>The box is centred vertically as well as horizontally. It was at the top.</summary>
    [Fact]
    public void A_Box_Sized_By_Its_Content_Is_Centred_On_Both_Axes()
    {
        var box = Lay(box => box.Overflow = "auto");

        Assert.Equal((1024 - box.Size.Width) / 2, box.Location.X, 1);
        Assert.Equal((768 - box.Size.Height) / 2, box.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a box with a definite height is centred during layout, and
    /// one with <c>height: auto</c> between the two insets fills the viewport, leaving nothing to centre.
    /// </summary>
    [Theory]
    [InlineData("definite height", 50f, 359f)]
    [InlineData("auto height", 768f, 0f)]
    public void Control_Boxes_Centred_During_Layout(string kind, float height, float top)
    {
        var box = Lay(kind switch
        {
            "definite height" => box => box.Height = "42px",
            "auto height" => box => box.Height = CssConstants.Auto,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        });

        Assert.Equal(height, box.Size.Height, 1);
        Assert.Equal(top, box.Location.Y, 1);
    }

    private static CssBox Lay(Action<CssBox> style)
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
        var box = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Fixed,
            Top = "0",
            Right = "0",
            Bottom = "0",
            Left = "0",
            Width = "fit-content",
            Height = "fit-content",
            MarginTop = CssConstants.Auto,
            MarginRight = CssConstants.Auto,
            MarginBottom = CssConstants.Auto,
            MarginLeft = CssConstants.Auto,
            PaddingTop = "4px",
            PaddingRight = "4px",
            PaddingBottom = "4px",
            PaddingLeft = "4px",
        };
        style(box);

        var text = new CssBox(box, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();

        root.PerformLayout(root.LayoutEnvironment);
        return box;
    }

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
