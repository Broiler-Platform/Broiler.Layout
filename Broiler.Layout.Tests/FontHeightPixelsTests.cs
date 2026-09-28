using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Broiler.Layout.IR;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A font's height is taken as the host gives it, in CSS pixels, so text stands its ascent, 0.8 of
/// that height, above the baseline, and what stands on the baseline beside it stands level with it.
/// </summary>
/// <remarks>
/// <para>
/// <c>ILayoutFont.Height</c> is in CSS pixels, and the normal line height
/// (<c>CssBoxProperties.GetNormalLineHeight</c>) already reads it so. The line layout and the SVG
/// text renderer multiplied it by 96/72 as though it were in points, which made the ascent and the
/// strut a third too tall: 16px text, whose glyphs are 18.56px tall, had its baseline 19.8px below
/// its top, under the glyphs themselves. An image on the baseline beside it sat that much too low
/// against the text, and SVG text was drawn that much too high above its <c>y</c>.
/// </para>
/// <para>
/// Each line here is in a 320px block and is 16px tall; words are 16px tall and 8px wide a letter,
/// in a font 16px high, so their ascent is 12.8px. Images are 8px wide.
/// </para>
/// </remarks>
public sealed class FontHeightPixelsTests
{
    private static readonly Uri BaseUrl = new("file:///font-height-pixels.html");

    private const double Ascent = 12.8;

    /// <summary>
    /// Beside a 30px image, whose bottom is the baseline, "a" stands with its baseline on the
    /// image's bottom: 17.2px down. It stood 12.93px down, its baseline 17.07px below its top.
    /// </summary>
    [Fact]
    public void Text_Beside_A_Tall_Image_Stands_On_The_Images_Bottom()
    {
        var tree = Build();
        var image = Image(tree, 30);
        Layout(tree);

        Assert.Equal(0, tree.Top(image), 1);
        Assert.Equal(30 - Ascent, tree.Top(tree.Before), 1);
        Assert.Equal(30 - Ascent, tree.Top(tree.After), 1);
    }

    /// <summary>
    /// A 10px image beside "a" stands on its baseline, 12.8px below the line's top, so its top is
    /// 2.8px down and the line stays 16px tall. Its top was 7.07px down, and the line 20.27px tall.
    /// </summary>
    [Fact]
    public void A_Short_Image_Stands_On_The_Baseline_Of_The_Text_Beside_It()
    {
        var tree = Build();
        var image = Image(tree, 10);
        Layout(tree);

        Assert.Equal(Ascent - 10, tree.Top(image), 1);
        Assert.Equal(0, tree.Top(tree.Before), 1);
        Assert.Equal(16, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// SVG <c>&lt;text y="30"&gt;</c> in a 16px font is drawn from 17.2px down, its ascent above
    /// the baseline at <c>y</c>. It was drawn from 12.93px down.
    /// </summary>
    [Fact]
    public void Svg_Text_Stands_On_Its_Baseline()
    {
        SvgTextEnvironment.Reset(new FakeLayoutEnvironment());

        try
        {
            var items = SvgRenderer.RenderSvgContent(
                """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="50"><text x="0" y="30" font-size="16">Hi</text></svg>""",
                new RectangleF(0, 0, 100, 50));

            var text = Assert.Single(items.OfType<DrawSvgTextItem>());
            Assert.Equal(30 - Ascent, text.Y, 1);
        }
        finally
        {
            SvgTextEnvironment.Reset(null);
        }
    }

    /// <summary>
    /// Laid out a second time, the text beside the 30px image is 17.2px down still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var tree = Build();
        Image(tree, 30);
        Layout(tree);
        Layout(tree);

        Assert.Equal(30 - Ascent, tree.Top(tree.Before), 1);
    }

    /// <summary>
    /// Control, which passes before and after: a line of text alone is 16px tall, its words at the
    /// top.
    /// </summary>
    [Fact]
    public void Control_A_Line_Of_Text_Is_As_Tall_As_Its_Line_Height()
    {
        var tree = Build();
        Layout(tree);

        Assert.Equal(16, tree.Block.Size.Height, 1);
        Assert.Equal(0, tree.Top(tree.Before), 1);
    }

    /// <summary>The root, the 320px block, and the words "a" and "b" on its line.</summary>
    private sealed record Tree(CssBox Root, CssBox Block, CssBox Before, CssBox After)
    {
        /// <summary>How far the box's first word stands below the block's top.</summary>
        public double Top(CssBox box) => box.Words[0].Top - Block.Location.Y;
    }

    /// <summary>In a 320px block in the root, the words "a" and "b".</summary>
    private static Tree Build()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        var before = Word(block, "a");
        var after = Word(block, "b");

        return new Tree(root, block, before, after);
    }

    /// <summary>Puts an image 8px wide and as tall as given, on the baseline, before "b".</summary>
    private static CssBoxImage Image(Tree tree, int height)
    {
        var image = new CssBoxImage(
            tree.Block,
            new HtmlTag("img", true, new Dictionary<string, string> { ["src"] = "i.png" }),
            BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "8px",
            Height = $"{height}px",
        };

        image.SetBeforeBox(tree.After);
        return image;
    }

    private static CssBox Word(CssBox parent, string text)
    {
        var word = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();
        return word;
    }

    private static void Layout(Tree tree)
    {
        FlexGridItemBlockification.Generate(tree.Root);
        tree.Root.PerformLayout(tree.Root.LayoutEnvironment);
    }

    // Every word 8px wide a letter and 16px tall in a font 16px high, a space 4px, and every image a
    // 300×150 bitmap that loads at once.
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
