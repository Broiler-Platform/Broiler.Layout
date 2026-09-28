using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A box aligned <c>middle</c> centres half its parent's x-height above the baseline, not below it.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1: <c>vertical-align: middle</c> aligns the vertical midpoint of the box with the
/// baseline of the parent box plus half the x-height of the parent. As for a length, plus raises.
/// The engine added the half x-height to the baseline's y instead, which lowered the midpoint by as
/// much: an empty 10px inline-block aligned middle in 16px text hung 11.8px below the line's top,
/// its bottom well under the text, where browsers centre it on the text's lowercase letters.
/// </para>
/// <para>
/// Each line here is in a 320px block and is 16px tall; words are 16px tall and 8px wide a letter.
/// The baseline of such a line is 12.8px below its top, and the engine takes half the font's
/// x-height as 4px.
/// </para>
/// </remarks>
public sealed class VerticalAlignMiddleTests
{
    private static readonly Uri BaseUrl = new("file:///vertical-align-middle.html");

    private const double Baseline = 12.8;

    private const double HalfXHeight = 4;

    /// <summary>
    /// An empty 10px inline-block aligned middle between "a" and "b" has its middle half an
    /// x-height above the baseline, 8.8px down, so its top is 3.8px down. Its top was 11.8px
    /// down, its middle as far below the baseline.
    /// </summary>
    [Fact]
    public void A_Box_Aligned_Middle_Centres_Above_The_Baseline()
    {
        var tree = Build();
        var box = EmptyBox(tree, 10, CssConstants.Middle);
        Layout(tree);

        Assert.Equal(Baseline - HalfXHeight - 5, tree.Top(box), 1);
        Assert.Equal(0, tree.Top(tree.Before), 1);
    }

    /// <summary>
    /// Beside a 30px image on the baseline, whose bottom is the baseline, an empty 10px inline-block
    /// aligned middle has its middle half an x-height above the image's bottom: 21px down. It was
    /// 29px down, its middle as far below the image's bottom.
    /// </summary>
    [Fact]
    public void A_Box_Aligned_Middle_Beside_An_Image_Centres_Above_The_Images_Bottom()
    {
        var tree = Build();
        var image = Image(tree, 30);
        var box = EmptyBox(tree, 10, CssConstants.Middle);
        Layout(tree);

        double imageBottom = tree.Top(image) + 30;
        Assert.Equal(30, imageBottom, 1);
        Assert.Equal(imageBottom - HalfXHeight - 5, tree.Top(box), 1);
    }

    /// <summary>
    /// An empty 40px inline-block aligned middle reaches 11.2px above the line's top, so the line
    /// moves down by as much: the box is at the top, the words 11.2px down. The box was 3.2px
    /// down.
    /// </summary>
    [Fact]
    public void A_Tall_Box_Aligned_Middle_Reaches_Above_The_Words_And_Moves_Them_Down()
    {
        var tree = Build();
        var box = EmptyBox(tree, 40, CssConstants.Middle);
        Layout(tree);

        double overhang = 20 - (Baseline - HalfXHeight);
        Assert.Equal(0, tree.Top(box), 1);
        Assert.Equal(overhang, tree.Top(tree.Before), 1);
        Assert.Equal(overhang, tree.Top(tree.After), 1);
    }

    /// <summary>
    /// A 16px word aligned middle has its middle half an x-height above the baseline, so it stands
    /// 0.8px down. It stood 8.8px down, half a line below the words beside it.
    /// </summary>
    [Fact]
    public void A_Word_Aligned_Middle_Centres_Above_The_Baseline()
    {
        var tree = Build();
        var word = Word(tree.Block, "m", before: tree.After);
        word.VerticalAlign = CssConstants.Middle;
        Layout(tree);

        Assert.Equal(Baseline - HalfXHeight - 8, tree.Top(word), 1);
        Assert.Equal(0, tree.Top(tree.Before), 1);
    }

    /// <summary>
    /// Laid out a second time, the 10px inline-block aligned middle is 3.8px down still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Position()
    {
        var tree = Build();
        var box = EmptyBox(tree, 10, CssConstants.Middle);
        Layout(tree);
        Layout(tree);

        Assert.Equal(Baseline - HalfXHeight - 5, tree.Top(box), 1);
    }

    /// <summary>
    /// Control, which passes before and after: a 10px and a 30px inline-block aligned middle on the
    /// same line share their middle.
    /// </summary>
    [Fact]
    public void Control_Boxes_Aligned_Middle_Share_Their_Middle()
    {
        var tree = Build();
        var small = EmptyBox(tree, 10, CssConstants.Middle);
        var large = EmptyBox(tree, 30, CssConstants.Middle);
        Layout(tree);

        Assert.Equal(tree.Top(large) + 15, tree.Top(small) + 5, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an empty 10px inline-block aligned top stays at the
    /// top of the line with the words.
    /// </summary>
    [Fact]
    public void Control_A_Box_Aligned_Top_Stays_At_The_Top()
    {
        var tree = Build();
        var box = EmptyBox(tree, 10, CssConstants.Top);
        Layout(tree);

        Assert.Equal(0, tree.Top(box), 1);
        Assert.Equal(0, tree.Top(tree.Before), 1);
    }

    /// <summary>
    /// An empty 10px inline-block on the baseline stands on it with its bottom, 2.8px down, the
    /// words at the top.
    /// </summary>
    [Fact]
    public void A_Box_On_The_Baseline_Stands_On_It()
    {
        var tree = Build();
        var box = EmptyBox(tree, 10, CssConstants.Baseline);
        Layout(tree);

        Assert.Equal(Baseline - 10, tree.Top(box), 1);
        Assert.Equal(0, tree.Top(tree.Before), 1);
    }

    /// <summary>The root, the 320px block, and the words "a" and "b" on its line.</summary>
    private sealed record Tree(CssBox Root, CssBox Block, CssBox Before, CssBox After)
    {
        /// <summary>How far the box's first word, or the box, stands below the block's top.</summary>
        public double Top(CssBox box) =>
            (box.Words.Count > 0 && !box.Words[0].IsImage ? box.Words[0].Top : box.Location.Y) - Block.Location.Y;
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
        var before = Word(block, "a", before: null);
        var after = Word(block, "b", before: null);

        return new Tree(root, block, before, after);
    }

    /// <summary>Puts an empty inline-block 8px wide and as tall as given before "b".</summary>
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

    private static CssBox Word(CssBox parent, string text, CssBox? before)
    {
        var word = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();

        if (before != null)
            word.SetBeforeBox(before);

        return word;
    }

    private static void Layout(Tree tree)
    {
        FlexGridItemBlockification.Generate(tree.Root);
        tree.Root.PerformLayout(tree.Root.LayoutEnvironment);
    }

    // Every word 8px wide a letter and 16px tall, a space 4px, and every image a 300×150 bitmap
    // that loads at once.
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
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
