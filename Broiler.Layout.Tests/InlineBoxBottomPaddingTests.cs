using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline box's rectangle on a line reaches its bottom padding and border below its content, as
/// it reaches its top padding and border above it.
/// </summary>
/// <remarks>
/// <para>
/// <c>CssLineBox.UpdateRectangle</c> grows an inline box's rectangle from its content by its padding
/// and border on each side. At the bottom it added the box's <em>top</em> padding: a link with 10px
/// of padding above and 30px below a 16px word had a 36px rectangle, where its padding box is 56px,
/// so its background stopped 20px short. That rectangle is what paints the box and what script reads
/// for it.
/// </para>
/// <para>
/// Each link here holds one word, 8×16px, in a 320px block, after a word outside it.
/// </para>
/// </remarks>
public sealed class InlineBoxBottomPaddingTests
{
    private static readonly Uri BaseUrl = new("file:///inline-box-bottom-padding.html");

    /// <summary>
    /// The rectangle runs from the top padding and border above the word to the bottom padding and
    /// border below it: 56px tall with 10px of padding above and 30px below, 36px with none above
    /// and 20px below, 56px with 30px above and 10px below, and 61px with 10px above, 30px below
    /// and a 5px bottom border. They were 36, 16, 76 and 41px.
    /// </summary>
    [Theory]
    [InlineData(10, 30, 0, 56)]
    [InlineData(0, 20, 0, 36)]
    [InlineData(30, 10, 0, 56)]
    [InlineData(10, 30, 5, 61)]
    public void The_Rectangle_Reaches_The_Bottom_Padding_And_Border(
        int paddingTop, int paddingBottom, int borderBottom, float height)
    {
        var (link, word) = Lay(link =>
        {
            link.PaddingTop = $"{paddingTop}px";
            link.PaddingBottom = $"{paddingBottom}px";
            if (borderBottom > 0)
            {
                link.BorderBottomWidth = $"{borderBottom}px";
                link.BorderBottomStyle = "solid";
            }
        });

        var rectangle = Assert.Single(link.Rectangles).Value;
        Assert.Equal(word.Top - paddingTop, rectangle.Top, 1);
        Assert.Equal(word.Bottom + paddingBottom + borderBottom, rectangle.Bottom, 1);
        Assert.Equal(height, rectangle.Height, 1);
    }

    /// <summary>
    /// A link with 20px of padding below it, holding one with 10px above it: the inner one ends at
    /// the word and the outer one 20px below it. Both ended 10px below it: the inner one's top
    /// padding was added at its bottom and carried out to the outer one, which added its own top
    /// padding, none.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Outer_Link_Ends_At_Its_Own_Bottom_Padding()
    {
        var (outer, word) = Lay(outer => outer.PaddingBottom = "20px", inner => inner.PaddingTop = "10px");

        var inner = outer.Boxes[0];
        Assert.Equal(word.Bottom, Assert.Single(inner.Rectangles).Value.Bottom, 1);
        Assert.Equal(word.Bottom + 20, Assert.Single(outer.Rectangles).Value.Bottom, 1);
    }

    /// <summary>
    /// Control, which passes before and after: with the same 10px above and below, the rectangle is
    /// 36px tall, 10px each side of the word.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_Equal_Padding_Above_And_Below()
    {
        var (link, word) = Lay(link => link.PaddingTop = link.PaddingBottom = "10px");

        var rectangle = Assert.Single(link.Rectangles).Value;
        Assert.Equal(word.Top - 10, rectangle.Top, 1);
        Assert.Equal(word.Bottom + 10, rectangle.Bottom, 1);
    }

    /// <summary>
    /// Control, which passes before and after: the bottom padding reaches below the line without
    /// making it taller. With 30px of it on the link, the block is 16px tall with a normal line
    /// height and 24px with a 24px one.
    /// </summary>
    [Theory]
    [InlineData(null, 16)]
    [InlineData("24px", 24)]
    public void Control_The_Line_Keeps_Its_Height(string? lineHeight, float height)
    {
        var (link, _) = Lay(link => link.PaddingBottom = "30px", styleBlock: block =>
        {
            if (lineHeight != null)
                block.LineHeight = lineHeight;
        });

        Assert.Equal(height, link.ParentBox!.Size.Height, 1);
    }

    /// <summary>
    /// A 320px block, styled by <paramref name="styleBlock"/>, holding a word and then a link, styled
    /// by <paramref name="styleLink"/>, holding a word, or, with <paramref name="styleInner"/>, holding
    /// a link styled by it that holds the word.
    /// </summary>
    private static (CssBox Link, CssRect Word) Lay(
        Action<CssBox> styleLink, Action<CssBox>? styleInner = null, Action<CssBox>? styleBlock = null)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var block = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        styleBlock?.Invoke(block);
        Word(block);

        var link = new CssBox(block, new HtmlTag("a", false, null), BaseUrl) { Display = "inline" };
        styleLink(link);

        var holder = link;
        if (styleInner != null)
        {
            holder = new CssBox(link, new HtmlTag("a", false, null), BaseUrl) { Display = "inline" };
            styleInner(holder);
        }

        var word = Word(holder);

        root.PerformLayout(root.LayoutEnvironment);
        return (link, word);
    }

    /// <summary>An anonymous inline box in <paramref name="parent"/> holding one word, and the word.</summary>
    private static CssRect Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
        return Assert.Single(text.Words);
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
