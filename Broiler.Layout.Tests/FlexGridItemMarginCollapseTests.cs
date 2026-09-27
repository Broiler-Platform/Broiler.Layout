using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A flex or grid item establishes an independent formatting context for its contents (CSS Flexbox
/// §4, CSS Grid §6.2), whatever its own <c>display</c>: its children's margins do not collapse
/// through its edges, and it contains its floats.
/// </summary>
/// <remarks>
/// <para>
/// <c>CssBoxHelper.EstablishesBfc</c>, which margin collapsing and float containment ask, counted
/// flex and grid containers and not their items, so a <c>display: block</c> item was treated as an
/// ordinary block in its parent's flow. A paragraph's 16px top margin collapsed through the top of
/// the unpadded item holding it: in a column, the item sat 16px low with the margin outside it,
/// and in a row or a grid, which places an item at the top of its line or area whatever its
/// margins, the margin was lost and the paragraph sat at the item's top. A row item holding only a
/// float did not grow around it.
/// </para>
/// <para>
/// Each item here is an unpadded block holding a paragraph with 16px margins above and below,
/// around one word, 8×16px, in a 320px container, with a 5px block after it.
/// </para>
/// </remarks>
public sealed class FlexGridItemMarginCollapseTests
{
    private static readonly Uri BaseUrl = new("file:///flex-grid-item-margin-collapse.html");

    /// <summary>
    /// The item starts at the top of its container, and the paragraph 16px down it, its margin
    /// inside the item: in a column its container stretches and one aligned <c>flex-start</c>, in an
    /// inline-flex column, in a row and in a grid. In the columns the item started 16px low, at the
    /// paragraph; in the row and the grid the paragraph started at the item's top.
    /// </summary>
    [Theory]
    [InlineData("column")]
    [InlineData("column flex-start")]
    [InlineData("inline-flex column")]
    [InlineData("row")]
    [InlineData("grid")]
    public void A_Childs_Top_Margin_Stays_Inside_A_Flex_Or_Grid_Item(string layout)
    {
        var (container, item, paragraph) = Lay(layout);

        Assert.Equal(container.ClientTop, item.Location.Y, 1);
        Assert.Equal(16, paragraph.Location.Y - item.Location.Y, 1);
    }

    /// <summary>
    /// A row item holding only a 30px float is 30px tall, around it. It took only the 5px of the
    /// line's other item.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Row_Item_Contains_Its_Float()
    {
        var root = Root();
        var container = Box(root, "flex");
        container.Width = "320px";
        var item = Box(container, "block", "section");
        item.Width = "100px";
        var floated = Box(item, "block", "i");
        floated.Float = "left";
        floated.Width = "20px";
        floated.Height = "30px";
        var other = Box(container, "block", "b");
        other.Height = "5px";
        other.Width = "10px";

        Layout(root);

        Assert.Equal(30, item.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an item holding only a word, in an 86px row that
    /// centres its items, as a site header does, is as tall as its line, 16px, and centred on the
    /// row, also when the page is laid out again, as a host lays it out when an image arrives. The
    /// item's text box has no position that line layout keeps, and the item's height, now that it
    /// is the root of a formatting context and measured again from its children, must not be taken
    /// from it: it is moved down with the item each time the row centres it, 35px a layout.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_A_Centred_Row_Item_Holding_A_Word_Is_As_Tall_As_Its_Line()
    {
        var root = Root();
        var container = Box(root, "flex");
        container.Width = "320px";
        container.Height = "86px";
        container.AlignItems = "center";
        container.JustifyContent = "space-between";

        CssBox Item()
        {
            var item = Box(container, "block", "span");
            var text = new CssBox(item, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
            text.ParseToWords();
            return item;
        }

        var first = Item();
        var second = Item();

        Layout(root);
        root.PerformLayout(root.LayoutEnvironment);

        foreach (var item in new[] { first, second })
        {
            Assert.Equal(16, item.Size.Height, 1);
            Assert.Equal(35, item.Location.Y - container.Location.Y, 1);
        }
    }

    /// <summary>
    /// Controls, which pass before and after: an unpadded block in a block lets the paragraph's
    /// margin collapse through its top (CSS 2.1 §8.3.1), and through its container's, which it is
    /// the first child of, so it starts 16px down the page at the paragraph; and one with
    /// <c>overflow: hidden</c>, which establishes a block formatting context, keeps it inside, at
    /// the top of the page with the paragraph 16px down it.
    /// </summary>
    [Theory]
    [InlineData("block", 16, 0)]
    [InlineData("block overflow", 0, 16)]
    public void Control_A_Block_Collapses_Unless_It_Establishes_A_Formatting_Context(
        string layout, float itemTop, float paragraphInItem)
    {
        var (container, item, paragraph) = Lay(layout);

        Assert.Equal(itemTop, item.Location.Y - container.ParentBox!.ClientTop, 1);
        Assert.Equal(paragraphInItem, paragraph.Location.Y - item.Location.Y, 1);
    }

    /// <summary>
    /// A 320px container of the kind <paramref name="layout"/> names, holding an unpadded block
    /// with a paragraph in it and a 5px block after it, laid out.
    /// </summary>
    private static (CssBox Container, CssBox Item, CssBox Paragraph) Lay(string layout)
    {
        var root = Root();
        var page = Box(root, "block");
        page.Width = "320px";

        var container = Box(page, layout switch
        {
            "grid" => "grid",
            "block" or "block overflow" => "block",
            "inline-flex column" => "inline-flex",
            _ => "flex",
        });

        if (layout.Contains("column"))
            container.FlexDirection = "column";
        if (layout == "column flex-start")
            container.AlignItems = "flex-start";

        var item = Box(container, "block", "section");
        if (layout == "block overflow")
            item.Overflow = "hidden";

        var paragraph = Box(item, "block", "p");
        paragraph.MarginTop = paragraph.MarginBottom = "16px";
        var text = new CssBox(paragraph, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();

        var next = Box(container, "block", "b");
        next.Height = "5px";
        next.Width = "10px";

        Layout(root);
        return (container, item, paragraph);
    }

    private static void Layout(CssBox root)
    {
        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
    }

    private static CssBox Root() =>
        new(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

    private static CssBox Box(CssBox parent, string display, string tag = "div") =>
        new(parent, new HtmlTag(tag, false, null), BaseUrl) { Display = display };

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
