using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A column flex container of <c>height: auto</c> ends where its last item's margin box does,
/// wherever negative margins let its items overlap.
/// </summary>
/// <remarks>
/// <para>
/// CSS Flexbox §9.2 and §9.9.1: the main size of a column container of <c>height: auto</c> is what
/// its items' outer sizes add up to along the column, margins and all, and an item with a negative
/// margin takes that much off. After a 30px item with <c>margin-bottom: -25px</c>, a 10px item
/// stands 5px down and the column is 15px tall.
/// </para>
/// <para>
/// This engine places a column's items one per line (CssLayoutEngine.CreateLineBoxes), and the
/// lines were stacked as line boxes are (CSS 2.1 §9.4.2): each line moved below the line above
/// it, and a line whose item a negative top margin put above it moved down to hold it. The items
/// stayed where the flow put them, but the column was measured from where the lines had moved:
/// 40px tall where browsers make it 15px. Nor did it end at its last item: it ended at the lowest
/// item's border box.
/// </para>
/// <para>
/// Each container here is 100px wide, with 20px lines of a 16px font, and its items are blocks of
/// the heights given.
/// </para>
/// </remarks>
public sealed class ColumnFlexStackHeightTests
{
    private static readonly Uri BaseUrl = new("file:///column-flex-stack-height.html");

    /// <summary>
    /// A 30px item, then a 10px item with <c>margin-top: -25px</c>: the second stands 5px down, and
    /// the column is 15px tall. It was 40px tall.
    /// </summary>
    [Fact]
    public void The_Column_Ends_At_An_Item_Pulled_Up_Over_The_One_Before()
    {
        var column = Column();
        Item(column, 30);
        var second = Item(column, 10, marginTop: "-25px");
        Layout(column);

        Assert.Equal(5, Top(second, column), 1);
        Assert.Equal(15, column.Size.Height, 1);
    }

    /// <summary>
    /// A 30px item with <c>margin-bottom: -25px</c>, then a 10px item: the second stands 5px down,
    /// and the column is 15px tall. It was 40px tall.
    /// </summary>
    [Fact]
    public void The_Column_Ends_At_An_Item_That_Overlaps_The_One_Before()
    {
        var column = Column();
        Item(column, 30, marginBottom: "-25px");
        var second = Item(column, 10);
        Layout(column);

        Assert.Equal(5, Top(second, column), 1);
        Assert.Equal(15, column.Size.Height, 1);
    }

    /// <summary>
    /// A 30px item, then a 10px item with <c>margin-top: -25px</c>: the second item's rectangle on
    /// the line holding it, which the fragment tree builds that line from, stays where the item
    /// stands, 5px down. The line had moved 30px down, below the first item, and the rectangle with
    /// it, while the item stayed 5px down.
    /// </summary>
    [Fact]
    public void The_Line_Of_An_Overlapping_Item_Stays_Where_The_Item_Stands()
    {
        var column = Column();
        Item(column, 30);
        var second = Item(column, 10, marginTop: "-25px");
        Layout(column);

        var line = column.LineBoxes.Single(l => l.Rectangles.ContainsKey(second));
        Assert.Equal(5, line.Rectangles[second].Top - column.Location.Y, 1);
    }

    /// <summary>
    /// A 30px item, then a 10px item with <c>margin-bottom: -20px</c>: the column ends 20px down,
    /// where the last item's margin box does. It ended 40px down, at the item's border box.
    /// </summary>
    [Fact]
    public void The_Column_Ends_At_The_Last_Items_Bottom_Margin_Edge()
    {
        var column = Column();
        Item(column, 30);
        var second = Item(column, 10, marginBottom: "-20px");
        Layout(column);

        Assert.Equal(30, Top(second, column), 1);
        Assert.Equal(20, column.Size.Height, 1);
    }

