using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.Layout.Diagnostics;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A column flex container that is itself a flex or grid item lays its items out by the column
/// passes, row gaps, flexing, alignment and <c>column-reverse</c>, whether or not its container
/// stretches it.
/// </summary>
/// <remarks>
/// <para>
/// An item of a column flex container or of a grid is laid out through line layout
/// (<c>CssLayoutEngine.FlowInlineBlock</c>), which only stacks the items of a column container.
/// The column passes that do the rest ran there for an <c>inline-flex</c> container alone: a
/// <c>display: flex</c> one was left to its container, which lays an item out again through block
/// layout, and so with the passes, when it stretches it. One it does not stretch never had them:
/// an item its container aligns <c>flex-start</c> or <c>center</c>, one with a width or an
/// <c>auto</c> margin of its own, and every item of a grid, which stretches an item without laying
/// it out again. Their row gaps were missing, their items did not flex, align or reverse.
/// </para>
/// <para>
/// Each nested column container here is 320px wide at most and holds three 10px blocks. A word is
/// 8×16px.
/// </para>
/// <para>
/// Serialized, because the cost test takes <see cref="LayoutWorkTrace"/>'s process-wide latch, the
/// way <c>LayoutWorkTraceTests</c> does.
/// </para>
/// </remarks>
[Collection(nameof(NestedColumnFlexPassesTests))]
[CollectionDefinition(nameof(NestedColumnFlexPassesTests), DisableParallelization = true)]
public sealed class NestedColumnFlexPassesTests
{
    private static readonly Uri BaseUrl = new("file:///nested-column-flex-passes.html");

    /// <summary>
    /// A 10px row gap separates the three items of a column container that its container does not
    /// stretch, at 0, 20 and 40, and the container is 50px tall, with what follows it placed after
    /// it: when its column container aligns it <c>flex-start</c> or <c>center</c>, when it has a
    /// width of its own or <c>auto</c> side margins, and when it is a grid item. The items were at
    /// 0, 10 and 20, and what followed started at 36.
    /// </summary>
    [Theory]
    [InlineData("flex-start")]
    [InlineData("center")]
    [InlineData("width")]
    [InlineData("auto margins")]
    [InlineData("grid")]
    public void An_Unstretched_Nested_Column_Places_Its_Row_Gaps(string layout)
    {
        var (_, nested, items, next) = Lay(layout, nested => nested.RowGap = "10px");

        AssertTops(nested, items, 0, 20, 40);
        Assert.Equal(50, nested.Size.Height, 1);
        Assert.Equal(nested.ActualBottom, next.Location.Y, 1);
    }

    /// <summary>
    /// In a 100px-tall nested column that its container aligns <c>flex-start</c>, a
    /// <c>flex-grow: 1</c> item takes the 70px left over: 80px tall, and the others follow it at 80
    /// and 90. It stayed 10px tall.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Unstretched_Nested_Column_Flexes_Its_Items()
    {
        var (_, nested, items, _) = Lay("flex-start", nested => nested.Height = "100px",
            styleItem: (i, item) => item.FlexGrow = i == 0 ? "1" : "0");

        Assert.Equal(80, items[0].Size.Height, 1);
        AssertTops(nested, items, 0, 80, 90);
    }

    /// <summary>
    /// A 200px-wide nested column with <c>align-items: center</c>, which its container aligns
    /// <c>flex-start</c>, centres its 50px items at 75. They were at 0.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Unstretched_Nested_Column_Aligns_Its_Items()
    {
        var (_, nested, items, _) = Lay("flex-start", nested =>
        {
            nested.Width = "200px";
            nested.AlignItems = "center";
        }, styleItem: (_, item) => item.Width = "50px");

        foreach (var item in items)
            Assert.Equal(75, item.Location.X - nested.Location.X, 1);
    }

    /// <summary>
    /// A nested <c>column-reverse</c> container that its container aligns <c>flex-start</c> puts
    /// its first item last: at 20, 10 and 0. They were in document order, at 0, 10 and 20.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Unstretched_Nested_Column_Reverses_Its_Items()
    {
        var (_, nested, items, _) = Lay("flex-start", nested => nested.FlexDirection = "column-reverse",
            styleItem: (_, item) => item.Width = "50px");

        AssertTops(nested, items, 20, 10, 0);
    }

