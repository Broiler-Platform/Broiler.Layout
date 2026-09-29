using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An image on a line is followed by a space only where one is in the markup.
/// </summary>
/// <remarks>
/// <para>
/// CSS Text 3 §4.1: a space between two pieces of inline content is white space in the text
/// between them, and nothing puts one after a replaced element. Every image was followed by a
/// space's width all the same (<c>CssRect.ActualWordSpacing</c>), so two images with nothing
/// between them stood a space apart, text right after an image a space along from it, and two 50px
/// images did not fit on a 100px line, where browsers put each against the one before it.
/// </para>
/// <para>
/// Each block here has 20px lines. Words are 8px wide a letter, and a space is 4px wide. The images
/// are 10px tall.
/// </para>
/// </remarks>
public sealed class ImageWordSpacingTests
{
    private static readonly Uri BaseUrl = new("file:///image-word-spacing.html");

    /// <summary>
    /// An 84px and a 20px image with nothing between them: the second starts where the first ends,
    /// 84px along. It started a space further, 88px along.
    /// </summary>
    [Fact]
    public void Two_Images_With_Nothing_Between_Them_Abut()
    {
        var block = Block();
        Image(block, 84);
        var second = Image(block, 20);
        Layout(block);

        Assert.Equal(84, second.Words[0].Left - block.Location.X, 1);
    }

    /// <summary>
    /// Two 50px images in a 100px block fill its line and stay on it: the second is 50px along, and
    /// the block is a line tall. The second went to the next line, and the block was 40px tall.
    /// </summary>
    [Fact]
    public void Two_Images_That_Fill_A_Line_Stay_On_It()
    {
        var block = Block("100px");
        var first = Image(block, 50);
        var second = Image(block, 50);
        Layout(block);

        Assert.Equal(50, second.Words[0].Left - block.Location.X, 1);
        Assert.Equal(first.Words[0].Top, second.Words[0].Top, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// "a", an 84px image, then "b" in a span: "b" starts where the image ends, 92px along. It
    /// started 96px along.
    /// </summary>
    [Fact]
    public void Text_Right_After_An_Image_Starts_Where_It_Ends()
    {
        var block = Block();
        Text(block, "a");
        Image(block, 84);
        Text(Span(block), "b");
        Layout(block);

        Assert.Equal(92, Word(block, "b").Left - block.Location.X, 1);
    }

    /// <summary>
    /// An 84px and a 20px image on a right-aligned 300px line: they end at the line's right, the
    /// first 196px along. It was 192px along, the space after it still on the line.
    /// </summary>
    [Fact]
    public void Images_On_A_Right_Aligned_Line_End_At_Its_Right()
    {
        var block = Block(textAlign: CssConstants.Right);
        var first = Image(block, 84);
        var second = Image(block, 20);
        Layout(block);

        Assert.Equal(196, first.Words[0].Left - block.Location.X, 1);
        Assert.Equal(280, second.Words[0].Left - block.Location.X, 1);
    }

    /// <summary>
    /// A space between an 84px and a 20px image is one space wide: the second is 88px along. It was
    /// 92px along, a space from the markup and one after the image.
    /// </summary>
    [Fact]
    public void A_Space_Between_Two_Images_Is_One_Space_Wide()
    {
        var block = Block();
        Image(block, 84);
        Text(block, " ");
        var second = Image(block, 20);
        Layout(block);

        Assert.Equal(88, second.Words[0].Left - block.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an inline-block holding only an 84px image is 84px
    /// wide, with no space after the image.
    /// </summary>
    [Fact]
    public void Control_An_Inline_Block_Holding_An_Image_Is_As_Wide_As_It()
    {
        var block = Block();
        var box = new CssBox(block, new HtmlTag("span", false, null), BaseUrl) { Display = CssConstants.InlineBlock };
        Image(box, 84);
        Layout(block);

        Assert.Equal(84, box.Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: "a ", then an 84px image: the space after "a" keeps
    /// its width, and the image starts 12px along.
    /// </summary>
    [Fact]
    public void Control_A_Space_Before_An_Image_Keeps_Its_Width()
    {
        var block = Block();
        Text(block, "a ");
        var image = Image(block, 84);
        Layout(block);

        Assert.Equal(12, image.Words[0].Left - block.Location.X, 1);
    }

    /// <summary>A block of the given width and alignment with 20px lines, in a block in the root.</summary>
    private static CssBox Block(string width = "300px", string textAlign = CssConstants.Left)
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
            TextAlign = textAlign,
        };
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    /// <summary>An inline image of the given width, 10px tall.</summary>
    private static CssBoxImage Image(CssBox parent, int width)
    {
        var image = new CssBoxImage(parent, new HtmlTag("img", true, new System.Collections.Generic.Dictionary<string, string> { ["src"] = "i.png" }), BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = width + "px",
            Height = "10px",
        };
        image.InheritStyle();
        return image;
    }

    /// <summary>A span in <paramref name="parent"/> inheriting its style.</summary>
    private static CssBox Span(CssBox parent)
    {
        var span = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl);
        span.InheritStyle();
        span.Display = CssConstants.Inline;
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
