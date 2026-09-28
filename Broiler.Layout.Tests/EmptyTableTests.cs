using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table with no cells has no border spacing: it is as big as its borders, its width and its
/// captions make it.
/// </summary>
/// <remarks>
/// <para>
/// Border spacing lies between a table's cells and between them and its border, so a table with no
/// cells has none, and browsers make <c>&lt;table&gt;&lt;/table&gt;</c> 0×0px with the default
/// <c>border-spacing: 2px</c>. The table algorithm put the spacing on both sides of the cells
/// whether there were any or not, so an empty table was 4×4px, and one with a 5px border 14×14px,
/// where browsers make it 10×10px.
/// </para>
/// <para>
/// Each table here is in a 500px block and has <c>border-spacing: 2px</c>; words are 16px tall and
/// 8px wide a letter.
/// </para>
/// </remarks>
public sealed class EmptyTableTests
{
    private static readonly Uri BaseUrl = new("file:///empty-table.html");

    /// <summary>
    /// A table with no row group, with an empty row group, or with a row with no cells, is 0×0px.
    /// Each was 4×4px.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_Table_With_No_Cells_Has_No_Border_Spacing(int depth)
    {
        var tree = Build(depth);
        Layout(tree);

        Assert.Equal(0, tree.Table.Size.Width, 1);
        Assert.Equal(0, tree.Table.Size.Height, 1);
    }

    /// <summary>
    /// With a 5px border, the empty table is 10×10px, its borders. It was 14×14px.
    /// </summary>
    [Fact]
    public void An_Empty_Table_Is_As_Big_As_Its_Borders()
    {
        var tree = Build(0);
        tree.Table.BorderLeftWidth = tree.Table.BorderTopWidth = tree.Table.BorderRightWidth = tree.Table.BorderBottomWidth = "5px";
        tree.Table.BorderLeftStyle = tree.Table.BorderTopStyle = tree.Table.BorderRightStyle = tree.Table.BorderBottomStyle = "solid";
        Layout(tree);

        Assert.Equal(10, tree.Table.Size.Width, 1);
        Assert.Equal(10, tree.Table.Size.Height, 1);
    }

    /// <summary>
    /// With <c>width: 100px</c>, the empty table is 100px wide and 0px tall. It was 4px tall.
    /// </summary>
    [Fact]
    public void An_Empty_Table_With_A_Width_Is_No_Taller()
    {
        var tree = Build(0);
        tree.Table.Width = "100px";
        Layout(tree);

        Assert.Equal(100, tree.Table.Size.Width, 1);
        Assert.Equal(0, tree.Table.Size.Height, 1);
    }

    /// <summary>
    /// With a caption holding a word and nothing else, the table is as tall as the caption, 16px.
    /// It was 20px tall.
    /// </summary>
    [Fact]
    public void An_Empty_Table_With_A_Caption_Is_As_Tall_As_The_Caption()
    {
        var tree = Build(0);
        var caption = new CssBox(tree.Table, new HtmlTag("caption", false, null), BaseUrl) { Display = CssConstants.TableCaption };
        var word = new CssBox(caption, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        word.ParseToWords();
        Layout(tree);

        Assert.Equal(16, tree.Table.Size.Height, 1);
    }

    /// <summary>
    /// After a 10px block, the empty table with <c>margin: 20px 0</c>, and then a block with
    /// <c>margin-top: 30px</c>: the table's margins stay apart, as it establishes a formatting
    /// context, so the block begins 10 + 20 + 0 + 30 = 60px down. It began 64px down, below the
    /// spacing.
    /// </summary>
    [Fact]
    public void An_Empty_Table_Keeps_Its_Margins_Apart()
    {
        var tree = Build(0);
        tree.Table.MarginTop = tree.Table.MarginBottom = "20px";
        var container = tree.Table.ParentBox!;
        var before = new CssBox(container, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "10px" };
        container.Boxes.Remove(before);
        container.Boxes.Insert(0, before);
        var after = new CssBox(container, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "10px", MarginTop = "30px" };
        Layout(tree);

        Assert.Equal(container.Location.Y + 60, after.Location.Y, 1);
    }

    /// <summary>
    /// Laid out a second time, the empty table is 0×0px still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Size()
    {
        var tree = Build(2);
        Layout(tree);
        Layout(tree);

        Assert.Equal(0, tree.Table.Size.Width, 1);
        Assert.Equal(0, tree.Table.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a table with one cell holding a word has the
    /// spacing around it, 12×20px, the cell 2px in and 2px down.
    /// </summary>
    [Fact]
    public void Control_A_Table_With_A_Cell_Keeps_Its_Border_Spacing()
    {
        var tree = Build(2);
        var cell = new CssBox(tree.Table.Boxes[0].Boxes[0], new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };
        var word = new CssBox(cell, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        word.ParseToWords();
        Layout(tree);

        Assert.Equal(12, tree.Table.Size.Width, 1);
        Assert.Equal(20, tree.Table.Size.Height, 1);
        Assert.Equal(tree.Table.Location.X + 2, cell.Location.X, 1);
        Assert.Equal(tree.Table.Location.Y + 2, cell.Location.Y, 1);
    }

    /// <summary>
    /// The root and the table.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Table);

    /// <summary>
    /// In a 500px block in the root, a table with <c>border-spacing: 2px</c> holding nothing, when
    /// <paramref name="depth"/> is 0; an empty row group, when it is 1; or a row group holding a row
    /// with no cells, when it is 2.
    /// </summary>
    private static Tree Build(int depth)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var container = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "500px" };
        var table = new CssBox(container, new HtmlTag("table", false, null), BaseUrl) { Display = "table", BorderSpacing = "2px" };

        if (depth >= 1)
        {
            var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };

            if (depth >= 2)
                _ = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };
        }

        return new Tree(root, table);
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
