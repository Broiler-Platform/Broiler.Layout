using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A box that <c>vertical-align</c> lowers from the baseline, or that is in an inline box lowered
/// from it, sets the line's baseline from where it stands, not from where the flow put it.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8: a line box reaches from the top of the highest box on it, and a box stands as far
/// above the baseline as its ascent less what lowers it. The engine puts the baseline far enough down
/// for each box to start at the line's top where the flow put it, and a lowered box then stood as
/// much below the line's top as it is lowered: the line was that much taller, and all on it that much
/// lower. A span with <c>line-height: 40px</c> lowered 10px in 16px/20px text made a 50px line,
/// where browsers make it 40px.
/// </para>
/// <para>
/// Each block here is 320px wide with a 16px font and 20px lines. Words are 8px wide a letter and
/// 16px tall, and stand half their line's leading down it, on a baseline 12.8px below their top: 2px
/// down a 20px line and 12px down a 40px one, so the strut's baseline is 14.8px down.
/// </para>
/// </remarks>
public sealed class LoweredBoxBaselineTests
{
    private static readonly Uri BaseUrl = new("file:///lowered-box-baseline.html");

    /// <summary>
    /// "a", then a span with <c>line-height: 40px</c> holding "W", lowered 10px, or by
    /// <c>sub</c>, a fifth of the block's 16px font and a pixel: the span reaches the top of the line
    /// less far down than the strut does, which sets the baseline 14.8px down, "a" 2px down. "W"
    /// stands as far below the baseline as it is lowered, 12px down, half the span's 24px of leading
    /// below the line's top, and the line is the span's 40px. The span set the baseline 24.8px down,
    /// where it would have stood unlowered, "a" 12px down and "W" 22px or 16.2px, and the line was 50px
    /// or 44.2px tall.
    /// </summary>
    [Theory]
    [InlineData("-10px", 2)]
    [InlineData(CssConstants.Sub, 7.8)]
    public void A_Lowered_Span_Starts_At_The_Lines_Top(string verticalAlign, double aTop)
    {
        var block = Block();
        Text(block, "a");
        Text(Span(block, verticalAlign, "40px"), "W");
        Layout(block);

        Assert.Equal(aTop, WordTop(block, "a"), 1);
        Assert.Equal(12, WordTop(block, "W"), 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// The same with "W" in a span of its own inside the lowered one: what is in a lowered inline
    /// box is as much lower. "W" stood 22px down, and the line was 50px tall.
    /// </summary>
    [Fact]
    public void Text_In_A_Span_In_A_Lowered_Span_Starts_At_The_Lines_Top()
    {
        var block = Block();
        Text(block, "a");
        Text(Span(Span(block, "-10px", "40px"), CssConstants.Baseline), "W");
        Layout(block);

        Assert.Equal(2, WordTop(block, "a"), 1);
        Assert.Equal(12, WordTop(block, "W"), 1);
        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", then a 30px image lowered 10px: it stands 20px above the baseline, which is 20px down,
    /// "a" 7.2px down, and starts at the line's top; the line is the image's 30px. The image set the
    /// baseline 30px down and stood 10px below the line's top, "a" 17.2px down, and the line was
    /// 40px tall.
    /// </summary>
    [Fact]
    public void A_Lowered_Image_Starts_At_The_Lines_Top()
    {
        var block = Block();
        Text(block, "a");
        var image = new CssBoxImage(block, new HtmlTag("img", true, new System.Collections.Generic.Dictionary<string, string> { ["src"] = "i.png" }), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "8px",
            Height = "30px",
            VerticalAlign = "-10px",
        };
        Layout(block);

        Assert.Equal(0, image.Location.Y - block.Location.Y, 1);
        Assert.Equal(7.2, WordTop(block, "a"), 1);
        Assert.Equal(30, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", then a 40px inline-block on the baseline of a span lowered 10px: the box stands 30px above
    /// the line's baseline, which is 30px down, "a" 17.2px down, and it starts at the line's top. The
    /// box set the baseline 40px down and stood 10px below the line's top, "a" 27.2px down.
    /// </summary>
    [Fact]
    public void A_Box_In_A_Lowered_Span_Starts_At_The_Lines_Top()
    {
        var block = Block();
        Text(block, "a");
        var box = new CssBox(Span(block, "-10px"), new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = "40px",
        };
        Layout(block);

        Assert.Equal(0, box.Location.Y - block.Location.Y, 1);
        Assert.Equal(17.2, WordTop(block, "a"), 1);
    }

    /// <summary>
    /// Controls, which pass before and after: the span with <c>line-height: 40px</c> raised 10px
    /// moves the line down by as much as it reaches above it, "W" 12px down, "a" 22px, in a 40px
    /// line; and "2" in a <c>sub</c> with the block's line height, which reaches the line's top less far
    /// down than the strut does, stands 4.2px below "a", on a 20px line.
    /// </summary>
    [Fact]
    public void Control_A_Raised_Span_And_A_Sub_Of_The_Blocks_Line_Height()
    {
        var raised = Block();
        Text(raised, "a");
        Text(Span(raised, "10px", "40px"), "W");
        Layout(raised);

        Assert.Equal(22, WordTop(raised, "a"), 1);
        Assert.Equal(12, WordTop(raised, "W"), 1);
        Assert.Equal(40, raised.Size.Height, 1);

        var sub = Block();
        Text(sub, "a");
        Text(Span(sub, CssConstants.Sub), "2");
        Layout(sub);

        Assert.Equal(2, WordTop(sub, "a"), 1);
        Assert.Equal(6.2, WordTop(sub, "2"), 1);
    }

    /// <summary>A 320px block with 20px lines, in a block in the root.</summary>
    private static CssBox Block()
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
            Width = "320px",
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
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => new ImageLoader(onComplete);
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class ImageLoader(Action<object?, RectangleF, bool> onComplete) : ILayoutImageLoader
    {
        private static readonly object TheImage = new();

        public object? Image { get; private set; }
        public RectangleF Rectangle => RectangleF.Empty;

        public void LoadImage(string src, System.Collections.Generic.IReadOnlyDictionary<string, string>? attributes, Uri baseUrl)
        {
            Image = TheImage;
            onComplete(TheImage, RectangleF.Empty, false);
        }

        public void Dispose() { }
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
