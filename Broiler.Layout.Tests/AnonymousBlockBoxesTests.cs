using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Text beside a block in an inline-block, or anywhere inside an inline-level box, is laid out in
/// an anonymous block box of its own, as it is in any other block, so it is on a line and painted.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §9.2.1.1: a block container that holds a block-level box holds only block-level boxes,
/// each run of inline-level content between them in an anonymous block box. The parser makes those
/// boxes for the blocks it reaches, but not for an inline-block, and it does not reach anything
/// inside an inline-level box. There the text beside a block was laid out as a block, which gave it
/// no line box: it was not painted, and the box around it was as tall as its blocks alone.
/// </para>
/// <para>
/// Each box here is in a 500px block with 10px of top padding, so its content begins 10px down.
/// Words are 16px tall and 8px wide a letter, and each block is 30px wide and 10px tall.
/// </para>
/// </remarks>
public sealed class AnonymousBlockBoxesTests
{
    private static readonly Uri BaseUrl = new("file:///anonymous-block-boxes.html");

    /// <summary>
    /// An inline-block holding a word and an absolutely positioned block lays the word out on a line
    /// at its top, and is as tall as the line, 16px. The word was on no line, and the inline-block
    /// 0px tall.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Lays_Out_Text_Beside_A_Positioned_Block()
    {
        var tree = Build(CssConstants.InlineBlock);
        var word = Word(tree.Box, "X");
        Positioned(Block(tree.Box));
        Layout(tree);

        AssertOnALineAt(tree, word, 10);
        Assert.Equal(16, tree.Box.Size.Height, 1);
    }

    /// <summary>
    /// An inline-block holding a word and then a block in flow lays the word out on a line at its
    /// top and the block below it, 16px further down, and is 26px tall. The word was on no line,
    /// the block at its top, and the inline-block 10px tall.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Lays_Out_Text_Above_A_Block()
    {
        var tree = Build(CssConstants.InlineBlock);
        var word = Word(tree.Box, "X");
        var block = Block(tree.Box);
        Layout(tree);

        AssertOnALineAt(tree, word, 10);
        Assert.Equal(26, block.Location.Y, 1);
        Assert.Equal(26, tree.Box.Size.Height, 1);
    }

    /// <summary>
    /// With a word on each side of the block, each is on a line of its own: the first at the top,
    /// the block below it, and the second below the block, 26px further down. The inline-block is
    /// 42px tall.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Lays_Out_Text_On_Both_Sides_Of_A_Block()
    {
        var tree = Build(CssConstants.InlineBlock);
        var first = Word(tree.Box, "X");
        Block(tree.Box);
        var second = Word(tree.Box, "Y");
        Layout(tree);

        AssertOnALineAt(tree, first, 10);
        AssertOnALineAt(tree, second, 36);
        Assert.Equal(42, tree.Box.Size.Height, 1);
    }

    /// <summary>
    /// A block inside an inline-block, holding a word and a block, lays the word out on a line and
    /// is 26px tall. The word was on no line, and the block 10px tall.
    /// </summary>
    [Fact]
    public void A_Block_Inside_An_Inline_Block_Lays_Out_Its_Text()
    {
        var tree = Build(CssConstants.InlineBlock);
        var inner = new CssBox(tree.Box, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };
        var word = Word(inner, "X");
        Block(inner);
        Layout(tree);

        AssertOnALineAt(tree, word, 10);
        Assert.Equal(26, inner.Size.Height, 1);
    }

    /// <summary>
    /// An item of an inline-flex container, holding a word and an absolutely positioned block, lays
    /// the word out on a line and is 16px tall. The word was on no line, and the item 0px tall.
    /// </summary>
    [Fact]
    public void A_Flex_Item_Lays_Out_Text_Beside_A_Positioned_Block()
    {
        var tree = Build("inline-flex");
        var item = new CssBox(tree.Box, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Position = CssConstants.Relative,
        };
        var word = Word(item, "X");
        Positioned(Block(item));
        Layout(tree);

        AssertOnALineAt(tree, word, 10);
        Assert.Equal(16, item.Size.Height, 1);
    }

    /// <summary>
    /// Laid out a second time, the inline-block around the word and the block below it is 26px tall
    /// still, the word on a line at its top.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Lines()
    {
        var tree = Build(CssConstants.InlineBlock);
        var word = Word(tree.Box, "X");
        Block(tree.Box);
        Layout(tree);
        Layout(tree);

        AssertOnALineAt(tree, word, 10);
        Assert.Equal(26, tree.Box.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: an inline-block holding a word alone, or a word and a
    /// float, lays the word out on a line at its top, beside the float where there is one.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Control_An_Inline_Block_Holding_Only_Inline_Content(bool withFloat)
    {
        var tree = Build(CssConstants.InlineBlock);
        var word = Word(tree.Box, "X");

        if (withFloat)
            Block(tree.Box).Float = CssConstants.Left;

        Layout(tree);

        AssertOnALineAt(tree, word, 10, left: withFloat ? null : 0);
    }

    /// <summary>
    /// Control, which passes before and after: an inline-block holding a word, a &lt;br&gt; and
    /// another word lays them out on two lines, as its own line layout does.
    /// </summary>
    [Fact]
    public void Control_Text_Broken_Only_By_A_Br_Keeps_Its_Line_Layout()
    {
        var tree = Build(CssConstants.InlineBlock);
        var first = Word(tree.Box, "X");
        _ = new CssBox(tree.Box, new HtmlTag("br", false, null), BaseUrl) { Display = CssConstants.Block };
        var second = Word(tree.Box, "Y");
        Layout(tree);

        AssertOnALineAt(tree, first, 10);
        AssertOnALineAt(tree, second, 26);
    }

    /// <summary>The root and the box whose content is laid out.</summary>
    private sealed record Tree(CssBox Root, CssBox Box);

    /// <summary>
    /// In a 500px block with 10px of top padding in the root, an empty box with the given display.
    /// </summary>
    private static Tree Build(string display)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };
        var container = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Width = "500px",
            PaddingTop = "10px",
        };

        var box = new CssBox(container, new HtmlTag("div", false, null), BaseUrl) { Display = display };

        return new Tree(root, box);
    }

    /// <summary>Adds to <paramref name="parent"/> a block 30px wide and 10px tall.</summary>
    private static CssBox Block(CssBox parent) =>
        new(parent, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block, Width = "30px", Height = "10px" };

    /// <summary>Makes <paramref name="box"/> absolutely positioned at its containing block's corner.</summary>
    private static void Positioned(CssBox box)
    {
        box.Position = CssConstants.Absolute;
        box.Top = box.Left = "0";
    }

    /// <summary>Adds to <paramref name="parent"/> a box holding the one word <paramref name="text"/>.</summary>
    private static CssRect Word(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl) { Display = CssConstants.Inline, Text = text.AsMemory() };
        box.ParseToWords();
        return box.Words[0];
    }

    /// <summary>
    /// Asserts that <paramref name="word"/> is on one of the line boxes under the tree's root,
    /// <paramref name="top"/>px down and, unless <paramref name="left"/> is null, that far in.
    /// </summary>
    private static void AssertOnALineAt(Tree tree, CssRect word, double top, double? left = 0)
    {
        Assert.True(IsOnALine(tree.Root, word), "The word is on no line box.");
        Assert.Equal(top, word.Top, 1);

        if (left is double x)
            Assert.Equal(x, word.Left, 1);
    }

    private static bool IsOnALine(CssBox box, CssRect word) =>
        box.LineBoxes.Any(line => line.Words.Contains(word)) || box.Boxes.Any(child => IsOnALine(child, word));

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
