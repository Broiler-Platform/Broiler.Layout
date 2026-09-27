using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// When a block's lines are moved down, an inline-block, or an inline flex or grid container, on
/// them moves as a whole: its content goes with it, and its height stays what it is.
/// </summary>
/// <remarks>
/// <para>
/// <c>CreateLineBoxes</c> moves every line of a block down when something on them reaches above
/// the block's content box, so the lines start at its top (CSS 2.1 §9.4.2). A 60px inline-block
/// that <c>vertical-align: middle</c> raises above its line does that: here the box being tested,
/// or an empty one before it on the line. The move gave an inline-block a new
/// <c>Location</c> and nothing else: what the box holds is positioned absolutely, on lines and in
/// blocks of its own, and stayed where it was, above the box by as much as the box had moved. It
/// then added the move to the box's <c>ActualBottom</c>, which is its <c>Location</c> plus its
/// height, so the box also grew by it. An inline flex or grid container was not moved at all,
/// only its rectangle on the line.
/// </para>
/// <para>
/// Each word here is 8px wide and 16px tall. The tests assert where a box is against its content
/// and its line rather than against the block: how far the lines move depends on where this engine
/// puts the line's baseline, which is not what they test.
/// </para>
/// </remarks>
public sealed class InlineBlockLineShiftTests
{
    private static readonly Uri BaseUrl = new("file:///inline-block-line-shift.html");

    /// <summary>
    /// A 60px inline-block that <c>vertical-align: middle</c> raises above its line keeps its 60px,
    /// and the 10px block it holds is at its top, where its line rectangle is too: alone on the line,
    /// and after a word. It was 67.6px tall, with its block 7.6px above it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Raised_Inline_Block_Moves_With_Its_Content(bool afterWord)
    {
        var (block, box, content) = Lay("inline-block", link: null, box => box.VerticalAlign = "middle", afterWord);

        AssertMovedWhole(block, box, content);
    }

    /// <summary>
    /// After an empty 60px inline-block that <c>vertical-align: middle</c> raises above the block's
    /// first line, an inline-block holding a block, an inline-block holding a word, and an inline
    /// flex or grid container holding an item are each 60px tall, at their line rectangle, with
    /// their content at their top. The inline-blocks were 67.6px tall with their content 7.6px above
    /// them, and the flex and grid containers 7.6px above their rectangles.
    /// </summary>
    [Theory]
    [InlineData("inline-block", false)]
    [InlineData("inline-block", true)]
    [InlineData("inline-flex", false)]
    [InlineData("inline-grid", false)]
    public void An_Atomic_Inline_Moves_With_Its_Content_When_A_Raised_Box_Moves_The_Lines(
        string display, bool holdsWord)
    {
        var (block, box, content) = Lay(display, link: null, _ => { }, raisedBefore: true, holdsWord: holdsWord);

        AssertMovedWhole(block, box, content);
    }

