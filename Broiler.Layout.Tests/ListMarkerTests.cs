using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A list item's marker: the counter style's symbol, standing on the item's first line box, its
/// baseline on that line's, wherever the line is — in a heading the item starts with, too — as
/// browsers draw it.
/// </summary>
/// <remarks>
/// The marker stood at the top of the item, so beside the larger text of a heading the item
/// started with the bullet sat level with the heading's cap height: reCAPTCHA's demo index lists
/// its sections as <c>&lt;li&gt;&lt;h2&gt;</c>. Circle was drawn as a letter "o" and square as a spade.
/// Words are 8px wide and 16px tall.
/// </remarks>
public sealed class ListMarkerTests
{
    private static readonly Uri BaseUrl = new("file:///list.html");

    /// <summary>
    /// An item that starts with a heading whose text is 8px down puts its marker 8px down with it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Marker_Stands_On_The_First_Line_Of_A_Heading_The_Item_Starts_With()
    {
        var (root, item) = Item("disc");
        var heading = new CssBox(item, new HtmlTag("h2", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            PaddingTop = "8px",
        };
        Text(heading, "Heading");

        root.PerformLayout(root.LayoutEnvironment);

        var marker = item.ListItemMarkerBox!;
        Assert.Equal(heading.Boxes[0].Words[0].Top, marker.Words[0].Top, 1);
        Assert.Equal(item.Location.Y + 8, marker.Words[0].Top, 1);
    }

    /// <summary>An item with nothing on a line keeps its marker at its top.</summary>
    [Fact(Timeout = 600000)]
    public void An_Item_Without_A_Line_Keeps_Its_Marker_At_Its_Top()
    {
        var (root, item) = Item("disc");
        new CssBox(item, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block, Height = "30px" };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(item.Location.Y, item.ListItemMarkerBox!.Words[0].Top, 1);
    }

    [Theory(Timeout = 600000)]
    [InlineData("disc", "•")]
    [InlineData("circle", "◦")]
    [InlineData("square", "▪")]
    public void The_Marker_Is_The_Counter_Styles_Symbol(string style, string symbol)
    {
        var (root, item) = Item(style);
        Text(item, "Item");

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(symbol, item.ListItemMarkerBox!.Words[0].Text);
    }

    private static (CssBox Root, CssBox Item) Item(string listStyleType)
    {
        var root = new CssBox(null, new HtmlTag("html", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };
        var list = new CssBox(root, new HtmlTag("ul", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            PaddingLeft = "40px",
        };
        var item = new CssBox(list, new HtmlTag("li", false, null), BaseUrl)
        {
            Display = CssConstants.ListItem,
            ListStyleType = listStyleType,
        };
        return (root, item);
    }

    private static void Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        box.ParseToWords();
    }

    // Minimal ILayoutEnvironment: every word 8px wide and 16px tall, a space 4px, a 1024×768 viewport.
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
