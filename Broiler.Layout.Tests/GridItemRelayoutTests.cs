using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A grid item that takes its grid area's width lays its content out at that width, and the rows
/// are sized from the height that gives.
/// </summary>
/// <remarks>
/// <para>
/// The grid measures an item before it sizes its tracks, as wide as the item's content, and the
/// item's content was laid out at that width. Resized to its area afterwards, the item kept it:
/// a block child holding a word was the word's width where browsers make it fill the item, a child
/// with <c>width: 50%</c> half the word, and a child with <c>padding-top: 56.25%</c>, the
/// intrinsic-ratio pattern of responsive embeds, had no height, its row sized without it.
/// </para>
/// <para>
/// Each grid here is 400px wide with columns of <c>1fr 1fr</c>, 200px each, in a block in the
/// root, and holds the item under test and then an item holding a word. Words are 8×16px.
/// </para>
/// </remarks>
public sealed class GridItemRelayoutTests
{
    private static readonly Uri BaseUrl = new("file:///grid-item-relayout.html");

    /// <summary>
    /// A block child holding a word fills the item, 200px wide. It was 8px wide, the word's width.
    /// </summary>
    [Fact]
    public void A_Block_Child_Fills_The_Item()
    {
        var (_, item, child) = Lay(child => Word(child));

        Assert.Equal(200, item.Size.Width, 1);
        Assert.Equal(200, child.Size.Width, 1);
    }

    /// <summary>
    /// A child with <c>width: 50%</c> holding a word is 100px wide. It was 4px, half the word.
    /// </summary>
    [Fact]
    public void A_Percentage_Width_Refers_To_The_Item()
    {
        var (_, _, child) = Lay(child => { child.Width = "50%"; Word(child); });

        Assert.Equal(100, child.Size.Width, 1);
    }

    /// <summary>
    /// An empty child with <c>padding-top: 56.25%</c> is 112.5px tall, and so are the item and the
    /// grid. It was 0px tall, and they were 16px, the word beside it.
    /// </summary>
    [Fact]
    public void A_Percentage_Padding_Sizes_The_Row()
    {
        var (grid, item, child) = Lay(child => child.PaddingTop = "56.25%");

        Assert.Equal(112.5, child.Size.Height, 1);
        Assert.Equal(112.5, item.Size.Height, 1);
        Assert.Equal(112.5, grid.Size.Height, 1);
    }

    /// <summary>
    /// A right-aligned child holding a word puts it at the item's right edge, 192px in. The word was
    /// at the item's left, the child as wide as the word.
    /// </summary>
    [Fact]
    public void Right_Aligned_Text_Reaches_The_Items_Right_Edge()
    {
        var (_, item, child) = Lay(child => { child.TextAlign = "right"; Word(child); });

        Assert.Equal(item.Location.X + 192, child.Boxes[0].Words[0].Left, 1);
    }

    /// <summary>
    /// Laid out a second time, the grid gives the same sizes: the child fills the item, and the
    /// ratio child is 112.5px tall.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Sizes()
    {
        var (grid, item, child) = Lay(child => child.PaddingTop = "56.25%");
        var root = grid.ParentBox!.ParentBox!;

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(200, child.Size.Width, 1);
        Assert.Equal(112.5, child.Size.Height, 1);
        Assert.Equal(112.5, item.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an item aligned to the start of its area keeps its
    /// content's width, and so does its child holding a word, 8px.
    /// </summary>
    [Fact]
    public void Control_An_Item_Aligned_To_The_Start_Keeps_Its_Contents_Width()
    {
        var (_, item, child) = Lay(child => Word(child), item => item.JustifySelf = "start");

        Assert.Equal(8, item.Size.Width, 1);
        Assert.Equal(8, child.Size.Width, 1);
    }

    private static void Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
    }

    /// <summary>
    /// The 400px grid, holding an item styled by <paramref name="item"/> with a block child that
    /// <paramref name="child"/> styles and fills, and then an item holding a word, laid out.
    /// </summary>
    private static (CssBox Grid, CssBox Item, CssBox Child) Lay(Action<CssBox> child, Action<CssBox>? item = null)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var grid = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "grid",
            Width = "400px",
            GridTemplateColumns = "1fr 1fr",
        };

        var first = new CssBox(grid, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        item?.Invoke(first);

        var inner = new CssBox(first, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        child(inner);

        Word(new CssBox(grid, new HtmlTag("div", false, null), BaseUrl) { Display = "block" });

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return (grid, first, inner);
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8, 16);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8; }
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
