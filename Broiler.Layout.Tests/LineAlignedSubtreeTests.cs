using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline box aligned <c>top</c> or <c>bottom</c> is aligned to its line box with what is in
/// it, its aligned subtree, and the line box grows to hold every such subtree taller than it.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1: <c>top</c> aligns the top of the element's aligned subtree with the top of the
/// line box, and <c>bottom</c> its bottom with the bottom of it. The aligned subtree is the element
/// and what is in it, but for what is aligned <c>top</c> or <c>bottom</c> itself. The engine moved the
/// inline box alone, which holds no words, and left its text on the line's baseline; the text set
/// the baseline as well. Each box aligned so was aligned to the line box as the rest of the line
/// made it, before any other made it taller: a box aligned <c>bottom</c> beside a taller one aligned
/// <c>top</c> ended at the bottom of the text, not of the line.
/// </para>
/// <para>
/// Each block here has a 16px font and 20px lines. Words are 8px wide a letter and 16px tall, and
/// stand half their line's 4px of leading down it, on a baseline 12.8px below their top.
/// </para>
/// </remarks>
public sealed class LineAlignedSubtreeTests
{
    private static readonly Uri BaseUrl = new("file:///line-aligned-subtree.html");

    /// <summary>
    /// "a", a 40px inline-block, which stands on the baseline and sets it 40px down, then a span
    /// aligned <c>top</c> holding "t": "t" stands at the top of the line, 2px down it, where "a"
    /// stands 27.2px down, and the line is 45.2px tall. "t" stood on the baseline beside "a".
    /// </summary>
    [Fact]
    public void Text_In_A_Span_Aligned_Top_Stands_At_The_Lines_Top()
    {
        var block = Block();
        Text(block, "a");
        Box(block, 40);
        Text(Span(block, CssConstants.Top), "t");
        Layout(block);

        Assert.Equal(2, WordTop(block, "t"), 1);
        Assert.Equal(27.2, WordTop(block, "a"), 1);
        Assert.Equal(45.2, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", a 40px inline-block aligned <c>top</c>, which makes the line 40px tall, then a span
    /// aligned <c>bottom</c> holding "u": "u" stands at the bottom of the line, 22px down it, and
    /// "a" at its top. "u" stood at the top beside "a".
    /// </summary>
    [Fact]
    public void Text_In_A_Span_Aligned_Bottom_Stands_At_The_Lines_Bottom()
    {
        var block = Block();
        Text(block, "a");
        Box(block, 40, CssConstants.Top);
        Text(Span(block, CssConstants.Bottom), "u");
        Layout(block);

        Assert.Equal(22, WordTop(block, "u"), 1);
        Assert.Equal(2, WordTop(block, "a"), 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// In a 150px block, a span with <c>line-height: 40px</c> holding "zeta" on the second line: the
    /// span's line height is the line's, 40px, and "zeta" stands half its 24px of leading down it,
    /// 32px down the block. The rest of the line stands in the strut, at the line's bottom for a span
    /// aligned <c>bottom</c>, "delta" 42px down, and at its top for one aligned <c>top</c>, 22px down.
    /// "zeta" set the line's baseline, and "delta" stood beside it, 32px down.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Bottom, 42)]
    [InlineData(CssConstants.Top, 22)]
    public void A_Span_With_A_Taller_Line_Height_Leaves_The_Rest_Of_The_Line_At_Its_Other_End(string verticalAlign, double deltaTop)
    {
        var block = Block("150px");
        Text(block, "alpha beta gamma delta ");
        Text(Span(block, verticalAlign, lineHeight: "40px"), "zeta");
        Text(block, " omega");
        Layout(block);

        Assert.Equal(32, WordTop(block, "zeta"), 1);
        Assert.Equal(deltaTop, WordTop(block, "delta"), 1);
        Assert.Equal(deltaTop, WordTop(block, "omega"), 1);
        Assert.Equal(60, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", then a span aligned <c>top</c> holding "u" and a 40px inline-block on the span's
    /// baseline: the span's subtree is 45.2px tall and starts at the line's top, the box at the top
    /// and "u" 27.2px down, on the box's bottom, and "a" stands at the top of the line, 2px down.
    /// The box and "u" set the line's baseline, and "a" stood on it, 27.2px down.
    /// </summary>
    [Fact]
    public void A_Box_In_A_Span_Aligned_Top_Moves_With_It_And_Leaves_The_Baseline_Alone()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block, CssConstants.Top);
        Text(span, "u");
        var box = Box(span, 40);
        Layout(block);

        Assert.Equal(0, box.Location.Y - block.Location.Y, 1);
        Assert.Equal(27.2, WordTop(block, "u"), 1);
        Assert.Equal(2, WordTop(block, "a"), 1);
        Assert.Equal(45.2, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", a 10px inline-block aligned <c>bottom</c> and a 40px one aligned <c>top</c>, in either
    /// order: the 40px box makes the line 40px tall, and the 10px box ends at its bottom, 30px down.
    /// It ended at the bottom of the text, 10px down, before the other box made the line taller.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Box_Aligned_Bottom_Ends_At_The_Bottom_Of_The_Line_Another_Made_Taller(bool topBoxFirst)
    {
        var block = Block();
        Text(block, "a");
        CssBox bottom;

        if (topBoxFirst)
        {
            Box(block, 40, CssConstants.Top);
            bottom = Box(block, 10, CssConstants.Bottom);
        }
        else
        {
            bottom = Box(block, 10, CssConstants.Bottom);
            Box(block, 40, CssConstants.Top);
        }

        Layout(block);

        Assert.Equal(30, bottom.Location.Y - block.Location.Y, 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: "a" and a span aligned <c>top</c> or <c>bottom</c>
    /// holding "t" in the block's own font and line height stand together at the top of a 20px line;
    /// and in a span aligned <c>bottom</c> holding "u" and a 40px inline-block, the subtree is taller
    /// than the rest of the line and starts at its top, the box at the top and "u" on its bottom,
    /// 27.2px down, and "a" beside "u", at the bottom of the 45.2px line.
    /// </summary>
    [Fact]
    public void Control_Spans_Whose_Subtree_Is_The_Lines_Height_Or_Aligned_Bottom()
    {
        foreach (var verticalAlign in new[] { CssConstants.Top, CssConstants.Bottom })
        {
            var block = Block();
            Text(block, "a ");
            Text(Span(block, verticalAlign), "t");
            Layout(block);

            Assert.Equal(2, WordTop(block, "a"), 1);
            Assert.Equal(2, WordTop(block, "t"), 1);
            Assert.Equal(20, block.Size.Height, 1);
        }

        var tall = Block();
        Text(tall, "a");
        var span = Span(tall, CssConstants.Bottom);
        Text(span, "u");
        var box = Box(span, 40);
        Layout(tall);

        Assert.Equal(0, box.Location.Y - tall.Location.Y, 1);
        Assert.Equal(27.2, WordTop(tall, "u"), 1);
        Assert.Equal(27.2, WordTop(tall, "a"), 1);
        Assert.Equal(45.2, tall.Size.Height, 1);
    }

    /// <summary>A block of the given width with 20px lines, in a block in the root.</summary>
    private static CssBox Block(string width = "320px")
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        return new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = width,
            LineHeight = "20px",
        };
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    /// <summary>
    /// A span in <paramref name="parent"/> inheriting its style, aligned as given and with its own
    /// line height if one is given.
    /// </summary>
    private static CssBox Span(CssBox parent, string verticalAlign, string? lineHeight = null)
    {
        var span = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl);
        span.InheritStyle();
        span.Display = "inline";
        span.VerticalAlign = verticalAlign;

        if (lineHeight != null)
            span.LineHeight = lineHeight;

        return span;
    }

    /// <summary>An empty 8px wide inline-block of the given height, aligned as given.</summary>
    private static CssBox Box(CssBox parent, int height, string verticalAlign = CssConstants.Baseline) =>
        new(parent, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = height + "px",
            VerticalAlign = verticalAlign,
        };

    /// <summary>
    /// An anonymous inline box in <paramref name="parent"/> holding the text, inheriting the
    /// parent's style as the box a text node makes does.
    /// </summary>
    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl);
        box.InheritStyle();
        box.Display = "inline";
        box.Text = text.AsMemory();
        box.ParseToWords();
    }

    /// <summary>How far down <paramref name="block"/> the word <paramref name="text"/> stands.</summary>
    private static double WordTop(CssBox block, string text) =>
        Descendants(block).SelectMany(b => b.Words).Single(w => w.Text == text).Top - block.Location.Y;

    private static System.Collections.Generic.IEnumerable<CssBox> Descendants(CssBox box)
    {
        foreach (var child in box.Boxes)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, 16);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8 * text.Length; }
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
        public double Size => 12;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
