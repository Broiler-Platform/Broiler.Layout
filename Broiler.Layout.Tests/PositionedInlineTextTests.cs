using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Broiler.Layout.IR;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// The text of an absolutely or fixed positioned inline box is laid out once, in the box, and
/// takes no part in the lines of the block it is in.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §9.6.1: an absolutely positioned box is taken out of the flow, and it has no effect on
/// the layout of later siblings; only its static position is worked out there (§10.3.7, §10.6.4).
/// The flow put the words of a positioned <c>&lt;span&gt;</c> on the block's line as well as on the
/// span's own, the same words, so they were drawn twice, and the block's line measured them where
/// the span put them: in a paragraph of 16px/20px text, a span with <c>top: 30px</c> holding "X" in
/// a 20px font moved the paragraph's text 33px down and made the paragraph 51px tall, where browsers
/// leave it 20px tall. Acid3's <c>map::after</c>, a box like it 18px down, made the line holding the
/// <c>&lt;map&gt;</c> 9px taller, and everything below it 9px lower.
/// </para>
/// <para>
/// Each paragraph here is 200px wide with 20px lines of a 16px font, in a block in the root, which
/// is the positioned boxes' containing block. Words are 8px wide a letter and as tall as their
/// font's height, 4/3 of its size in points.
/// </para>
/// </remarks>
public sealed class PositionedInlineTextTests
{
    private static readonly Uri BaseUrl = new("file:///positioned-inline-text.html");

    /// <summary>
    /// "a ", an absolutely positioned span with <c>top: 30px; left: 100px</c> holding "X", then "b":
    /// the paragraph is as tall as it is without the span, 20px, with its words where they are
    /// without it. It was 51.2px tall, its words 33.2px down.
    /// </summary>
    [Fact]
    public void An_Absolutely_Positioned_Span_Leaves_The_Paragraph_As_It_Is_Without_It()
    {
        var with = Paragraph(CssConstants.Absolute);
        var without = Paragraph(position: null);

        Assert.Equal(Height(without), Height(with), 1);
        Assert.Equal(20, Height(with), 1);
        Assert.Equal(Word(without, "a").Top, Word(with, "a").Top, 1);
        Assert.Equal(Word(without, "b").Left, Word(with, "b").Left, 1);
        Assert.Single(with.LineBoxes);
    }

    /// <summary>
    /// The span's "X" is drawn once, at the span's offsets from its containing block: one text
    /// fragment, 100px in and 30px down, in the span's own fragment there. It was drawn twice, a
    /// second time from the paragraph's line.
    /// </summary>
    [Fact]
    public void An_Absolutely_Positioned_Spans_Text_Is_Drawn_Once_At_Its_Offsets()
    {
        var paragraph = Paragraph(CssConstants.Absolute);

        var (owner, text) = Assert.Single(TextFragments(paragraph), f => f.Text.Text == "X");
        Assert.Equal(100, text.X, 1);
        Assert.Equal(30, text.Y, 1);
        Assert.Equal(new PointF(100, 30), owner.Location);
    }

    /// <summary>
    /// As above with <c>position: fixed</c>: the paragraph is 20px tall, and "X" is drawn once,
    /// 100px in and 30px down the viewport.
    /// </summary>
    [Fact]
    public void A_Fixed_Span_Leaves_The_Paragraph_As_It_Is_And_Its_Text_Is_Drawn_Once()
    {
        var with = Paragraph(CssConstants.Fixed);
        var without = Paragraph(position: null);

        Assert.Equal(Height(without), Height(with), 1);
        Assert.Equal(Word(without, "a").Top, Word(with, "a").Top, 1);

        var (owner, text) = Assert.Single(TextFragments(with), f => f.Text.Text == "X");
        Assert.Equal(100, text.X, 1);
        Assert.Equal(30, text.Y, 1);
        Assert.Equal(new PointF(100, 30), owner.Location);
    }

