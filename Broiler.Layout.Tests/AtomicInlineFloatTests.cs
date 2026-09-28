using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// The floats inside an inline-block, or inside a flex item laid out as one, are laid out, and the
/// box contains them.
/// </summary>
/// <remarks>
/// <para>
/// <c>FlowInlineBlock</c> lays such a box's inline content out through <c>CreateLineBoxes</c>, which
/// skips floats as out of flow, and took the box's height from its lines. A block container lays its
/// floats out itself once its lines exist, and flows its lines again beside them, and one that
/// establishes a formatting context contains them (CSS2.1 §10.6.7). Neither happened here: the float
/// stayed 0×0 at the page's origin, painted nowhere, and an inline-block, or a column flex item,
/// holding only a 30px float was 0px tall, with the next item on top of it.
/// </para>
/// <para>
/// Words here are 8×16px, and floats 20×30px, in boxes 100px wide, in a block 100px down the page.
/// </para>
/// </remarks>
public sealed class AtomicInlineFloatTests
{
    private static readonly Uri BaseUrl = new("file:///atomic-inline-float.html");

    /// <summary>
    /// An inline-block holding only a float, or a word and then a float, is 30px tall, with the
    /// float laid out at its top left, 20×30px. The inline-block was 0px tall with no word, and
    /// its line's height with one, and the float 0×0 at the page's origin.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_Inline_Block_Lays_Out_And_Contains_Its_Float(bool wordFirst)
    {
        var (box, floated, _) = Lay(CssConstants.InlineBlock, wordFirst);

        Assert.Equal(30, box.Size.Height, 1);
        Assert.Equal(box.Location.Y, floated.Location.Y, 1);
        Assert.Equal(box.Location.X, floated.Location.X, 1);
        Assert.Equal(20, floated.Size.Width, 1);
        Assert.Equal(30, floated.Size.Height, 1);
    }

    /// <summary>
    /// In a column flex container that aligns its items to its start, an item holding only a float
    /// is 30px tall, with the float at its top, and the next item starts 30px down. The item was 0px
    /// tall and the next item started at its top.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Column_Item_Lays_Out_And_Contains_Its_Float()
    {
        var (item, floated, next) = Lay("column", wordFirst: false);

        Assert.Equal(30, item.Size.Height, 1);
        Assert.Equal(item.Location.Y, floated.Location.Y, 1);
        Assert.Equal(item.Location.Y + 30, next!.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an <c>overflow: hidden</c> block, laid out as a
    /// block, holding only a float is 30px tall, with the float at its top.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_A_Block_Formatting_Context_Contains_Its_Float()
    {
        var (box, floated, _) = Lay("overflow", wordFirst: false);

        Assert.Equal(30, box.Size.Height, 1);
        Assert.Equal(box.Location.Y, floated.Location.Y, 1);
    }

    /// <summary>
    /// A 320px block 100px down the page holding a 100px box — an inline-block, an item of a column
    /// flex container that aligns its items to its start, followed by a 5px item, or an
    /// <c>overflow: hidden</c> block — which holds a 20×30px left float, after a word when
    /// <paramref name="wordFirst"/>.
    /// </summary>
    private static (CssBox Box, CssBox Float, CssBox? Next) Lay(string kind, bool wordFirst)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };
        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        _ = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "100px" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };

        if (kind == "column")
        {
            block.Display = "flex";
            block.FlexDirection = "column";
            block.AlignItems = "flex-start";
        }

        var box = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = kind == CssConstants.InlineBlock ? CssConstants.InlineBlock : "block",
            Width = "100px",
        };
        if (kind == "overflow")
            box.Overflow = "hidden";

        if (wordFirst)
        {
            var text = new CssBox(box, null, BaseUrl) { Display = "inline", Text = "A".AsMemory() };
            text.ParseToWords();
        }

        var floated = new CssBox(box, new HtmlTag("i", false, null), BaseUrl)
        {
            Display = "block",
            Float = "left",
            Width = "20px",
            Height = "30px",
        };

        CssBox? next = kind == "column"
            ? new CssBox(block, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Width = "10px", Height = "5px" }
            : null;

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return (box, floated, next);
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
