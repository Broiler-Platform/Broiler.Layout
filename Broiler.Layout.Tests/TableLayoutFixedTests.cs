using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table with <c>table-layout: fixed</c> and a width of its own sizes its columns from their own
/// widths and its first row's, and the content of its cells overflows them.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §17.5.2.1: a column element's width sets its column's; otherwise the first row's cell
/// that starts in the column sets it, divided over the columns it spans; and the columns left share
/// the room the table has left. The engine had no <c>table-layout</c> property, and every table went
/// through the automatic algorithm, which sizes columns by their content: a table with
/// <c>table-layout: fixed; width: 100px</c> holding a 400px block was 400px wide, where browsers
/// keep it 100px.
/// </para>
/// <para>
/// Each table here has <c>border-spacing: 0</c> and cells with no padding unless noted, in a block
/// in the root. Words are 8px wide a letter and 16px tall.
/// </para>
/// </remarks>
public sealed class TableLayoutFixedTests
{
    private static readonly Uri BaseUrl = new("file:///table-layout-fixed.html");

    private const string LongWord = "abcdefghijabcdefghijabcdefghijabcdefghij";

    /// <summary>
    /// A 40-letter word, 320px, in a table 100px wide: the table and its cell are 100px wide, and
    /// the word overflows. The table was 320px wide.
    /// </summary>
    [Fact]
    public void A_Long_Word_Does_Not_Widen_The_Table()
    {
        var t = Build("100px", [[LongWord]]);
        Layout(t);

        Assert.Equal(100, t.Table.Size.Width, 1);
        Assert.Equal(100, t.Cells[0][0].Size.Width, 1);
    }

    /// <summary>
    /// A 400px block in a cell of a table 100px wide leaves the table 100px wide. It was 400px.
    /// </summary>
    [Fact]
    public void A_Wide_Block_Does_Not_Widen_The_Table()
    {
        var t = Build("100px", [[""]]);
        _ = new CssBox(t.Cells[0][0], new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "400px", Height = "5px" };
        Layout(t);

        Assert.Equal(100, t.Table.Size.Width, 1);
    }

    /// <summary>
    /// The rows after the first do not size the columns: with a 150px cell in the second row of a
    /// 200px table, the two columns share it equally, 100px each. They were 150px and 50px.
    /// </summary>
    [Fact]
    public void Only_The_First_Row_Sizes_The_Columns()
    {
        var t = Build("200px", [["x", "y"], ["z", "w"]]);
        t.Cells[1][0].Width = "150px";
        Layout(t);

        Assert.Equal(100, t.Cells[0][0].Size.Width, 1);
        Assert.Equal(100, t.Cells[0][1].Size.Width, 1);
    }

    /// <summary>
    /// A first-row cell 50px wide with 5px of padding makes its column 60px, its border box, and the
    /// other column has the 140px left. The column was 50px.
    /// </summary>
    [Fact]
    public void A_Cells_Padding_Is_Added_To_Its_Width()
    {
        var t = Build("200px", [["x", "y"]]);
        var cell = t.Cells[0][0];
        cell.Width = "50px";
        cell.PaddingLeft = cell.PaddingRight = cell.PaddingTop = cell.PaddingBottom = "5px";
        Layout(t);

        Assert.Equal(60, cell.Size.Width, 1);
        Assert.Equal(140, t.Cells[0][1].Size.Width, 1);
    }

    /// <summary>
    /// With <c>box-sizing: border-box</c>, the same cell's column is its 50px.
    /// </summary>
    [Fact]
    public void A_Border_Box_Cell_Width_Is_Its_Columns()
    {
        var t = Build("200px", [["x", "y"]]);
        var cell = t.Cells[0][0];
        cell.Width = "50px";
        cell.BoxSizing = "border-box";
        cell.PaddingLeft = cell.PaddingRight = "5px";
        Layout(t);

        Assert.Equal(50, cell.Size.Width, 1);
        Assert.Equal(150, t.Cells[0][1].Size.Width, 1);
    }

