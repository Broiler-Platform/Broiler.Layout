using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.Layout.Diagnostics;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §9: an <c>inline-flex</c> column container lays its items out as a block-level one
/// does. They are stretched across it (§9.4 step 11) or aligned by <c>align-items</c> (§9.6),
/// stacked upwards under <c>column-reverse</c> (§9.5), and flexed into a definite height (§9.7).
/// </summary>
/// <remarks>
/// <para>
/// A column container is laid out by line layout, one item per line, and a few passes after it do
/// the rest. A block-level container runs them in <c>LayoutBlockChildren</c>. An inline-level one
/// is laid out by <c>CssLayoutEngine.FlowInlineBlock</c>, which stopped after line layout, so its
/// items kept the width of their own content. Nothing stretched them, <c>align-items</c> did
/// nothing, <c>column-reverse</c> stacked them in document order, and <c>flex-grow</c> left them
/// at their content height.
/// </para>
/// <para>
/// Serialized, because the cost test takes <see cref="LayoutWorkTrace"/>'s process-wide latch, the
/// way <c>LayoutWorkTraceTests</c> does.
/// </para>
/// </remarks>
[Collection(nameof(InlineFlexColumnTests))]
[CollectionDefinition(nameof(InlineFlexColumnTests), DisableParallelization = true)]
public sealed class InlineFlexColumnTests
{
    /// <summary>The container's left padding: its inline-start content edge.</summary>
    private const float ContentLeft = 10;

    private static readonly Uri BaseUrl = new("file:///inline-flex-column.html");

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

    /// <summary>An inline box holding <paramref name="count"/> 8×16 words, 4px apart.</summary>
    private static void Words(CssBox parent, int count)
    {
        var text = Box(parent, "inline", "span");
        for (int i = 0; i < count; i++)
            text.Words.Add(new CssRectWord(text, "X", i > 0, false));
    }

    /// <summary>
    /// A container on a line of its own, with 10px of left padding, holding a three-word item
    /// (32px of content) and a one-word item (8px).
    /// </summary>
    private static (CssBox Container, CssBox First, CssBox Second) Container(
        string display = "inline-flex", string direction = "column", string width = "600px",
        string height = "auto", string? alignItems = null, string? secondGrow = null)
    {
        var root = Root();
        var line = Box(root, "block");
        var container = Box(line, display, "span");
        container.FlexDirection = direction;
        container.Width = width;
        container.Height = height;
        container.PaddingLeft = "10px";
        if (alignItems != null)
            container.AlignItems = alignItems;

        var first = Box(container, "block", "section");
        Words(first, 3);
        var second = Box(container, "block", "section");
        Words(second, 1);
        if (secondGrow != null)
            second.FlexGrow = secondGrow;

        root.PerformLayout(root.LayoutEnvironment);
        return (container, first, second);
    }

    /// <summary>Both items fill the 600px content box; they were 32 and 8px wide.</summary>
    [Fact(Timeout = 600000)]
    public void Its_Items_Are_Stretched_Across_It()
    {
        var (_, first, second) = Container();

        Assert.Equal(ContentLeft, first.Location.X, 1);
        Assert.Equal(600, first.Size.Width, 1);
        Assert.Equal(ContentLeft, second.Location.X, 1);
        Assert.Equal(600, second.Size.Width, 1);
    }

    /// <summary>
    /// Sized by its content, the container is as wide as its widest item, and the narrower item
    /// is stretched to match it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Auto_Width_One_Stretches_Its_Items_To_The_Widest()
    {
        var (container, first, second) = Container(width: "auto");

        Assert.Equal(ContentLeft + 32, container.Size.Width, 1);
        Assert.Equal(32, first.Size.Width, 1);
        Assert.Equal(32, second.Size.Width, 1);
    }

