using System;
using System.Drawing;
using System.Linq;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table with an auto width whose content is wider than the room there is fills the room, and
/// its columns share it in proportion to how much wider their content would have them.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §17.5.2.2: a table with an auto width is as wide as its columns' content, or as the
/// room there is if that is less; CSS Tables 3 shares the room left over the columns' minimums in
/// proportion to what each would take beyond its minimum, up to its maximum. The engine gave each
/// column in turn what was left divided by the columns still to come, so what a column could not
/// take went unused, and the columns before the last took more than their share: a table holding
/// text and a 600px block in a 1000px block was 804px wide, where browsers make it 1000px.
/// </para>
/// <para>
/// Each table here is in a 1000px block and has no border spacing unless stated; words are 16px
/// tall and 8px wide a letter, and a space is 4px wide. A hundred x's with spaces between them are
/// 1196px wide on one line and 8px at their narrowest; two hundred, 2396px.
/// </para>
/// </remarks>
public sealed class AutoTableSpareRoomTests
{
    private static readonly Uri BaseUrl = new("file:///auto-table-spare-room.html");

    private static readonly string Text = Spaced(100);

    /// <summary>
    /// Text beside a 600px block makes the table 1000px wide, the text's column 400px: it takes
    /// all the room the block cannot. The table was 804px wide, the text's column 204px.
    /// </summary>
    [Fact]
    public void A_Column_Takes_The_Room_Its_Neighbour_Cannot()
    {
        var tree = Build(null, "0");
        Word(Cell(tree), Text);
        Block(Cell(tree), "600px");
        Layout(tree);

        Assert.Equal(1000, tree.Table.Size.Width, 1);
        Assert.Equal(400, tree.Cells[0].Size.Width, 1);
        Assert.Equal(600, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// Text beside twice as much text makes the columns 334.9px and 665.1px wide: each takes the
    /// room in proportion to the 1188px and 2388px its content would take beyond its minimum. They
    /// were 500px each.
    /// </summary>
    [Fact]
    public void Columns_Share_The_Room_In_Proportion_To_Their_Content()
    {
        var tree = Build(null, "0");
        Word(Cell(tree), Text);
        Word(Cell(tree), Spaced(200));
        Layout(tree);

        double share = 984.0 / (1188 + 2388);
        Assert.Equal(1000, tree.Table.Size.Width, 1);
        Assert.Equal(8 + 1188 * share, tree.Cells[0].Size.Width, 1);
        Assert.Equal(8 + 2388 * share, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// The text in two columns on either side of a cell with <c>width: 200px</c> takes 400px on
    /// each side. The columns were 269.33px and 530.67px wide, the first taking a third of the room
    /// and the last the rest.
    /// </summary>
    [Fact]
    public void Columns_With_Alike_Content_Get_Alike_Shares()
    {
        var tree = Build(null, "0");
        Word(Cell(tree), Text);
        var middle = Cell(tree);
        middle.Width = "200px";
        Word(middle, "x");
        Word(Cell(tree), Text);
        Layout(tree);

        Assert.Equal(1000, tree.Table.Size.Width, 1);
        Assert.Equal(400, tree.Cells[0].Size.Width, 1);
        Assert.Equal(200, tree.Cells[1].Size.Width, 1);
        Assert.Equal(400, tree.Cells[2].Size.Width, 1);
    }

    /// <summary>
    /// With <c>border-spacing: 4px</c>, the text beside the 600px block takes 388px, the spacing
    /// the rest of the 1000px. It took 198px.
    /// </summary>
    [Fact]
    public void The_Spacing_Takes_Its_Room_First()
    {
        var tree = Build(null, "4px");
        Word(Cell(tree), Text);
        Block(Cell(tree), "600px");
        Layout(tree);

        Assert.Equal(1000, tree.Table.Size.Width, 1);
        Assert.Equal(388, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the text beside the 600px block is 400px wide still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Widths()
    {
        var tree = Build(null, "0");
        Word(Cell(tree), Text);
        Block(Cell(tree), "600px");
        Layout(tree);
        Layout(tree);

        Assert.Equal(400, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: content that fits leaves each column as wide as it,
    /// ten spaced x's and a 10-letter word 116px and 80px; and a table with <c>width: 1000px</c>
    /// holding the text and a 60-letter word gives the text the 520px the word leaves.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Control_Content_That_Fits_And_Tables_With_A_Width_Are_As_Before(bool withWidth)
    {
        var tree = Build(withWidth ? "1000px" : null, "0");
        Word(Cell(tree), withWidth ? Text : Spaced(10));
        Word(Cell(tree), new string('x', withWidth ? 60 : 10));
        Layout(tree);

        Assert.Equal(withWidth ? 1000 : 196, tree.Table.Size.Width, 1);
        Assert.Equal(withWidth ? 520 : 116, tree.Cells[0].Size.Width, 1);
    }

    private static string Spaced(int count) => string.Join(" ", Enumerable.Repeat("x", count));

    /// <summary>The root, the table, and its one row.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox Row)
    {
        public CssBox[] Cells => Row.Boxes.ToArray();
    }

    /// <summary>
    /// In a 1000px block in the root, a table with the given width, auto when null, and border
    /// spacing, and one row with no cells yet.
    /// </summary>
    private static Tree Build(string? width, string spacing)
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
        var row = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };

        return new Tree(root, table, row);
    }

    /// <summary>Adds an empty cell, not padded, to the table's row.</summary>
    private static CssBox Cell(Tree tree) =>
        new(tree.Row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };

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
