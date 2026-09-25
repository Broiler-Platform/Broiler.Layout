using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS 2.1 §9.6 and §10.6.7, CSS Flexbox §4.1: an absolutely or fixed positioned box is out of
/// flow, so it gives no height to the atomic inline-level box, or the column flex item, it sits in.
/// </summary>
/// <remarks>
/// An atomic inline-level box whose children are blocks is laid out as a block inside
/// <c>FlowInlineBlock</c>, which took the box's height from the lowest bottom among its children,
/// the positioned ones included. A column flex container flows its items through the same path, so
/// a <c>position: relative</c> item holding a tall positioned menu pushed every item after it down
/// by the menu's height, even though the item itself was re-laid out at its right size afterwards.
/// DuckDuckGo's home page is that shape (Broiler-Platform/Broiler.Browser#154): its
/// <c>&lt;main&gt;</c> is a column flex container whose first item, an 86px header, holds a
/// viewport-tall <c>position: fixed</c> off-canvas <c>&lt;nav&gt;</c>, and the hero after it
/// started at y=768, one whole empty screen down. Block flow and a row flex container never took
/// that path, and an item with a definite height takes its height from the declaration instead;
/// all three are pinned here as controls.
/// </remarks>
public sealed class OutOfFlowDescendantHeightTests
{
    private static readonly Uri BaseUrl = new("file:///out-of-flow-height.html");

    /// <param name="Container">The block the atomic box's line is in, or the flex container.</param>
    /// <param name="Holder">The inline-block, or the flex item, holding the out-of-flow box.</param>
    /// <param name="Next">The block after the line, or the flex item after the holder.</param>
    private sealed record Laid(CssBox Root, CssBox Container, CssBox Holder, CssBox? OutOfFlow, CssBox Next);

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

    /// <summary>An inline box holding one 8×16 word.</summary>
    private static void Word(CssBox parent)
    {
        var text = Box(parent, "inline", "span");
        text.Words.Add(new CssRectWord(text, "X", false, false));
    }

    /// <summary>
    /// A 40px block, then — unless <paramref name="position"/> is null — a 300px box positioned at
    /// the top-left corner of its containing block.
    /// </summary>
    private static CssBox? Contents(CssBox parent, string? position)
    {
        var inFlow = Box(parent, "block");
        inFlow.Height = "40px";
        Word(inFlow);

        if (position == null)
            return null;

        var outOfFlow = Box(parent, "block", "nav");
        outOfFlow.Position = position;
        outOfFlow.Top = "0";
        outOfFlow.Left = "0";
        outOfFlow.Width = "100px";
        outOfFlow.Height = "300px";
        Word(outOfFlow);
        return outOfFlow;
    }

    /// <summary>
    /// A block holding a relative <c>display: inline-block</c> 200px wide around
    /// <see cref="Contents"/>, and a block after it.
    /// </summary>
    private static Laid InlineBlockOnALine(string? position)
    {
        var root = Root();
        var line = Box(root, "block");
        var inlineBlock = Box(line, "inline-block", "span");
        inlineBlock.Position = "relative";
        inlineBlock.Width = "200px";
        var outOfFlow = Contents(inlineBlock, position);
        var next = Box(root, "block");
        Word(next);

        root.PerformLayout(root.LayoutEnvironment);
        return new(root, line, inlineBlock, outOfFlow, next);
    }

    /// <summary>
    /// A <c>&lt;main&gt;</c> flex container in <paramref name="direction"/> — or a plain block when
    /// that is null — holding a relative item around <see cref="Contents"/> and a one-word item
    /// after it.
    /// </summary>
    private static Laid ItemInAContainer(string? direction, string? position, string itemHeight = "auto")
    {
        var root = Root();
        var main = Box(root, direction == null ? "block" : "flex", "main");
        if (direction != null)
            main.FlexDirection = direction;

        var item = Box(main, "block");
        item.Position = "relative";
        item.Height = itemHeight;
        var outOfFlow = Contents(item, position);
        var next = Box(main, "block");
        Word(next);

        root.PerformLayout(root.LayoutEnvironment);
        return new(root, main, item, outOfFlow, next);
    }

    /// <summary>
    /// The inline-block is its 40px block tall, so its line and the block after that line are
    /// exactly where they are with no positioned child at all; they were 300px tall and 300px down.
    /// </summary>
    [Theory]
    [InlineData("absolute")]
    [InlineData("fixed")]
    public void An_Inline_Block_Takes_No_Height_From_A_Positioned_Child(string position)
    {
        var laid = InlineBlockOnALine(position);
        var alone = InlineBlockOnALine(null);

        Assert.Equal(40, laid.Holder.Size.Height, 1);
        Assert.Equal(alone.Container.Size.Height, laid.Container.Size.Height, 1);
        Assert.Equal(alone.Next.Location.Y, laid.Next.Location.Y, 1);
    }

