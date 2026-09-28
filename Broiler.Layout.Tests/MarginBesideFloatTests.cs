using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A block that establishes a formatting context keeps its border box clear of the floats beside
/// it, and its own margin may lie under them.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §9.5: the border box of an element in normal flow that establishes a new block formatting
/// context must not overlap the margin box of any floats in the same block formatting context. It
/// says nothing of the element's margin, and browsers let it lie under the float. The engine added
/// the element's margin to the float's edge, so the margin began where the float ends: the classic
/// sidebar layout, a 200px float and a main column with <c>overflow: hidden</c> and
/// <c>margin-left: 220px</c>, put the column 420px in, 200px narrower, where browsers put it 220px
/// in.
/// </para>
/// <para>
/// Each container here is 500px wide and holds a 200×50px float and then a block with
/// <c>overflow: hidden</c> holding one 8×16px word.
/// </para>
/// </remarks>
public sealed class MarginBesideFloatTests
{
    private static readonly Uri BaseUrl = new("file:///margin-beside-float.html");

    /// <summary>
    /// With <c>margin-left: 220px</c> beside a left float, or <c>margin-right: 220px</c> beside a
    /// right one, the block begins 220px in or at the container's left, and is 280px wide. It began
    /// 420px in, 80px wide, or was 80px wide.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.Left, 220, 280)]
    [InlineData(CssConstants.Right, 0, 280)]
    public void A_Margin_As_Wide_As_The_Float_And_A_Gutter_Takes_The_Float_In(string side, float x, float width)
    {
        var tree = Build(side, block =>
        {
            if (side == CssConstants.Left)
                block.MarginLeft = "220px";
            else
                block.MarginRight = "220px";
        });
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft + x, tree.Block.Location.X, 1);
        Assert.Equal(width, tree.Block.Size.Width, 1);
    }

    /// <summary>
    /// With <c>margin-left: 10px</c>, less than the float is wide, the block begins at the float's
    /// edge, 200px in, 300px wide. It began 210px in, 290px wide.
    /// </summary>
    [Fact]
    public void A_Margin_Narrower_Than_The_Float_Lies_Under_It()
    {
        var tree = Build(CssConstants.Left, block => block.MarginLeft = "10px");
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft + 200, tree.Block.Location.X, 1);
        Assert.Equal(300, tree.Block.Size.Width, 1);
    }

    /// <summary>
    /// A 250px block with <c>margin-left: 30px</c> begins at the float's edge, 200px in, where its
    /// border box fits. It began 230px in.
    /// </summary>
    [Fact]
    public void A_Block_With_A_Width_Begins_At_The_Floats_Edge()
    {
        var tree = Build(CssConstants.Left, block =>
        {
            block.Width = "250px";
            block.MarginLeft = "30px";
        });
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft + 200, tree.Block.Location.X, 1);
        Assert.Equal(250, tree.Block.Size.Width, 1);
    }

    /// <summary>
    /// A flex container, which establishes a formatting context too, with
    /// <c>margin-left: 220px</c>, begins 220px in, 280px wide. It began 420px in, 80px wide.
    /// </summary>
    [Fact]
    public void A_Flex_Container_Takes_The_Float_In_Its_Margin_Too()
    {
        var tree = Build(CssConstants.Left, block =>
        {
            block.Overflow = CssConstants.Visible;
            block.Display = "flex";
            block.MarginLeft = "220px";
        });
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft + 220, tree.Block.Location.X, 1);
        Assert.Equal(280, tree.Block.Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the sidebar layout gives the same places.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Places()
    {
        var tree = Build(CssConstants.Left, block => block.MarginLeft = "220px");
        Layout(tree);

        float x = tree.Block.Location.X, width = tree.Block.Size.Width;
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft + 220, x, 1);
        Assert.Equal(x, tree.Block.Location.X, 1);
        Assert.Equal(width, tree.Block.Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after:
    /// <list type="bullet">
    /// <item>with no margin, the block begins at the float's edge, 200px in, 300px wide;</item>
    /// <item>beside a float with <c>margin-right: 20px</c>, at the float's margin edge, 220px in,
    /// 280px wide;</item>
    /// <item>a block that does not establish a formatting context, with
    /// <c>margin-left: 220px</c>, begins 220px in and lays its line out beside the float.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData("none", 200, 300)]
    [InlineData("float-margin", 220, 280)]
    [InlineData("plain", 220, 280)]
    public void Control_A_Block_Clear_Of_The_Float_As_It_Was(string kind, float x, float width)
    {
        var tree = Build(CssConstants.Left, block =>
        {
            if (kind == "plain")
            {
                block.Overflow = CssConstants.Visible;
                block.MarginLeft = "220px";
            }
        });

        if (kind == "float-margin")
            tree.Float.MarginRight = "20px";

        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft + x, tree.Block.Location.X, 1);
        Assert.Equal(width, tree.Block.Size.Width, 1);
    }

    /// <summary>
    /// The root, the 500px container, the float and the block.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Container, CssBox Float, CssBox Block);

    /// <summary>
    /// In a block in the root, a 500px container holding a 200×50px float on the given
    /// <paramref name="side"/> and then a block with <c>overflow: hidden</c> holding a word, styled by
    /// <paramref name="style"/>.
    /// </summary>
    private static Tree Build(string side, Action<CssBox> style)
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
        var floated = new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Float = side,
            Width = "200px",
            Height = "50px",
        };

        var block = new CssBox(container, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Overflow = "hidden" };
        style(block);

        var text = new CssBox(block, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();

        return new Tree(root, container, floated, block);
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