    /// <summary>
    /// A cell spanning two columns of a 200px table, 100px wide, gives each 50px; the third column
    /// has the 100px left, and the second row's first cell is 50px wide.
    /// </summary>
    [Fact]
    public void A_Spanning_Cells_Width_Is_Divided_Over_Its_Columns()
    {
        var t = Build("200px", [["x", "y"], ["z", "w", "v"]], firstRowSpan: 2);
        t.Cells[0][0].Width = "100px";
        Layout(t);

        Assert.Equal(100, t.Cells[0][0].Size.Width, 1);
        Assert.Equal(100, t.Cells[0][1].Size.Width, 1);
        Assert.Equal(50, t.Cells[1][0].Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the table is 100px wide still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Width()
    {
        var t = Build("100px", [[LongWord]]);
        Layout(t);
        Layout(t);

        Assert.Equal(100, t.Table.Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: in a 200px table, a first-row cell 30px wide leaves
    /// the other column 170px; one 25% wide is 50px; and a column element's 50px wins over a
    /// first-row cell's 120px.
    /// </summary>
    [Theory]
    [InlineData("30px", false, 30)]
    [InlineData("25%", false, 50)]
    [InlineData("120px", true, 50)]
    public void Control_Columns_With_Widths_Of_Their_Own(string cellWidth, bool columnElement, double width)
    {
        var t = Build("200px", [["x", "y"]], columnWidth: columnElement ? "50px" : null);
        t.Cells[0][0].Width = cellWidth;
        Layout(t);

        Assert.Equal(width, t.Cells[0][0].Size.Width, 1);
        Assert.Equal(200 - width, t.Cells[0][1].Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: 50px and 100px columns in a 300px table are widened in
    /// proportion to 100px and 200px, and 80px and 80px ones in a 100px table widen it to 160px.
    /// </summary>
    [Theory]
    [InlineData("300px", "50px", "100px", 100, 200, 300)]
    [InlineData("100px", "80px", "80px", 80, 80, 160)]
    public void Control_Columns_That_All_Have_Widths(string tableWidth, string first, string second, double firstWidth, double secondWidth, double width)
    {
        var t = Build(tableWidth, [["x", "y"]]);
        t.Cells[0][0].Width = first;
        t.Cells[0][1].Width = second;
        Layout(t);

        Assert.Equal(firstWidth, t.Cells[0][0].Size.Width, 1);
        Assert.Equal(secondWidth, t.Cells[0][1].Size.Width, 1);
        Assert.Equal(width, t.Table.Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a table with <c>table-layout: fixed</c> and no width
    /// of its own is laid out by the automatic algorithm, as wide as the word it holds, 320px.
    /// </summary>
    [Fact]
    public void Control_A_Table_With_An_Auto_Width_Is_Laid_Out_Automatically()
    {
        var t = Build(null, [[LongWord]]);
        Layout(t);

        Assert.Equal(320, t.Table.Size.Width, 1);
    }

    /// <summary>The root, the table, and its cells row by row.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, List<List<CssBox>> Cells);

    /// <summary>
    /// A table with <c>table-layout: fixed</c> and the given width, in a block in the root, holding
    /// a row of cells for each list of texts; the first row's first cell spans
    /// <paramref name="firstRowSpan"/> columns, and a column element of the given width comes
    /// first when <paramref name="columnWidth"/> is given.
    /// </summary>
    private static Tree Build(string? width, string[][] rows, string? columnWidth = null, int firstRowSpan = 1)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var tableBox = new CssBox(body, new HtmlTag("table", false, null), BaseUrl)
        {
            Display = CssConstants.Table,
            BorderSpacing = "0",
            TableLayout = "fixed",
        };

        if (width != null)
            tableBox.Width = width;

        if (columnWidth != null)
        {
            _ = new CssBox(tableBox, new HtmlTag("col", false, null), BaseUrl) { Display = "table-column", Width = columnWidth };
            _ = new CssBox(tableBox, new HtmlTag("col", false, null), BaseUrl) { Display = "table-column" };
        }

        var group = new CssBox(tableBox, new HtmlTag("tbody", false, null), BaseUrl) { Display = CssConstants.TableRowGroup };
        var cells = new List<List<CssBox>>();

        for (int r = 0; r < rows.Length; r++)
        {
            var row = new CssBox(group, new HtmlTag("tr", false, null), BaseUrl) { Display = CssConstants.TableRow };
            var rowCells = new List<CssBox>();

            for (int c = 0; c < rows[r].Length; c++)
                rowCells.Add(Cell(row, rows[r][c], r == 0 && c == 0 ? firstRowSpan : 1));

            cells.Add(rowCells);
        }

        return new Tree(root, tableBox, cells);
    }

    private static CssBox Cell(CssBox row, string text, int colspan)
    {
        var attributes = new Dictionary<string, string>();
        if (colspan > 1)
            attributes["colspan"] = colspan.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var cellBox = new CssBox(row, new HtmlTag("td", false, attributes), BaseUrl) { Display = CssConstants.TableCell };

        if (!string.IsNullOrEmpty(text))
        {
            var word = new CssBox(cellBox, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
            word.ParseToWords();
        }

        return cellBox;
    }

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
