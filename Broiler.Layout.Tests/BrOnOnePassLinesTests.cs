using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A <c>&lt;br&gt;</c> on the lines a box lays out in one pass, an inline-block's or a flex or grid
/// item's, ends the line of the inline element before it, and the empty line it does make is as
/// tall as its line height, as on a block's lines.
/// </summary>
/// <remarks>
/// <para>
/// The host's DOM parser gives a <c>&lt;br&gt;</c> it takes to follow a block a height of
/// <c>.95em</c>, and it takes a line holding an inline element to be one, as the text is in the
/// element's children. A block drops the height where the <c>&lt;br&gt;</c> ends a line of content,
/// and makes it the line height where it makes an empty line (see
/// <c>CssBox.ResolveBrLineHeight</c>), when it lays the <c>&lt;br&gt;</c> out. On the lines a box
/// lays out in one pass the <c>&lt;br&gt;</c> is flowed, not laid out, and kept its <c>.95em</c>:
/// <c>&lt;span&gt;Hello&lt;/span&gt;&lt;br&gt;World</c> in an inline-block was three lines, the
/// middle one empty and 15.2px tall, where browsers make it two.
/// </para>
/// <para>
/// Each box here is in a 320px block in the root, and holds its content as the host hands it over:
/// inline content around <c>&lt;br&gt;</c> blocks, those the parser takes to follow a block with a
/// height of <c>.95em</c>, and all with a 40px line height. Words are 8px wide a letter and 16px
/// tall, one line 16px.
/// </para>
/// </remarks>
public sealed class BrOnOnePassLinesTests
{
    private static readonly Uri BaseUrl = new("file:///br-on-one-pass-lines.html");

    /// <summary>
    /// After a span holding "Hello", the <c>&lt;br&gt;</c> ends Hello's line: "World" is 16px down,
    /// and the box two lines tall, 32px, in an inline-block, a flex item and a grid item. "World"
    /// was 31.2px down, below an empty line as tall as the <c>.95em</c>, and the box 47.2px tall.
    /// </summary>
    [Theory]
    [InlineData("inline-block")]
    [InlineData("flex")]
    [InlineData("grid")]
    public void A_Br_After_An_Inline_Element_Ends_Its_Line(string kind)
    {
        var tree = Lay(kind, "span", ".95em");

        Assert.Equal(16, tree.Top(tree.After), 1);
        Assert.Equal(32, tree.Box.Size.Height, 1);
    }

    /// <summary>
    /// After an inline-block holding "Hi", the <c>&lt;br&gt;</c> ends its line too: "World" is
    /// 16px down. It was 31.2px down.
    /// </summary>
    [Fact]
    public void A_Br_After_An_Inline_Block_Ends_Its_Line()
    {
        var tree = Lay("inline-block", "inline-block", ".95em");

        Assert.Equal(16, tree.Top(tree.After), 1);
        Assert.Equal(32, tree.Box.Size.Height, 1);
    }

    /// <summary>
    /// A second <c>&lt;br&gt;</c> after the span makes an empty line as tall as its line height,
    /// 40px: "World" is 56px down, the box 72px tall. The two made two empty lines, each 15.2px
    /// tall, and the box was 62.4px tall.
    /// </summary>
    [Fact]
    public void A_Second_Br_Makes_An_Empty_Line_As_Tall_As_Its_Line_Height()
    {
        var tree = Lay("inline-block", "span", ".95em", brs: 2);

        Assert.Equal(56, tree.Top(tree.After), 1);
        Assert.Equal(72, tree.Box.Size.Height, 1);
    }

    /// <summary>
    /// A <c>&lt;br&gt;</c> at the start of the box makes an empty line 40px tall, in an
    /// inline-block and a flex item: "World" is 40px down. It was 15.2px down.
    /// </summary>
    [Theory]
    [InlineData("inline-block")]
    [InlineData("flex")]
    public void A_Br_At_The_Start_Makes_An_Empty_Line_As_Tall_As_Its_Line_Height(string kind)
    {
        var tree = Lay(kind, null, ".95em");

        Assert.Equal(40, tree.Top(tree.After), 1);
        Assert.Equal(56, tree.Box.Size.Height, 1);
    }

    /// <summary>
    /// Laid out a second time, the <c>&lt;br&gt;</c> after the span still ends its line: "World"
    /// is 16px down.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Lines()
    {
        var tree = Lay("inline-block", "span", ".95em");
        tree.Root.PerformLayout(tree.Root.LayoutEnvironment);

        Assert.Equal(16, tree.Top(tree.After), 1);
        Assert.Equal(32, tree.Box.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a <c>&lt;br&gt;</c> after text, which the parser
    /// gives no height, ends the text's line, in an inline-block and a flex item.
    /// </summary>
    [Theory]
    [InlineData("inline-block")]
    [InlineData("flex")]
    public void Control_A_Br_After_Text(string kind)
    {
        var tree = Lay(kind, "text", CssConstants.Auto);

        Assert.Equal(16, tree.Top(tree.After), 1);
        Assert.Equal(32, tree.Box.Size.Height, 1);
    }

    /// <summary>The root, the box holding the lines, and the box holding "World".</summary>
    private sealed record Tree(CssBox Root, CssBox Box, CssBox After)
    {
        /// <summary>How far the box's first word stands below the top of the box holding the lines.</summary>
        public double Top(CssBox box) => box.Words[0].Top - Box.Location.Y;
    }

    /// <summary>
    /// In a 320px block in the root, an inline-block, or the item of a flex or grid container,
    /// holding what <paramref name="before"/> names (a span holding "Hello", an inline-block holding
    /// "Hi", the text "Hello", or nothing), then <paramref name="brs"/> <c>&lt;br&gt;</c> blocks, the
    /// first with a height of <paramref name="brHeight"/> and any other with <c>.95em</c>, as after
    /// a <c>&lt;br&gt;</c>, and then "World".
    /// </summary>
    private static Tree Lay(string kind, string? before, string brHeight, int brs = 1)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };

        CssBox box = kind == "inline-block"
            ? new CssBox(block, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.InlineBlock }
            : new CssBox(new CssBox(block, new HtmlTag("div", false, null), BaseUrl) { Display = kind }, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };

        switch (before)
        {
            case "span":
                Text(new CssBox(box, new HtmlTag("span", false, null), BaseUrl) { Display = "inline" }, "Hello");
                break;
            case "inline-block":
                Text(new CssBox(box, new HtmlTag("span", false, null), BaseUrl) { Display = CssConstants.InlineBlock }, "Hi");
                break;
            case "text":
                Text(box, "Hello");
                break;
        }

        for (int i = 0; i < brs; i++)
        {
            _ = new CssBox(box, new HtmlTag("br", false, null), BaseUrl)
            {
                Display = "block",
                Height = i == 0 ? brHeight : ".95em",
                LineHeight = "40px",
            };
        }

        var after = Text(box, "World");

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return new Tree(root, box, after);
    }

    private static CssBox Text(CssBox parent, string text)
    {
        var box = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        box.ParseToWords();
        return box;
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