    /// <summary>
    /// An absolutely positioned span holding more text than the paragraph's line takes adds no
    /// line to the paragraph, which stays 20px tall with one line, and its words are drawn once.
    /// The flow broke its text over lines of the paragraph, which were left behind it.
    /// </summary>
    [Fact]
    public void An_Absolutely_Positioned_Spans_Long_Text_Adds_No_Line_To_The_Paragraph()
    {
        var paragraph = Paragraph(CssConstants.Absolute, "one two six ten red sky fox owl bee cat elk yak");

        Assert.Single(paragraph.LineBoxes);
        Assert.Equal(20, Height(paragraph), 1);
        Assert.Single(TextFragments(paragraph), f => f.Text.Text == "yak");
    }

    /// <summary>
    /// Control, which passes before and after: a span in the flow is on the paragraph's line and
    /// drawn once there, "X" 12px in after "a "; and one with <c>position: relative; top: 30px;
    /// left: 100px</c> is drawn once too, 100px right of and 30px below where that one is, in a
    /// paragraph as tall.
    /// </summary>
    [Fact]
    public void Control_A_Static_Or_Relative_Span_Stays_On_The_Paragraphs_Line()
    {
        var inFlow = Paragraph(position: null, "X", offsets: false);
        var relative = Paragraph(CssConstants.Relative);

        var (_, still) = Assert.Single(TextFragments(inFlow), f => f.Text.Text == "X");
        var (_, moved) = Assert.Single(TextFragments(relative), f => f.Text.Text == "X");

        Assert.Equal(12, still.X, 1);
        Assert.Equal(still.X + 100, moved.X, 1);
        Assert.Equal(still.Y + 30, moved.Y, 1);
        Assert.Equal(Height(inFlow), Height(relative), 1);
        Assert.True(Height(inFlow) > 20);
    }

    /// <summary>
    /// Control, which passes before and after: CSS 2.1 §9.4.2, a line holding nothing in the flow
    /// is a zero-height line box, as though it did not exist. A block whose only content is an
    /// absolutely positioned span holding "X" is 0px tall, and the block after it starts where it
    /// does.
    /// </summary>
    [Fact]
    public void Control_A_Block_Holding_Only_An_Absolutely_Positioned_Span_Is_Zero_Tall()
    {
        var (block, next) = Block(b => Positioned(b, "X"));

        Assert.Equal(0, Height(block), 1);
        Assert.Equal(block.Location.Y, next.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: as above with an empty span before the positioned
    /// one. An empty inline box with no margin, padding or border puts nothing in the flow either,
    /// and the block is 0px tall.
    /// </summary>
    [Fact]
    public void Control_A_Block_Holding_An_Empty_Span_And_An_Absolutely_Positioned_Span_Is_Zero_Tall()
    {
        var (block, next) = Block(b =>
        {
            Span(b);
            Positioned(b, "X");
        });

        Assert.Equal(0, Height(block), 1);
        Assert.Equal(block.Location.Y, next.Location.Y, 1);
    }

    /// <summary>
    /// Acid3's <c>&lt;map&gt;</c>: an empty inline box holding only absolutely positioned boxes,
    /// an anchor holding "YOU" and a generated box holding "X", with collapsible white space
    /// around them. The block holding it is 0px tall, and the block after it starts where it does.
    /// The positioned boxes' text was on its line, which was 39.2px tall here and 42px on Acid3.
    /// </summary>
    [Fact]
    public void A_Block_Holding_An_Inline_With_Only_Positioned_Boxes_In_It_Is_Zero_Tall()
    {
        var (block, next) = Block(b =>
        {
            Text(b, " ");
            var map = Span(b);
            Positioned(map, "YOU");
            Text(map, " ");
            Positioned(map, "X");
            Text(b, " ");
        });

        Assert.Equal(0, Height(block), 1);
        Assert.Equal(block.Location.Y, next.Location.Y, 1);
    }

    /// <summary>
    /// A laid out block, 200px wide with 20px lines of a 16px font, filled by
    /// <paramref name="fill"/>, and the block after it, 10px tall.
    /// </summary>
    private static (CssBox Block, CssBox Next) Block(Action<CssBox> fill)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        _ = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "10px" };

        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = "200px",
            FontSize = "16px",
            LineHeight = "20px",
        };

