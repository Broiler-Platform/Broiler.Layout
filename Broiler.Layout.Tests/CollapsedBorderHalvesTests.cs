using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// In the collapsing border model, a cell takes half of each border it shares into its border box,
/// the table half of those on its perimeter, and the cells stand side by side with no spacing.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §17.6.2: the borders that meet at an edge resolve to one border, centred on the grid
/// line between the cells, or between a cell and the table's edge, and "the width of the table
/// includes half the table border". The resolved border was written into the cells' own border
/// properties whole, one cell of a shared edge getting it and the other none; the table kept its
/// own border whole too; and the cells overlapped by a pixel of spacing. So a cell of a table with
/// a 10px border started 9px in, inside the whole border, where browsers put it 5px in, and each
/// column after the first overlapped the one before by a pixel.
/// </para>
/// <para>
/// Each table here has <c>border-collapse: collapse</c> and holds cells with no padding, in a
/// block in the root. Words are 8px wide a letter and 16px tall.
/// </para>
/// </remarks>
public sealed class CollapsedBorderHalvesTests
{
    private static readonly Uri BaseUrl = new("file:///collapsed-border-halves.html");

    /// <summary>
    /// A 320px table with a 10px border around a cell holding an x: the cell starts 5px in, at the
    /// middle of the border, is 310px wide and 26px tall, the x and 5px of border above and below
    /// it, and the table is 36px tall. The cell started 9px in, 302px wide.
    /// </summary>
    [Fact]
    public void A_Cell_Starts_At_The_Middle_Of_The_Tables_Border()
    {
        var t = Build(table: b => Border(b, 10, "solid", "black"), width: "320px", rows: [["x"]]);
        Layout(t);

        var cell = t.Cells[0][0];
        Assert.Equal(5, cell.Location.X - t.Table.Location.X, 1);
        Assert.Equal(5, cell.Location.Y - t.Table.Location.Y, 1);
        Assert.Equal(310, cell.Size.Width, 1);
        Assert.Equal(26, cell.Size.Height, 1);
        Assert.Equal(320, t.Table.Size.Width, 1);
        Assert.Equal(36, t.Table.Size.Height, 1);
    }

    /// <summary>
    /// The cell's used border on each side is half the table's, in its style: 5px, solid, where it
    /// has none of its own; its own border width is left as it was. The table's used border is
    /// half its own, 5px, and its own is left as it was, 10px. The whole 10px border was written
    /// into the cell's own border width.
    /// </summary>
    [Fact]
    public void The_Cell_Uses_Half_The_Winning_Border_And_Keeps_Its_Own()
    {
        var t = Build(table: b => Border(b, 10, "solid", "black"), width: "320px", rows: [["x"]]);
        var cell = t.Cells[0][0];
        string ownWidth = cell.BorderTopWidth;
        Layout(t);

        Assert.Equal(5, cell.ActualBorderTopWidth, 2);
        Assert.Equal("solid", cell.BorderTopStyle);
        Assert.Equal(ownWidth, cell.BorderTopWidth);
        Assert.Equal(5, t.Table.ActualBorderLeftWidth, 2);
        Assert.Equal("10px", t.Table.BorderLeftWidth);
    }

    /// <summary>
    /// A cell with a 4px border of its own in the 10px-bordered table: the table's wider border wins
    /// at every edge, and the cell starts 5px in, 310px wide, as without its own.
    /// </summary>
    [Fact]
    public void The_Wider_Border_Wins_At_An_Edge()
    {
        var t = Build(table: b => Border(b, 10, "solid", "black"), cell: c => Border(c, 4, "solid", "red"), width: "320px", rows: [["x"]]);
        Layout(t);

        var cell = t.Cells[0][0];
        Assert.Equal(5, cell.Location.X - t.Table.Location.X, 1);
        Assert.Equal(310, cell.Size.Width, 1);
        Assert.Equal(5, cell.ActualBorderRightWidth, 2);
    }

