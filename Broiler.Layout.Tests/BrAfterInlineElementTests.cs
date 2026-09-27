using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A <c>&lt;br&gt;</c> that ends a line of text held in an inline element ends that line, and
/// makes no empty one.
/// </summary>
/// <remarks>
/// <para>
/// The host's DOM parser gives a <c>&lt;br&gt;</c> the height of an empty line, <c>.95em</c>,
/// when it follows a block, and takes an element that holds no text of its own for one: an
/// inline-block, whose case <c>PerformLayoutImp</c> already corrects, and an inline element whose
/// text is in its children. So <c>&lt;a&gt;one&lt;/a&gt;&lt;br&gt;two</c> was three lines, the
/// middle one empty, where browsers make it two.
/// </para>
/// <para>
/// Each block here is 320px wide, in a block in the root, and holds its content as the host hands
/// it over: the <c>&lt;br&gt;</c> a block with that height, and the inline content around it in
/// anonymous blocks. Words are 8×16px, one line 16px.
/// </para>
/// </remarks>
public sealed class BrAfterInlineElementTests
{
    private static readonly Uri BaseUrl = new("file:///br-after-inline-element.html");

    /// <summary>
    /// After a link holding a word, or a span holding a bold word, the <c>&lt;br&gt;</c> ends the
    /// line, 0px tall: the block is two lines tall, 32px, the second word on the second line. The
    /// <c>&lt;br&gt;</c> was an empty line between them, as tall as its <c>.95em</c>.
    /// </summary>
    [Theory]
    [InlineData("a")]
    [InlineData("span b")]
    public void The_Br_Ends_The_Inline_Elements_Line(string elements)
    {
        var (block, brs, after) = Lay(line => Inline(line, elements.Split(' ')), br: 1);

        Assert.Equal(0, brs[0].Size.Height, 1);
        Assert.Equal(32, block.Size.Height, 1);
        Assert.Equal(block.Location.Y + 16, after.Location.Y, 1);
    }

    /// <summary>
    /// A second <c>&lt;br&gt;</c> after the first still makes an empty line between the two lines;
    /// the first is 0px tall. The first was an empty line too.
    /// </summary>
    [Fact]
    public void A_Second_Br_Still_Makes_An_Empty_Line()
    {
        var (block, brs, _) = Lay(line => Inline(line, ["a"]), br: 2);

        Assert.Equal(0, brs[0].Size.Height, 1);
        Assert.True(brs[1].Size.Height > 1);
        Assert.Equal(32 + brs[1].Size.Height, block.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a <c>&lt;br&gt;</c> after a <c>&lt;div&gt;</c>
    /// holding a word, or after an empty bold element, makes an empty line; one after an
    /// inline-block holding a word ends its line, 0px tall.
    /// </summary>
    [Theory]
    [InlineData("div", true)]
    [InlineData("empty", true)]
    [InlineData("inline-block", false)]
    public void Control_A_Br_After_A_Block_Or_Nothing(string before, bool emptyLine)
    {
        var (_, brs, _) = before switch
        {
            "div" => Lay(null, br: 1, blockBefore: true),
            "empty" => Lay(line => _ = new CssBox(line, new HtmlTag("b", false, null), BaseUrl) { Display = "inline" }, br: 1),
            _ => Lay(line => Word(new CssBox(line, new HtmlTag("span", false, null), BaseUrl) { Display = "inline-block" }), br: 1),
        };

        if (emptyLine)
            Assert.True(brs[0].Size.Height > 1);
        else
            Assert.Equal(0, brs[0].Size.Height, 1);
    }

    /// <summary>
    /// Nests inline elements named by <paramref name="tags"/> in <paramref name="line"/>, the
    /// innermost holding a word.
    /// </summary>
    private static void Inline(CssBox line, string[] tags)
    {
        var parent = line;
        foreach (var tag in tags)
            parent = new CssBox(parent, new HtmlTag(tag, false, null), BaseUrl) { Display = "inline" };

        Word(parent);
    }

    private static void Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
    }

    /// <summary>
    /// A 320px block in a block in the root holding, in turn: a <c>&lt;div&gt;</c> with a word when
    /// <paramref name="blockBefore"/>, or else an anonymous block that <paramref name="line"/> fills;
    /// <paramref name="br"/> <c>&lt;br&gt;</c> blocks with a height of <c>.95em</c>; and an anonymous
    /// block with a word, which is returned with the block and the <c>&lt;br&gt;</c> blocks.
    /// </summary>
    private static (CssBox Block, CssBox[] Brs, CssBox After) Lay(Action<CssBox>? line, int br, bool blockBefore = false)
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

        if (blockBefore)
            Word(new CssBox(block, new HtmlTag("div", false, null), BaseUrl) { Display = "block" });
        else
            line!(new CssBox(block, null, BaseUrl) { Display = "block" });

        var brs = new CssBox[br];
        for (int i = 0; i < br; i++)
            brs[i] = new CssBox(block, new HtmlTag("br", false, null), BaseUrl) { Display = "block", Height = ".95em" };

        var after = new CssBox(block, null, BaseUrl) { Display = "block" };
        Word(after);

        root.PerformLayout(root.LayoutEnvironment);
        return (block, brs, after);
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
