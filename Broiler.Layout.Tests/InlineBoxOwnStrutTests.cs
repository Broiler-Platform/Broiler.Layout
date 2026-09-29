using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline box that holds no text of its own on a line, only an image or an inline-block, is as
/// tall on that line as its own line height around its own font.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1 makes every inline box as tall as its <c>line-height</c>, half its leading above
/// its font's glyphs and half below, whatever it holds, and the line box as tall as all of them. A
/// line was measured from its words and the boxes placed on it whole, so an inline box counted only
/// through its own text. Holding only an inline-block, it counted for nothing: a span with
/// <c>line-height: 40px</c> around one made a 20px line of 16px/20px text, where browsers make it
/// 40px. Holding only an image, it counted as though its text started where the image does, below
/// the line's top: that span made the line too short, and one in a 32px font too tall.
/// </para>
/// <para>
/// Each block here is 300px wide with 20px lines of a 16px font, whose baseline is 12.8px below the
/// top of its glyphs; the strut's is 14.8px down its line. Words are 8px wide a letter, and the
/// images and inline-blocks 8 × 10px.
/// </para>
/// </remarks>
public sealed class InlineBoxOwnStrutTests
{
    private static readonly Uri BaseUrl = new("file:///inline-box-own-strut.html");

    /// <summary>
    /// "a", then a span with <c>line-height: 40px</c> holding an image: the span's line height is
    /// the line's, which is 40px tall, its baseline 24.8px down, half the span's leading and the
    /// font's ascent. The image stands on it, 14.8px down, and "a" 12px down.
    /// </summary>
    [Fact]
    public void A_Span_With_A_Taller_Line_Height_Around_An_Image_Makes_Its_Line_That_Tall()
    {
        var block = Block();
        Text(block, "a");
        var image = Image(Span(block, lineHeight: "40px"));
        Layout(block);

        Assert.Equal(40, block.Size.Height, 1);
        Assert.Equal(14.8, image.Words[0].Top - block.Location.Y, 1);
        Assert.Equal(12, Word(block, "a").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// The same span around an inline-block: the line is 40px tall, and the box stands on its
    /// baseline, 14.8px down.
    /// </summary>
    [Fact]
    public void A_Span_With_A_Taller_Line_Height_Around_An_Inline_Block_Makes_Its_Line_That_Tall()
    {
        var block = Block();
        Text(block, "a");
        var box = InlineBlock(Span(block, lineHeight: "40px"));
        Layout(block);

        Assert.Equal(40, block.Size.Height, 1);
        Assert.Equal(14.8, box.Location.Y - block.Location.Y, 1);
    }

    /// <summary>
    /// In a 20px block, "a", the same span around an inline-block, then " bb", which wraps: the
    /// second line starts below the span, 40px down, and "bb" stands 42px down.
    /// </summary>
    [Fact]
    public void The_Next_Line_Starts_Below_A_Span_With_A_Taller_Line_Height_Around_An_Inline_Block()
    {
        var block = Block("20px");
        Text(block, "a");
        InlineBlock(Span(block, lineHeight: "40px"));
        Text(block, " bb");
        Layout(block);

        Assert.Equal(42, Word(block, "bb").Top - block.Location.Y, 1);
        Assert.Equal(60, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", then a span in a 32px font holding an image. The span's 20px line height stands its
    /// glyphs' top 6px above its own top, so its baseline is 19.6px below the line's top, 4.8px
    /// lower than the strut's. The line reaches the strut's descent below that, and is 24.8px tall;
    /// the image stands 9.6px down, and "a" 6.8px down.
    /// </summary>
    [Fact]
    public void A_Span_In_A_Larger_Font_Around_An_Image_Lowers_The_Baseline_By_Its_Own_Strut()
    {
        var block = Block();
        Text(block, "a");
        var image = Image(Span(block, fontSize: "32px"));
        Layout(block);

        Assert.Equal(24.8, block.Size.Height, 1);
        Assert.Equal(9.6, image.Words[0].Top - block.Location.Y, 1);
        Assert.Equal(6.8, Word(block, "a").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "a", then a span with <c>line-height: 40px</c> raised 10px, around an image: the span's top
    /// reaches 10px above where it would stand on the baseline, and the line moves down to hold it.
    /// The line is 40px tall, its baseline 34.8px down; the image stands on the span's, 14.8px down,
    /// and "a" 22px down.
    /// </summary>
    [Fact]
    public void A_Raised_Span_Around_An_Image_Moves_Its_Line_Down_To_Hold_Its_Strut()
    {
        var block = Block();
        Text(block, "a");
        var span = Span(block, lineHeight: "40px");
        span.VerticalAlign = "10px";
        var image = Image(span);
        Layout(block);

        Assert.Equal(40, block.Size.Height, 1);
        Assert.Equal(14.8, image.Words[0].Top - block.Location.Y, 1);
        Assert.Equal(22, Word(block, "a").Top - block.Location.Y, 1);
    }

    /// <summary>
    /// "a", a span with <c>line-height: 40px</c> around an image, then an inline-block aligned
    /// <c>bottom</c>: the box ends at the bottom of the 40px line, 30px down.
    /// </summary>
    [Fact]
    public void A_Box_Aligned_Bottom_Ends_At_The_Bottom_Of_A_Spans_Strut()
    {
        var block = Block();
        Text(block, "a");
        Image(Span(block, lineHeight: "40px"));
        var box = InlineBlock(block);
        box.VerticalAlign = CssConstants.Bottom;
        Layout(block);

        Assert.Equal(40, block.Size.Height, 1);
        Assert.Equal(30, box.Location.Y - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a span with <c>line-height: 40px</c> holding text
    /// makes its line 40px tall through its words.
    /// </summary>
    [Fact]
    public void Control_A_Span_With_A_Taller_Line_Height_Around_Text()
    {
        var block = Block();
        Text(block, "a");
        Text(Span(block, lineHeight: "40px"), "b");
        Layout(block);

        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a span with the block's line height and font around
    /// an inline-block has the strut's line: 20px tall, the box standing on its baseline, 4.8px down.
    /// </summary>
    [Fact]
    public void Control_A_Span_With_The_Blocks_Line_Height_Around_An_Inline_Block()
    {
        var block = Block();
        Text(block, "a");
        var box = InlineBlock(Span(block));
        Layout(block);

        Assert.Equal(20, block.Size.Height, 1);
        Assert.Equal(4.8, box.Location.Y - block.Location.Y, 1);
    }

    /// <summary>A block of the given width with 20px lines of a 16px font, in a block in the root.</summary>
    private static CssBox Block(string width = "300px")
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
            FontSize = "16px",
            LineHeight = "20px",
        };
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    /// <summary>A span in <paramref name="parent"/> inheriting its style, with the line height or font size given.</summary>
    private static CssBox Span(CssBox parent, string? lineHeight = null, string? fontSize = null)
    {
        var span = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl);
        span.InheritStyle();
        span.Display = CssConstants.Inline;

        if (lineHeight != null)
            span.LineHeight = lineHeight;

        if (fontSize != null)
            span.FontSize = fontSize;

        return span;
    }

    /// <summary>An 8 × 10px inline image.</summary>
    private static CssBoxImage Image(CssBox parent)
    {
        var image = new CssBoxImage(parent, new HtmlTag("img", true, new System.Collections.Generic.Dictionary<string, string> { ["src"] = "i.png" }), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "8px",
            Height = "10px",
        };
        image.InheritStyle();
        return image;
    }

    /// <summary>An empty 8 × 10px inline-block.</summary>
    private static CssBox InlineBlock(CssBox parent) =>
        new(parent, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = "10px",
        };

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
