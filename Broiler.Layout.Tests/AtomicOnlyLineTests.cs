using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A line holding only boxes placed on it whole, inline-blocks or an image, starts where the flow
/// put it: its strut stands at its top, and it is as tall as its line height.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8: a line box holds a strut, an empty inline box in the block's font and line
/// height, and an inline-block or an image with no text beside it stands on the strut's baseline,
/// within the line. The engine took a line's top to be that of the highest thing on it. An
/// inline-block alone moves down to the baseline, and the line, measured from the moved box, was
/// taller than its line height by as much; an image stands on the baseline from the flow, and alone
/// on its line put the strut's baseline, measured from it, as far below again.
/// </para>
/// <para>
/// Each block here is 320px wide with a 20px line height, in a block in the root. Its font is
/// 16px tall, with an ascent of 12.8px, and stands half its 4px of leading below the line's top: the
/// strut's baseline is 14.8px below the line's top, and its descent below the baseline 5.2px, the
/// font's 3.2px and the other half of the leading. Words are 8px wide a letter.
/// </para>
/// </remarks>
public sealed class AtomicOnlyLineTests
{
    private static readonly Uri BaseUrl = new("file:///atomic-only-line.html");

    /// <summary>
    /// An empty 100 × 10px inline-block alone stands on the baseline, 4.8px down, and the block is
    /// 20px tall. The block was 22.8px tall.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Alone_Makes_A_Line_As_Tall_As_The_Line_Height()
    {
        var (root, block, boxes) = Build(b => InlineBlock(b, 10));
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(14.8 - 10, boxes[0].Location.Y - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// Two of them alone make a 20px line too. It was 22.8px tall.
    /// </summary>
    [Fact]
    public void Two_Inline_Blocks_Alone_Make_A_Line_As_Tall_As_The_Line_Height()
    {
        var (root, block, boxes) = Build(b =>
        {
            InlineBlock(b, 10);
            InlineBlock(b, 10);
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(14.8 - 10, boxes[1].Location.Y - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// A 100 × 10px image alone stands on the strut's baseline, 4.8px down, and the block is 20px
    /// tall. It stood 5.6px down, and the block was 25.6px tall.
    /// </summary>
    [Fact]
    public void An_Image_Alone_Stands_On_The_Struts_Baseline()
    {
        var (root, block, boxes) = Build(Image);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(14.8 - 10, ImageTop(boxes[0], block), 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// The image's box stands where its rectangle does, 4.8px down, alone on its line or beside a
    /// word. Beside a word, where the flow had stood the image on the baseline already and the
    /// alignment did not move it, the box stayed at the page's top-left corner.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_Images_Box_Stands_Where_Its_Rectangle_Does(bool word)
    {
        var (root, block, boxes) = Build(b =>
        {
            Image(b);
            if (word)
                Word(b, "x");
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(14.8 - 10, ImageTop(boxes[0], block), 1);
        Assert.Equal(14.8 - 10, boxes[0].Location.Y - block.Location.Y, 1);
    }

    /// <summary>
    /// An image after a word, standing on the baseline or aligned <c>middle</c>, has a box as
    /// wide and as tall as its rectangle, where the rectangle is: 16px in, after "xx", and 100 ×
    /// 10px. Its box was 0px wide at the page's left edge, which is where script found it.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Baseline)]
    [InlineData(CssConstants.Middle)]
    public void An_Images_Box_Is_Its_Rectangle(string verticalAlign)
    {
        var (root, block, boxes) = Build(b =>
        {
            Word(b, "xx");
            Image(b);
        });
        boxes[0].VerticalAlign = verticalAlign;
        root.PerformLayout(root.LayoutEnvironment);

        var image = boxes[0];
        var rectangle = image.Rectangles.Values.Single();
        Assert.Equal(16, rectangle.X - block.Location.X, 1);
        Assert.Equal(rectangle.X, image.Location.X, 1);
        Assert.Equal(rectangle.Y, image.Location.Y, 1);
        Assert.Equal(100, image.Size.Width, 1);
        Assert.Equal(10, image.Size.Height, 1);
    }

    /// <summary>
    /// Laid out a second time, the inline-block's line is 20px tall still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var (root, block, boxes) = Build(b => InlineBlock(b, 10));
        root.PerformLayout(root.LayoutEnvironment);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(14.8 - 10, boxes[0].Location.Y - block.Location.Y, 1);
        Assert.Equal(20, block.Size.Height, 1);
    }

    /// <summary>
    /// Controls: beside a word, the inline-block stands on the word's baseline and the block is
    /// 20px tall; a 30px inline-block alone stands at the line's top, and the block is its height
    /// and the strut's descent, 35.2px.
    /// </summary>
    [Theory]
    [InlineData(true, 10, 4.8, 20)]
    [InlineData(false, 30, 0, 35.2)]
    public void Control_Beside_A_Word_Or_Taller_Than_The_Strut(bool word, int height, double top, double blockHeight)
    {
        var (root, block, boxes) = Build(b =>
        {
            InlineBlock(b, height);
            if (word)
                Word(b, "x");
        });
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(top, boxes[0].Location.Y - block.Location.Y, 1);
        Assert.Equal(blockHeight, block.Size.Height, 1);
    }

    /// <summary>
    /// A 320px block with a 20px line height in the root, holding what <paramref name="content"/>
    /// adds. Returns the root, the block and the boxes added.
    /// </summary>
    private static (CssBox Root, CssBox Block, List<CssBox> Boxes) Build(Action<(CssBox Block, List<CssBox> Boxes)> content)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new ImageLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px", LineHeight = "20px" };

        var boxes = new List<CssBox>();
        content((block, boxes));
        return (root, block, boxes);
    }

    private static void InlineBlock((CssBox Block, List<CssBox> Boxes) at, int height) =>
        at.Boxes.Add(new CssBox(at.Block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "100px",
            Height = height + "px",
        });

    private static void Image((CssBox Block, List<CssBox> Boxes) at) =>
        at.Boxes.Add(new CssBoxImage(
            at.Block,
            new HtmlTag("img", true, new Dictionary<string, string> { ["src"] = "i.png" }),
            BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "100px",
            Height = "10px",
        });

    private static void Word((CssBox Block, List<CssBox> Boxes) at, string text)
    {
        var box = new CssBox(at.Block, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        box.ParseToWords();
    }

    /// <summary>Where the image is drawn: the top of its line rectangle, below the block's top.</summary>
    private static double ImageTop(CssBox image, CssBox block)
    {
        Assert.Single(image.Rectangles);
        foreach (var rectangle in image.Rectangles.Values)
            return rectangle.Y - block.Location.Y;

        return double.NaN;
    }

    // Every word 8px wide a letter and 16px tall, a space 4px, and every image a 300×150 bitmap
    // that loads at once.
    private sealed class ImageLayoutEnvironment : ILayoutEnvironment
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

        public void LoadImage(string src, IReadOnlyDictionary<string, string>? attributes, Uri baseUrl)
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
