using System;
using System.Drawing;
using System.Linq;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline-block with a line of text in it stands on its last line's baseline: the text beside
/// it stands level with that line, and the line is no taller than what is on it.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8.1: the baseline of an <c>inline-block</c> is the baseline of its last line box in
/// the normal flow, unless it has no in-flow line boxes or its <c>overflow</c> is not
/// <c>visible</c>, when it is its bottom margin edge. The engine left an inline-block of text at the
/// top of its line and made room for a strut's descent below its bottom, as if it stood on its
/// bottom edge, and inside it again below its last line: a 16px line holding an inline-block of
/// 16px text was 22.4px tall, the inline-block 19.2px, where browsers make both 16px tall. Text
/// beside an inline-block of two lines stood level with its first line, not its last.
/// </para>
/// <para>
/// Each line here is in a 320px block and is 16px tall; words are 16px tall and 8px wide a
/// letter.
/// </para>
/// </remarks>
public sealed class InlineBlockTextBaselineTests
{
    private static readonly Uri BaseUrl = new("file:///inline-block-text-baseline.html");

    /// <summary>
    /// An inline-block holding "x" between "a" and "b" stands with its text on the line's
    /// baseline: the line and the inline-block are 16px tall, and the three words stand level. The
    /// line was 22.4px tall and the inline-block 19.2px.
    /// </summary>
    [Fact]
    public void A_Line_Holding_An_Inline_Block_Of_Text_Is_As_Tall_As_The_Text()
    {
        var tree = Build();
        var x = Word(tree.InlineBlock, "x");
        Layout(tree);

        Assert.Equal(16, tree.Block.Size.Height, 1);
        Assert.Equal(0, tree.Top(tree.InlineBlock), 1);
        Assert.Equal(16, tree.InlineBlock.Size.Height, 1);
        Assert.Equal(0, tree.Top(x), 1);
        Assert.Equal(0, tree.Top(tree.Before!), 1);
        Assert.Equal(0, tree.Top(tree.After!), 1);
    }

    /// <summary>
    /// Beside an inline-block of two blocks of text, "x" above "y", the words stand level with "y",
    /// on its last line, 16px down, and the line is 32px tall, as tall as the inline-block. They
    /// stood level with "x", and the line was 35.2px tall.
    /// </summary>
    [Fact]
    public void Text_Beside_An_Inline_Block_Of_Two_Lines_Stands_Level_With_Its_Last()
    {
        var tree = Build();
        var x = Word(Block(tree.InlineBlock), "x");
        var y = Word(Block(tree.InlineBlock), "y");
        Layout(tree);

        Assert.Equal(32, tree.Block.Size.Height, 1);
        Assert.Equal(32, tree.InlineBlock.Size.Height, 1);
        Assert.Equal(0, tree.Top(x), 1);
        Assert.Equal(16, tree.Top(y), 1);
        Assert.Equal(16, tree.Top(tree.Before!), 1);
        Assert.Equal(16, tree.Top(tree.After!), 1);
    }

    /// <summary>
    /// An inline-block holding "x", alone on its line, makes a 16px line. It made a 22.4px one.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Of_Text_Alone_On_Its_Line_Makes_It_As_Tall_As_The_Text()
    {
        var tree = Build(alone: true);
        Word(tree.InlineBlock, "x");
        Layout(tree);

        Assert.Equal(16, tree.Block.Size.Height, 1);
        Assert.Equal(16, tree.InlineBlock.Size.Height, 1);
    }

    /// <summary>
    /// An inline-block with <c>height: 40px</c> holding "x" stands on its one line's baseline, so
    /// the words beside it stand level with "x", at the top, and the line is 40px tall, as tall as
    /// the inline-block. The line was 43.2px tall.
    /// </summary>
    [Fact]
    public void A_Taller_Inline_Block_Stands_On_Its_Line_Not_On_Its_Bottom()
    {
        var tree = Build();
        tree.InlineBlock.Height = "40px";
        var x = Word(tree.InlineBlock, "x");
        Layout(tree);

        Assert.Equal(40, tree.Block.Size.Height, 1);
        Assert.Equal(0, tree.Top(x), 1);
        Assert.Equal(0, tree.Top(tree.Before!), 1);
        Assert.Equal(0, tree.Top(tree.After!), 1);
    }

    /// <summary>
    /// An inline-block holding "z" beside one holding "x" above "y" moves down with its text to
    /// stand on the same baseline: "z" stands level with "y", 16px down, and the line is 32px tall.
    /// It stayed at the top of the line, "z" level with "x", and the line was 35.2px tall.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Of_Text_Moves_Down_With_Its_Text_To_The_Baseline()
    {
        var tree = Build();
        Word(Block(tree.InlineBlock), "x");
        var y = Word(Block(tree.InlineBlock), "y");
        var (other, z) = TextInlineBlock(tree.Block, "z");
        Layout(tree);

        Assert.Equal(32, tree.Block.Size.Height, 1);
        Assert.Equal(16, tree.Top(y), 1);
        Assert.Equal(16, tree.Top(other), 1);
        Assert.Equal(16, tree.Top(z), 1);
    }

