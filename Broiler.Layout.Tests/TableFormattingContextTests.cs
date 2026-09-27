using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table establishes a block formatting context of its own: its caption's top margin stays
/// inside it, and it is placed beside the floats before it, in the space they leave it, or below
/// them where it does not fit.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §17.4: the table wrapper box establishes a block formatting context, and §9.5 keeps the
/// border box of a table clear of the floats beside it. <c>EstablishesBfc</c> did not count tables.
/// So a caption's own top margin collapsed through its table's top and moved the table down, and
/// the table then left room for the margin again above its cells: a caption with
/// <c>margin-top: 10px</c> put the table 10px low. And a table was placed as if there were no
/// floats, over them.
/// </para>
/// <para>
/// Each table here has one row of one cell holding a word, in a 500px block, after a 10px block or
/// a 200×50px float. Words are 16px tall and 8px wide a letter, and a space is 4px wide.
/// </para>
/// </remarks>
public sealed class TableFormattingContextTests
{
    private static readonly Uri BaseUrl = new("file:///table-formatting-context.html");

    /// <summary>
    /// A 45-letter word, 360px wide: wider than the 300px beside the float.
    /// </summary>
    private static readonly string LongWord = new('X', 45);

    /// <summary>
    /// After a 10px block, a table whose caption has <c>margin-top: 10px</c> begins right below
    /// the block, with the caption 10px down it and the cell right below the caption. The table
    /// began 10px lower.
    /// </summary>
    [Fact]
    public void A_Captions_Top_Margin_Stays_Inside_Its_Table()
    {
        var tree = Build(before: Before.Block, width: "300px");
        var caption = Caption(tree.Table, marginTop: "10px");
        Layout(tree);

        Assert.Equal(tree.Before.ActualBottom, tree.Table.Location.Y, 1);
        Assert.Equal(tree.Table.Location.Y + 10, caption.Location.Y, 1);
        Assert.Equal(caption.ActualBottom, tree.Cell.Location.Y, 1);
    }

