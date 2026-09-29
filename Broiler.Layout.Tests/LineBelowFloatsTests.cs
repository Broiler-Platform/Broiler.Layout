using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A line too narrow beside floats for its first content is shifted down until the content fits or
/// no float is beside it, whether that content is a word or a box placed whole, as an inline-block
/// is.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §9.5: "If a shortened line box is too small to contain any content, then the line box is
/// shifted downward (and its width recomputed) until either some content fits or there are no more
/// floats present." Only a line a word wrapped to was shifted, and only when the word fitted in the
/// block's whole width. The first word on the block's first line stayed beside a float it did not
/// fit beside and ran on across the float's side; an inline-block was checked against the block's
/// whole width, and one too wide for the room beside a float went to the block's left edge, over the
/// float.
/// </para>
/// <para>
/// Each block here is 200px wide, in a block in the root, and starts with a 150 × 30px float, which
/// leaves 50px beside it. Words are 8px wide a letter and 16px tall, and stand on a baseline 12.8px
/// below their top; a line is 16px tall. Inline-blocks are empty and 10px tall, and stand on the
/// baseline with their bottom edge.
/// </para>
/// </remarks>
public sealed class LineBelowFloatsTests
{
    private static readonly Uri BaseUrl = new("file:///line-below-floats.html");

    /// <summary>
    /// "abcdefgh", 64px, does not fit in the 50px beside the float, and goes below it: at the
    /// block's left edge, 30px down. It was beside the float, 150px in, at the top.
    /// </summary>
    [Fact]
    public void A_Word_Too_Wide_Beside_A_Float_Goes_Below_It()
    {
        var tree = Build(b => Word(b, "abcdefgh"));
        Layout(tree);

        Assert.Equal(0, tree.Left(tree.Content[0]), 1);
        Assert.Equal(30, tree.Top(tree.Content[0]), 1);
    }

    /// <summary>
    /// A word wider than the block itself, 240px, goes below the float too, and overflows there.
    /// It was beside the float, 150px in.
    /// </summary>
    [Fact]
    public void A_Word_Wider_Than_The_Block_Goes_Below_The_Float()
    {
        var tree = Build(b => Word(b, "abcdefghijklmnopqrstuvwxyz1234"));
        Layout(tree);

        Assert.Equal(0, tree.Left(tree.Content[0]), 1);
        Assert.Equal(30, tree.Top(tree.Content[0]), 1);
    }

    /// <summary>
    /// After "ab", which fits beside the float, a word wider than the block wraps to a line that
    /// goes below the float, 30px down. It wrapped to the next line, 16px down, still beside the
    /// float, 150px in.
    /// </summary>
    [Fact]
    public void A_Wrapped_Word_Wider_Than_The_Block_Goes_Below_The_Float()
    {
        var tree = Build(b => Word(b, "ab abcdefghijklmnopqrstuvwxyz1234"));
        Layout(tree);

        var words = tree.Content[0].Words;
        Assert.Equal(150, words[0].Left - tree.Block.Location.X, 1);
        Assert.Equal(0, words[0].Top - tree.Block.Location.Y, 1);
        Assert.Equal(0, words[^1].Left - tree.Block.Location.X, 1);
        Assert.Equal(30, words[^1].Top - tree.Block.Location.Y, 1);
    }

    /// <summary>
    /// An inline-block 100px wide does not fit beside a left or a right float, and goes below it:
    /// at the block's left edge, standing on the baseline of a line 30px down, 32.8px down. It
    /// went to the block's left edge at the top, over the float.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Left)]
    [InlineData(CssConstants.Right)]
    public void An_Inline_Block_Too_Wide_Beside_A_Float_Goes_Below_It(string side)
    {
        var tree = Build(b => InlineBlock(b, 100), floatSide: side);
        Layout(tree);

        Assert.Equal(0, tree.Left(tree.Content[0]), 1);
        Assert.Equal(30 + 12.8 - 10, tree.Top(tree.Content[0]), 1);
    }

    /// <summary>
    /// An inline-block wider than the block itself, 300px, goes below the float too. It went to
    /// the block's left edge at the top, over the float.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Wider_Than_The_Block_Goes_Below_The_Float()
    {
        var tree = Build(b => InlineBlock(b, 300));
        Layout(tree);

        Assert.Equal(0, tree.Left(tree.Content[0]), 1);
        Assert.Equal(30 + 12.8 - 10, tree.Top(tree.Content[0]), 1);
    }

