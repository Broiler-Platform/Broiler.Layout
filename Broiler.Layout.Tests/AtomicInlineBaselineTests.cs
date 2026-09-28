using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline-block whose baseline is its bottom margin edge, aligned to the baseline, stands on the
/// line's baseline, and the text beside it stands level with its bottom.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1: the baseline of an empty inline-block, or of one whose <c>overflow</c> is not
/// <c>visible</c>, is its bottom margin edge, and on the baseline it stands with that edge on the
/// line's baseline, the text beside it too. The engine stood such boxes on the lowest bottom among
/// them and left the text at the top of the line: beside an empty 30px inline-block, 16px text sat
/// at the top where browsers stand it on the box's bottom, 17.2px down, and an empty 10px one
/// stayed at the top where browsers stand it on the text's baseline.
/// </para>
/// <para>
/// Each line here is in a 320px block and is 16px tall; words are 16px tall and 8px wide a letter,
/// and stand on a baseline 12.8px below their top.
/// </para>
/// </remarks>
public sealed class AtomicInlineBaselineTests
{
    private static readonly Uri BaseUrl = new("file:///atomic-inline-baseline.html");

    private const double Ascent = 12.8;

    /// <summary>
    /// Beside an empty 30px inline-block, "a" and "b" stand with their baseline on its bottom,
    /// 17.2px down, and the line is 33.2px tall, the strut's descent below the box. They stood at
    /// the top of the line.
    /// </summary>
    [Fact]
    public void Text_Beside_An_Empty_Inline_Block_Stands_On_Its_Bottom()
    {
        var tree = Build();
        var box = EmptyBox(tree, 30);
        Layout(tree);

        Assert.Equal(0, tree.Top(box), 1);
        Assert.Equal(30 - Ascent, tree.Top(tree.Before), 1);
        Assert.Equal(30 - Ascent, tree.Top(tree.After), 1);
        Assert.Equal(33.2, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// An empty 10px inline-block stands on the baseline of the text beside it, 2.8px down, and the
    /// line is 16px tall. It stood at the top of the line.
    /// </summary>
    [Fact]
    public void A_Short_Empty_Inline_Block_Stands_On_The_Baseline_Of_The_Text()
    {
        var tree = Build();
        var box = EmptyBox(tree, 10);
        Layout(tree);

        Assert.Equal(Ascent - 10, tree.Top(box), 1);
        Assert.Equal(0, tree.Top(tree.Before), 1);
        Assert.Equal(16, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// Beside an inline-block holding "x" with <c>overflow: hidden</c>, the words stand with their
    /// baseline on its bottom. They stood at the top of the line.
    /// </summary>
    [Fact]
    public void Text_Beside_An_Inline_Block_That_Clips_Stands_On_Its_Bottom()
    {
        var tree = Build();
        var box = EmptyBox(tree, 0);
        box.Height = CssConstants.Auto;
        box.Width = CssConstants.Auto;
        box.Overflow = "hidden";
        var word = new CssBox(box, null, BaseUrl) { Display = "inline", Text = "x".AsMemory() };
        word.ParseToWords();
        Layout(tree);

        double bottom = tree.Top(box) + box.Size.Height;
        Assert.True(bottom > Ascent, $"The box ends {bottom}px down.");
        Assert.Equal(bottom, tree.Top(tree.Before) + Ascent, 1);
    }

    /// <summary>
    /// An empty 30px inline-block with a 5px bottom margin stands on the baseline with its margin
    /// box: the words stand with their baseline 35px down, 22.2px below the line's top, and the box
    /// at the top. The words stood at the top of the line.
    /// </summary>
    [Fact]
    public void The_Margin_Box_Stands_On_The_Baseline()
    {
        var tree = Build();
        var box = EmptyBox(tree, 30);
        box.MarginBottom = "5px";
        Layout(tree);

        Assert.Equal(0, tree.Top(box), 1);
        Assert.Equal(35 - Ascent, tree.Top(tree.Before), 1);
    }

    /// <summary>
    /// An inline-block holding a word of its own, as a <c>::before</c> with
    /// <c>display: inline-block</c> does, has its baseline at the word's: beside the 30px
    /// inline-block, "X" stands 17.2px down with "a". It stood with its bottom on the box's bottom,
    /// 14px down.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Holding_Its_Own_Word_Stands_It_With_The_Text()
    {
        var tree = Build();
        EmptyBox(tree, 30);
        var marker = new CssBox(tree.Block, null, BaseUrl) { Display = "inline-block" };
        marker.Words.Add(new CssRectWord(marker, "X", false, false));
        marker.SetBeforeBox(tree.After);
        Layout(tree);

        Assert.Equal(30 - Ascent, tree.Top(tree.Before), 1);
        Assert.Equal(30 - Ascent, tree.Top(marker), 1);
    }

    /// <summary>
    /// Laid out a second time, the words beside the 30px inline-block are 17.2px down still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var tree = Build();
        EmptyBox(tree, 30);
        Layout(tree);
        Layout(tree);

        Assert.Equal(30 - Ascent, tree.Top(tree.Before), 1);
        Assert.Equal(33.2, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: with no text on the line, an empty 20px and an empty
    /// 30px inline-block have their bottoms flush, the 20px one 10px down; and an empty 30px one
    /// aligned <c>top</c> stays at the top, with the words.
    /// </summary>
    [Fact]
    public void Control_Boxes_Without_Text_Share_Their_Bottom_And_A_Box_Aligned_Top_Stays_There()
    {
        var bare = Build(words: false);
        var shorter = EmptyBox(bare, 20);
        var taller = EmptyBox(bare, 30);
        Layout(bare);

        Assert.Equal(bare.Top(taller) + 10, bare.Top(shorter), 1);

        var tree = Build();
        var top = EmptyBox(tree, 30);
        top.VerticalAlign = CssConstants.Top;
        Layout(tree);

        Assert.Equal(0, tree.Top(top), 1);
        Assert.Equal(0, tree.Top(tree.Before), 1);
    }

    /// <summary>The root, the 320px block, and the words "a" and "b" on its line, if any.</summary>
    private sealed record Tree(CssBox Root, CssBox Block, CssBox Before, CssBox After)
    {
        /// <summary>How far the box's first word, or the box, stands below the block's top.</summary>
        public double Top(CssBox box) =>
            (box.Words.Count > 0 ? box.Words[0].Top : box.Location.Y) - Block.Location.Y;
    }

    /// <summary>In a 320px block in the root, the words "a" and "b", or no words.</summary>
    private static Tree Build(bool words = true)
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
        var before = words ? Word(block, "a") : block;
        var after = words ? Word(block, "b") : block;

        return new Tree(root, block, before, after);
    }

    /// <summary>
    /// Puts an empty inline-block 8px wide and as tall as given before "b", or at the end of a line
    /// with no words.
    /// </summary>
    private static CssBox EmptyBox(Tree tree, int height)
    {
        var box = new CssBox(tree.Block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "inline-block",
            Width = "8px",
            Height = $"{height}px",
        };

        if (tree.After != tree.Block)
            box.SetBeforeBox(tree.After);

        return box;
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