    /// <summary>
    /// Laid out a second time, the inline-block holding "z" and its text are 16px down still, and
    /// the line 32px tall.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var tree = Build();
        Word(Block(tree.InlineBlock), "x");
        Word(Block(tree.InlineBlock), "y");
        var (other, z) = TextInlineBlock(tree.Block, "z");
        Layout(tree);
        Layout(tree);

        Assert.Equal(32, tree.Block.Size.Height, 1);
        Assert.Equal(16, tree.Top(other), 1);
        Assert.Equal(16, tree.Top(z), 1);
    }

    /// <summary>
    /// An inline-block holding "x" with <c>vertical-align: -5px</c> stands 5px below the line's
    /// baseline, its text with it, and the words beside it stay at the top: the line is 21px tall.
    /// It was 2.87px down, and the line 25.27px tall.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Of_Text_Lowered_From_The_Baseline_Is_Lowered_Once()
    {
        var tree = Build();
        tree.InlineBlock.VerticalAlign = "-5px";
        var x = Word(tree.InlineBlock, "x");
        Layout(tree);

        Assert.Equal(21, tree.Block.Size.Height, 1);
        Assert.Equal(5, tree.Top(tree.InlineBlock), 1);
        Assert.Equal(5, tree.Top(x), 1);
        Assert.Equal(0, tree.Top(tree.Before!), 1);
    }

    /// <summary>
    /// An inline-block holding "x" aligned to the top or the bottom of its line leaves the line
    /// 16px tall, everything on it at the top. The line was 22.4px tall.
    /// </summary>
    [Theory]
    [InlineData("top")]
    [InlineData("bottom")]
    public void An_Inline_Block_Of_Text_At_The_Top_Or_Bottom_Of_Its_Line_Leaves_It_As_Tall_As_The_Text(string verticalAlign)
    {
        var tree = Build();
        tree.InlineBlock.VerticalAlign = verticalAlign;
        var x = Word(tree.InlineBlock, "x");
        Layout(tree);

        Assert.Equal(16, tree.Block.Size.Height, 1);
        Assert.Equal(0, tree.Top(tree.InlineBlock), 1);
        Assert.Equal(0, tree.Top(x), 1);
        Assert.Equal(0, tree.Top(tree.Before!), 1);
    }

    /// <summary>
    /// Control, which passes before and after: an inline-block holding "x" with
    /// <c>overflow: hidden</c> stands on its bottom edge, so the line reaches a strut's descent,
    /// 3.2px, below its bottom.
    /// </summary>
    [Fact]
    public void Control_An_Inline_Block_That_Clips_Stands_On_Its_Bottom_Edge()
    {
        var tree = Build();
        tree.InlineBlock.Overflow = "hidden";
        Word(tree.InlineBlock, "x");
        Layout(tree);

        double bottom = tree.Top(tree.InlineBlock) + tree.InlineBlock.Size.Height;
        Assert.Equal(bottom + 3.2, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an empty inline-block 30px tall stands on its bottom
    /// edge, at the top of the line, and the line is 33.2px tall.
    /// </summary>
    [Fact]
    public void Control_An_Empty_Inline_Block_Stands_On_Its_Bottom_Edge()
    {
        var tree = Build();
        tree.InlineBlock.Width = "8px";
        tree.InlineBlock.Height = "30px";
        Layout(tree);

        Assert.Equal(33.2, tree.Block.Size.Height, 1);
        Assert.Equal(0, tree.Top(tree.InlineBlock), 1);
    }

    /// <summary>
    /// The root, the 320px block, the inline-block on its line, and the words "a" before it and "b"
    /// after it, when there are any.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Block, CssBox InlineBlock, CssBox? Before, CssBox? After)
    {
        /// <summary>How far the box's first word, or the box, stands below the block's top.</summary>
        public double Top(CssBox box) =>
            (box.Words.Count > 0 ? box.Words[0].Top : box.Location.Y) - Block.Location.Y;
    }

    /// <summary>
    /// In a 320px block in the root, "a", an empty inline-block and "b", or the inline-block alone,
    /// and after the block a 1px block.
    /// </summary>
    private static Tree Build(bool alone = false)
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
        var before = alone ? null : Word(block, "a");
        var inlineBlock = new CssBox(block, new HtmlTag("span", false, null), BaseUrl) { Display = "inline-block" };
        var after = alone ? null : Word(block, "b");
        _ = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "1px" };

        return new Tree(root, block, inlineBlock, before, after);
    }

    /// <summary>Adds a block to <paramref name="parent"/>.</summary>
    private static CssBox Block(CssBox parent) =>
        new(parent, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };

    /// <summary>Adds to <paramref name="parent"/> an inline-block holding the given text.</summary>
    private static (CssBox Box, CssBox Text) TextInlineBlock(CssBox parent, string text)
    {
        var box = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl) { Display = "inline-block" };
        return (box, Word(box, text));
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
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