    /// <summary>
    /// A 30px item with <c>margin-top: -10px</c>, then a 10px item: the first stands 10px above the
    /// column, the second 20px down, and the column is 30px tall. It was 40px tall.
    /// </summary>
    [Fact]
    public void An_Item_Pulled_Up_Above_The_Column_Leaves_It_Shorter()
    {
        var column = Column();
        var first = Item(column, 30, marginTop: "-10px");
        var second = Item(column, 10);
        Layout(column);

        Assert.Equal(-10, Top(first, column), 1);
        Assert.Equal(20, Top(second, column), 1);
        Assert.Equal(30, column.Size.Height, 1);
    }

    /// <summary>
    /// Three items: 30px with <c>margin-bottom: -25px</c>, 10px with <c>margin-bottom: -10px</c>,
    /// and 10px: the third stands 5px down, and the column is 15px tall. It was 50px tall.
    /// </summary>
    [Fact]
    public void The_Column_Ends_At_The_Last_Of_Three_Overlapping_Items()
    {
        var column = Column();
        Item(column, 30, marginBottom: "-25px");
        Item(column, 10, marginBottom: "-10px");
        var third = Item(column, 10);
        Layout(column);

        Assert.Equal(5, Top(third, column), 1);
        Assert.Equal(15, column.Size.Height, 1);
    }

    /// <summary>
    /// With <c>row-gap: 10px</c>, a 30px item with <c>margin-bottom: -25px</c>, then a 10px item:
    /// the second stands 15px down, and the column is 25px tall. It was 40px tall.
    /// </summary>
    [Fact]
    public void The_Row_Gap_Follows_An_Overlapping_Item()
    {
        var column = Column();
        column.RowGap = "10px";
        Item(column, 30, marginBottom: "-25px");
        var second = Item(column, 10);
        Layout(column);

        Assert.Equal(15, Top(second, column), 1);
        Assert.Equal(25, column.Size.Height, 1);
    }

    /// <summary>
    /// A column holding a column of a 30px item and a 10px item with <c>margin-top: -25px</c>,
    /// then a 10px item: the inner column is 15px tall, the 10px item after it stands 15px down,
    /// and the outer column is 25px tall. The inner one was 40px tall, the item 40px down, and the
    /// outer column 50px tall.
    /// </summary>
    [Fact]
    public void A_Column_In_A_Column_Ends_At_Its_Last_Item()
    {
        var column = Column();
        var inner = new CssBox(column, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "flex",
            FlexDirection = "column",
        };
        Item(inner, 30);
        Item(inner, 10, marginTop: "-25px");
        var last = Item(column, 10);
        Layout(column);

        Assert.Equal(15, inner.Size.Height, 1);
        Assert.Equal(15, Top(last, column), 1);
        Assert.Equal(25, column.Size.Height, 1);
    }

    /// <summary>
    /// An <c>inline-flex</c> column of a 30px item and a 10px item with <c>margin-top: -25px</c> is
    /// 15px tall; it was 40px. One of a 30px item and a 10px item with <c>margin-bottom: 10px</c>
    /// is 50px tall, as a block-level one is; it was 40px, the margin left out.
    /// </summary>
    [Theory]
    [InlineData("-25px", "0", 15)]
    [InlineData("0", "10px", 50)]
    public void An_Inline_Flex_Column_Ends_At_Its_Last_Items_Bottom_Margin_Edge(
        string marginTop, string marginBottom, float height)
    {
        var block = Block();
        var column = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "inline-flex",
            FlexDirection = "column",
            Width = "100px",
            VerticalAlign = CssConstants.Top,
        };
        Item(column, 30);
        Item(column, 10, marginTop, marginBottom);
        Layout(block);

