using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A block that establishes a formatting context, with a width of its own, goes beside the floats
/// before it only where it fits, and otherwise below them, as far down as it takes.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §9.5: the border box of an element in normal flow that establishes a new block formatting
/// context must not overlap the margin box of any floats; if necessary, it is placed below them. The
/// float avoidance placed such a box beside the floats whenever any space was left there, and
/// narrowed only a box with an auto width, so a box with a width of its own ran past its
/// container's edge: after a 100px float, a block with <c>overflow: hidden</c> and
/// <c>width: 100%</c> began 100px in, where browsers place it below the float. When it did clear, it
/// went below all the floats at once, past the space beside those that go on further down.
/// </para>
/// <para>
/// Each container here is 500px wide and holds floats and then a block with
/// <c>overflow: hidden</c> holding one 8×16px word.
/// </para>
/// </remarks>
public sealed class BlockBesideFloatFitTests
{
    private static readonly Uri BaseUrl = new("file:///block-beside-float-fit.html");

    /// <summary>
    /// After a 200×50px left float, a block with <c>width: 100%</c> or <c>width: 350px</c>, wider
    /// than the 300px beside the float, begins below the float at the container's left. It began
    /// beside it, 200px in, running past the container's right edge.
    /// </summary>
    [Theory]
    [InlineData("100%", 500)]
    [InlineData("350px", 350)]
    public void A_Block_Too_Wide_For_The_Space_Beside_Goes_Below(string width, float usedWidth)
    {
        var tree = Build(width, (CssConstants.Left, 200, 50));
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft, tree.Block.Location.X, 1);
        Assert.Equal(tree.Container.ClientTop + 50, tree.Block.Location.Y, 1);
        Assert.Equal(usedWidth, tree.Block.Size.Width, 1);
    }

    /// <summary>
    /// After a 200×50px right float, a 350px block begins below it too. It began beside it at the
    /// left, running under the float.
    /// </summary>
    [Fact]
    public void A_Block_Too_Wide_Beside_A_Right_Float_Goes_Below()
    {
        var tree = Build("350px", (CssConstants.Right, 200, 50));
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft, tree.Block.Location.X, 1);
        Assert.Equal(tree.Container.ClientTop + 50, tree.Block.Location.Y, 1);
    }

    /// <summary>
    /// After a 150×20px left float and a 50×40px right one, a 320px block begins 20px down, below
    /// the left float and beside the right one, where 450px is left. It began beside both, 150px in,
    /// and going below all the floats at once took it 40px down.
    /// </summary>
    [Fact]
    public void It_Goes_Only_As_Far_Down_As_It_Takes()
    {
        var tree = Build("320px", (CssConstants.Left, 150, 20), (CssConstants.Right, 50, 40));
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft, tree.Block.Location.X, 1);
        Assert.Equal(tree.Container.ClientTop + 20, tree.Block.Location.Y, 1);
    }

    /// <summary>
    /// A 600px block, wider than the container, begins below the float at the container's left,
    /// where it would with no float. It began beside it, 200px in.
    /// </summary>
    [Fact]
    public void A_Block_Wider_Than_Its_Container_Goes_Below_The_Float()
    {
        var tree = Build("600px", (CssConstants.Left, 200, 50));
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft, tree.Block.Location.X, 1);
        Assert.Equal(tree.Container.ClientTop + 50, tree.Block.Location.Y, 1);
    }

    /// <summary>
    /// Laid out a second time, the <c>width: 100%</c> block gives the same place.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Places()
    {
        var tree = Build("100%", (CssConstants.Left, 200, 50));
        Layout(tree);

        float x = tree.Block.Location.X, y = tree.Block.Location.Y;
        Layout(tree);

        Assert.Equal(tree.Container.ClientTop + 50, y, 1);
        Assert.Equal(x, tree.Block.Location.X, 1);
        Assert.Equal(y, tree.Block.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after, after a 200×50px left float: a block with
    /// <c>width: 300px</c>, just as wide as the space, <c>width: 250px</c>, or an auto width, begins
    /// beside the float, 200px in, 300, 250 and 300px wide.
    /// </summary>
    [Theory]
    [InlineData("300px", 300)]
    [InlineData("250px", 250)]
    [InlineData(null, 300)]
    public void Control_A_Block_That_Fits_Beside_The_Float(string? width, float usedWidth)
    {
        var tree = Build(width, (CssConstants.Left, 200, 50));
        Layout(tree);

        Assert.Equal(tree.Container.ClientLeft + 200, tree.Block.Location.X, 1);
        Assert.Equal(tree.Container.ClientTop, tree.Block.Location.Y, 1);
        Assert.Equal(usedWidth, tree.Block.Size.Width, 1);
    }

    /// <summary>
    /// The root, the 500px container and the block.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Container, CssBox Block);

    /// <summary>
    /// In a block in the root, a 500px container holding the given floats, each with its side,
    /// width and height, and then a block with <c>overflow: hidden</c> and the given width, auto
    /// when null, holding a word.
    /// </summary>
    private static Tree Build(string? width, params (string Side, int Width, int Height)[] floats)
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

        foreach (var (side, floatWidth, floatHeight) in floats)
        {
            _ = new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = "block",
                Float = side,
                Width = floatWidth + "px",
                Height = floatHeight + "px",
            };
        }

        var block = new CssBox(container, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Overflow = "hidden" };

        if (width != null)
            block.Width = width;

        var text = new CssBox(block, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();

        return new Tree(root, container, block);
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
