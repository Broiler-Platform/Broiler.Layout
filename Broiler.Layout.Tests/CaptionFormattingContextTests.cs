using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table caption establishes a block formatting context of its own, so its first child's top
/// margin stays inside it and it contains its floats.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §9.4.1 names table captions, with table cells, among the block containers that
/// establish new block formatting contexts. <c>EstablishesBfc</c> left captions out, so a
/// caption's first child's top margin collapsed through the caption's top and moved the caption
/// down, and the table then put the caption back at its own top with everything in it: in
/// <c>&lt;caption&gt;&lt;p style="margin-top: 16px"&gt;</c> the 16px was lost, where browsers keep
/// it inside the caption. A float in a caption hung out of it.
/// </para>
/// <para>
/// Each table here is 300px wide with a caption and a row of one cell holding a word, in a 320px
/// block after a 10px block. Paragraphs have no bottom margin, and words are 8×16px.
/// </para>
/// </remarks>
public sealed class CaptionFormattingContextTests
{
    private static readonly Uri BaseUrl = new("file:///caption-formatting-context.html");

    /// <summary>
    /// A caption whose first child is a paragraph with <c>margin-top: 16px</c> is 32px tall, with
    /// the paragraph 16px down it and the cell right below it. It was 16px tall, the paragraph at
    /// its top.
    /// </summary>
    [Fact]
    public void A_First_Childs_Margin_Stays_Inside_The_Caption()
    {
        var tree = Build();
        var paragraph = Paragraph(tree.Caption, "16px");
        Layout(tree);

        Assert.Equal(32, tree.Caption.Size.Height, 1);
        Assert.Equal(tree.Caption.Location.Y + 16, paragraph.Location.Y, 1);
        Assert.Equal(tree.Caption.ActualBottom, tree.Cell.Location.Y, 1);
    }

    /// <summary>
    /// A caption holding a 30×40px float and then a paragraph is 40px tall, the float's height,
    /// and the cell is right below it. It was 16px tall, the float hanging out of it over the cell.
    /// </summary>
    [Fact]
    public void The_Caption_Contains_Its_Floats()
    {
        var tree = Build();
        var floated = new CssBox(tree.Caption, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Float = CssConstants.Left,
            Width = "30px",
            Height = "40px",
        };
        Paragraph(tree.Caption, "0");
        Layout(tree);

        Assert.Equal(40, tree.Caption.Size.Height, 1);
        Assert.Equal(tree.Caption.Location.Y, floated.Location.Y, 1);
        Assert.Equal(tree.Caption.ActualBottom, tree.Cell.Location.Y, 1);
    }

    /// <summary>
    /// Laid out a second time, the caption with the paragraph's margin inside it gives the same
    /// places.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Places()
    {
        var tree = Build();
        var paragraph = Paragraph(tree.Caption, "16px");
        Layout(tree);

        float paragraphTop = paragraph.Location.Y, cellTop = tree.Cell.Location.Y;
        Layout(tree);

        Assert.Equal(tree.Caption.Location.Y + 16, paragraphTop, 1);
        Assert.Equal(paragraphTop, paragraph.Location.Y, 1);
        Assert.Equal(cellTop, tree.Cell.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after:
    /// <list type="bullet">
    /// <item>a caption holding a paragraph with no margin is 16px tall, with the cell below it;</item>
    /// <item>one with 1px of top padding keeps the paragraph's 16px margin inside, 33px tall;</item>
    /// <item>one holding an unpadded block around the paragraph keeps it inside too, 32px tall:
    /// the block took the margin, one level down.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData("none", 16, 0)]
    [InlineData("padding", 33, 17)]
    [InlineData("wrapper", 32, 16)]
    public void Control_A_Caption_That_Kept_Its_Content_Inside(string kind, float height, float paragraphTop)
    {
        var tree = Build();
        var holder = tree.Caption;

        if (kind == "padding")
            tree.Caption.PaddingTop = "1px";
        else if (kind == "wrapper")
            holder = new CssBox(tree.Caption, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };

        var paragraph = Paragraph(holder, kind == "none" ? "0" : "16px");
        Layout(tree);

        Assert.Equal(height, tree.Caption.Size.Height, 1);
        Assert.Equal(tree.Caption.Location.Y + paragraphTop, paragraph.Location.Y, 1);
        Assert.Equal(tree.Caption.ActualBottom, tree.Cell.Location.Y, 1);
    }

    /// <summary>
    /// The root, the table and its caption and cell.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox Caption, CssBox Cell);

    /// <summary>
    /// In a block in the root, a 320px block holding a 10px block and then a 300px table with a
    /// caption and one row of one cell holding a word.
    /// </summary>
    private static Tree Build()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var page = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        _ = new CssBox(page, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "10px" };

        var table = new CssBox(page, new HtmlTag("table", false, null), BaseUrl) { Display = "table", Width = "300px" };
        var caption = new CssBox(table, new HtmlTag("caption", false, null), BaseUrl) { Display = CssConstants.TableCaption };
        var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
        var row = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };
        var cell = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };
        Word(cell);

        return new Tree(root, table, caption, cell);
    }

    /// <summary>
    /// A paragraph with the given top margin and no bottom margin, holding a word.
    /// </summary>
    private static CssBox Paragraph(CssBox parent, string marginTop)
    {
        var paragraph = new CssBox(parent, new HtmlTag("p", false, null), BaseUrl)
        {
            Display = "block",
            MarginTop = marginTop,
            MarginBottom = "0",
        };

        Word(paragraph);
        return paragraph;
    }

    private static void Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
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
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8, 16);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8; }
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