        Assert.Equal(height, column.Size.Height, 1);
    }

    /// <summary>
    /// One 10px item with <c>margin-bottom: -30px</c>: the items add up to less than nothing, and
    /// the column is empty, 0px tall. It was 10px tall.
    /// </summary>
    [Fact]
    public void A_Column_Whose_Items_Add_Up_To_Less_Than_Nothing_Is_Empty()
    {
        var column = Column();
        Item(column, 10, marginBottom: "-30px");
        Layout(column);

        Assert.Equal(0, column.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a 30px item, then a 10px item, make a 40px column,
    /// the second 30px down.
    /// </summary>
    [Fact]
    public void Control_Two_Items()
    {
        var column = Column();
        Item(column, 30);
        var second = Item(column, 10);
        Layout(column);

        Assert.Equal(30, Top(second, column), 1);
        Assert.Equal(40, column.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: positive bottom margins, 10px on the first of a
    /// 30px and a 10px item, or on the second, make a 50px column.
    /// </summary>
    [Theory]
    [InlineData(true, 40)]
    [InlineData(false, 30)]
    public void Control_A_Positive_Bottom_Margin(bool onFirst, float secondTop)
    {
        var column = Column();
        Item(column, 30, marginBottom: onFirst ? "10px" : "0");
        var second = Item(column, 10, marginBottom: onFirst ? "0" : "10px");
        Layout(column);

        Assert.Equal(secondTop, Top(second, column), 1);
        Assert.Equal(50, column.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: under <c>column-reverse</c>, a 30px item with
    /// <c>margin-bottom: -25px</c> and a 10px item make a 15px column, the second at its top.
    /// </summary>
    [Fact]
    public void Control_A_Reversed_Column()
    {
        var column = Column();
        column.FlexDirection = "column-reverse";
        Item(column, 30, marginBottom: "-25px");
        var second = Item(column, 10);
        Layout(column);

        Assert.Equal(0, Top(second, column), 1);
        Assert.Equal(15, column.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a relative offset moves only the item. A 30px item,
    /// then a 10px item with <c>position: relative; top: -25px</c>, make a 40px column, the second
    /// drawn 5px down.
    /// </summary>
    [Fact]
    public void Control_A_Relatively_Positioned_Item()
    {
        var column = Column();
        Item(column, 30);
        var second = Item(column, 10);
        second.Position = CssConstants.Relative;
        second.Top = "-25px";
        Layout(column);

        Assert.Equal(5, Top(second, column), 1);
        Assert.Equal(40, column.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a column of <c>height: 50px</c> is 50px tall
    /// whatever its items add up to.
    /// </summary>
    [Fact]
    public void Control_A_Column_Of_A_Given_Height()
    {
        var column = Column();
        column.Height = "50px";
        Item(column, 30);
        Item(column, 10, marginTop: "-25px");
        Layout(column);

        Assert.Equal(50, column.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a <c>row-reverse</c> container of a 30px item and a
    /// 10px item with <c>margin-top: -25px</c> is as tall as the taller item, 30px.
    /// </summary>
    [Fact]
    public void Control_A_Row()
    {
        var column = Column();
        column.FlexDirection = "row-reverse";
        Item(column, 30).Width = "10px";
        Item(column, 10, marginTop: "-25px").Width = "10px";
        Layout(column);

        Assert.Equal(30, column.Size.Height, 1);
    }

    /// <summary><see cref="Block"/>, made a 100px-wide column flex container.</summary>
    private static CssBox Column()
    {
        var column = Block();
        column.Display = "flex";
        column.FlexDirection = "column";
        column.Width = "100px";
        return column;
    }

    /// <summary>A block with 20px lines of a 16px font, in a block in the root.</summary>
    private static CssBox Block()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        return new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = "100px",
            FontSize = "16px",
            LineHeight = "20px",
        };
    }

    /// <summary>A block item of the given height and vertical margins in <paramref name="parent"/>.</summary>
    private static CssBox Item(CssBox parent, int height, string marginTop = "0", string marginBottom = "0") =>
        new(parent, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Height = $"{height}px",
            MarginTop = marginTop,
            MarginBottom = marginBottom,
        };

    private static double Top(CssBox item, CssBox container) => item.Location.Y - container.Location.Y;

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly ConcurrentDictionary<double, FakeFont> Fonts = new();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => Fonts.GetOrAdd(size, s => new FakeFont(s));
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, (float)font.Height);
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
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => null!;
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    /// <summary>A font of the given size in points, as tall in pixels as its size in pixels.</summary>
    private sealed class FakeFont(double size) : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => size;
        public double Height => size * 4 / 3;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