    /// <summary>
    /// Controls, which pass before and after: a nested column that its container stretches, one in a
    /// row flex container, and a block-level column container in a block each place their row gaps,
    /// with their items at 0, 20 and 40.
    /// </summary>
    [Theory]
    [InlineData("stretch")]
    [InlineData("row")]
    [InlineData("block")]
    public void Control_A_Column_Laid_Out_Through_Block_Layout_Places_Its_Row_Gaps(string layout)
    {
        var (_, nested, items, _) = Lay(layout, nested => nested.RowGap = "10px");

        AssertTops(nested, items, 0, 20, 40);
        Assert.Equal(50, nested.Size.Height, 1);
    }

    /// <summary>
    /// The passes of a column container wait, inside an item its container will stretch, for the
    /// layout that stretch does. They stretch the items of the container they run for, and each
    /// stretch lays an item out again, so run in the first layout as well they would double the work
    /// at every stretched level of a nest. Twelve and twenty-four column containers nested in an
    /// inline-flex one, aligned in turn <c>flex-start</c> and <c>stretch</c>, take at most three
    /// layouts a level; running the passes in the first layout took 65 and 4,097.
    /// </summary>
    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    public void Nested_Columns_Aligned_In_Turn_Are_Not_Laid_Out_Twice_Per_Level(int depth)
    {
        var root = Root();
        CssBox parent = Box(root, "block");

        for (int level = 0; level < depth; level++)
        {
            var container = Box(parent, level == 0 ? "inline-flex" : "flex", level == 0 ? "span" : "div");
            container.FlexDirection = "column";
            if (level % 2 == 0)
                container.AlignItems = "flex-start";

            var wider = Box(container, "block", "section");
            for (int i = 0; i < depth - level + 1; i++)
                Word(wider);

            parent = container;
        }

        Word(Box(parent, "block", "section"));

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
    }

    private static void AssertTops(CssBox nested, CssBox[] items, params float[] tops)
    {
        for (int i = 0; i < items.Length; i++)
            Assert.Equal(tops[i], items[i].Location.Y - nested.Location.Y, 1);
    }

    /// <summary>
    /// A 320px container holding a column flex container, styled by <paramref name="styleNested"/>,
    /// with three 10px blocks styled by <paramref name="styleItem"/> with their index, and a 5px
    /// block after it. The outer container is laid out as <paramref name="layout"/> names:
    /// a column flex container aligning its items <c>flex-start</c>, <c>center</c> or by default
    /// (<c>stretch</c>, also for <c>width</c>, which gives the nested column a 100px width, and
    /// <c>auto margins</c>), a grid, a row flex container, or a block.
    /// </summary>
    private static (CssBox Outer, CssBox Nested, CssBox[] Items, CssBox Next) Lay(
        string layout, Action<CssBox> styleNested, Action<int, CssBox>? styleItem = null)
    {
        var root = Root();
        var outer = Box(root, layout switch
        {
            "grid" => "grid",
            "block" => "block",
            _ => "flex",
        });
        outer.Width = "320px";

        if (layout is not ("grid" or "row" or "block"))
            outer.FlexDirection = "column";

        if (layout is "flex-start" or "center")
            outer.AlignItems = layout;

        var nested = Box(outer, "flex", "section");
        nested.FlexDirection = "column";
        if (layout == "width")
            nested.Width = "100px";
        if (layout == "auto margins")
            nested.MarginLeft = nested.MarginRight = "auto";
        styleNested(nested);

        var items = new CssBox[3];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = Box(nested, "block", "i");
            items[i].Height = "10px";
            styleItem?.Invoke(i, items[i]);
        }

        var next = Box(outer, "block", "b");
        next.Height = "5px";
        next.Width = "300px";

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return (outer, nested, items, next);
    }

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

    private static void Word(CssBox parent)
    {
        var text = Box(parent, "inline", "span");
        text.Words.Add(new CssRectWord(text, "X", false, false));
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
