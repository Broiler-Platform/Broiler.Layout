using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline flex or grid container stands on the line's baseline with its first item's baseline.
/// </summary>
/// <remarks>
/// <para>
/// CSS Flexbox §8.5: a flex container's first baseline is that of its first item, or, where the
/// item has none, one synthesized from the item's border box: its bottom edge. A grid container's
/// is its first item's in the same way. The engine left an inline flex or grid container at the top
/// of the line, aligned it nowhere, and added the strut's descent below its bottom, so the text
/// beside it stood at the top of the line and the line was a descent taller than the box.
/// </para>
/// <para>
/// Each line here is in a 320px block. Words are 16px tall and 8px wide a letter, and stand on a
/// baseline 12.8px below their top; the strut's descent is 3.2px. "a" and "b" stand on either side
/// of the container.
/// </para>
/// </remarks>
public sealed class InlineFlexBaselineTests
{
    private static readonly Uri BaseUrl = new("file:///inline-flex-baseline.html");

    private const double Ascent = 12.8;

    /// <summary>
    /// A 32px inline-flex holding a 20px item it centres, 6px down: "a" and "b" stand on the item's
    /// bottom, 13.2px down, the container stays at the top, and the line is 32px tall, the 6px below
    /// the item deeper than the strut's descent. The words stood at the top, and the line was 35.2px
    /// tall.
    /// </summary>
    [Fact]
    public void Text_Stands_On_The_Bottom_Of_A_Centred_Empty_Item()
    {
        var tree = Build();
        var box = FlexBox(tree, "inline-flex", b => { b.Height = "32px"; b.AlignItems = "center"; });
        Item(box).Height = "20px";
        Layout(tree);

        Assert.Equal(0, tree.Top(box), 1);
        Assert.Equal(26 - Ascent, tree.Top(tree.Before), 1);
        Assert.Equal(26 - Ascent, tree.Top(tree.After), 1);
        Assert.Equal(32, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// The same item at the top of the container, where it is not centred: the words stand on its
    /// bottom, 7.2px down. They stood at the top.
    /// </summary>
    [Fact]
    public void Text_Stands_On_The_Bottom_Of_An_Empty_Item_At_The_Top()
    {
        var tree = Build();
        var box = FlexBox(tree, "inline-flex", b => { b.Height = "32px"; b.AlignItems = "flex-start"; });
        Item(box).Height = "20px";
        Layout(tree);

        Assert.Equal(20 - Ascent, tree.Top(tree.Before), 1);
        Assert.Equal(32, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// An inline flex or grid container with 5px of padding, holding an item with the word "x":
    /// "a" stands level with "x", 5px down, where it stood 5px above it. The line is as tall as the
    /// 26px container, where it was 29.2px.
    /// </summary>
    [Theory]
    [InlineData("inline-flex")]
    [InlineData("inline-grid")]
    public void Text_Stands_Level_With_The_First_Item_Text(string display)
    {
        var tree = Build();
        var box = FlexBox(tree, display, b => { b.PaddingTop = "5px"; b.PaddingBottom = "5px"; b.PaddingLeft = "5px"; b.PaddingRight = "5px"; });
        var x = Word(Item(box), "x");
        Layout(tree);

        Assert.Equal(5, tree.Top(x), 1);
        Assert.Equal(5, tree.Top(tree.Before), 1);
        Assert.Equal(26, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// The first item in the order the items are laid out in, not in the document: with
    /// <c>order</c> putting an item with 10px of top padding and the word "B" first, "a" stands
    /// level with "B", 10px down. It stood at the top.
    /// </summary>
    [Fact]
    public void The_First_Item_Is_The_First_In_Order()
    {
        var tree = Build();
        var box = FlexBox(tree, "inline-flex", _ => { });
        var second = Item(box);
        second.Order = "2";
        Word(second, "A");
        var first = Item(box);
        first.Order = "1";
        first.PaddingTop = "10px";
        var b = Word(first, "B");
        Layout(tree);

        Assert.Equal(10, tree.Top(b), 1);
        Assert.Equal(10, tree.Top(tree.Before), 1);
    }

    /// <summary>
    /// In a column, the first item is the top one: with an empty 10px item above an item with "x",
    /// the container's baseline is the 10px item's bottom, 2.8px above the text's, so the container
    /// stands 2.8px down and "a" at the top. The container stood at the top.
    /// </summary>
    [Fact]
    public void A_Column_Stands_On_Its_Top_Item()
    {
        var tree = Build();
        var box = FlexBox(tree, "inline-flex", b => b.FlexDirection = "column");
        Item(box).Height = "10px";
        Word(Item(box), "x");
        Layout(tree);

        Assert.Equal(Ascent - 10, tree.Top(box), 1);
        Assert.Equal(0, tree.Top(tree.Before), 1);
    }

    /// <summary>
    /// An empty 32px inline-flex has no baseline, and stands on the line's with its bottom edge, as
    /// an empty inline-block does: the words stand on its bottom, 19.2px down, and the strut's
    /// descent lies below it. The words stood at the top.
    /// </summary>
    [Fact]
    public void An_Empty_Container_Stands_On_Its_Bottom()
    {
        var tree = Build();
        var box = FlexBox(tree, "inline-flex", b => { b.Height = "32px"; b.Width = "32px"; });
        Layout(tree);

        Assert.Equal(0, tree.Top(box), 1);
        Assert.Equal(32 - Ascent, tree.Top(tree.Before), 1);
        Assert.Equal(35.2, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// Laid out a second time, the words beside the centred item are 13.2px down still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var tree = Build();
        var box = FlexBox(tree, "inline-flex", b => { b.Height = "32px"; b.AlignItems = "center"; });
        Item(box).Height = "20px";
        Layout(tree);
        Layout(tree);

        Assert.Equal(26 - Ascent, tree.Top(tree.Before), 1);
        Assert.Equal(32, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a container aligned <c>top</c> stays at the top,
    /// with the words.
    /// </summary>
    [Fact]
    public void Control_A_Container_Aligned_Top_Stays_There()
    {
        var tree = Build();
        var box = FlexBox(tree, "inline-flex", b => { b.Height = "32px"; b.AlignItems = "center"; b.VerticalAlign = CssConstants.Top; });
        Item(box).Height = "20px";
        Layout(tree);

        Assert.Equal(0, tree.Top(box), 1);
        Assert.Equal(0, tree.Top(tree.Before), 1);
    }

    /// <summary>The root, the 320px block, and the words "a" and "b" on its line.</summary>
    private sealed record Tree(CssBox Root, CssBox Block, CssBox Before, CssBox After)
    {
        /// <summary>How far the box's first word, or the box, stands below the block's top.</summary>
        public double Top(CssBox box) =>
            (box.Words.Count > 0 ? box.Words[0].Top : box.Location.Y) - Block.Location.Y;
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

    /// <summary>Puts a container of the given display, styled by <paramref name="style"/>, before "b".</summary>
    private static CssBox FlexBox(Tree tree, string display, Action<CssBox> style)
    {
        var box = new CssBox(tree.Block, new HtmlTag("span", false, null), BaseUrl) { Display = display };
        style(box);
        box.SetBeforeBox(tree.After);
        return box;
    }

    /// <summary>Adds an empty 20px wide item to <paramref name="container"/>.</summary>
    private static CssBox Item(CssBox container) =>
        new(container, new HtmlTag("span", false, null), BaseUrl) { Display = CssConstants.Block, Width = "20px" };

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