    /// <summary>
    /// A move of the lines moves the inline-block's content once: an inline-block inside the one
    /// that is moved stays at the top of its container, and that container's content at its top.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Content_Nested_In_An_Inline_Block_Moves_Once()
    {
        var (_, box, content) = Lay("inline-block", link: null, _ => { }, raisedBefore: true, nestInlineBlock: true);

        var block = (CssBox)content;
        var nested = block.ParentBox!;
        Assert.Equal(box.Location.Y, nested.Location.Y, 1);
        Assert.Equal(box.Location.Y, block.Location.Y, 1);
        Assert.Equal(60, box.ActualBottom - box.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: after an unpadded link nothing moves the lines, and
    /// the inline-block is 60px tall at the top of the block, with its content at its top.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_An_Inline_Block_On_Lines_That_Do_Not_Move()
    {
        var (block, box, content) = Lay("inline-block", _ => { }, _ => { });

        Assert.Equal(block.Location.Y, box.Location.Y, 1);
        AssertMovedWhole(block, box, content);
    }

    /// <summary>
    /// Control, which passes before and after: the words on the lines are moved once. An
    /// inline-block that holds a word of its own, as a <c>::before</c> with
    /// <c>display: inline-block</c> does, has it on the block's line, and when a raised box moves
    /// the lines it moves as far as the link's word beside it. The raised box is aligned to the
    /// bottom of the line, which it reaches 44px above: one aligned to the middle would set where
    /// the inline-block stands, since this engine stands an inline-block on the lowest bottom of the
    /// inline-blocks beside it, whatever their alignment.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_An_Inline_Block_Holding_Its_Own_Word_Moves_It_Once()
    {
        var (root, block) = Block();
        Raised(block, "bottom");
        var link = new CssBox(block, new HtmlTag("a", false, null), BaseUrl) { Display = "inline" };
        var linkWord = Word(link);
        var marker = new CssBox(block, null, BaseUrl) { Display = "inline-block" };
        var markerWord = new CssRectWord(marker, "X", false, false);
        marker.Words.Add(markerWord);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.True(linkWord.Top > block.Location.Y, $"The lines did not move: the word is at {linkWord.Top}.");
        Assert.Equal(linkWord.Top, markerWord.Top, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a padded inline-block holding a word in an inline
    /// box lays the word out on a line of its own, which the block moves inside the inline-block
    /// rather than moving the inline-block's content again. The word stays the 40px padding below
    /// the box's top.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_A_Padded_Inline_Block_Keeps_Its_Word_Inside_Its_Padding()
    {
        var (root, block) = Block();
        var box = new CssBox(block, new HtmlTag("span", false, null), BaseUrl) { Display = "inline-block" };
        box.PaddingTop = box.PaddingBottom = box.PaddingLeft = box.PaddingRight = "40px";
        var word = Word(box);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(box.Location.Y + 40, word.Top, 1);
    }

    /// <summary>
    /// The box is 60px tall, its rectangle on the line of <paramref name="block"/> it is on is where
    /// it is, and its content is at its top.
    /// </summary>
    private static void AssertMovedWhole(CssBox block, CssBox box, object content)
    {
        Assert.Equal(60, box.ActualBottom - box.Location.Y, 1);

        var rectangle = Assert.Single(box.Rectangles, entry => entry.Key.OwnerBox == block).Value;
        Assert.Equal(box.Location.Y, rectangle.Y, 1);
        Assert.Equal(60, rectangle.Height, 1);

        double contentTop = content is CssRect word ? word.Top : ((CssBox)content).Location.Y;
        Assert.Equal(box.Location.Y, contentTop, 1);
    }

    /// <summary>
    /// A 320px block holding a word when <paramref name="wordBefore"/>, then a raised box when
    /// <paramref name="raisedBefore"/> (see <see cref="Raised"/>), then a link holding a word and
    /// styled by <paramref name="link"/> unless that is <see langword="null"/>, and then a 10×60px
    /// box of <paramref name="display"/>, styled by <paramref name="styleBox"/>. The box holds a 10px
    /// block, or a word when <paramref name="holdsWord"/>, or, when
    /// <paramref name="nestInlineBlock"/>, a 10px-wide inline-block holding the block.
    /// </summary>
    private static (CssBox Block, CssBox Box, object Content) Lay(
        string display,
        Action<CssBox>? link,
        Action<CssBox> styleBox,
        bool wordBefore = false,
        bool raisedBefore = false,
        bool holdsWord = false,
        bool nestInlineBlock = false)
    {
        var (root, block) = Block();

        if (wordBefore)
            Word(block);

        if (raisedBefore)
            Raised(block);

        if (link is not null)
        {
            var a = new CssBox(block, new HtmlTag("a", false, null), BaseUrl) { Display = "inline" };
            link(a);
            Word(a);
        }

        var box = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = display,
            Width = "10px",
            Height = "60px",
        };
        styleBox(box);

        object content;
        if (holdsWord)
        {
            content = Word(box);
        }
        else
        {
            var holder = box;
            if (nestInlineBlock)
                holder = new CssBox(box, new HtmlTag("span", false, null), BaseUrl) { Display = "inline-block", Width = "10px" };

            content = new CssBox(holder, new HtmlTag("i", false, null), BaseUrl) { Display = "block", Height = "10px" };
        }

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return (block, box, content);
    }

    /// <summary>
    /// An empty 10×60px inline-block in <paramref name="block"/> that <paramref name="verticalAlign"/>
    /// raises above its line, which moves the block's lines down: <c>middle</c> does on a line it
    /// sets the height of, and <c>bottom</c> on a line shorter than it.
    /// </summary>
    private static void Raised(CssBox block, string verticalAlign = "middle") =>
        _ = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "inline-block",
            Width = "10px",
            Height = "60px",
            VerticalAlign = verticalAlign,
        };

    /// <summary>A 320px block, the only box in the root.</summary>
    private static (CssBox Root, CssBox Block) Block()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        return (root, new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" });
    }

    /// <summary>An inline box in <paramref name="parent"/> holding one word, which it returns.</summary>
    private static CssRect Word(CssBox parent)
    {
        var text = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl) { Display = "inline" };
        var word = new CssRectWord(text, "X", false, false);
        text.Words.Add(word);
        return word;
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
