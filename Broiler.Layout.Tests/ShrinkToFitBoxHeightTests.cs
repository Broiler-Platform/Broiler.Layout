using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline-block, and a flex item laid out at its shrink-to-fit width, ends at the bottom margin
/// edge of its lowest in-flow child, and then its own bottom padding and border (CSS2.1 §10.6.7).
/// It establishes a block formatting context, so that margin cannot collapse through it (§8.3.1).
/// </summary>
/// <remarks>
/// <para>
/// Such a box, holding block-level children, took its height from its children's border boxes and
/// nothing after them: no bottom margin, no bottom padding, no bottom border. A
/// <c>display: inline-block; padding: 40px</c> box holding a 20px block was 60px tall where browsers
/// make it 100, and an item of a column flex container that did not stretch it was as short, with
/// the next item moved up by as much. A box holding inline content only was right: it is laid out
/// by line layout, which adds them.
/// </para>
/// <para>
/// Words are 8px wide and 16px tall. A paragraph here is a block with 16px margins above and below
/// holding one word.
/// </para>
/// </remarks>
public sealed class ShrinkToFitBoxHeightTests
{
    private static readonly Uri BaseUrl = new("file:///shrink-to-fit-box-height.html");

    /// <summary>
    /// An inline-block, and an item of a column flex container that aligns its items
    /// <c>flex-start</c>, with <c>padding: 40px</c>: 100px tall around a 20px block, and 128px
    /// around a paragraph, whose bottom margin stays inside it. They were 60 and 72.
    /// </summary>
    [Theory]
    [InlineData("inline-block", "block", 100)]
    [InlineData("inline-block", "paragraph", 128)]
    [InlineData("column item", "block", 100)]
    [InlineData("column item", "paragraph", 128)]
    public void A_Padded_Box_Ends_After_Its_Last_Childs_Margin_And_Its_Padding(
        string box, string content, float height)
    {
        var (outer, next) = Lay(box, outer =>
        {
            outer.PaddingTop = outer.PaddingRight = outer.PaddingBottom = outer.PaddingLeft = "40px";
            AddContent(outer, content);
        });

        Assert.Equal(height, Height(outer), 1);
        if (next != null)
            Assert.Equal(height, next.Location.Y - outer.Location.Y, 1);
    }

    /// <summary>
    /// Without padding the paragraph's bottom margin is still inside the box, which ends 16px below
    /// the paragraph, and a column item's next item starts where it ends. It ended at the
    /// paragraph, and the next item was 16px higher with it.
    /// </summary>
    [Theory]
    [InlineData("inline-block")]
    [InlineData("column item")]
    public void An_Unpadded_Box_Contains_Its_Last_Childs_Margin(string box)
    {
        CssBox? paragraph = null;
        var (outer, next) = Lay(box, outer => paragraph = AddContent(outer, "paragraph"));

        Assert.Equal(16, outer.ActualBottom - paragraph!.ActualBottom, 1);
        if (next != null)
            Assert.Equal(outer.ActualBottom, next.Location.Y, 1);
    }

    /// <summary>
    /// A margin that has collapsed through a child from the child's own last child is contained
    /// too: a paragraph inside a plain block inside a column item leaves the item 48px tall.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Margin_Collapsed_Through_A_Child_Is_Contained()
    {
        var (outer, _) = Lay("column item", outer => AddContent(Block(outer), "paragraph"));

        Assert.Equal(48, Height(outer), 1);
    }

    /// <summary>A 5px bottom border is added below the 20px block: 25px.</summary>
    [Fact(Timeout = 600000)]
    public void Its_Bottom_Border_Follows_Its_Content()
    {
        var (outer, _) = Lay("inline-block", outer =>
        {
            outer.BorderBottomStyle = "solid";
            outer.BorderBottomWidth = "5px";
            AddContent(outer, "block");
        });

        Assert.Equal(25, Height(outer), 1);
    }

    /// <summary>
    /// A relative offset moves a child visually and leaves the flow where it was (CSS2.1 §9.4.3):
    /// a 20px block moved down 30px leaves the box 20px tall. It was 50.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Relatively_Positioned_Child_Is_Measured_Where_The_Flow_Put_It()
    {
        var (outer, _) = Lay("inline-block", outer =>
        {
            var child = AddContent(outer, "block");
            child.Position = CssConstants.Relative;
            child.Top = "30px";
        });

        Assert.Equal(20, Height(outer), 1);
    }