    /// <summary>
    /// Two cells with 1px borders side by side and two below them: each starts where the one before
    /// it ends, half a pixel into the table, with half of each 1px border in it, 9px wide around an
    /// x; the table is 19px wide and 35px tall. The second cell overlapped the first by a pixel, and
    /// the table was a pixel narrower.
    /// </summary>
    [Fact]
    public void Cells_Stand_Side_By_Side_With_Half_Of_Each_Border()
    {
        var t = Build(cell: c => Border(c, 1, "solid", "black"), rows: [["x", "y"], ["z", "w"]]);
        Layout(t);

        var (a, b, c) = (t.Cells[0][0], t.Cells[0][1], t.Cells[1][0]);
        Assert.Equal(0.5, a.Location.X - t.Table.Location.X, 2);
        Assert.Equal(0.5, a.Location.Y - t.Table.Location.Y, 2);
        Assert.Equal(9, a.Size.Width, 2);
        Assert.Equal(a.Location.X + a.Size.Width, b.Location.X, 2);
        Assert.Equal(a.Location.Y + a.Size.Height, c.Location.Y, 2);
        Assert.Equal(17, a.Size.Height, 2);
        Assert.Equal(19, t.Table.Size.Width, 2);
        Assert.Equal(35, t.Table.Size.Height, 2);
    }

    /// <summary>
    /// A cell spanning both columns and holding a 400px block, over two cells holding an x, in a
    /// table with <c>width: 100px</c>, all with 1px borders: the table is 402px wide, the spanning
    /// cell 401px, and the two below 200.5px each, side by side. The table was 401px wide, and the
    /// two below overlapped by a pixel.
    /// </summary>
    [Fact]
    public void A_Spanning_Cell_Is_As_Wide_As_The_Cells_Below_It()
    {
        var t = Build(cell: c => Border(c, 1, "solid", "black"), width: "100px", rows: [["", "x", "x"]], span: true);
        Layout(t);

        var (spanning, left, right) = (t.Cells[0][0], t.Cells[1][0], t.Cells[1][1]);
        Assert.Equal(402, t.Table.Size.Width, 1);
        Assert.Equal(401, spanning.Size.Width, 1);
        Assert.Equal(200.5, left.Size.Width, 1);
        Assert.Equal(200.5, right.Size.Width, 1);
        Assert.Equal(left.Location.X + left.Size.Width, right.Location.X, 2);
    }

    /// <summary>
    /// A <c>hidden</c> border wins at its edge and leaves no border there: between a cell whose
    /// right border is hidden and one with a 6px border, neither has one, and the second cell
    /// starts where the first ends.
    /// </summary>
    [Fact]
    public void A_Hidden_Border_Leaves_No_Border_At_Its_Edge()
    {
        var t = Build(cell: c => Border(c, 6, "solid", "black"), rows: [["x", "y"]]);
        t.Cells[0][0].BorderRightStyle = CssConstants.Hidden;
        Layout(t);

        var (a, b) = (t.Cells[0][0], t.Cells[0][1]);
        Assert.Equal(0, a.ActualBorderRightWidth, 2);
        Assert.Equal(0, b.ActualBorderLeftWidth, 2);
        Assert.Equal(a.Location.X + a.Size.Width, b.Location.X, 2);
    }

    /// <summary>
    /// Laid out a second time, the cell starts 5px in still: the halves are taken from the borders
    /// the table and the cell have of their own, not from the halves.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Halves()
    {
        var t = Build(table: b => Border(b, 10, "solid", "black"), width: "320px", rows: [["x"]]);
        Layout(t);
        Layout(t);

        Assert.Equal(5, t.Cells[0][0].Location.X - t.Table.Location.X, 1);
        Assert.Equal(5, t.Cells[0][0].ActualBorderTopWidth, 2);
        Assert.Equal(36, t.Table.Size.Height, 1);
    }

    /// <summary>
    /// A shrink-to-fit float around the 2 × 2 table of 1px borders is as wide as the table, 19px.
    /// </summary>
    [Fact]
    public void A_Float_Around_The_Table_Is_As_Wide_As_It()
    {
        var t = Build(cell: c => Border(c, 1, "solid", "black"), rows: [["x", "y"], ["z", "w"]], inFloat: true);
        Layout(t);

        Assert.Equal(19, t.Table.Size.Width, 2);
        Assert.Equal(19, t.Table.ParentBox!.Size.Width, 2);
    }

