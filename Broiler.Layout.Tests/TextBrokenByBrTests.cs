using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Text broken by a &lt;br&gt; in a block the parser leaves as it is, a flex or grid item or a
/// block inside one or inside an inline-block, is laid out on lines, one before the break and one
/// after it.
/// </summary>
/// <remarks>
/// <para>
/// The parser computes a &lt;br&gt; to a block-level box, and where it reaches a block holding
/// text and &lt;br&gt;s it puts each run of text in an anonymous block, so the block lays out
/// blocks only. It does not reach a flex or grid container's children, or anything inside an
/// inline-level box. There a block holding <c>Hello&lt;br&gt;World</c> was laid out child by child
/// as blocks, which gives text no line box: neither word was painted. The inline-block path
/// already lays such content out on lines of its own (<c>InlineContentWithBrsOnly</c>); the block
/// path did not.
/// </para>
/// <para>
/// Each box here is in a 500px block with 10px of top padding, so its content begins 10px down.
/// Words are 16px tall and 8px wide a letter.
/// </para>
/// </remarks>
public sealed class TextBrokenByBrTests
{
    private static readonly Uri BaseUrl = new("file:///text-broken-by-br.html");

    /// <summary>
    /// An item of a row flex container, and a block inside an item of a row flex, column flex or
    /// grid container or inside an inline-block, each holding a word, a &lt;br&gt; and another
    /// word, lay the first word out on a line at their top and the second on the next line, 16px
    /// down. Neither word was on a line.
    /// </summary>
    [Theory]
    [InlineData("flex", false)]
    [InlineData("flex", true)]
    [InlineData("column", true)]
    [InlineData("grid", true)]
    [InlineData("inline-block", true)]
    public void Text_Broken_By_A_Br_Is_Laid_Out_On_Two_Lines(string container, bool nested)
    {
        var tree = Build(container, nested);
        var first = Word(tree.Box, "Hello");
        Br(tree.Box);
        var second = Word(tree.Box, "World");
        Layout(tree);

        AssertOnALineAt(tree, first, 10);
        AssertOnALineAt(tree, second, 26);
    }

    /// <summary>
    /// The block inside the flex item is 32px tall, as tall as its two lines. It was 0px tall.
    /// </summary>
    [Fact]
    public void A_Block_Inside_A_Flex_Item_Is_As_Tall_As_Its_Lines()
    {
        var tree = Build("flex", nested: true);
        Word(tree.Box, "Hello");
        Br(tree.Box);
        Word(tree.Box, "World");
        Layout(tree);

        Assert.Equal(32, tree.Box.Size.Height, 1);
    }

    /// <summary>
    /// Laid out a second time, the flex item's two words are on the same lines.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Lines()
    {
        var tree = Build("flex", nested: false);
        var first = Word(tree.Box, "Hello");
        Br(tree.Box);
        var second = Word(tree.Box, "World");
        Layout(tree);
        Layout(tree);

        AssertOnALineAt(tree, first, 10);
        AssertOnALineAt(tree, second, 26);
    }

    /// <summary>
    /// Controls, which pass before and after: an item of a column flex or grid container holding
    /// the two words and the &lt;br&gt;, which the inline-block path lays out on lines of its own,
    /// and a block holding its words each in an anonymous block around the &lt;br&gt;, as the parser
    /// leaves a block it reaches, lay them out on two lines.
    /// </summary>
    [Theory]
    [InlineData("column")]
    [InlineData("grid")]
    [InlineData("wrapped")]
    public void Control_Text_Already_Laid_Out_On_Lines(string container)
    {
        var tree = Build(container == "wrapped" ? CssConstants.Block : container, nested: false);
        CssRect first, second;

        if (container == "wrapped")
        {
            first = Word(new CssBox(tree.Box, null, BaseUrl) { Display = CssConstants.Block }, "Hello");
            Br(tree.Box);
            second = Word(new CssBox(tree.Box, null, BaseUrl) { Display = CssConstants.Block }, "World");
        }
        else
        {
            first = Word(tree.Box, "Hello");
            Br(tree.Box);
            second = Word(tree.Box, "World");
        }

        Layout(tree);

        AssertOnALineAt(tree, first, 10);
        AssertOnALineAt(tree, second, 26);
    }

    /// <summary>The root and the box holding the text.</summary>
    private sealed record Tree(CssBox Root, CssBox Box);

    /// <summary>
    /// In a 500px block with 10px of top padding in the root, the only item of a row flex, column
    /// flex or grid container, or a block inside an inline-block or in the 500px block, as
    /// <paramref name="container"/> says; or, when <paramref name="nested"/>, a block inside it.
    /// </summary>
    private static Tree Build(string container, bool nested)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };
        var outer = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Width = "500px",
            PaddingTop = "10px",
        };

        CssBox parent = container switch
        {
            "flex" => new CssBox(outer, new HtmlTag("div", false, null), BaseUrl) { Display = "flex" },
            "column" => new CssBox(outer, new HtmlTag("div", false, null), BaseUrl) { Display = "flex", FlexDirection = "column" },
            "grid" => new CssBox(outer, new HtmlTag("div", false, null), BaseUrl) { Display = "grid" },
            "inline-block" => new CssBox(outer, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.InlineBlock },
            _ => outer,
        };

        var box = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };

        if (nested)
            box = new CssBox(box, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };

        return new Tree(root, box);
    }

    /// <summary>Adds to <paramref name="parent"/> a &lt;br&gt;, block-level as the parser makes it.</summary>
    private static void Br(CssBox parent) =>
        _ = new CssBox(parent, new HtmlTag("br", false, null), BaseUrl) { Display = CssConstants.Block };

    /// <summary>Adds to <paramref name="parent"/> a box holding the one word <paramref name="text"/>.</summary>
    private static CssRect Word(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl) { Display = CssConstants.Inline, Text = text.AsMemory() };
        box.ParseToWords();
        return box.Words[0];
    }

    /// <summary>
    /// Asserts that <paramref name="word"/> is on one of the line boxes under the tree's root, at the
    /// left of its box and <paramref name="top"/>px down.
    /// </summary>
    private static void AssertOnALineAt(Tree tree, CssRect word, double top)
    {
        Assert.True(IsOnALine(tree.Root, word), "The word is on no line box.");
        Assert.Equal(0, word.Left, 1);
        Assert.Equal(top, word.Top, 1);
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
