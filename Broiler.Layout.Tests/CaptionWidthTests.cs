using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table's caption is as wide as the table's border box and begins at its left border edge.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §17.4: the caption boxes and the table box are laid out across the same width, the
/// table's border box. The engine began a caption inside the table's left border and made it as
/// wide as the table's border box and its border spacing again, so it ran past the table's right
/// edge: with a 5px border and <c>border-spacing: 4px</c> around a 80px column, the caption began
/// 5px in and was 106px wide, where browsers begin it at the table's edge, 98px wide.
/// </para>
/// <para>
/// Each table here is in a 500px block; words are 16px tall and 8px wide a letter.
/// </para>
/// </remarks>
public sealed class CaptionWidthTests
{
    private static readonly Uri BaseUrl = new("file:///caption-width.html");

    /// <summary>
    /// A caption above a table with a 5px border and <c>border-spacing: 4px</c> around a 10-letter
    /// word begins at the table's left edge and is 98px wide, as wide as the table. It began 5px
    /// in and was 106px wide.
    /// </summary>
    [Fact]
    public void A_Caption_Is_As_Wide_As_Its_Table()
    {
        var tree = Build(null, 5, "4px", bottom: false, "xxxxxxxxxx");
        Layout(tree);

        Assert.Equal(98, tree.Table.Size.Width, 1);
        Assert.Equal(tree.Table.Location.X, tree.Caption.Location.X, 1);
        Assert.Equal(98, tree.Caption.Size.Width, 1);
    }

    /// <summary>
    /// A caption below the table does the same: it began 5px in and was 106px wide.
    /// </summary>
    [Fact]
    public void A_Caption_Below_Its_Table_Is_As_Wide_As_It()
    {
        var tree = Build(null, 5, "4px", bottom: true, "xxxxxxxxxx");
        Layout(tree);

        Assert.Equal(tree.Table.Location.X, tree.Caption.Location.X, 1);
        Assert.Equal(98, tree.Caption.Size.Width, 1);
    }

    /// <summary>
    /// Over two columns and no border, with <c>border-spacing: 4px</c>, the caption is 100px wide:
    /// the spacing on either side of each column counts once. It was 112px wide.
    /// </summary>
    [Fact]
    public void The_Spacing_Counts_Once()
    {
        var tree = Build(null, 0, "4px", bottom: false, "xxxxxxxxxx", "y");
        Layout(tree);

        Assert.Equal(100, tree.Table.Size.Width, 1);
        Assert.Equal(100, tree.Caption.Size.Width, 1);
    }

    /// <summary>
    /// Over a table with <c>width: 300px</c>, a 5px border and <c>border-spacing: 4px</c>, the
    /// caption is 300px wide. It was 308px wide.
    /// </summary>
    [Fact]
    public void A_Caption_Is_As_Wide_As_A_Table_With_A_Width()
    {
        var tree = Build("300px", 5, "4px", bottom: false, "x");
        Layout(tree);

        Assert.Equal(300, tree.Table.Size.Width, 1);
        Assert.Equal(300, tree.Caption.Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the caption is 98px wide still, at the table's left edge.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Caption()
    {
        var tree = Build(null, 5, "4px", bottom: false, "xxxxxxxxxx");
        Layout(tree);
        Layout(tree);

        Assert.Equal(tree.Table.Location.X, tree.Caption.Location.X, 1);
        Assert.Equal(98, tree.Caption.Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: with no border and no border spacing, the caption
    /// begins at the table's left edge and is 80px wide, as wide as the table.
    /// </summary>
    [Fact]
    public void Control_A_Table_With_No_Border_Or_Spacing()
    {
        var tree = Build(null, 0, "0", bottom: false, "xxxxxxxxxx");
        Layout(tree);

        Assert.Equal(tree.Table.Location.X, tree.Caption.Location.X, 1);
        Assert.Equal(80, tree.Caption.Size.Width, 1);
    }

    /// <summary>The root, the table and its caption.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox Caption);

    /// <summary>
    /// In a 500px block in the root, a table of the given width, auto when null, with a border of
    /// the given width on every side and the given border spacing, a caption holding "cap", above
    /// or below the table, and one row of cells holding the given texts, none of them padded.
    /// </summary>
    private static Tree Build(string? width, int border, string borderSpacing, bool bottom, params string[] texts)
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
        var table = new CssBox(container, new HtmlTag("table", false, null), BaseUrl) { Display = "table", BorderSpacing = borderSpacing };

        if (width != null)
            table.Width = width;

        if (border > 0)
        {
            table.BorderLeftWidth = table.BorderTopWidth = table.BorderRightWidth = table.BorderBottomWidth = border + "px";
            table.BorderLeftStyle = table.BorderTopStyle = table.BorderRightStyle = table.BorderBottomStyle = "solid";
        }

        var caption = new CssBox(table, new HtmlTag("caption", false, null), BaseUrl) { Display = CssConstants.TableCaption };

        if (bottom)
            caption.CaptionSide = CssConstants.Bottom;

        Word(caption, "cap");

        var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
        var row = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };

        foreach (string text in texts)
            Word(new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" }, text);

        return new Tree(root, table, caption);
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
