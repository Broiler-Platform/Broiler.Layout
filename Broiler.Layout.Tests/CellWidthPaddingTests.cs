using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table cell's <c>width</c> is its content box's, and its column holds its border box: the
/// cell's padding and borders are added to its width.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §17.5.2.2: a column is as wide as the cells in it, and a cell's <c>width</c>, like any
/// box's, is its content box's unless its <c>box-sizing</c> says otherwise. The automatic algorithm
/// took it for the border box: a cell with <c>width: 50px; padding: 5px</c> made a 50px column,
/// where browsers make it 60px.
/// </para>
/// <para>
/// Each table here has <c>border-spacing: 0</c> and is laid out by the automatic algorithm, in a
/// block in the root. Words are 8px wide a letter and 16px tall.
/// </para>
/// </remarks>
public sealed class CellWidthPaddingTests
{
    private static readonly Uri BaseUrl = new("file:///cell-width-padding.html");

    /// <summary>
    /// In a 200px table, a cell 50px wide with 5px of padding makes its column 60px, and the other
    /// column has the 140px left. It made it 50px.
    /// </summary>
    [Fact]
    public void A_Cells_Padding_Widens_Its_Column()
    {
        var t = Build("200px", [["x", "y"]]);
        Pad(t.Cells[0][0], "50px", "5px");
        Layout(t);

        Assert.Equal(60, t.Cells[0][0].Size.Width, 1);
        Assert.Equal(140, t.Cells[0][1].Size.Width, 1);
    }

    /// <summary>
    /// A table of one such cell and no width of its own is 60px wide. It was 50px.
    /// </summary>
    [Fact]
    public void A_Table_Is_As_Wide_As_Its_Cells_Border_Box()
    {
        var t = Build(null, [["x"]]);
        Pad(t.Cells[0][0], "50px", "5px");
        Layout(t);

        Assert.Equal(60, t.Table.Size.Width, 1);
        Assert.Equal(60, t.Cells[0][0].Size.Width, 1);
    }

    /// <summary>
    /// A cell 50px wide with 5px of padding on each side and a 3px border makes a 66px column; beside
    /// a cell holding a y, the table is 74px wide. The column was 50px and the table 58px.
    /// </summary>
    [Fact]
    public void A_Cells_Border_Widens_Its_Column_Too()
    {
        var t = Build(null, [["x", "y"]]);
        var cell = t.Cells[0][0];
        cell.Width = "50px";
        cell.PaddingLeft = cell.PaddingRight = "5px";
        cell.BorderLeftWidth = cell.BorderRightWidth = cell.BorderTopWidth = cell.BorderBottomWidth = "3px";
        cell.BorderLeftStyle = cell.BorderRightStyle = cell.BorderTopStyle = cell.BorderBottomStyle = "solid";
        Layout(t);

        Assert.Equal(66, cell.Size.Width, 1);
        Assert.Equal(74, t.Table.Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the column is 60px still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Width()
    {
        var t = Build("200px", [["x", "y"]]);
        Pad(t.Cells[0][0], "50px", "5px");
        Layout(t);
        Layout(t);

        Assert.Equal(60, t.Cells[0][0].Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: with <c>box-sizing: border-box</c> the cell's column
    /// is its 50px, and a cell with padding and no width is as wide as its x and its padding, 18px.
    /// </summary>
    [Theory]
    [InlineData("50px", true, 50)]
    [InlineData(null, false, 18)]
    public void Control_Border_Box_And_Auto_Widths(string? width, bool borderBox, double expected)
    {
        var t = Build(null, [["x"]]);
        var cell = t.Cells[0][0];
        Pad(cell, width, "5px");

        if (borderBox)
            cell.BoxSizing = "border-box";

        Layout(t);

        Assert.Equal(expected, cell.Size.Width, 1);
    }

    private static void Pad(CssBox cell, string? width, string padding)
    {
        if (width != null)
            cell.Width = width;

        cell.PaddingLeft = cell.PaddingRight = cell.PaddingTop = cell.PaddingBottom = padding;
    }

    /// <summary>The root, the table, and its cells row by row.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, List<List<CssBox>> Cells);

    /// <summary>
    /// A table of the given width, or <c>auto</c>, in a block in the root, holding a row of cells
    /// for each list of texts.
    /// </summary>
    private static Tree Build(string? width, string[][] rows)
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
        };

        if (width != null)
            tableBox.Width = width;

        var group = new CssBox(tableBox, new HtmlTag("tbody", false, null), BaseUrl) { Display = CssConstants.TableRowGroup };
        var cells = new List<List<CssBox>>();

        for (int r = 0; r < rows.Length; r++)
        {
            var row = new CssBox(group, new HtmlTag("tr", false, null), BaseUrl) { Display = CssConstants.TableRow };
            var rowCells = new List<CssBox>();

            for (int c = 0; c < rows[r].Length; c++)
                rowCells.Add(Cell(row, rows[r][c], 1));

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
