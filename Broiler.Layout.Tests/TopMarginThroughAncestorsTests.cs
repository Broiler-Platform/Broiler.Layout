using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A first child's top margin collapses through every block it is the first child of, as far as
/// nothing separates their top margins, and not only through its parent.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §8.3.1: the top margin of a box collapses with its first in-flow child's when no border,
/// padding or clearance separates them, and margins that adjoin in turn collapse together.
/// <c>MarginTopCollapse</c> moved only the child's parent, by the part of the child's margin the
/// margins above did not already give, so the margin collapsed through one block and stopped
/// inside the next: a block holding an unpadded block holding a paragraph began right below the
/// block before it and held the inner block 16px down, where browsers begin it 16px down with
/// everything at its top.
/// </para>
/// <para>
/// Each run of blocks here is in a 320px block in a block in the root, as a page's are in its
/// body, after a 10px block unless it is the first child there. Its innermost block holds a
/// paragraph with a 16px top margin and no bottom margin, around one word, 8×16px.
/// </para>
/// </remarks>
public sealed class TopMarginThroughAncestorsTests
{
    private static readonly Uri BaseUrl = new("file:///top-margin-through-ancestors.html");

    /// <summary>
    /// A block holding an unpadded block holding the paragraph begins 16px below the 10px block,
    /// 16px tall, with the inner block and the paragraph at its top. It began right below the 10px
    /// block, 32px tall, with the inner block 16px down.
    /// </summary>
    [Fact]
    public void The_Margin_Collapses_Through_Two_Blocks()
    {
        var tree = Lay(afterBlock: true, Plain, Plain);

        Assert.Equal(tree.Before!.ActualBottom + 16, tree.Run[0].Location.Y, 1);
        Assert.Equal(16, tree.Run[0].Size.Height, 1);
        Assert.Equal(tree.Run[0].Location.Y, tree.Run[1].Location.Y, 1);
        Assert.Equal(tree.Run[0].Location.Y, tree.Paragraph.Location.Y, 1);
    }

    /// <summary>
    /// With three blocks, the outermost begins 16px below the 10px block, with the other two and
    /// the paragraph at its top. It began right below it, with the innermost block 16px down.
    /// </summary>
    [Fact]
    public void The_Margin_Collapses_Through_Three_Blocks()
    {
        var tree = Lay(afterBlock: true, Plain, Plain, Plain);

        Assert.Equal(tree.Before!.ActualBottom + 16, tree.Run[0].Location.Y, 1);
        Assert.Equal(tree.Run[0].Location.Y, tree.Run[2].Location.Y, 1);
        Assert.Equal(tree.Run[0].Location.Y, tree.Paragraph.Location.Y, 1);
    }

    /// <summary>
    /// The inner block's own 10px top margin collapses with the paragraph's 16px: the outer block
    /// begins 16px below the 10px block, with the inner block and the paragraph at its top. It
    /// began 10px below it, with the inner block 6px down.
    /// </summary>
    [Fact]
    public void The_Inner_Blocks_Own_Smaller_Margin_Collapses_With_It()
    {
        var tree = Lay(afterBlock: true, Plain, box => box.MarginTop = "10px");

        Assert.Equal(tree.Before!.ActualBottom + 16, tree.Run[0].Location.Y, 1);
        Assert.Equal(tree.Run[0].Location.Y, tree.Run[1].Location.Y, 1);
        Assert.Equal(tree.Run[0].Location.Y, tree.Paragraph.Location.Y, 1);
    }

    /// <summary>
    /// When the run is the first child of the 320px block, that block begins 16px down the block in
    /// the root, which keeps its place as a page's root element does, with the run and the
    /// paragraph at its top. It began at the top, with the run's inner block 16px down.
    /// </summary>
    [Fact]
    public void The_Margin_Collapses_Up_To_The_Block_In_The_Root()
    {
        var tree = Lay(afterBlock: false, Plain, Plain);

        Assert.Equal(tree.Body.ClientTop + 16, tree.Page.Location.Y, 1);
        Assert.Equal(tree.Page.Location.Y, tree.Run[0].Location.Y, 1);
        Assert.Equal(tree.Page.Location.Y, tree.Run[1].Location.Y, 1);
        Assert.Equal(tree.Page.Location.Y, tree.Paragraph.Location.Y, 1);
    }

