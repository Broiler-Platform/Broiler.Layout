using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A block with a width of its own in a table cell widens the cell's column to that width, as a
/// long word does, so a table with a width narrower than its cells' content grows to fit them.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §17.5.2.2: a table's used width is the greater of its <c>width</c> and the minimum its
/// columns need, and a column needs at least the minimum content width of each of its cells, the
/// width their content needs not to overflow them. The engine measured that minimum from the
/// cells' words alone, so a block 400px wide in the cell of a table with <c>width: 100px</c>
/// counted for nothing: the table stayed 100px wide and the block ran 300px out of it, where
/// browsers make the table 400px wide.
/// </para>
/// <para>
/// A cell spanning columns shares its widths out over them, its blocks with the rest; see
/// <see cref="SpanningCellWidthTests"/>.
/// </para>
/// <para>
/// Each table here is in a 500px block and has no border spacing; words are 16px tall and 8px wide
/// a letter, and each block in a cell is 20px tall.
/// </para>
/// </remarks>
public sealed class TableCellBlockWidthTests
{
    private static readonly Uri BaseUrl = new("file:///table-cell-block-width.html");

    /// <summary>
    /// A table with <c>width: 100px</c> whose cell holds a block 400px wide is 400px wide, and so
    /// is the cell. Both were 100px wide.
    /// </summary>
    [Fact]
    public void A_Block_In_A_Cell_Widens_Its_Table()
    {
        var tree = Build("100px", 1);
        Block(tree.Cells[0], "400px");
        Layout(tree);

        Assert.Equal(400, tree.Table.Size.Width, 1);
        Assert.Equal(400, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>
    /// A table with <c>width: 300px</c> whose two cells hold blocks 250px and 200px wide is 450px
    /// wide, the first cell 250px wide and the second 200px wide beside it. The table was 300px
    /// wide, and each cell 150px.
    /// </summary>
    [Fact]
    public void Blocks_In_Two_Cells_Widen_Each_Column()
    {
        var tree = Build("300px", 2);
        Block(tree.Cells[0], "250px");
        Block(tree.Cells[1], "200px");
        Layout(tree);

        Assert.Equal(450, tree.Table.Size.Width, 1);
        Assert.Equal(250, tree.Cells[0].Size.Width, 1);
        Assert.Equal(200, tree.Cells[1].Size.Width, 1);
        Assert.Equal(tree.Table.Location.X + 250, tree.Cells[1].Location.X, 1);
    }

    /// <summary>
    /// With 10px of padding around the cell's 400px block, the table is 420px wide. It was 100px
    /// wide.
    /// </summary>
    [Fact]
    public void A_Cell_Is_As_Wide_As_Its_Block_And_Its_Padding()
    {
        var tree = Build("100px", 1);
        tree.Cells[0].PaddingLeft = tree.Cells[0].PaddingRight = "10px";
        Block(tree.Cells[0], "400px");
        Layout(tree);

        Assert.Equal(420, tree.Table.Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the table is 400px wide still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Width()
    {
        var tree = Build("100px", 1);
        Block(tree.Cells[0], "400px");
        Layout(tree);
        Layout(tree);

        Assert.Equal(400, tree.Table.Size.Width, 1);
    }

    /// <summary>
    /// A table with <c>width: 100px</c> whose second row holds a 400px block, or a word as wide,
    /// beside an x, under a row of two x's, is 408px wide, the column 400px. With the word it was
    /// 392px wide, the column taking the word less the x's the row above had in it and in the
    /// column after it; with the block 100px, and so 392px once the block was counted.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Content_Under_A_Row_Of_Words_Widens_Its_Column_To_Its_Whole_Width(bool block)
    {
        var tree = Build("100px", 2);
        Word(tree.Cells[0], "x");
        Word(tree.Cells[1], "x");
        var cells = AddRow(tree, 2);

        if (block)
            Block(cells[0], "400px");
        else
            Word(cells[0], new string('x', 50));

        Word(cells[1], "x");
        Layout(tree);

        Assert.Equal(408, tree.Table.Size.Width, 1);
        Assert.Equal(400, cells[0].Size.Width, 1);
    }

    /// <summary>
    /// A table with <c>width: 100px</c> holding a 200px block in its first row's first cell and a
    /// 300px block in its second row's second cell, each beside an x, is 500px wide, the columns
    /// 200px and 300px. It was 100px wide, and with the blocks counted but the second less the x
    /// above it, 492px.
    /// </summary>
    [Fact]
    public void Blocks_In_Two_Rows_Widen_Each_Column()
    {
        var tree = Build("100px", 2);
        Block(tree.Cells[0], "200px");
        Word(tree.Cells[1], "x");
        var cells = AddRow(tree, 2);
        Word(cells[0], "x");
        Block(cells[1], "300px");
        Layout(tree);

        Assert.Equal(500, tree.Table.Size.Width, 1);
        Assert.Equal(200, cells[0].Size.Width, 1);
        Assert.Equal(300, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// A cell spanning both columns and holding a 400px block, above or below a row of two cells
    /// holding an x, leaves the two columns as wide as each other, as browsers do; with an auto
    /// width the table is 400px wide, as in browsers, each column 200px. With a spanning cell's
    /// blocks counted as its words are, the second column took the whole block, less the first
    /// column's x when the x's were above: it was 400px or 392px wide, and the table 600px or
    /// 592px, or with <c>width: 100px</c> 450px or 442px.
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData(null, false)]
    [InlineData("100px", true)]
    [InlineData("100px", false)]
    public void A_Block_In_A_Cell_Spanning_Two_Columns_Leaves_Them_Equal(string? width, bool spanningAbove)
    {
        var tree = Build(width, 0);
        CssBox[] spanning = [], cells = [];

        for (int i = 0; i < 2; i++)
        {
            if (i == 0 == spanningAbove)
                spanning = AddRow(tree, 1, 2);
            else
                cells = AddRow(tree, 2);
        }

        Block(spanning[0], "400px");
        Word(cells[0], "x");
        Word(cells[1], "x");
        Layout(tree);

        Assert.Equal(cells[0].Size.Width, cells[1].Size.Width, 1);

        if (width == null)
        {
            Assert.Equal(400, tree.Table.Size.Width, 1);
            Assert.Equal(200, cells[0].Size.Width, 1);
        }
    }

    /// <summary>
    /// Control, which passes before and after: a table with an auto width fits the 400px block,
    /// 400px wide.
    /// </summary>
    [Fact]
    public void Control_A_Table_With_An_Auto_Width_Fits_Its_Block()
    {
        var tree = Build(null, 1);
        Block(tree.Cells[0], "400px");
        Layout(tree);

        Assert.Equal(400, tree.Table.Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: the table with <c>width: 100px</c> stays 100px wide
    /// around a word, around a block with <c>width: 50%</c>, whose width comes from the cell, and
    /// around an absolutely positioned block 400px wide, which is out of flow and takes no room.
    /// </summary>
    [Theory]
    [InlineData("word")]
    [InlineData("percentage")]
    [InlineData("absolute")]
    public void Control_Content_That_Fits_Leaves_The_Width(string content)
    {
        var tree = Build("100px", 1);
        var cell = tree.Cells[0];

        switch (content)
        {
            case "word":
                Word(cell, "X");
                break;
            case "percentage":
                Block(cell, "50%");
                break;
            default:
                cell.Position = CssConstants.Relative;
                Word(cell, "X");
                var positioned = Block(cell, "400px");
                positioned.Position = CssConstants.Absolute;
                positioned.Top = positioned.Left = "0";
                break;
        }

        Layout(tree);

        Assert.Equal(100, tree.Table.Size.Width, 1);
    }

    /// <summary>The root, the table, its row group, and the cells of its first row.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox Rows, CssBox[] Cells);

    /// <summary>
    /// In a 500px block in the root, a table with the given width, auto when null, and a first row
    /// of <paramref name="cells"/> empty cells, none of them padded; with no cells, no row.
    /// </summary>
    private static Tree Build(string? width, int cells)
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
        var table = new CssBox(container, new HtmlTag("table", false, null), BaseUrl) { Display = "table", BorderSpacing = "0" };

        if (width != null)
            table.Width = width;

        var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };

        return new Tree(root, table, rows, cells > 0 ? Row(rows, cells, 1) : []);
    }

    /// <summary>
    /// Adds to the table a row of <paramref name="cells"/> empty cells, none of them padded, each
    /// spanning <paramref name="colspan"/> columns.
    /// </summary>
    private static CssBox[] AddRow(Tree tree, int cells, int colspan = 1) => Row(tree.Rows, cells, colspan);

    private static CssBox[] Row(CssBox rows, int cells, int colspan)
    {
        var row = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };
        var attributes = new Dictionary<string, string> { ["colspan"] = colspan.ToString(CultureInfo.InvariantCulture) };

        return Enumerable.Range(0, cells)
            .Select(_ => new CssBox(row, new HtmlTag("td", false, attributes), BaseUrl) { Display = "table-cell" })
            .ToArray();
    }

    /// <summary>Adds to <paramref name="parent"/> a 20px tall block with the given width.</summary>
    private static CssBox Block(CssBox parent, string width) =>
        new(parent, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = width, Height = "20px" };

    private static void Word(CssBox parent, string text)
    {
        var word = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();
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