    /// <summary>
    /// Controls, which pass before and after: in the separated border model a cell starts inside
    /// the table's whole 10px border, and the table keeps its own border.
    /// </summary>
    [Fact]
    public void Control_Separated_Borders_Are_Their_Own()
    {
        var t = Build(table: b => Border(b, 10, "solid", "black"), width: "320px", rows: [["x"]], collapse: false);
        Layout(t);

        Assert.Equal(10, t.Cells[0][0].Location.X - t.Table.Location.X, 1);
        Assert.Equal(10, t.Table.ActualBorderLeftWidth, 2);
        Assert.Equal(0, t.Cells[0][0].ActualBorderLeftWidth, 2);
    }

    /// <summary>The root, the table, and its cells row by row.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, List<List<CssBox>> Cells);

    private static void Border(CssBox box, int width, string style, string color)
    {
        box.BorderTopWidth = box.BorderRightWidth = box.BorderBottomWidth = box.BorderLeftWidth = width + "px";
        box.BorderTopStyle = box.BorderRightStyle = box.BorderBottomStyle = box.BorderLeftStyle = style;
        box.BorderTopColor = box.BorderRightColor = box.BorderBottomColor = box.BorderLeftColor = color;
    }

    /// <summary>
    /// A table in a block in the root, or in a float if <paramref name="inFloat"/>, with the given
    /// width and <c>border-collapse: collapse</c> unless <paramref name="collapse"/> is false, and a
    /// row of cells for each list of texts. With <paramref name="span"/>, the first row holds one
    /// cell spanning two columns and holding a 400px block, and the other texts go in the second.
    /// </summary>
    private static Tree Build(string[][] rows, Action<CssBox>? table = null, Action<CssBox>? cell = null,
        string? width = null, bool collapse = true, bool span = false, bool inFloat = false)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var parent = inFloat
            ? new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Float = CssConstants.Left }
            : body;
        var tableBox = new CssBox(parent, new HtmlTag("table", false, null), BaseUrl)
        {
            Display = CssConstants.Table,
            BorderSpacing = "0",
            BorderCollapse = collapse ? CssConstants.Collapse : "separate",
        };

        if (width != null)
            tableBox.Width = width;

        table?.Invoke(tableBox);
        var group = new CssBox(tableBox, new HtmlTag("tbody", false, null), BaseUrl) { Display = CssConstants.TableRowGroup };
        var cells = new List<List<CssBox>>();

        if (span)
        {
            var first = new CssBox(group, new HtmlTag("tr", false, null), BaseUrl) { Display = CssConstants.TableRow };
            var spanning = Cell(first, cell, null, colspan: 2);
            _ = new CssBox(spanning, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "400px", Height = "5px" };
            cells.Add([spanning]);

            var second = new CssBox(group, new HtmlTag("tr", false, null), BaseUrl) { Display = CssConstants.TableRow };
            var below = new List<CssBox>();
            foreach (var text in rows[0][1..])
                below.Add(Cell(second, cell, text));
            cells.Add(below);
        }
        else
        {
            foreach (var texts in rows)
            {
                var row = new CssBox(group, new HtmlTag("tr", false, null), BaseUrl) { Display = CssConstants.TableRow };
                var rowCells = new List<CssBox>();
                foreach (var text in texts)
                    rowCells.Add(Cell(row, cell, text));
                cells.Add(rowCells);
            }
        }

        return new Tree(root, tableBox, cells);
    }

    private static CssBox Cell(CssBox row, Action<CssBox>? style, string? text, int colspan = 1)
    {
        var attributes = new Dictionary<string, string>();
        if (colspan > 1)
            attributes["colspan"] = colspan.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var cellBox = new CssBox(row, new HtmlTag("td", false, attributes), BaseUrl) { Display = CssConstants.TableCell };
        style?.Invoke(cellBox);

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
