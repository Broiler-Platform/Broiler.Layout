using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A grid item's percentage margins and padding refer to the width of its grid area, and the
/// grid's rows are sized with them.
/// </summary>
/// <remarks>
/// <para>
/// CSS Grid §6.2 makes a grid item's grid area its containing block, so its percentages refer to
/// the area's width (CSS2.1 §8.3, §8.4). The grid measures its items before it sizes its tracks,
/// each as wide as its content, and their percentages referred to their own width then: an empty
/// item with <c>padding-top: 50%</c> had no padding, its row was sized without it, and the item,
/// stretched to its 100px column afterwards, was as tall as the row where browsers make it 50px.
/// </para>
/// <para>
/// Each grid here is 400px wide, in a block in the root, with columns of 100px and 200px unless
/// said otherwise. Words are 8×16px.
/// </para>
/// </remarks>
public sealed class GridItemPercentageBasisTests
{
    private static readonly Uri BaseUrl = new("file:///grid-item-percentage-basis.html");

    /// <summary>
    /// An empty item with <c>padding-top: 50%</c> in the 100px column is 50px tall, and so are the
    /// grid and the item beside it holding a word. They were 16px tall, the row the word's.
    /// </summary>
    [Fact]
    public void An_Empty_Items_Top_Padding_Is_Half_Its_Column()
    {
        var (grid, items) = Lay("100px 200px", item => item.PaddingTop = "50%", Word);

        Assert.Equal(100, items[0].Size.Width, 1);
        Assert.Equal(50, items[0].Size.Height, 1);
        Assert.Equal(50, items[1].Size.Height, 1);
        Assert.Equal(50, grid.Size.Height, 1);
    }

    /// <summary>
    /// With columns of <c>1fr 1fr</c>, 200px each, <c>padding-top: 25%</c> is 50px. It was 0.
    /// </summary>
    [Fact]
    public void The_Padding_Refers_To_A_Flexible_Column()
    {
        var (grid, items) = Lay("1fr 1fr", item => item.PaddingTop = "25%", Word);

        Assert.Equal(200, items[0].Size.Width, 1);
        Assert.Equal(50, items[0].Size.Height, 1);
        Assert.Equal(50, grid.Size.Height, 1);
    }

    /// <summary>
    /// An item holding a word, with <c>padding-top: 50%</c>, is 66px tall with the word 50px down
    /// it, and with <c>padding-left: 10%</c> in the 200px column the word is 20px in. The word was
    /// 4px down and 0.8px in, a share of the word's own 8px.
    /// </summary>
    [Fact]
    public void A_Word_Moves_With_The_Padding()
    {
        var (grid, items) = Lay("100px 200px",
            item => { item.PaddingTop = "50%"; Word(item); },
            item => { item.PaddingLeft = "10%"; Word(item); });

        Assert.Equal(66, items[0].Size.Height, 1);
        Assert.Equal(items[0].Location.Y + 50, WordTop(items[0]), 1);
        Assert.Equal(items[1].Location.X + 20, WordLeft(items[1]), 1);
        Assert.Equal(66, grid.Size.Height, 1);
    }

    /// <summary>
    /// An item holding a word, with <c>margin-top: 10%</c> in the 100px column, starts 10px down
    /// its row, which is 26px tall. It started at the row's top, and the row was 16px tall.
    /// </summary>
    [Fact]
    public void A_Top_Margin_Is_A_Tenth_Of_The_Column()
    {
        var (grid, items) = Lay("100px 200px", item => { item.MarginTop = "10%"; Word(item); });

        Assert.Equal(grid.ClientTop + 10, items[0].Location.Y, 1);
        Assert.Equal(26, grid.Size.Height, 1);
    }

    /// <summary>
    /// An item aligned to the start of its area keeps its own width, 8px around its word, and its
    /// <c>padding-top: 50%</c> still refers to the 100px column: 50px, the item 66px tall.
    /// </summary>
    [Fact]
    public void An_Item_That_Keeps_Its_Own_Width_Still_Refers_To_Its_Column()
    {
        var (_, items) = Lay("100px 200px", item =>
        {
            item.JustifySelf = "start";
            item.AlignSelf = "start";
            item.PaddingTop = "50%";
            Word(item);
        });

        Assert.Equal(8, items[0].Size.Width, 1);
        Assert.Equal(66, items[0].Size.Height, 1);
        Assert.Equal(items[0].Location.Y + 50, WordTop(items[0]), 1);
    }

    /// <summary>
    /// In columns as wide as their content (<c>auto auto</c>, <c>justify-content: start</c>), an
    /// item with <c>padding-left: 50%</c> and a word makes its column as wide as the word, 8px, the
    /// padding half of that and the word 4px in, and so again each time the grid is laid out: the
    /// percentage refers to the column, which is measured with it at zero. The column was 62.5px
    /// wide, then 27.6px, then 18.9px, the percentage measured against the item's width from the
    /// layout before.
    /// </summary>
    [Fact]
    public void A_Column_Measured_With_The_Padding_Keeps_Its_Width()
    {
        var (grid, items) = Lay("auto auto", item => { item.PaddingLeft = "50%"; Word(item); }, Word);
        grid.JustifyContent = "start";

        for (int layout = 0; layout < 3; layout++)
        {
            var root = grid.ParentBox!.ParentBox!;
            root.PerformLayout(root.LayoutEnvironment);

            Assert.Equal(8, items[0].Size.Width, 1);
            Assert.Equal(items[0].Location.X + 4, WordLeft(items[0]), 1);
            Assert.Equal(items[0].Location.X + 8, items[1].Location.X, 1);
        }
    }

    /// <summary>
    /// Controls, which pass before and after: an item with 10px of top padding holding a word is
    /// 26px tall, and a 100px block with <c>padding-top: 50%</c> in a 400px block is 200px tall.
    /// </summary>
    [Fact]
    public void Control_A_Fixed_Padding_And_A_Block()
    {
        var (grid, items) = Lay("100px 200px", item => { item.PaddingTop = "10px"; Word(item); });

        Assert.Equal(26, items[0].Size.Height, 1);
        Assert.Equal(26, grid.Size.Height, 1);

        var (_, blocks) = Lay(null, item => { item.Width = "100px"; item.PaddingTop = "50%"; });

        Assert.Equal(200, blocks[0].Size.Height, 1);
    }

    private static void Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
    }

    private static double WordTop(CssBox item) => item.Boxes[0].Words[0].Top;

    private static double WordLeft(CssBox item) => item.Boxes[0].Words[0].Left;

    /// <summary>
    /// A 400px grid with <paramref name="columns"/>, or a 400px block when that is null, in a block
    /// in the root, holding an item for each of <paramref name="items"/>, styled and filled by it.
    /// </summary>
    private static (CssBox Grid, CssBox[] Items) Lay(string? columns, params Action<CssBox>[] items)
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
            Display = columns == null ? "block" : "grid",
            Width = "400px",
        };

        if (columns != null)
            grid.GridTemplateColumns = columns;

        var boxes = new CssBox[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            boxes[i] = new CssBox(grid, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
            items[i](boxes[i]);
        }

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return (grid, boxes);
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
