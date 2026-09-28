using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline box's rectangle on a line reaches its own vertical padding and border around its
/// content, and not those of the inline boxes inside it.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §10.6.1: the vertical padding and border of an inline, non-replaced box lie outside its
/// content area, which its font sets. <c>CssLineBox.UpdateRectangle</c> grew an inline box's
/// rectangle from its content by its padding and border, and then carried the grown rectangle out to
/// the inline box around it, which grew it by its own. A link with no padding holding one with 10px
/// had a rectangle 10px above and below its word, so its background painted there too, where
/// browsers paint the inner link's padding over a band only as tall as the text.
/// </para>
/// <para>
/// Each outer link here holds the inner ones and one word, 8×16px, in a 320px block, after a word
/// outside them.
/// </para>
/// </remarks>
public sealed class InlineBoxNestedPaddingRectTests
{
    private static readonly Uri BaseUrl = new("file:///inline-box-nested-padding.html");

    /// <summary>
    /// A link with no padding, or with 5px, holding one with 10px: the outer rectangle runs as far
    /// above and below the word as its own padding, 16px or 26px tall, and the inner one 10px each
    /// side of the word, 36px. The outer one was 36px or 46px, the inner one's padding carried out.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(0)]
    [InlineData(5)]
    public void An_Outer_Link_Reaches_Its_Own_Padding_Only(int outerPadding)
    {
        var (outer, inner, word) = Lay(
            outer => outer.PaddingTop = outer.PaddingBottom = $"{outerPadding}px",
            inner => inner.PaddingTop = inner.PaddingBottom = "10px");

        var outerRect = Assert.Single(outer.Rectangles).Value;
        Assert.Equal(word.Top - outerPadding, outerRect.Top, 1);
        Assert.Equal(word.Bottom + outerPadding, outerRect.Bottom, 1);

        var innerRect = Assert.Single(inner.Rectangles).Value;
        Assert.Equal(word.Top - 10, innerRect.Top, 1);
        Assert.Equal(word.Bottom + 10, innerRect.Bottom, 1);
    }

    /// <summary>
    /// A link with a 3px top border holding one with 10px of top padding: the outer rectangle
    /// starts 3px above the word. It started 13px above it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Outer_Border_Sits_On_The_Content_Not_On_The_Inner_Padding()
    {
        var (outer, _, word) = Lay(
            outer =>
            {
                outer.BorderTopWidth = "3px";
                outer.BorderTopStyle = "solid";
            },
            inner => inner.PaddingTop = "10px");

        Assert.Equal(word.Top - 3, Assert.Single(outer.Rectangles).Value.Top, 1);
    }

    /// <summary>
    /// Three links, the outer one with no padding around one with 4px around one with 10px: the
    /// outer rectangle is as tall as the word, 16px, and the middle one 4px each side of it, 24px.
    /// Both were 10px and 4px each side added up, 44px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Each_Of_Three_Links_Reaches_Its_Own_Padding()
    {
        var (outer, middle, word) = Lay(
            outer => { },
            middle => middle.PaddingTop = middle.PaddingBottom = "4px",
            inner => inner.PaddingTop = inner.PaddingBottom = "10px");

        Assert.Equal(16, Assert.Single(outer.Rectangles).Value.Height, 1);

        var middleRect = Assert.Single(middle.Rectangles).Value;
        Assert.Equal(word.Top - 4, middleRect.Top, 1);
        Assert.Equal(24, middleRect.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: across the line, the outer rectangle takes in the
    /// inner link's side padding, which takes up the line. With 10px each side of the inner link,
    /// the outer one is 28px wide, the word and both paddings, and starts where the inner one does.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_An_Outer_Link_Spans_The_Inner_Side_Padding()
    {
        var (outer, inner, _) = Lay(
            outer => { },
            inner => inner.PaddingLeft = inner.PaddingRight = "10px");

        var outerRect = Assert.Single(outer.Rectangles).Value;
        Assert.Equal(28, outerRect.Width, 1);
        Assert.Equal(Assert.Single(inner.Rectangles).Value.Left, outerRect.Left, 1);
    }

    /// <summary>
    /// A 320px block holding a word and then an outer link, styled by <paramref name="styleOuter"/>,
    /// holding one link styled by each of <paramref name="styleInner"/>, one inside the other, the
    /// innermost holding a word. Returns the outer link, the link inside it, and the word.
    /// </summary>
    private static (CssBox Outer, CssBox Inner, CssRect Word) Lay(
        Action<CssBox> styleOuter, params Action<CssBox>[] styleInner)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var block = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        Word(block);

        var outer = new CssBox(block, new HtmlTag("a", false, null), BaseUrl) { Display = CssConstants.Inline };
        styleOuter(outer);

        var holder = outer;
        CssBox? inner = null;
        foreach (var style in styleInner)
        {
            holder = new CssBox(holder, new HtmlTag("a", false, null), BaseUrl) { Display = CssConstants.Inline };
            style(holder);
            inner ??= holder;
        }

        var word = Word(holder);

        root.PerformLayout(root.LayoutEnvironment);
        return (outer, inner!, word);
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
