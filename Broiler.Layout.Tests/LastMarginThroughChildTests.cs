using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A block that keeps its last child's bottom margin inside itself keeps that margin as it has
/// collapsed with the margins of the child's own last children.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §8.3.1: the bottom margin of a last in-flow child collapses with its parent's when the
/// parent has no bottom padding or border and an auto height, so a paragraph's margin collapses
/// through an unpadded wrapper and becomes the wrapper's. A block whose bottom padding or border
/// keeps its last child's margin inside it measured only the child's own margin, which is the
/// wrapper's zero, and lost the paragraph's; the root, which keeps the body's margin inside it,
/// measured the body's 8px where the paragraph's 16px had collapsed with it.
/// </para>
/// <para>
/// Each block here is in a block in the root, as a page's are in its body, after a 5px block. It
/// holds an unpadded block holding a paragraph with a 16px bottom margin and no top margin, around
/// one word, 8×16px, and is followed by a 5px block.
/// </para>
/// </remarks>
public sealed class LastMarginThroughChildTests
{
    private static readonly Uri BaseUrl = new("file:///last-margin-through-child.html");

    /// <summary>
    /// A block with 1px of bottom padding, or a 1px bottom border, is 33px tall, its bottom 17px
    /// below the paragraph's, with the next block right after. It was 17px tall, 1px below the
    /// paragraph, and the next block started 16px higher.
    /// </summary>
    [Theory]
    [InlineData("padding")]
    [InlineData("border")]
    public void The_Block_Keeps_The_Margin_That_Collapsed_Through_Its_Last_Child(string kind)
    {
        var (box, paragraph, next) = Lay(Styled(kind), wrappers: 1);

        Assert.Equal(33, box.Size.Height, 1);
        Assert.Equal(paragraph.ActualBottom + 17, box.ActualBottom, 1);
        Assert.Equal(box.ActualBottom, next.Location.Y, 1);
    }

    /// <summary>
    /// With two unpadded blocks between them, the block with 1px of bottom padding is 33px tall as
    /// well. It was 17px tall.
    /// </summary>
    [Fact]
    public void The_Margin_Collapses_Through_Two_Blocks()
    {
        var (box, paragraph, next) = Lay(Styled("padding"), wrappers: 2);

        Assert.Equal(33, box.Size.Height, 1);
        Assert.Equal(paragraph.ActualBottom + 17, box.ActualBottom, 1);
        Assert.Equal(box.ActualBottom, next.Location.Y, 1);
    }

    /// <summary>
    /// The wrapper's own 4px bottom margin collapses with the paragraph's 16px, so the block with
    /// 1px of bottom padding is 33px tall. It was 21px tall, holding the 4px.
    /// </summary>
    [Fact]
    public void The_Wrappers_Own_Smaller_Margin_Collapses_With_The_Paragraphs()
    {
        var (box, paragraph, _) = Lay(Styled("padding"), wrappers: 1, wrapperMarginBottom: "4px");

        Assert.Equal(33, box.Size.Height, 1);
        Assert.Equal(paragraph.ActualBottom + 17, box.ActualBottom, 1);
    }

    /// <summary>
    /// The root keeps the body's bottom margin inside it, as it has collapsed with the 16px of the
    /// body's last paragraph: after the body's 1000px block, the root ends 16px below the paragraph,
    /// not the body's 8px. The paragraph here is the body's own last child.
    /// </summary>
    [Fact]
    public void The_Root_Keeps_The_Margin_That_Collapsed_Through_The_Body()
    {
        var root = new CssBox(null, new HtmlTag("html", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("body", false, null), BaseUrl)
        {
            Display = "block",
            MarginTop = "8px",
            MarginBottom = "8px",
        };
        _ = new CssBox(body, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "1000px" };
        var paragraph = Paragraph(body);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(paragraph.ActualBottom, body.ActualBottom, 1);
        Assert.Equal(paragraph.ActualBottom + 16, root.ActualBottom, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: the wrapper's own 24px bottom margin, larger than the
    /// paragraph's, makes the block with 1px of bottom padding 41px tall; an unpadded block lets the
    /// margin collapse through it, 16px tall with the next block 16px below; and an
    /// <c>overflow: hidden</c> block keeps it, 32px tall.
    /// </summary>
    [Theory]
    [InlineData("larger", 41, 0)]
    [InlineData("plain", 16, 16)]
    [InlineData("overflow", 32, 0)]
    public void Control_Other_Blocks(string kind, float height, float gap)
    {
        var (box, _, next) = kind == "larger"
            ? Lay(Styled("padding"), wrappers: 1, wrapperMarginBottom: "24px")
            : Lay(Styled(kind), wrappers: 1);

        Assert.Equal(height, box.Size.Height, 1);
        Assert.Equal(box.ActualBottom + gap, next.Location.Y, 1);
    }

    private static Action<CssBox> Styled(string kind) => kind switch
    {
        "padding" => box => box.PaddingBottom = "1px",
        "border" => box => { box.BorderBottomStyle = "solid"; box.BorderBottomWidth = "1px"; },
        "overflow" => box => box.Overflow = "hidden",
        "plain" => _ => { },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// In a block in the root: a 5px block, then a 320px block, styled by <paramref name="style"/>,
    /// holding <paramref name="wrappers"/> unpadded blocks, one inside the other, around the
    /// paragraph, and then a 5px block.
    /// </summary>
    private static (CssBox Box, CssBox Paragraph, CssBox Next) Lay(Action<CssBox> style, int wrappers, string? wrapperMarginBottom = null)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        _ = new CssBox(body, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "5px" };
        var box = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        style(box);

        var holder = box;
        for (int i = 0; i < wrappers; i++)
        {
            holder = new CssBox(holder, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };

            if (i == 0 && wrapperMarginBottom != null)
                holder.MarginBottom = wrapperMarginBottom;
        }

        var paragraph = Paragraph(holder);

        var next = new CssBox(body, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "5px" };

        root.PerformLayout(root.LayoutEnvironment);
        return (box, paragraph, next);
    }

    private static CssBox Paragraph(CssBox parent)
    {
        var paragraph = new CssBox(parent, new HtmlTag("p", false, null), BaseUrl)
        {
            Display = "block",
            MarginTop = "0",
            MarginBottom = "16px",
        };
        var text = new CssBox(paragraph, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
        return paragraph;
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
