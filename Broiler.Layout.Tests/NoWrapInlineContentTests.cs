using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// In a box with <c>white-space: nowrap</c>, a line does not break between its inline children,
/// whether they are text, images or inline-blocks.
/// </summary>
/// <remarks>
/// <para>
/// CSS Text 3 §5.1: whether a line may break between two pieces of content is for the
/// <c>white-space</c> of their nearest common ancestor, and <c>nowrap</c> allows no break. The flow
/// kept a box that does not wrap from breaking inside itself, but broke the line before any such box
/// that did not fit, and before any inline-block: in a 25px block with <c>white-space: nowrap</c>, an
/// 84px and a 20px inline-block went on two lines, and so did "aaa" and a span holding "bbbbbb", where
/// browsers keep each pair on one line and let it overflow.
/// </para>
/// <para>
/// Each block here is 25px wide with 20px lines. Words are 8px wide a letter and 16px tall, and stand
/// 2px down their line; a 10px inline-block stands 4.8px down, on the baseline.
/// </para>
/// </remarks>
public sealed class NoWrapInlineContentTests
{
    private static readonly Uri BaseUrl = new("file:///nowrap-inline-content.html");

    /// <summary>
    /// An 84px and a 20px inline-block in a block that does not wrap: the second stands beside the
    /// first, 84px along and 4.8px down, and the block is a line tall. It went to the second line.
    /// </summary>
    [Fact]
    public void Inline_Blocks_In_A_Block_That_Does_Not_Wrap_Stay_On_One_Line()
    {
        var block = Block(CssConstants.NoWrap);
        Box(block, 84);
        var second = Box(block, 20);
        Layout(block);

        Assert.Equal(84, second.Location.X - block.Location.X, 1);
        Assert.Equal(4.8, second.Location.Y - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// The two inline-blocks in a span that does not wrap, in a block that does: they stay on one
    /// line too.
    /// </summary>
    [Fact]
    public void Inline_Blocks_In_A_Span_That_Does_Not_Wrap_Stay_On_One_Line()
    {
        var block = Block();
        var span = Span(block, CssConstants.NoWrap);
        Box(span, 84);
        var second = Box(span, 20);
        Layout(block);

        Assert.Equal(84, second.Location.X - block.Location.X, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// "aaa", then a span holding "bbbbbb", in a block that does not wrap: "bbbbbb" follows "aaa" on
    /// the first line, 24px along and 2px down. It went to the second line, 22px down.
    /// </summary>
    [Fact]
    public void Text_After_Text_In_A_Block_That_Does_Not_Wrap_Stays_On_Its_Line()
    {
        var block = Block(CssConstants.NoWrap);
        Text(block, "aaa");
        Text(Span(block), "bbbbbb");
        Layout(block);

        Assert.Equal(24, Word(block, "bbbbbb").Left - block.Location.X, 1);
        Assert.Equal(2, Word(block, "bbbbbb").Top - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", then an 84px image, in a block that does not wrap: the image follows "a", 8px along. It
    /// went to the second line.
    /// </summary>
    [Fact]
    public void An_Image_After_Text_In_A_Block_That_Does_Not_Wrap_Stays_On_Its_Line()
    {
        var block = Block(CssConstants.NoWrap);
        Text(block, "a");
        var image = new CssBoxImage(block, new HtmlTag("img", true, new System.Collections.Generic.Dictionary<string, string> { ["src"] = "i.png" }), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "84px",
            Height = "10px",
        };
        image.InheritStyle();
        Layout(block);

        Assert.Equal(8, image.Words[0].Left - block.Location.X, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "aaa " then a span that does not wrap holding
    /// "bbbbbb", in a block that does: the line breaks at the space, where the block allows it, and
    /// "bbbbbb" starts the second line, 22px down.
    /// </summary>
    [Fact]
    public void Control_A_Span_That_Does_Not_Wrap_After_Text_That_Does()
    {
        var block = Block();
        Text(block, "aaa ");
        Text(Span(block, CssConstants.NoWrap), "bbbbbb");
        Layout(block);

        Assert.Equal(0, Word(block, "bbbbbb").Left - block.Location.X, 1);
        Assert.Equal(22, Word(block, "bbbbbb").Top - block.Location.Y, 1);
    }

    /// <summary>A 25px block with 20px lines and the given <c>white-space</c>, in a block in the root.</summary>
    private static CssBox Block(string whiteSpace = CssConstants.Normal)
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
            Width = "25px",
            LineHeight = "20px",
            WhiteSpace = whiteSpace,
        };
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    /// <summary>A span in <paramref name="parent"/> inheriting its style, with its own <c>white-space</c> if one is given.</summary>
    private static CssBox Span(CssBox parent, string? whiteSpace = null)
    {
        var span = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl);
        span.InheritStyle();
        span.Display = CssConstants.Inline;

        if (whiteSpace != null)
            span.WhiteSpace = whiteSpace;

        return span;
    }

    /// <summary>An empty 10px tall inline-block of the given width.</summary>
    private static CssBox Box(CssBox parent, int width) =>
        new(parent, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = width + "px",
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