    /// <summary>
    /// The item after the header starts at the header's 40px, not at the 300px its positioned
    /// child reaches down to.
    /// </summary>
    [Theory]
    [InlineData("absolute")]
    [InlineData("fixed")]
    public void A_Column_Flex_Item_Takes_No_Height_From_A_Positioned_Child(string position)
    {
        var laid = ItemInAContainer("column", position);

        Assert.Equal(40, laid.Holder.Size.Height, 1);
        Assert.Equal(laid.Holder.Location.Y + 40, laid.Next.Location.Y, 1);
        Assert.Equal(40 + 16, laid.Container.Size.Height, 1);
    }

    /// <summary>
    /// DuckDuckGo's shape at its sizes: a 1024×768 viewport, an 86px header and a fixed menu as
    /// tall as the viewport. The hero belongs at y=86; it was at y=768.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Viewport_Tall_Fixed_Menu_Leaves_The_Hero_Under_The_Header()
    {
        var root = Root();
        var main = Box(root, "flex", "main");
        main.FlexDirection = "column";
        var header = Box(main, "block");
        header.Position = "relative";
        var bar = Box(header, "block");
        bar.Height = "86px";
        Word(bar);
        var menu = Box(header, "block", "nav");
        menu.Position = "fixed";
        menu.Top = "0";
        menu.Right = "0";
        menu.Width = "259px";
        menu.Height = "100%";
        Word(menu);
        var hero = Box(main, "block", "section");
        Word(hero);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(768, menu.Size.Height, 1);
        Assert.Equal(86, header.Size.Height, 1);
        Assert.Equal(86, hero.Location.Y, 1);
    }

    /// <summary>
    /// The positioned box itself is untouched: 300px tall at the top of the box that contains it,
    /// and still part of the document's scrollable size, which is what its own layout reports.
    /// </summary>
    [Theory]
    [InlineData("inline-block")]
    [InlineData("column flex item")]
    public void The_Positioned_Child_Keeps_Its_Box_And_Its_Scrollable_Overflow(string holder)
    {
        var laid = holder == "inline-block"
            ? InlineBlockOnALine("absolute")
            : ItemInAContainer("column", "absolute");
        var outOfFlow = laid.OutOfFlow!;

        Assert.Equal(300, outOfFlow.Size.Height, 1);
        Assert.Equal(laid.Holder.Location.Y, outOfFlow.Location.Y, 1);
        Assert.True(
            laid.Root.LayoutEnvironment.ActualSize.Height >= outOfFlow.Location.Y + 300,
            $"The scrollable height {laid.Root.LayoutEnvironment.ActualSize.Height} no longer reaches the positioned box's bottom.");
    }

    /// <summary>Control: in block flow the item is a plain block, and was always 40px.</summary>
    [Fact(Timeout = 600000)]
    public void Block_Flow_Already_Took_No_Height_From_It()
    {
        var laid = ItemInAContainer(direction: null, "absolute");

        Assert.Equal(40, laid.Holder.Size.Height, 1);
        Assert.Equal(laid.Holder.Location.Y + 40, laid.Next.Location.Y, 1);
    }

    /// <summary>Control: a column flex item with a definite height is sized by it.</summary>
    [Fact(Timeout = 600000)]
    public void A_Definite_Height_Column_Flex_Item_Already_Took_No_Height_From_It()
    {
        var laid = ItemInAContainer("column", "absolute", itemHeight: "40px");

        Assert.Equal(40, laid.Holder.Size.Height, 1);
        Assert.Equal(laid.Holder.Location.Y + 40, laid.Next.Location.Y, 1);
    }

    /// <summary>
    /// Control: a row flex container lays its items out as blocks, side by side, and is as tall as
    /// the tallest of them.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Row_Flex_Container_Already_Took_No_Height_From_It()
    {
        var laid = ItemInAContainer("row", "absolute");

        Assert.Equal(40, laid.Holder.Size.Height, 1);
        Assert.Equal(40, laid.Container.Size.Height, 1);
    }

    // Minimal ILayoutEnvironment: every word 8px wide and 16px tall, a space 4px, and
    // DuckDuckGo's 1024×768 viewport.
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