    /// <summary>
    /// When the paragraph is empty, the run is too, and a block with a word after it collapses its
    /// top with the same margin: the run's outer block begins 16px below the 10px block, 0px tall,
    /// and the block after it at the same place, not 16px further down. It began right below the
    /// 10px block, 16px tall.
    /// </summary>
    [Fact]
    public void An_Empty_Run_Hands_Its_Margin_On_Once()
    {
        var tree = Build(afterBlock: true, empty: true, [Plain, Plain]);

        var after = new CssBox(tree.Page, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        Word(after);

        Layout(tree);

        Assert.Equal(tree.Before!.ActualBottom + 16, tree.Run[0].Location.Y, 1);
        Assert.Equal(0, tree.Run[0].Size.Height, 1);
        Assert.Equal(tree.Before.ActualBottom + 16, after.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after, each with an inner block holding the paragraph:
    /// <list type="bullet">
    /// <item>an outer block with its own 30px top margin begins 30px below the 10px block, with the
    /// inner block at its top;</item>
    /// <item>one with 1px of top padding begins right below the 10px block and keeps the margin
    /// inside, the inner block 17px down it;</item>
    /// <item>one with <c>overflow: hidden</c> keeps it too, the inner block 16px down it;</item>
    /// <item>a row flex container's and a grid container's item keeps it, the inner block 16px down
    /// the item, which begins at the container's top, right below the 10px block.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData("margin", 30, 0)]
    [InlineData("padding", 0, 17)]
    [InlineData("overflow", 0, 16)]
    [InlineData("flex", 0, 16)]
    [InlineData("grid", 0, 16)]
    public void Control_A_Run_That_Stops_Or_Needs_Nothing(string kind, float outerTop, float innerTop)
    {
        var tree = kind switch
        {
            "margin" => Lay(afterBlock: true, box => box.MarginTop = "30px", Plain),
            "padding" => Lay(afterBlock: true, box => box.PaddingTop = "1px", Plain),
            "overflow" => Lay(afterBlock: true, box => box.Overflow = "hidden", Plain),
            "flex" => Lay(afterBlock: true, box => box.Display = "flex", Plain, Plain),
            "grid" => Lay(afterBlock: true, box => { box.Display = "grid"; box.GridTemplateColumns = "100px"; }, Plain, Plain),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        // In a flex or grid container, the item is the block that holds the inner one.
        var outer = kind is "flex" or "grid" ? tree.Run[1] : tree.Run[0];
        var inner = kind is "flex" or "grid" ? tree.Run[2] : tree.Run[1];

        Assert.Equal(tree.Before!.ActualBottom + outerTop, tree.Run[0].Location.Y, 1);
        Assert.Equal(tree.Run[0].Location.Y, outer.Location.Y, 1);
        Assert.Equal(outer.Location.Y + innerTop, inner.Location.Y, 1);
    }

    private static void Plain(CssBox box)
    {
    }

    /// <summary>
    /// The root, the block in it, the 320px block in that, the 10px block if there is one, the
    /// run's blocks, the outermost first, and the paragraph.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Body, CssBox Page, CssBox? Before, CssBox[] Run, CssBox Paragraph);

    /// <summary>
    /// <see cref="Build"/>, with the paragraph's word, laid out.
    /// </summary>
    private static Tree Lay(bool afterBlock, params Action<CssBox>[] levels)
    {
        var tree = Build(afterBlock, empty: false, levels);
        Layout(tree);
        return tree;
    }

    /// <summary>
    /// In a block in the root: a 320px block holding a 10px block when <paramref name="afterBlock"/>,
    /// and then the run, one block inside the other, each styled by its entry in
    /// <paramref name="levels"/>, the outermost first, with the paragraph in the innermost, holding
    /// its word unless <paramref name="empty"/>.
    /// </summary>
    private static Tree Build(bool afterBlock, bool empty, Action<CssBox>[] levels)
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
        var before = afterBlock
            ? new CssBox(page, new HtmlTag("b", false, null), BaseUrl) { Display = "block", Height = "10px" }
            : null;

        var run = new CssBox[levels.Length];
        var parent = page;

        for (int i = 0; i < levels.Length; i++)
        {
            run[i] = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
            levels[i](run[i]);
            parent = run[i];
        }

        var paragraph = new CssBox(parent, new HtmlTag("p", false, null), BaseUrl)
        {
            Display = "block",
            MarginTop = "16px",
            MarginBottom = "0",
        };

        if (!empty)
            Word(paragraph);

        return new Tree(root, body, page, before, run, paragraph);
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
