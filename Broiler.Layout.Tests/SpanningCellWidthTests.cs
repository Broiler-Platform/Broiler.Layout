using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A cell spanning columns shares its minimum and maximum widths out over them as browsers do:
/// first over the columns given a width of their own, up to it, then over the others, up to their
/// maximums, and past that over the others in proportion to their maximums.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §17.5.2.2: a cell spanning columns needs them, with the spacing between them, to be as
/// wide as it together; CSS Tables 3 says how browsers share what they lack out. The engine put a
/// spanning cell's minimum on its last column alone, less what the columns before it had so far,
/// counting its longest word only, and split its maximum evenly. In a table with
/// <c>width: 100px</c>, a cell spanning two columns and holding a 40-letter word, over a row of two
/// cells holding an x, made the columns 50px and 320px wide, where browsers make each 160px; with a
/// 400px block in it, the table stayed 100px wide and the block ran out of it.
/// </para>
/// <para>
/// Each table here is in a 1000px block and has no border spacing unless stated; words are 16px
/// tall and 8px wide a letter, and each block in a cell is 20px tall.
/// </para>
/// </remarks>
public sealed class SpanningCellWidthTests
{
    private static readonly Uri BaseUrl = new("file:///spanning-cell-width.html");

    /// <summary>
    /// A cell spanning both columns of a table with <c>width: 100px</c> and holding a 400px block,
    /// above or below a row of two cells holding an x, makes the table 400px wide and each column
    /// 200px. The table was 100px wide.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_Block_In_A_Spanning_Cell_Widens_Its_Columns_Evenly(bool spanningAbove)
    {
        var tree = Build("100px");
        CssBox[] spanning = [], cells = [];

        for (int i = 0; i < 2; i++)
        {
            if (i == 0 == spanningAbove)
                spanning = AddRow(tree, (2, null));
            else
                cells = AddRow(tree, (1, null), (1, null));
        }

        Block(spanning[0], "400px");
        Word(cells[0], "x");
        Word(cells[1], "x");
        Layout(tree);

        Assert.Equal(400, tree.Table.Size.Width, 1);
        Assert.Equal(200, cells[0].Size.Width, 1);
        Assert.Equal(200, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// With a 40-letter word in the spanning cell above the x's, the table is 320px wide and each
    /// column 160px. It was 370px wide, the columns 50px and 320px.
    /// </summary>
    [Fact]
    public void A_Long_Word_In_A_Spanning_Cell_Is_Shared_Out_Too()
    {
        var tree = Build("100px");
        var spanning = AddRow(tree, (2, null));
        var cells = AddRow(tree, (1, null), (1, null));
        Word(spanning[0], new string('x', 40));
        Word(cells[0], "x");
        Word(cells[1], "x");
        Layout(tree);

        Assert.Equal(320, tree.Table.Size.Width, 1);
        Assert.Equal(160, cells[0].Size.Width, 1);
        Assert.Equal(160, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// Over an x and a 10-letter word, the 400px block is shared out in proportion to the columns'
    /// maximums, 8px and 80px: the columns are 36.36px and 363.64px wide, with <c>width: 100px</c>
    /// or an auto width. The table was 130px wide with <c>width: 100px</c>; with an auto width it
    /// was 400px wide, each column 200px.
    /// </summary>
    [Theory]
    [InlineData("100px")]
    [InlineData(null)]
    public void The_Block_Is_Shared_Out_In_Proportion_To_The_Columns_Maximums(string? width)
    {
        var tree = Build(width);
        var spanning = AddRow(tree, (2, null));
        var cells = AddRow(tree, (1, null), (1, null));
        Block(spanning[0], "400px");
        Word(cells[0], "x");
        Word(cells[1], new string('x', 10));
        Layout(tree);

        Assert.Equal(400, tree.Table.Size.Width, 1);
        Assert.Equal(400.0 * 8 / 88, cells[0].Size.Width, 1);
        Assert.Equal(400.0 * 80 / 88, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// With an auto width, twenty x's with spaces between them in the spanning cell, 236px wide on
    /// one line, over an x and a 10-letter word, make the table 236px wide: what the columns'
    /// maximums lack of the cell's is shared out in proportion to them, 8px and 80px, so the
    /// columns are 21.45px and 214.55px wide. They were 118px each, the cell's maximum split
    /// evenly.
    /// </summary>
    [Fact]
    public void A_Spanning_Cells_Maximum_Is_Shared_Out_In_Proportion_Too()
    {
        var tree = Build(null);
        var spanning = AddRow(tree, (2, null));
        var cells = AddRow(tree, (1, null), (1, null));
        Word(spanning[0], string.Join(" ", Enumerable.Repeat("x", 20)));
        Word(cells[0], "x");
        Word(cells[1], new string('x', 10));
        Layout(tree);

        Assert.Equal(236, tree.Table.Size.Width, 1);
        Assert.Equal(8 + 148.0 * 8 / 88, cells[0].Size.Width, 1);
        Assert.Equal(80 + 148.0 * 80 / 88, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// With an auto width, a 40-letter word in the spanning cell makes the table 320px wide and
    /// each column 160px. It was 480px wide, the columns 160px and 320px.
    /// </summary>
    [Fact]
    public void A_Table_With_An_Auto_Width_Shares_A_Long_Word_Out()
    {
        var tree = Build(null);
        var spanning = AddRow(tree, (2, null));
        var cells = AddRow(tree, (1, null), (1, null));
        Word(spanning[0], new string('x', 40));
        Word(cells[0], "x");
        Word(cells[1], "x");
        Layout(tree);

        Assert.Equal(320, tree.Table.Size.Width, 1);
        Assert.Equal(160, cells[0].Size.Width, 1);
        Assert.Equal(160, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// With <c>border-spacing: 4px</c>, the 400px block over the two x's makes the table 408px
    /// wide and each column 198px, the spacing between them counted in the block's width. The table
    /// was 104px wide.
    /// </summary>
    [Fact]
    public void The_Spacing_Between_The_Columns_Counts_Toward_The_Block()
    {
        var tree = Build("100px", "4px");
        var spanning = AddRow(tree, (2, null));
        var cells = AddRow(tree, (1, null), (1, null));
        Block(spanning[0], "400px");
        Word(cells[0], "x");
        Word(cells[1], "x");
        Layout(tree);

        Assert.Equal(408, tree.Table.Size.Width, 1);
        Assert.Equal(198, cells[0].Size.Width, 1);
        Assert.Equal(198, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// A 300px block in a cell spanning three columns, over three x's, makes each column 100px
    /// wide; and a 400px block in a cell spanning the second and third of three columns, beside an
    /// x, makes them 200px each and leaves the first as wide as its x, 8px. The tables were 100px
    /// wide.
    /// </summary>
    [Theory]
    [InlineData(0, 3, 300, 100, 100)]
    [InlineData(1, 2, 400, 8, 200)]
    public void A_Spanning_Cell_Is_Shared_Out_Over_The_Columns_It_Spans(int before, int span, int block, double first, double spanned)
    {
        var tree = Build("100px");
        var spans = Enumerable.Repeat<(int, string?)>((1, null), before).Append((span, null)).ToArray();
        var spanningRow = AddRow(tree, spans);
        var cells = AddRow(tree, (1, null), (1, null), (1, null));

        foreach (var cell in spanningRow.Take(before))
            Word(cell, "x");

        Block(spanningRow[before], block + "px");

        foreach (var cell in cells)
            Word(cell, "x");

        Layout(tree);

        Assert.Equal(first + 2 * spanned, tree.Table.Size.Width, 1);
        Assert.Equal(first, cells[0].Size.Width, 1);
        Assert.Equal(spanned, cells[1].Size.Width, 1);
        Assert.Equal(spanned, cells[2].Size.Width, 1);
    }

    /// <summary>
    /// A 95-letter word, 760px long, in a cell spanning three columns, over cells with widths of
    /// 150px, auto and 160px each holding an x, makes the columns 150px, 450px and 160px wide, with
    /// <c>width: 760px</c> or an auto width: the columns given a width keep it, and the other takes
    /// the rest. The table was 1360px wide with <c>width: 760px</c>, the last column 760px, and
    /// 918px with an auto width, the middle column 8px.
    /// </summary>
    [Theory]
    [InlineData("760px")]
    [InlineData(null)]
    public void The_Columns_Given_A_Width_Keep_It_And_The_Others_Take_The_Rest(string? width)
    {
        var tree = Build(width);
        var spanning = AddRow(tree, (3, null));
        var cells = AddRow(tree, (1, "150px"), (1, null), (1, "160px"));
        Word(spanning[0], new string('x', 95));

        foreach (var cell in cells)
            Word(cell, "x");

        Layout(tree);

        Assert.Equal(760, tree.Table.Size.Width, 1);
        Assert.Equal(150, cells[0].Size.Width, 1);
        Assert.Equal(450, cells[1].Size.Width, 1);
        Assert.Equal(160, cells[2].Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the table with the 400px block over two x's is 400px wide still,
    /// each column 200px.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Widths()
    {
        var tree = Build("100px");
        var spanning = AddRow(tree, (2, null));
        var cells = AddRow(tree, (1, null), (1, null));
        Block(spanning[0], "400px");
        Word(cells[0], "x");
        Word(cells[1], "x");
        Layout(tree);
        Layout(tree);

        Assert.Equal(400, tree.Table.Size.Width, 1);
        Assert.Equal(200, cells[0].Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: an x in a cell spanning two columns, over two
    /// 10-letter words, leaves the columns as wide as the words, 80px each, with an auto width;
    /// and a table with <c>width: 100px</c> and no spanning cell, holding two x's, is 100px wide,
    /// each column 50px.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Control_Columns_Wide_Enough_Are_Left_As_They_Are(bool spanning)
    {
        var tree = Build(spanning ? null : "100px");

        if (spanning)
            Word(AddRow(tree, (2, null))[0], "x");

        var cells = AddRow(tree, (1, null), (1, null));
        Word(cells[0], spanning ? new string('x', 10) : "x");
        Word(cells[1], spanning ? new string('x', 10) : "x");
        Layout(tree);

        Assert.Equal(spanning ? 160 : 100, tree.Table.Size.Width, 1);
        Assert.Equal(spanning ? 80 : 50, cells[0].Size.Width, 1);
        Assert.Equal(spanning ? 80 : 50, cells[1].Size.Width, 1);
    }

    /// <summary>The root, the table, and its row group.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox Rows);

    /// <summary>
    /// In a 1000px block in the root, a table with the given width, auto when null, and border
    /// spacing, and no rows yet.
    /// </summary>
    private static Tree Build(string? width, string spacing = "0")
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var container = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "1000px" };
        var table = new CssBox(container, new HtmlTag("table", false, null), BaseUrl) { Display = "table", BorderSpacing = spacing };

        if (width != null)
            table.Width = width;

        var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };

        return new Tree(root, table, rows);
    }

    /// <summary>
    /// Adds to the table a row of empty cells, none of them padded, each spanning the given number
    /// of columns and with the given width, auto when null.
    /// </summary>
    private static CssBox[] AddRow(Tree tree, params (int Span, string? Width)[] cells)
    {
        var row = new CssBox(tree.Rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };

        return cells
            .Select(c =>
            {
                var attributes = new Dictionary<string, string> { ["colspan"] = c.Span.ToString(CultureInfo.InvariantCulture) };
                var cell = new CssBox(row, new HtmlTag("td", false, attributes), BaseUrl) { Display = "table-cell" };

                if (c.Width != null)
                    cell.Width = c.Width;

                return cell;
            })
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
