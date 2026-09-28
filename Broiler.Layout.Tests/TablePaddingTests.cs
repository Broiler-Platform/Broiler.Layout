using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table's padding lies between its border and the spacing around its cells, in the separated
/// border model, and its <c>width</c> counts the padding as it counts the border.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §17.6.2: in the collapsing border model a table has no padding; in the separated model
/// it has. The engine dropped it in both: a table with <c>padding: 10px</c> around a cell holding
/// an x was 8px wide, the x at its corner, where browsers make it 28px wide with the x 10px in.
/// </para>
/// <para>
/// Each table here is in a 500px block and has no border spacing unless stated; words are 16px
/// tall and 8px wide a letter, and no cell is padded.
/// </para>
/// </remarks>
public sealed class TablePaddingTests
{
    private static readonly Uri BaseUrl = new("file:///table-padding.html");

    /// <summary>
    /// With <c>padding: 10px</c>, the table around an x is 28px wide and 36px tall, and the cell is
    /// 10px in from its left and its top. The table was 8px wide and 16px tall, the cell at its
    /// corner.
    /// </summary>
    [Fact]
    public void A_Tables_Padding_Surrounds_Its_Cells()
    {
        var tree = Build(padding: "10px", texts: "x");
        Layout(tree);

        Assert.Equal(28, tree.Table.Size.Width, 1);
        Assert.Equal(36, tree.Table.Size.Height, 1);
        Assert.Equal(tree.Table.Location.X + 10, tree.Cells[0].Location.X, 1);
        Assert.Equal(tree.Table.Location.Y + 10, tree.Cells[0].Location.Y, 1);
    }

    /// <summary>
    /// With a 1px border, <c>border-spacing: 2px</c> and <c>padding: 10px</c>, the table around an x
    /// and a y is 44px wide, and the first cell 13px in: the border, then the padding, then the
    /// spacing. The table was 24px wide, the cell 3px in.
    /// </summary>
    [Fact]
    public void The_Padding_Lies_Between_The_Border_And_The_Spacing()
    {
        var tree = Build(padding: "10px", border: 1, spacing: "2px", texts: ["x", "y"]);
        Layout(tree);

        Assert.Equal(44, tree.Table.Size.Width, 1);
        Assert.Equal(tree.Table.Location.X + 13, tree.Cells[0].Location.X, 1);
        Assert.Equal(tree.Table.Location.Y + 13, tree.Cells[0].Location.Y, 1);
    }

    /// <summary>
    /// A table with <c>width: 200px</c> and <c>padding: 10px 20px</c> is 200px wide, its cell 160px
    /// wide and 20px in: the width counts the padding. The cell was 200px wide.
    /// </summary>
    [Fact]
    public void A_Tables_Width_Counts_Its_Padding()
    {
        var tree = Build(width: "200px", padding: "10px 20px", texts: "x");
        Layout(tree);

        Assert.Equal(200, tree.Table.Size.Width, 1);
        Assert.Equal(160, tree.Cells[0].Size.Width, 1);
        Assert.Equal(tree.Table.Location.X + 20, tree.Cells[0].Location.X, 1);
    }

    /// <summary>
    /// A caption above a table with <c>padding: 10px</c> lies above the padding and is as wide as
    /// the table, 28px; the cell is 10px below it. The caption was 8px wide, and the table as wide.
    /// </summary>
    [Fact]
    public void A_Caption_Lies_Above_The_Padding()
    {
        var tree = Build(padding: "10px", caption: true, texts: "x");
        Layout(tree);

        Assert.Equal(28, tree.Table.Size.Width, 1);
        Assert.Equal(tree.Table.Location.Y, tree.Caption!.Location.Y, 1);
        Assert.Equal(28, tree.Caption.Size.Width, 1);
        Assert.Equal(tree.Caption.Location.Y + 16 + 10, tree.Cells[0].Location.Y, 1);
    }

    /// <summary>
    /// Laid out a second time, the table with <c>padding: 10px</c> is 28px wide still: the padding
    /// is not lost after the first layout.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Keeps_The_Padding()
    {
        var tree = Build(padding: "10px", texts: "x");
        Layout(tree);
        Layout(tree);

        Assert.Equal(28, tree.Table.Size.Width, 1);
        Assert.Equal(tree.Table.Location.X + 10, tree.Cells[0].Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: with collapsed borders a table has no padding, so
    /// one with <c>padding: 10px</c> around an x is as wide as one without, its cell as far in.
    /// </summary>
    [Fact]
    public void Control_A_Table_With_Collapsed_Borders_Has_No_Padding()
    {
        var padded = Build(padding: "10px", collapse: true, texts: "x");
        var plain = Build(collapse: true, texts: "x");
        Layout(padded);
        Layout(plain);

        Assert.Equal(plain.Table.Size.Width, padded.Table.Size.Width, 1);
        Assert.Equal(plain.Cells[0].Location.X - plain.Table.Location.X, padded.Cells[0].Location.X - padded.Table.Location.X, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a table with no padding around an x is 8px wide,
    /// the cell at its corner.
    /// </summary>
    [Fact]
    public void Control_A_Table_With_No_Padding()
    {
        var tree = Build(texts: "x");
        Layout(tree);

        Assert.Equal(8, tree.Table.Size.Width, 1);
        Assert.Equal(tree.Table.Location.X, tree.Cells[0].Location.X, 1);
    }

    /// <summary>The root, the table, its caption if any, and the cells of its one row.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox? Caption, CssBox[] Cells);

    /// <summary>
    /// In a 500px block in the root, a table with the given width, auto when null, padding, border
    /// width on every side, border spacing and border model, a caption holding a word if asked, and
    /// one row of cells holding the given texts.
    /// </summary>
    private static Tree Build(string? width = null, string? padding = null, int border = 0, string spacing = "0",
        bool collapse = false, bool caption = false, params string[] texts)
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
        var table = new CssBox(container, new HtmlTag("table", false, null), BaseUrl) { Display = "table", BorderSpacing = spacing };

        if (width != null)
            table.Width = width;

        if (padding != null)
        {
            string[] parts = padding.Split(' ');
            table.PaddingTop = table.PaddingBottom = parts[0];
            table.PaddingLeft = table.PaddingRight = parts.Length > 1 ? parts[1] : parts[0];
        }

        if (border > 0)
        {
            table.BorderLeftWidth = table.BorderTopWidth = table.BorderRightWidth = table.BorderBottomWidth = border + "px";
            table.BorderLeftStyle = table.BorderTopStyle = table.BorderRightStyle = table.BorderBottomStyle = "solid";
        }

        if (collapse)
            table.BorderCollapse = CssConstants.Collapse;

        CssBox? captionBox = null;

        if (caption)
        {
            captionBox = new CssBox(table, new HtmlTag("caption", false, null), BaseUrl) { Display = CssConstants.TableCaption };
            Word(captionBox, "c");
        }

        var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
        var row = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };

        var cells = texts
            .Select(text =>
            {
                var cell = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };
                Word(cell, text);
                return cell;
            })
            .ToArray();

        return new Tree(root, table, captionBox, cells);
    }

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
