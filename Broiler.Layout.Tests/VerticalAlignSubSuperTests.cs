using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A box aligned <c>sub</c> or <c>super</c> is lowered or raised by a share of its parent's font
/// size, as browsers do, not by a share of its own height.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1: <c>sub</c> lowers the baseline of the box to the proper position for subscripts
/// of the parent's box, and <c>super</c> raises it to the proper position for superscripts. The
/// engine moved the box by a share of its own height instead, half of it down or a fifth of it up:
/// a 60px inline-block aligned <c>sub</c> hung 30px below the baseline, and a 10px one aligned
/// <c>super</c> rose 2px above it. Chromium lowers any box by a fifth of the parent's font size and
/// a pixel, 4.2px in 16px text, and raises it by a third of that size and a pixel, 6.33px.
/// </para>
/// <para>
/// Each line here is in a 320px block with a 16px font unless given otherwise; words are 16px tall
/// and 8px wide a letter, and stand on a baseline 12.8px below their top. Images are 8px wide.
/// </para>
/// </remarks>
public sealed class VerticalAlignSubSuperTests
{
    private static readonly Uri BaseUrl = new("file:///vertical-align-sub-super.html");

    private const double Ascent = 12.8;

    /// <summary>
    /// An empty inline-block between "a" and "b" aligned <c>sub</c> ends 4.2px below the baseline,
    /// and one aligned <c>super</c> 6.33px above it, whatever its height. A 10px, 30px and 60px one
    /// ended 5px, 15px and 30px below it, or 2px, 6px and 12px above it.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Sub, 10, 4.2)]
    [InlineData(CssConstants.Sub, 30, 4.2)]
    [InlineData(CssConstants.Sub, 60, 4.2)]
    [InlineData(CssConstants.Super, 10, -6.33)]
    [InlineData(CssConstants.Super, 30, -6.33)]
    [InlineData(CssConstants.Super, 60, -6.33)]
    public void A_Box_Is_Moved_By_A_Share_Of_The_Font_Size(string verticalAlign, int height, double below)
    {
        var tree = Build();
        var box = EmptyBox(tree, height, verticalAlign);
        Layout(tree);

        Assert.Equal(below, tree.Bottom(box) - tree.Baseline, 1);
    }

    /// <summary>
    /// The share is of the parent's font: in a block with a 24px font, an empty 10px inline-block
    /// with an 8px font of its own ends 5.8px below the baseline aligned <c>sub</c>, and 9px above
    /// it aligned <c>super</c>. It ended 5px below and 2px above.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Sub, 5.8)]
    [InlineData(CssConstants.Super, -9)]
    public void The_Share_Is_Of_The_Parents_Font_Size(string verticalAlign, double below)
    {
        var tree = Build(fontSize: "24px");
        var box = EmptyBox(tree, 10, verticalAlign);
        box.FontSize = "8px";
        Layout(tree);

        Assert.Equal(below, tree.Bottom(box) - tree.Baseline, 1);
    }

    /// <summary>
    /// A 30px image aligned <c>sub</c> ends 4.2px below the baseline, as an inline-block does. It
    /// ended 15px below it.
    /// </summary>
    [Fact]
    public void An_Image_Aligned_Sub_Is_Lowered_As_Much()
    {
        var tree = Build();
        var image = Image(tree, 30, CssConstants.Sub);
        Layout(tree);

        Assert.Equal(4.2, tree.Bottom(image) - tree.Baseline, 1);
    }

    /// <summary>
    /// Laid out a second time, a 60px inline-block aligned <c>sub</c> ends 4.2px below the baseline
    /// still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var tree = Build();
        var box = EmptyBox(tree, 60, CssConstants.Sub);
        Layout(tree);
        Layout(tree);

        Assert.Equal(4.2, tree.Bottom(box) - tree.Baseline, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: an empty 30px inline-block on the baseline ends on
    /// it, and one raised by <c>vertical-align: 5px</c> ends 5px above it.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Baseline, 0)]
    [InlineData("5px", -5)]
    public void Control_A_Box_On_The_Baseline_Or_Raised_By_A_Length(string verticalAlign, double below)
    {
        var tree = Build();
        var box = EmptyBox(tree, 30, verticalAlign);
        Layout(tree);

        Assert.Equal(below, tree.Bottom(box) - tree.Baseline, 1);
    }

    /// <summary>The root, the 320px block, and the words "a" and "b" on its line.</summary>
    private sealed record Tree(CssBox Root, CssBox Block, CssBox Before, CssBox After)
    {
        /// <summary>How far below the block's top the words stand on their baseline.</summary>
        public double Baseline => Before.Words[0].Top - Block.Location.Y + Ascent;

        /// <summary>How far below the block's top the box ends.</summary>
        public double Bottom(CssBox box) => box.Location.Y + box.Size.Height - Block.Location.Y;
    }

    /// <summary>In a 320px block in the root, with the font size given, the words "a" and "b".</summary>
    private static Tree Build(string? fontSize = null)
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
        if (fontSize != null)
            block.FontSize = fontSize;

        var before = Word(block, "a");
        var after = Word(block, "b");

        return new Tree(root, block, before, after);
    }

    /// <summary>Puts an empty inline-block 8px wide, as tall and aligned as given, before "b".</summary>
    private static CssBox EmptyBox(Tree tree, int height, string verticalAlign)
    {
        var box = new CssBox(tree.Block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "inline-block",
            Width = "8px",
            Height = $"{height}px",
            VerticalAlign = verticalAlign,
        };

        box.SetBeforeBox(tree.After);
        return box;
    }

    /// <summary>Puts an image 8px wide, as tall and aligned as given, before "b".</summary>
    private static CssBoxImage Image(Tree tree, int height, string verticalAlign)
    {
        var image = new CssBoxImage(
            tree.Block,
            new HtmlTag("img", true, new Dictionary<string, string> { ["src"] = "i.png" }),
            BaseUrl)
        {
            Display = CssConstants.Inline,
            Width = "8px",
            Height = $"{height}px",
            VerticalAlign = verticalAlign,
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

    // Every word 8px wide a letter and 16px tall in a font 16px high, of the size asked for, a space
    // 4px, and every image a 300×150 bitmap that loads at once.
    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => new FakeFont(size);
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

    private sealed class FakeFont(double size) : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => size;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