    /// <summary>
    /// After "ab" beside the float, an inline-block 100px wide goes on the next line, which goes
    /// below the float, 30px down. It went to the next line, 16px down, at the block's left edge,
    /// over the float.
    /// </summary>
    [Fact]
    public void An_Inline_Block_After_A_Word_Goes_On_A_Line_Below_The_Float()
    {
        var tree = Build(b =>
        {
            Word(b, "ab ");
            InlineBlock(b, 100);
        });
        Layout(tree);

        Assert.Equal(150, tree.Left(tree.Content[0]), 1);
        Assert.Equal(0, tree.Top(tree.Content[0]), 1);
        Assert.Equal(0, tree.Left(tree.Content[1]), 1);
        Assert.Equal(30 + 12.8 - 10, tree.Top(tree.Content[1]), 1);
    }

    /// <summary>
    /// Control, which passes before and after: after "ab" beside a float 10px tall, an
    /// inline-block 100px wide goes on the next line, 16px down, which no float is beside, at the
    /// block's left edge.
    /// </summary>
    [Fact]
    public void Control_A_Line_Below_A_Short_Float_Is_Not_Moved()
    {
        var tree = Build(b =>
        {
            Word(b, "ab ");
            InlineBlock(b, 100);
        }, floatHeight: 10);
        Layout(tree);

        Assert.Equal(0, tree.Left(tree.Content[1]), 1);
        Assert.Equal(16 + 12.8 - 10, tree.Top(tree.Content[1]), 1);
    }

    /// <summary>
    /// Laid out a second time, the word is below the float still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var tree = Build(b => Word(b, "abcdefgh"));
        Layout(tree);
        Layout(tree);

        Assert.Equal(0, tree.Left(tree.Content[0]), 1);
        Assert.Equal(30, tree.Top(tree.Content[0]), 1);
    }

    /// <summary>
    /// Controls, which pass before and after: "ab", 16px, fits beside the float and stays there,
    /// 150px in, at the top; so does an inline-block 40px wide, standing on the first line's
    /// baseline.
    /// </summary>
    [Fact]
    public void Control_What_Fits_Beside_The_Float_Stays_There()
    {
        var word = Build(b => Word(b, "ab"));
        Layout(word);
        var box = Build(b => InlineBlock(b, 40));
        Layout(box);

        Assert.Equal(150, word.Left(word.Content[0]), 1);
        Assert.Equal(0, word.Top(word.Content[0]), 1);
        Assert.Equal(150, box.Left(box.Content[0]), 1);
        Assert.Equal(12.8 - 10, box.Top(box.Content[0]), 1);
    }

    /// <summary>
    /// Control, which passes before and after: with no float, a word wider than the block stays
    /// on the first line and overflows it.
    /// </summary>
    [Fact]
    public void Control_With_No_Float_A_Wide_Word_Stays_On_The_First_Line()
    {
        var tree = Build(b => Word(b, "abcdefghijklmnopqrstuvwxyz1234"), floatHeight: 0);
        Layout(tree);

        Assert.Equal(0, tree.Left(tree.Content[0]), 1);
        Assert.Equal(0, tree.Top(tree.Content[0]), 1);
    }

    /// <summary>The root, the 200px block, and what it holds after the float.</summary>
    private sealed record Tree(CssBox Root, CssBox Block, System.Collections.Generic.List<CssBox> Content)
    {
        /// <summary>How far in from the block's left edge the box's first word, or the box, starts.</summary>
        public double Left(CssBox box) =>
            (box.Words.Count > 0 ? box.Words[0].Left : box.Location.X) - Block.Location.X;

        /// <summary>How far below the block's top the box's first word, or the box, stands.</summary>
        public double Top(CssBox box) =>
            (box.Words.Count > 0 ? box.Words[0].Top : box.Location.Y) - Block.Location.Y;
    }

    /// <summary>
    /// A 200px block in the root holding a float 150px wide and <paramref name="floatHeight"/>
    /// tall, if any, on <paramref name="floatSide"/>, then what <paramref name="content"/> adds.
    /// </summary>
    private static Tree Build(Action<Tree> content, string floatSide = CssConstants.Left, int floatHeight = 30)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "200px" };

        if (floatHeight > 0)
        {
            _ = new CssBox(block, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = "block",
                Float = floatSide,
                Width = "150px",
                Height = floatHeight + "px",
            };
        }

        var tree = new Tree(root, block, []);
        content(tree);
        return tree;
    }

    private static void Word(Tree tree, string text)
    {
        var box = new CssBox(tree.Block, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        box.ParseToWords();
        tree.Content.Add(box);
    }

    private static void InlineBlock(Tree tree, int width) =>
        tree.Content.Add(new CssBox(tree.Block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = width + "px",
            Height = "10px",
        });

    private static void Layout(Tree tree) => tree.Root.PerformLayout(tree.Root.LayoutEnvironment);

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