    /// <summary>
    /// After a 200×50px left float, a table with <c>width: 50%</c>, an auto width, or
    /// <c>width: 280px</c> and <c>margin-left: 30px</c>, which lies under the float, begins beside
    /// the float, 200px in, at the top. It began at the container's left, or 30px in, over the
    /// float.
    /// </summary>
    [Theory]
    [InlineData("50%", null)]
    [InlineData(null, null)]
    [InlineData("280px", "30px")]
    public void A_Table_That_Fits_Beside_A_Float_Is_Placed_There(string? width, string? marginLeft)
    {
        var tree = Build(before: Before.Float, width: width);

        if (marginLeft != null)
            tree.Table.MarginLeft = marginLeft;

        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft + 200, tree.Table.Location.X, 1);
        Assert.Equal(tree.Container.ClientTop, tree.Table.Location.Y, 1);
    }

    /// <summary>
    /// After the float, a table with <c>width: 100%</c> or <c>width: 350px</c>, wider than the
    /// 300px beside it, begins below the float at the container's left. It began at the top, over
    /// the float.
    /// </summary>
    [Theory]
    [InlineData("100%")]
    [InlineData("350px")]
    public void A_Table_Too_Wide_For_The_Space_Beside_A_Float_Goes_Below(string width)
    {
        var tree = Build(before: Before.Float, width: width);
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft, tree.Table.Location.X, 1);
        Assert.Equal(tree.Container.ClientTop + 50, tree.Table.Location.Y, 1);
    }

    /// <summary>
    /// After the float, a table with an auto width whose cell holds forty one-letter words, 476px
    /// on one line, begins beside the float, 200px in, and is only as wide as the 300px there. It
    /// began at the container's left, over the float, 476px wide.
    /// </summary>
    [Fact]
    public void A_Table_With_An_Auto_Width_Takes_Only_The_Space_Beside_A_Float()
    {
        var tree = Build(before: Before.Float, width: null, cellText: string.Join(' ', Enumerable.Repeat("X", 40)));
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft + 200, tree.Table.Location.X, 1);
        Assert.Equal(tree.Container.ClientTop, tree.Table.Location.Y, 1);
        Assert.Equal(300, tree.Table.Size.Width, 1);
    }

    /// <summary>
    /// After the float, a table with <c>width: 100px</c> or an auto width whose cell holds a word
    /// 360px wide, wider than the 300px beside the float, begins below the float at the container's
    /// left, 360px wide. It began at the top, over the float.
    /// </summary>
    [Theory]
    [InlineData("100px")]
    [InlineData(null)]
    public void A_Table_Whose_Content_Is_Too_Wide_For_The_Space_Beside_A_Float_Goes_Below(string? width)
    {
        var tree = Build(before: Before.Float, width: width, cellText: LongWord);
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft, tree.Table.Location.X, 1);
        Assert.Equal(tree.Container.ClientTop + 50, tree.Table.Location.Y, 1);
        Assert.Equal(360, tree.Table.Size.Width, 1);
    }

    /// <summary>
    /// An inline table establishes a formatting context too. After the 10px block, one whose
    /// caption has <c>margin-top: 10px</c> begins right below the block, with the caption 10px down
    /// it; it began 10px lower. After the float, one with <c>width: 280px</c> begins beside it,
    /// 200px in; it began at the container's left, over the float.
    /// </summary>
    [Theory]
    [InlineData("caption")]
    [InlineData("float")]
    public void An_Inline_Table_Establishes_A_Formatting_Context_Too(string kind)
    {
        var tree = Build(before: kind == "caption" ? Before.Block : Before.Float, width: kind == "caption" ? null : "280px");
        tree.Table.Display = CssConstants.InlineTable;

        if (kind == "caption")
        {
            var caption = Caption(tree.Table, marginTop: "10px");
            Layout(tree);

            Assert.Equal(tree.Before.ActualBottom, tree.Table.Location.Y, 1);
            Assert.Equal(tree.Table.Location.Y + 10, caption.Location.Y, 1);
        }
        else
        {
            Layout(tree);

            Assert.Equal(tree.Container.ClientLeft + 200, tree.Table.Location.X, 1);
            Assert.Equal(tree.Container.ClientTop, tree.Table.Location.Y, 1);
        }
    }

    /// <summary>
    /// Laid out a second time, the <c>width: 100%</c> table, and the <c>width: 100px</c> one whose
    /// cell holds the 360px word, each give the same place.
    /// </summary>
    [Theory]
    [InlineData("100%", "X")]
    [InlineData("100px", null)]
    public void A_Second_Layout_Gives_The_Same_Places(string width, string? cellText)
    {
        var tree = Build(before: Before.Float, width: width, cellText: cellText ?? LongWord);
        Layout(tree);

        float x = tree.Table.Location.X, y = tree.Table.Location.Y;
        Layout(tree);

        Assert.Equal(tree.Container.ClientTop + 50, y, 1);
        Assert.Equal(x, tree.Table.Location.X, 1);
        Assert.Equal(y, tree.Table.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: after the 10px block, a table with a caption with no
    /// margin begins right below the block with the caption at its top; and a table after a
    /// 200×10px float and a 20px block, which end above it, begins at the container's left.
    /// </summary>
    [Theory]
    [InlineData("caption")]
    [InlineData("float-above")]
    public void Control_A_Table_With_Nothing_To_Keep_Out(string kind)
    {
        if (kind == "caption")
        {
            var tree = Build(before: Before.Block, width: "300px");
            var caption = Caption(tree.Table, marginTop: null);
            Layout(tree);

            Assert.Equal(tree.Before.ActualBottom, tree.Table.Location.Y, 1);
            Assert.Equal(tree.Table.Location.Y, caption.Location.Y, 1);
        }
        else
        {
            var tree = Build(before: Before.ShortFloatAndBlock, width: "300px");
            Layout(tree);

            Assert.Equal(tree.Container.ClientLeft, tree.Table.Location.X, 1);
            Assert.Equal(tree.Container.ClientTop + 20, tree.Table.Location.Y, 1);
        }
    }

    private enum Before
    {
        Block,
        Float,
        ShortFloatAndBlock,
    }

    /// <summary>
    /// The root, the 500px container, what comes before the table, the table and its cell.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Container, CssBox Before, CssBox Table, CssBox Cell);

    /// <summary>
    /// In a block in the root, a 500px container holding what <paramref name="before"/> names and
    /// then a table of the given width, auto when null, with one row of one cell holding
    /// <paramref name="cellText"/>.
    /// </summary>
    private static Tree Build(Before before, string? width, string cellText = "X")
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

        CssBox first = before switch
        {
            Before.Block => new CssBox(container, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "10px" },
            Before.Float => new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = "block",
                Float = CssConstants.Left,
                Width = "200px",
                Height = "50px",
            },
            _ => new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = "block",
                Float = CssConstants.Left,
                Width = "200px",
                Height = "10px",
            },
        };

        if (before == Before.ShortFloatAndBlock)
            first = new CssBox(container, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "20px" };

        var table = new CssBox(container, new HtmlTag("table", false, null), BaseUrl) { Display = "table" };

        if (width != null)
            table.Width = width;

        var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
        var row = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };
        var cell = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };
        Text(cell, cellText);

        return new Tree(root, container, first, table, cell);
    }

    /// <summary>
    /// Gives <paramref name="table"/> a caption, before its rows, with the given top margin and a
    /// word.
    /// </summary>
    private static CssBox Caption(CssBox table, string? marginTop)
    {
        var caption = new CssBox(table, new HtmlTag("caption", false, null), BaseUrl) { Display = CssConstants.TableCaption };

        if (marginTop != null)
            caption.MarginTop = marginTop;

        table.Boxes.Remove(caption);
        table.Boxes.Insert(0, caption);
        Text(caption, "X");

        return caption;
    }

    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        box.ParseToWords();
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
