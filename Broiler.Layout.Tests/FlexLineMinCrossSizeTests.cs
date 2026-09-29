using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A row flex container with one line gives that line its own <c>min-height</c> and
/// <c>max-height</c> across, and aligns its items in it.
/// </summary>
/// <remarks>
/// <para>
/// CSS Flexbox §9.4 step 8: "If the flex container is single-line, then clamp the line's cross-size
/// to be within the container's computed min and max cross sizes." The line was as tall as its
/// tallest item: a container 32px tall by its <c>min-height</c> aligned its items in a 20px line
/// at its top, so a 20px icon centred in it stood at the top, where browsers centre it, 6px down.
/// MediaWiki's icon buttons are such containers: <c>display: inline-flex; align-items: center;
/// min-height: 32px</c>.
/// </para>
/// <para>
/// Each container here is a flex row in a block in the root, holding one 20 × 20px item unless
/// noted. Words are 8px wide a letter and 16px tall.
/// </para>
/// </remarks>
public sealed class FlexLineMinCrossSizeTests
{
    private static readonly Uri BaseUrl = new("file:///flex-line-min-cross-size.html");

    /// <summary>
    /// With <c>min-height: 32px</c> and <c>align-items: center</c>, the item is 6px down, in the
    /// middle of the container, in a block-level and an inline-level container. It was at the top.
    /// </summary>
    [Theory]
    [InlineData("flex")]
    [InlineData("inline-flex")]
    public void An_Item_Is_Centred_In_A_Container_Its_Min_Height_Makes_Taller(string display)
    {
        var (root, container, item) = Build(display, c => c.MinHeight = "32px", "center");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(32, container.Size.Height, 1);
        Assert.Equal(6, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// A 1px border and <c>box-sizing: border-box</c>, as MediaWiki's buttons have, leave 30px
    /// inside, and the item is 6px down the button, 5px down its content. It was 1px down, at the
    /// top of the content.
    /// </summary>
    [Fact]
    public void The_Border_Box_Min_Height_Counts_Its_Border()
    {
        var (root, container, item) = Build("inline-flex", c =>
        {
            c.MinHeight = "32px";
            c.BoxSizing = "border-box";
            c.BorderTopWidth = c.BorderBottomWidth = "1px";
            c.BorderTopStyle = c.BorderBottomStyle = "solid";
        }, "center");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(32, container.Size.Height, 1);
        Assert.Equal(6, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// With <c>align-items: flex-end</c> the item is at the bottom, 12px down. It was at the top.
    /// </summary>
    [Fact]
    public void An_Item_Aligned_To_The_End_Is_At_The_Bottom()
    {
        var (root, container, item) = Build("flex", c => c.MinHeight = "32px", "flex-end");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(12, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// A stretched item with no height of its own, holding an x, is 32px tall. It was 16px.
    /// </summary>
    [Fact]
    public void A_Stretched_Item_Fills_The_Min_Height()
    {
        var (root, container, item) = Build("flex", c => c.MinHeight = "32px", null, itemHeight: null);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(0, item.Location.Y - container.Location.Y, 1);
        Assert.Equal(32, item.Size.Height, 1);
    }

    /// <summary>
    /// With <c>max-height: 10px</c>, the 20px item is centred on the 10px line and overflows it,
    /// 5px above the container's top. It was at the top.
    /// </summary>
    [Fact]
    public void An_Item_Taller_Than_The_Max_Height_Overflows_Centred()
    {
        var (root, container, item) = Build("flex", c => c.MaxHeight = "10px", "center");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(10, container.Size.Height, 1);
        Assert.Equal(-5, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// A wrapping container holding one line centres the item in its <c>min-height</c> too.
    /// </summary>
    [Fact]
    public void A_Wrapping_Container_With_One_Line_Centres_Too()
    {
        var (root, container, item) = Build("flex", c =>
        {
            c.MinHeight = "32px";
            c.FlexWrap = "wrap";
        }, "center");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(6, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// In a container 10px tall by its <c>height</c>, the line is 10px too, and the 20px item
    /// centred on it is 5px above the container's top, or 10px above it aligned to the end. It was at
    /// the top: the line only grew to a definite height, and stayed as tall as the item.
    /// </summary>
    [Theory]
    [InlineData("center", -5)]
    [InlineData("flex-end", -10)]
    public void A_Definite_Height_Smaller_Than_The_Item_Is_The_Lines(string alignItems, double top)
    {
        var (root, container, item) = Build("flex", c => c.Height = "10px", alignItems);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(10, container.Size.Height, 1);
        Assert.Equal(top, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// A stretched item holding an x, in a container 10px tall, is 10px tall. It was 16px.
    /// </summary>
    [Fact]
    public void A_Stretched_Item_Shrinks_To_A_Smaller_Definite_Height()
    {
        var (root, _, item) = Build("flex", c => c.Height = "10px", null, itemHeight: null);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(10, item.Size.Height, 1);
    }

    /// <summary>
    /// A wrapping container's lines are not clamped to its <c>max-height</c>: its one 20px line
    /// overflows the 10px container, and the item centred on it is at the top.
    /// </summary>
    [Fact]
    public void A_Wrapping_Containers_Line_Is_Not_Clamped_To_Its_Max_Height()
    {
        var (root, container, item) = Build("flex", c =>
        {
            c.MaxHeight = "10px";
            c.FlexWrap = "wrap";
        }, "center");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(10, container.Size.Height, 1);
        Assert.Equal(0, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// A wrapping container's line is stretched to its <c>min-height</c> or its <c>height</c> only by
    /// <c>align-content</c>: with <c>flex-start</c> it keeps its 20px, and the item centred on it
    /// is at the top; with <c>center</c> the line is centred, and the item 6px down. A lone line
    /// grew to a definite height whatever <c>align-content</c> said.
    /// </summary>
    [Theory]
    [InlineData("min-height", "flex-start", 0)]
    [InlineData("height", "flex-start", 0)]
    [InlineData("min-height", "center", 6)]
    [InlineData("height", "flex-end", 12)]
    public void A_Wrapping_Containers_Line_Is_Placed_By_Align_Content(string property, string alignContent, double top)
    {
        var (root, container, item) = Build("flex", c =>
        {
            if (property == "height")
                c.Height = "32px";
            else
                c.MinHeight = "32px";

            c.FlexWrap = "wrap";
            c.AlignContent = alignContent;
        }, "center");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(32, container.Size.Height, 1);
        Assert.Equal(top, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// Two 20px lines in a wrapping container 100px tall share its 60px of room: stretched, each
    /// is 50px tall and the second item, at its line's start, is 50px down; packed to the start,
    /// it is 20px down; centred, the lines start 30px down and the second item is 50px down.
    /// </summary>
    [Theory]
    [InlineData("normal", 0, 50)]
    [InlineData("flex-start", 0, 20)]
    [InlineData("center", 30, 50)]
    [InlineData("space-between", 0, 80)]
    public void Two_Lines_Share_The_Room_By_Align_Content(string alignContent, double firstTop, double secondTop)
    {
        var (root, container, first) = Build("flex", c =>
        {
            c.Width = "30px";
            c.Height = "100px";
            c.FlexWrap = "wrap";
            c.AlignContent = alignContent;
        }, "flex-start");
        var second = new CssBox(container, new HtmlTag("span", false, null), BaseUrl) { Display = "block", Width = "20px", Height = "20px" };
        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(firstTop, first.Location.Y - container.Location.Y, 1);
        Assert.Equal(secondTop, second.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// A single-line container 32px tall has no room beside its line for <c>align-content</c> to
    /// share: an item at its line's start stays at the top with <c>align-content: center</c>. The
    /// container's content was moved down by the room below it, 6px, as a block's is.
    /// </summary>
    [Fact]
    public void A_Single_Line_Container_Is_Not_Moved_By_Align_Content()
    {
        var (root, container, item) = Build("flex", c =>
        {
            c.Height = "32px";
            c.AlignContent = "center";
        }, "flex-start");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(0, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// Laid out a second time, the item is centred still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Place()
    {
        var (root, container, item) = Build("inline-flex", c => c.MinHeight = "32px", "center");
        root.PerformLayout(root.LayoutEnvironment);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(6, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: with <c>height: 32px</c>, a definite height, the item
    /// is centred, 6px down.
    /// </summary>
    [Fact]
    public void Control_A_Definite_Height()
    {
        var (root, container, item) = Build("flex", c => c.Height = "32px", "center");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(6, item.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// A flex container of the given display in a block in the root, styled by
    /// <paramref name="style"/> and with the given <c>align-items</c>, holding a 20px wide item
    /// of the given height, or holding an x if none. Returns the root, the container and the item.
    /// </summary>
    private static (CssBox Root, CssBox Container, CssBox Item) Build(string display, Action<CssBox> style,
        string? alignItems, int? itemHeight = 20)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "60px" };
        var container = new CssBox(block, new HtmlTag("a", false, null), BaseUrl) { Display = display };

        if (alignItems != null)
            container.AlignItems = alignItems;

        style(container);

        var item = new CssBox(container, new HtmlTag("span", false, null), BaseUrl) { Display = "block", Width = "20px" };
        if (itemHeight is int height)
        {
            item.Height = height + "px";
        }
        else
        {
            var word = new CssBox(item, null, BaseUrl) { Display = "inline", Text = "x".AsMemory() };
            word.ParseToWords();
        }

        FlexGridItemBlockification.Generate(root);
        return (root, container, item);
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
        public double Size => 12;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