    /// <summary><c>align-items</c> places the items across the content box instead.</summary>
    [Theory]
    [InlineData("center", ContentLeft + (600 - 32) / 2f, ContentLeft + (600 - 8) / 2f)]
    [InlineData("flex-end", ContentLeft + 600 - 32, ContentLeft + 600 - 8)]
    public void Align_Items_Places_Its_Items(string alignItems, float firstLeft, float secondLeft)
    {
        var (_, first, second) = Container(alignItems: alignItems);

        Assert.Equal(firstLeft, first.Location.X, 1);
        Assert.Equal(32, first.Size.Width, 1);
        Assert.Equal(secondLeft, second.Location.X, 1);
        Assert.Equal(8, second.Size.Width, 1);
    }

    /// <summary><c>column-reverse</c> stacks the second item above the first.</summary>
    [Fact(Timeout = 600000)]
    public void Column_Reverse_Stacks_Its_Items_Upwards()
    {
        var (container, first, second) = Container(direction: "column-reverse");

        Assert.Equal(container.Location.Y, second.Location.Y, 1);
        Assert.Equal(container.Location.Y + 16, first.Location.Y, 1);
    }

    /// <summary><c>flex-grow: 1</c> gives the second item the 84px the first leaves of 100.</summary>
    [Fact(Timeout = 600000)]
    public void Flex_Grow_Fills_A_Definite_Height()
    {
        var (container, first, second) = Container(height: "100px", secondGrow: "1");

        Assert.Equal(100, container.Size.Height, 1);
        Assert.Equal(16, first.Size.Height, 1);
        Assert.Equal(84, second.Size.Height, 1);
    }

    /// <summary>
    /// Twelve <c>display: flex</c> column containers nested in an inline-flex one, each beside a
    /// wider sibling, so that every level is stretched. Stretching lays an item out again at its
    /// new width, and a nested column container is left to the container that stretches it, which
    /// lays it out through block layout and so with the passes. Running them in line layout as
    /// well would lay out every level twice: 6,144 boxes for these twelve levels, where this takes
    /// 25.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Nested_Column_Containers_Are_Not_Laid_Out_Twice_Per_Level()
    {
        const int depth = 12;
        var root = Root();
        CssBox parent = Box(root, "block");
        CssBox top = null!;

        for (int level = 0; level < depth; level++)
        {
            var container = Box(parent, level == 0 ? "inline-flex" : "flex", level == 0 ? "span" : "div");
            container.FlexDirection = "column";
            top ??= container;

            var wider = Box(container, "block", "section");
            Words(wider, depth - level + 1);
            parent = container;
        }

        var leaf = Box(parent, "block", "section");
        Words(leaf, 1);

        LayoutWorkTrace.Reset();
        LayoutWorkTrace.Enabled = true;
        long laidOut;
        try
        {
            root.PerformLayout(root.LayoutEnvironment);
            laidOut = LayoutWorkTrace.Counts().GetValueOrDefault(LayoutWorkTrace.Counters.BoxesLaidOut);
        }
        finally
        {
            LayoutWorkTrace.Enabled = false;
            LayoutWorkTrace.Reset();
        }

        Assert.True(laidOut <= 3 * depth, $"{laidOut} boxes laid out for {depth} nested column containers");

        // And the inline-flex one did stretch its nested container to its widest item.
        var nested = top.Boxes[1];
        Assert.Equal(top.Boxes[0].Size.Width, nested.Size.Width, 1);
    }

    /// <summary>Control: a block-level column container already stretched its items.</summary>
    [Fact(Timeout = 600000)]
    public void A_Block_Level_Column_Container_Already_Stretched_Its_Items()
    {
        var (_, first, second) = Container(display: "flex");

        Assert.Equal(600, first.Size.Width, 1);
        Assert.Equal(600, second.Size.Width, 1);
    }

    /// <summary>
    /// Control: an inline-flex row lays its items out side by side at their content width, as
    /// before.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Inline_Flex_Row_Is_Unchanged()
    {
        var (_, first, second) = Container(direction: "row");

        Assert.Equal(ContentLeft, first.Location.X, 1);
        Assert.Equal(32, first.Size.Width, 1);
        Assert.Equal(ContentLeft + 32, second.Location.X, 1);
        Assert.Equal(first.Location.Y, second.Location.Y, 1);
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