    /// <summary>
    /// The box contains a floated child with its bottom margin, as a block formatting context
    /// contains its floats (§10.6.7): with 10px of padding, a 20px block and beside it a 50px
    /// float with a 5px bottom margin make 75px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Float_And_Its_Margin_Are_Contained()
    {
        var (outer, _) = Lay("inline-block", outer =>
        {
            outer.PaddingTop = outer.PaddingBottom = "10px";
            var child = AddContent(outer, "block");
            child.Float = CssConstants.Left;
            child.Height = "50px";
            child.MarginBottom = "5px";
            AddContent(outer, "block");
        });

        Assert.Equal(75, Height(outer), 1);
    }

    /// <summary>
    /// A box whose only child is absolutely positioned has no content height, and keeps its
    /// padding: 20px from <c>padding: 10px</c>. It was 0.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Box_With_Only_An_Absolutely_Positioned_Child_Keeps_Its_Padding()
    {
        var (outer, _) = Lay("inline-block", outer =>
        {
            outer.PaddingTop = outer.PaddingBottom = "10px";
            AddContent(outer, "block").Position = CssConstants.Absolute;
        });

        Assert.Equal(20, Height(outer), 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a block, which block layout lays out, is 100px tall
    /// with <c>padding: 40px</c> around a 20px block, and 96px around a word.
    /// </summary>
    [Theory]
    [InlineData("block", "block", 100)]
    [InlineData("block", "word", 96)]
    public void Control_Boxes_That_Were_Already_Right(string box, string content, float height)
    {
        var (outer, _) = Lay(box, outer =>
        {
            outer.PaddingTop = outer.PaddingRight = outer.PaddingBottom = outer.PaddingLeft = "40px";
            AddContent(outer, content);
        });

        Assert.Equal(height, Height(outer), 1);
    }

    /// <summary>
    /// The box under test, styled and filled by <paramref name="fill"/>: an inline-block in a
    /// block, a <c>flex-start</c> column flex item followed by a 10px item, or a block. Returns
    /// the item after it when there is one.
    /// </summary>
    private static (CssBox Box, CssBox? Next) Lay(string kind, Action<CssBox> fill)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var container = new CssBox(root, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Width = "600px",
        };

        CssBox? next = null;
        CssBox box;

        switch (kind)
        {
            case "inline-block":
                box = new CssBox(container, new HtmlTag("span", false, null), BaseUrl) { Display = CssConstants.InlineBlock };
                break;
            case "column item":
                container.Display = "flex";
                container.FlexDirection = "column";
                container.AlignItems = "flex-start";
                box = Block(container);
                break;
            default:
                box = Block(container);
                break;
        }

        fill(box);

        if (kind == "column item")
        {
            next = Block(container);
            next.Height = "10px";
        }

        root.PerformLayout(root.LayoutEnvironment);
        return (box, next);
    }

    private static CssBox Block(CssBox parent) =>
        new(parent, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };

    /// <summary>
    /// A 20px-tall block 50px wide, a paragraph (a block with 16px margins above and below holding
    /// one word), or a word.
    /// </summary>
    private static CssBox AddContent(CssBox parent, string content)
    {
        switch (content)
        {
            case "block":
                var block = Block(parent);
                block.Width = "50px";
                block.Height = "20px";
                return block;

            case "paragraph":
                var paragraph = new CssBox(parent, new HtmlTag("p", false, null), BaseUrl)
                {
                    Display = CssConstants.Block,
                    MarginTop = "16px",
                    MarginBottom = "16px",
                };
                Word(paragraph);
                return paragraph;

            default:
                return Word(parent);
        }
    }

    private static CssBox Word(CssBox parent)
    {
        var text = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl) { Display = CssConstants.Inline };
        text.Words.Add(new CssRectWord(text, "X", false, false));
        return text;
    }

    private static double Height(CssBox box) => box.ActualBottom - box.Location.Y;

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
