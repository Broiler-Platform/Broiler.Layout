using System;
using System.Drawing;
using System.Linq;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// In a table with a width of its own, a column widened to its minimum takes the room from the
/// columns that can spare it, each in proportion to what it can spare, and the table keeps its
/// width as long as they can.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §17.5.2.2: a table with a width is that wide, or as wide as its columns' minimums if
/// they need more. The engine shares the width out, then widens each column narrower than its
/// minimum and takes what that costs from the next column alone. With no room to spare there,
/// the table grew: a table with <c>width: 400px</c> holding a cell of short words and a cell of
/// a word 240px long was 440px wide, the short words' column 200px, where browsers keep the table
/// 400px wide and give that column 160px.
/// </para>
/// <para>
/// Each table here is in a 500px block and has no border spacing; words are 16px tall and 8px wide
/// a letter, and a space is 4px wide. A cell of twenty x's with spaces between them is 236px wide
/// on one line and 8px at its narrowest.
/// </para>
/// </remarks>
public sealed class ColumnMinimumRoomTests
{
    private static readonly Uri BaseUrl = new("file:///column-minimum-room.html");

    private static readonly string ShortWords = string.Join(" ", Enumerable.Repeat("x", 20));

    /// <summary>
    /// A table with <c>width: 400px</c> holding the short words and a 30-letter word is 400px wide,
    /// the word's column 240px and the short words' 160px. It was 440px wide, the short words'
    /// column 200px.
    /// </summary>
    [Fact]
    public void A_Column_Widened_To_Its_Word_Takes_The_Room_From_The_Column_Beside_It()
    {
        var tree = Build("400px", ShortWords, new string('x', 30));
        Layout(tree);

        Assert.Equal(400, tree.Table.Size.Width, 1);
        Assert.Equal(160, tree.Cells[0].Size.Width, 1);
        Assert.Equal(240, tree.Cells[1].Size.Width, 1);
        Assert.Equal(tree.Table.Location.X + 160, tree.Cells[1].Location.X, 1);
    }

    /// <summary>
    /// With two cells of short words before a 40-letter word, in a table with <c>width: 600px</c>,
    /// the table is 600px wide, the word's column 320px and each short words' column 140px: each
    /// gives up as much, as each can spare as much. The table was 720px wide, the short words'
    /// columns 200px.
    /// </summary>
    [Fact]
    public void The_Columns_Beside_It_Each_Give_In_Proportion_To_What_They_Can_Spare()
    {
        var tree = Build("600px", ShortWords, ShortWords, new string('x', 40));
        Layout(tree);

        Assert.Equal(600, tree.Table.Size.Width, 1);
        Assert.Equal(140, tree.Cells[0].Size.Width, 1);
        Assert.Equal(140, tree.Cells[1].Size.Width, 1);
        Assert.Equal(320, tree.Cells[2].Size.Width, 1);
    }

    /// <summary>
    /// A column given a width of its own gives room only once the others have none left: in a
    /// table with <c>width: 300px</c>, the short words in a cell with <c>width: 150px</c>, the
    /// short words again and a 30-letter word make columns 52px, 8px and 240px wide, the second
    /// down to its minimum first. The table was 465px wide, the columns 150px, 75px and 240px.
    /// </summary>
    [Fact]
    public void The_Columns_Without_A_Width_Of_Their_Own_Give_First()
    {
        var tree = Build("300px", ShortWords, ShortWords, new string('x', 30));
        tree.Cells[0].Width = "150px";
        Layout(tree);

        Assert.Equal(300, tree.Table.Size.Width, 1);
        Assert.Equal(52, tree.Cells[0].Size.Width, 1);
        Assert.Equal(8, tree.Cells[1].Size.Width, 1);
        Assert.Equal(240, tree.Cells[2].Size.Width, 1);
    }

    /// <summary>
    /// A table with <c>width: 100px</c> holding the short words and a 40-letter word is 328px
    /// wide, as wide as its columns' minimums, the short words' column 8px: it gives up all it can
    /// spare, and the table grows by the rest. The table was 370px wide, the short words' column
    /// 50px.
    /// </summary>
    [Fact]
    public void A_Table_Too_Narrow_For_Its_Columns_Minimums_Is_As_Wide_As_Them()
    {
        var tree = Build("100px", ShortWords, new string('x', 40));
        Layout(tree);

        Assert.Equal(328, tree.Table.Size.Width, 1);
        Assert.Equal(8, tree.Cells[0].Size.Width, 1);
        Assert.Equal(320, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the table with <c>width: 400px</c> is 400px wide still, the short
    /// words' column 160px.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Widths()
    {
        var tree = Build("400px", ShortWords, new string('x', 30));
        Layout(tree);
        Layout(tree);

        Assert.Equal(400, tree.Table.Size.Width, 1);
        Assert.Equal(160, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: with the 40-letter word first, the table with
    /// <c>width: 100px</c> is 328px wide, the short words' column 8px, as the column after the
    /// word gave up its room already.
    /// </summary>
    [Fact]
    public void Control_A_Column_Before_The_Room_Takes_It_From_The_Next()
    {
        var tree = Build("100px", new string('x', 40), ShortWords);
        Layout(tree);

        Assert.Equal(328, tree.Table.Size.Width, 1);
        Assert.Equal(320, tree.Cells[0].Size.Width, 1);
        Assert.Equal(8, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a table with <c>width: 400px</c> holding two x's is
    /// 400px wide, each column 200px; one with an auto width holding ten x's with spaces between
    /// them and a 30-letter word is 356px wide, the columns as wide as their content, 116px and
    /// 240px.
    /// </summary>
    [Theory]
    [InlineData("400px", "x", "x", 400, 200)]
    [InlineData(null, "x x x x x x x x x x", "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 356, 116)]
    public void Control_Columns_That_Fit_Keep_Their_Widths(string? width, string first, string second, double tableWidth, double firstWidth)
    {
        var tree = Build(width, first, second);
        Layout(tree);

        Assert.Equal(tableWidth, tree.Table.Size.Width, 1);
        Assert.Equal(firstWidth, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>The root, the table, and the cells of its one row.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox[] Cells);

    /// <summary>
    /// In a 500px block in the root, a table with the given width, auto when null, and one row of
    /// cells holding the given texts, none of them padded.
    /// </summary>
    private static Tree Build(string? width, params string[] texts)
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
        var row = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };

        var cells = texts
            .Select(text =>
            {
                var cell = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };
                var words = new CssBox(cell, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
                words.ParseToWords();
                return cell;
            })
            .ToArray();

        return new Tree(root, table, cells);
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
