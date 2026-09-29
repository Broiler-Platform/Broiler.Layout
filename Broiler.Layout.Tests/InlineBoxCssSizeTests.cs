using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline, non-replaced box takes neither its <c>width</c> nor its <c>height</c> on its line; a
/// replaced one laid out inline takes both.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.3.1 and §10.6.1: <c>width</c> and <c>height</c> do not apply to an inline,
/// non-replaced element; its box is as wide as what it holds and its line as tall as its line
/// height. The flow gave every inline box both, padding the line out to the box's width and the
/// line after it down to its height. MediaWiki's footer badge is a <c>&lt;picture&gt;</c> whose empty
/// <c>&lt;source width="84" height="29"&gt;</c> took 84px of its line, putting the image beside it
/// 84px along, where browsers put it at the start.
/// </para>
/// <para>
/// Each block here has a 16px font and 20px lines. Words are 8px wide a letter and 16px tall, and a
/// space is 4px wide.
/// </para>
/// </remarks>
public sealed class InlineBoxCssSizeTests
{
    private static readonly Uri BaseUrl = new("file:///inline-box-css-size.html");

    /// <summary>
    /// "a", an empty span with <c>width: 84px; height: 29px</c>, then "b": "b" follows "a", 8px
    /// along. It was 92px along, after the span's 84px.
    /// </summary>
    [Fact]
    public void An_Empty_Span_With_A_Width_Takes_No_Room()
    {
        var block = Block();
        Text(block, "a");
        Span(block, "84px", "29px");
        Text(block, "b");
        Layout(block);

        Assert.Equal(8, WordLeft(block, "b"), 1);
    }

    /// <summary>
    /// "a", a span with <c>width: 84px</c> holding "xy", then "b": the span is as wide as "xy", and
    /// "b" is 24px along. It was 92px along.
    /// </summary>
    [Fact]
    public void A_Span_With_A_Width_Is_As_Wide_As_Its_Text()
    {
        var block = Block();
        Text(block, "a");
        Text(Span(block, "84px", null), "xy");
        Text(block, "b");
        Layout(block);

        Assert.Equal(24, WordLeft(block, "b"), 1);
    }

    /// <summary>
    /// In a 60px block, a span with <c>height: 50px</c> holding "aa", then "bb cc dd": "dd" wraps to
    /// the second line, 20px down, 2px of leading above it, and the block is 40px tall. The second
    /// line started 50px down, and the block was 70px.
    /// </summary>
    [Fact]
    public void A_Span_With_A_Height_Leaves_The_Next_Line_Where_Its_Line_Ends()
    {
        var block = Block("60px");
        Text(Span(block, null, "50px"), "aa");
        Text(block, " bb cc dd");
        Layout(block);

        Assert.Equal(22, WordTop(block, "dd"), 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an <c>&lt;svg&gt;</c> laid out inline, a replaced
    /// box, with <c>width: 84px; height: 29px</c> between "a" and "b" takes its width, and "b" is
    /// 92px along.
    /// </summary>
    [Fact]
    public void Control_An_Inline_Svg_Takes_Its_Width()
    {
        var block = Block();
        Text(block, "a");
        _ = new CssBox(block, new HtmlTag("svg", false, null), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "84px",
            Height = "29px",
        };
        Text(block, "b");
        Layout(block);

        Assert.Equal(92, WordLeft(block, "b"), 1);
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
    /// A span in <paramref name="parent"/> inheriting its style, with the given width and height if
    /// any.
    /// </summary>
    private static CssBox Span(CssBox parent, string? width, string? height)
    {
        var span = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl);
        span.InheritStyle();
        span.Display = CssConstants.Inline;

        if (width != null)
            span.Width = width;

        if (height != null)
            span.Height = height;

        return span;
    }

    /// <summary>
    /// An anonymous inline box in <paramref name="parent"/> holding the text, inheriting the
    /// parent's style as the box a text node makes does.
    /// </summary>
    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl);
        box.InheritStyle();
        box.Display = CssConstants.Inline;
        box.Text = text.AsMemory();
        box.ParseToWords();
    }

    private static CssRect Word(CssBox block, string text) =>
        Descendants(block).SelectMany(b => b.Words).Single(w => w.Text == text);

    /// <summary>How far along <paramref name="block"/> the word <paramref name="text"/> starts.</summary>
    private static double WordLeft(CssBox block, string text) => Word(block, text).Left - block.Location.X;

    /// <summary>How far down <paramref name="block"/> the word <paramref name="text"/> stands.</summary>
    private static double WordTop(CssBox block, string text) => Word(block, text).Top - block.Location.Y;

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