        fill(block);

        var next = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "10px" };

        root.PerformLayout(root.LayoutEnvironment);
        return (block, next);
    }

    /// <summary>An element's inline box in <paramref name="parent"/>, inheriting its style.</summary>
    private static CssBox Span(CssBox parent)
    {
        var span = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl);
        span.InheritStyle();
        span.Display = CssConstants.Inline;
        return span;
    }

    /// <summary>
    /// A span in <paramref name="parent"/> with <c>position: absolute; top: 18px; left: 100px</c>
    /// and a 20px font with <c>line-height: 1</c>, holding the text.
    /// </summary>
    private static CssBox Positioned(CssBox parent, string text)
    {
        var span = Span(parent);
        span.Position = CssConstants.Absolute;
        span.Top = "18px";
        span.Left = "100px";
        span.FontSize = "20px";
        span.LineHeight = "1";
        Text(span, text);
        return span;
    }

    /// <summary>
    /// A laid out paragraph holding "a ", a span with the position given (none when null) holding
    /// the text in a 20px font with <c>line-height: 1</c>, offset by <c>top: 30px; left: 100px</c>
    /// unless <paramref name="offsets"/> is false, then "b". Without a position there is no span.
    /// </summary>
    private static CssBox Paragraph(string? position, string text = "X", bool offsets = true)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var paragraph = new CssBox(body, new HtmlTag("p", false, null), BaseUrl)
        {
            Display = "block",
            Width = "200px",
            FontSize = "16px",
            LineHeight = "20px",
        };

        Text(paragraph, "a ");

        if (position != null || !offsets)
        {
            var span = new CssBox(paragraph, new HtmlTag("span", false, null), BaseUrl);
            span.InheritStyle();
            span.Display = CssConstants.Inline;
            span.FontSize = "20px";
            span.LineHeight = "1";

            if (position != null)
                span.Position = position;

            if (offsets)
            {
                span.Top = "30px";
                span.Left = "100px";
            }

            Text(span, text);
        }

        Text(paragraph, "b");

        root.PerformLayout(root.LayoutEnvironment);
        return paragraph;
    }

    private static double Height(CssBox box) => box.ActualBottom - box.Location.Y;

    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl);
        box.InheritStyle();
        box.Display = CssConstants.Inline;
        box.Text = text.AsMemory();
        box.ParseToWords();
    }

    private static CssRect Word(CssBox paragraph, string text) =>
        paragraph.Boxes.SelectMany(b => b.Words).Single(w => w.Text == text);

    /// <summary>
    /// Every text fragment painted for the tree <paramref name="paragraph"/> is in, with the
    /// fragment whose line holds it.
    /// </summary>
    private static List<(Fragment Owner, InlineFragment Text)> TextFragments(CssBox paragraph)
    {
        var root = paragraph.ParentBox!.ParentBox!;

        return Fragments(FragmentTreeBuilder.Build(root))
            .SelectMany(f => (f.Lines ?? []).SelectMany(l => l.Inlines).Select(i => (f, i)))
            .ToList();
    }

    private static IEnumerable<Fragment> Fragments(Fragment fragment)
    {
        yield return fragment;

        foreach (var child in fragment.Children)
        {
            foreach (var descendant in Fragments(child))
                yield return descendant;
        }
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly ConcurrentDictionary<double, FakeFont> Fonts = new();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => Fonts.GetOrAdd(size, s => new FakeFont(s));
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, (float)font.Height);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8 * text.Length; }
        public double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 4;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => new(300, 150, true);
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
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => throw new NotSupportedException();
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    /// <summary>A font of the given size in points, as tall in pixels as its size in pixels.</summary>
    private sealed class FakeFont(double size) : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => size;
        public double Height => size * 4 / 3;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
