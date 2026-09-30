using Broiler.CSS;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace Broiler.Layout.Engine;

internal sealed class CssLineBox
{
    public CssLineBox(CssBox ownerBox)
    {
        Rectangles = [];
        RelatedBoxes = [];
        Words = [];
        OwnerBox = ownerBox;
        OwnerBox.LineBoxes.Add(this);
    }

    public List<CssBox> RelatedBoxes { get; }
    public List<CssRect> Words { get; }
    public CssBox OwnerBox { get; }
    public Dictionary<CssBox, RectangleF> Rectangles { get; }

    /// <summary>
    /// The inline boxes the flow put on the line with nothing in them, in the order it put them
    /// there, each at its <see cref="CssBoxProperties.Location"/>: the left of its content, at the
    /// top of the line.
    /// </summary>
    /// <remarks>
    /// They are not in <see cref="Rectangles"/> while the line is laid out, since a rectangle
    /// would make a line holding nothing else count as content (CssLayoutEngine.FlowBox); they get
    /// theirs once the line's baseline is placed (CssLayoutEngine.PlaceEmptyInlineBoxes).
    /// </remarks>
    internal List<EmptyInlineBox> EmptyInlineBoxes { get; } = [];

    /// <summary>
    /// The collapsible white space the flow has put on the line since the last word or box it
    /// placed on it whole: the space after that word, and any run of white space after that.
    /// </summary>
    internal double TrailingSpace { get; set; }

    /// <summary>
    /// How many words and boxes placed whole the flow has put on the line, not counting the forced
    /// break that starts it.
    /// </summary>
    internal int ContentCount { get; private set; }

    /// <summary>
    /// The inline boxes the flow opened on the line, in the order it opened them, and what it had
    /// put on the line when it did (CssLayoutEngine.CarryOpenedInlineBoxes).
    /// </summary>
    internal List<InlineBoxOpening> Openings { get; } = [];

    /// <summary>
    /// Notes that the flow put a word or a box whole on the line, with
    /// <paramref name="trailingSpace"/> of collapsible white space after it.
    /// </summary>
    internal void ReportContent(double trailingSpace)
    {
        // The white space since the last content lies before or after each empty inline box put on
        // the line since then.
        for (int i = EmptyInlineBoxes.Count - 1; i >= 0 && EmptyInlineBoxes[i].ContentBefore == ContentCount; i--)
        {
            var empty = EmptyInlineBoxes[i];
            EmptyInlineBoxes[i] = empty with { SpaceAfter = TrailingSpace - empty.SpaceBefore };
        }

        ContentCount++;
        TrailingSpace = trailingSpace;
    }

    /// <summary>
    /// Takes the line back to what the flow had put on it before it laid out a box out of the flow
    /// there: its words, white space and inline boxes are no part of the line.
    /// </summary>
    internal void RestoreFlowState(int contentCount, double trailingSpace, int openings)
    {
        ContentCount = contentCount;
        TrailingSpace = trailingSpace;

        if (Openings.Count > openings)
            Openings.RemoveRange(openings, Openings.Count - openings);
    }

    /// <summary>
    /// The top of the line, where the flow put it, before vertical alignment moved what is on it;
    /// null for a line made outside the flow.
    /// </summary>
    /// <remarks>
    /// What is on a line does not say where the line starts once it is aligned: an image stands
    /// on the strut's baseline from the start, and an inline-block moves down to it, below the
    /// line's top, when no text on the line starts there.
    /// </remarks>
    internal double? FlowTop { get; set; }

    /// <summary>
    /// Where <c>ApplyVerticalAlignment</c> put the line's baseline, and where moving the line or
    /// the box it belongs to has taken it since; null before the alignment has put it, or for a
    /// line with nothing on it.
    /// </summary>
    internal double? Baseline { get; set; }

    /// <summary>
    /// The bottom of the line, where the flow ended it, before vertical alignment moved what is on
    /// it: the lowest the flow had reached when it began the next line, or when it ended. Null for a
    /// line made outside the flow.
    /// </summary>
    /// <remarks>
    /// A float that does not fit beside what is on its line goes below the line (CSS 2.1 §9.5.1),
    /// which is here, not at the next line's top: that line may be moved further down, past floats
    /// it does not fit beside.
    /// </remarks>
    internal double? FlowBottom { get; set; }

    /// <summary>
    /// How far the line's top has moved down since the flow put it at <see cref="FlowTop"/>: by as
    /// much as content raised above the lines before it moved them (CSS2.1 §10.8.1).
    /// </summary>
    internal double RestackTop { get; set; }

    /// <summary>
    /// How far the line's bottom has moved down since the flow ended it at <see cref="FlowBottom"/>:
    /// <see cref="RestackTop"/>, and as much again as content raised above the line's own top.
    /// </summary>
    internal double RestackBottom { get; set; }

    public double LineBottom
    {
        get
        {
            double bottom = 0;

            foreach (var rect in Rectangles)
                bottom = Math.Max(bottom, rect.Value.Bottom);

            return bottom;
        }
    }

    internal void ReportExistanceOf(CssRect word)
    {
        if (!Words.Contains(word))
            Words.Add(word);

        if (!RelatedBoxes.Contains(word.OwnerBox))
            RelatedBoxes.Add(word.OwnerBox);
    }

    internal List<CssRect> WordsOf(CssBox box)
    {
        List<CssRect> r = [];

        foreach (CssRect word in Words)
            if (word.OwnerBox.Equals(box))
                r.Add(word);

        return r;
    }

    internal void UpdateRectangle(CssBox box, double x, double y, double r, double b)
    {
        double leftspacing = box.ActualBorderLeftWidth + box.ActualPaddingLeft;
        double rightspacing = box.ActualBorderRightWidth + box.ActualPaddingRight;
        double topspacing = box.ActualBorderTopWidth + box.ActualPaddingTop;
        double bottomspacing = box.ActualBorderBottomWidth + box.ActualPaddingBottom;

        // What the box holds reaches as high and as low in the inline box around it as it does in
        // this one: its own vertical padding and border lie outside its content area and take no
        // part in its parent's (CSS2.1 §10.6.1), where its horizontal ones take up the parent's line.
        // Carried out with the rest, they grew every inline box around it by as much, so a link
        // with no padding holding one with 10px painted 10px above and below its words.
        double contentTop = y;
        double contentBottom = b;

        if ((box.FirstHostingLineBox != null && box.FirstHostingLineBox.Equals(this)) || box.IsImage)
            x -= leftspacing;

        if ((box.LastHostingLineBox != null && box.LastHostingLineBox.Equals(this)) || box.IsImage)
            r += rightspacing;

        if (!box.IsImage)
        {
            y -= topspacing;
            b += bottomspacing;
        }

        if (!Rectangles.TryGetValue(box, out RectangleF f))
        {
            Rectangles.Add(box, RectangleF.FromLTRB((float)x, (float)y, (float)r, (float)b));
        }
        else
        {
            Rectangles[box] = RectangleF.FromLTRB(
                (float)Math.Min(f.X, x), (float)Math.Min(f.Y, y),
                (float)Math.Max(f.Right, r), (float)Math.Max(f.Bottom, b));
        }

        // An inline box's rectangle is part of the rectangles of the inline boxes it sits in, up to
        // the block this line belongs to and not into it. That block can itself be inline-level, an
        // inline-block or an absolutely positioned inline box laid out as a block, with its content
        // on lines of its own. Bubbling into it gave it a rectangle on its own line, as though it
        // were its own inline content, reaching out to its border edge: its lines then reached above
        // its content box by its top padding and border, and were moved down by as much, the box
        // with them. A `display: inline-block; padding: 40px` box holding a word came out 40px low
        // and 139px tall, where it is 96px tall at the top of its line. The inline boxes around it
        // got their rectangles from its lines too; CssLayoutEngine.BubbleAtomicInlineRectangles
        // gives them theirs on the line it sits on.
        if (box.ParentBox != null && box.ParentBox.IsInline && box.ParentBox != OwnerBox)
            UpdateRectangle(box.ParentBox, x, contentTop, r, contentBottom);
    }

    /// <summary>
    /// Gives <paramref name="box"/>, an inline box holding nothing on the line, its rectangle there:
    /// from <paramref name="x"/> to <paramref name="r"/>, the edges of its content, with its padding
    /// and border around them, and as tall as the content area <paramref name="contentArea"/> gives
    /// it. The inline boxes around it take it in along the line only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CSS 2.1 §10.6.1: an inline box's content area is as tall as its own font, whatever it holds.
    /// An inline box around the box keeps the height what else it holds on the line gives it, and
    /// one holding nothing else there gets its own content area. Fitted to the box as
    /// <see cref="UpdateRectangle"/> fits them to a word, a span of 16px text around an empty span in
    /// a 40px font was 46.4px tall, where browsers keep it 17px tall.
    /// </para>
    /// <para>
    /// One that takes no room on the line, and holds what it does on a later line, gets no
    /// rectangle here: browsers leave such a piece of it out of <c>getBoundingClientRect</c> and
    /// hit testing, and a link starting with an empty span at the end of a full line, its text
    /// wrapped to the next, was found across both lines, over the words of the first. The boxes
    /// around it still take it in: skipped with it, a link with <c>padding-left: 5px</c> around such
    /// a box had no piece on the line, and its padding was not drawn there, where browsers draw it.
    /// </para>
    /// <para>
    /// CSS 2.1 §9.4.2: the horizontal margins of the boxes on a line are respected between them, so
    /// an inline box's content holds the margin boxes of the inline boxes in it, and each box passes
    /// its margins at its edges on the line to the box around it. They were left out, so a link
    /// starting with an empty span with <c>margin-left: 5px</c> started at the span's border, 5px
    /// after browsers start it.
    /// </para>
    /// </remarks>
    internal void UpdateRectangleAlongLine(
        CssBox box, double x, double r, Func<CssBox, (double Top, double Bottom)> contentArea)
    {
        bool starts = box.FirstHostingLineBox != null && box.FirstHostingLineBox.Equals(this);
        bool ends = box.LastHostingLineBox != null && box.LastHostingLineBox.Equals(this);

        if (starts)
            x -= box.ActualBorderLeftWidth + box.ActualPaddingLeft;

        if (ends)
            r += box.ActualBorderRightWidth + box.ActualPaddingRight;

        if (Rectangles.TryGetValue(box, out RectangleF f))
        {
            Rectangles[box] = RectangleF.FromLTRB(
                (float)Math.Min(f.X, x), f.Top, (float)Math.Max(f.Right, r), f.Bottom);
        }
        else if (r > x || ends || box.LastHostingLineBox == null)
        {
            var (top, bottom) = contentArea(box);
            Rectangles.Add(box, RectangleF.FromLTRB(
                (float)x, (float)(top - box.ActualBorderTopWidth - box.ActualPaddingTop),
                (float)r, (float)(bottom + box.ActualBorderBottomWidth + box.ActualPaddingBottom)));
        }

        if (box.ParentBox != null && box.ParentBox.IsInline && box.ParentBox != OwnerBox)
        {
            UpdateRectangleAlongLine(box.ParentBox,
                starts ? x - OuterMargin(box.ActualMarginLeft) : x,
                ends ? r + OuterMargin(box.ActualMarginRight) : r,
                contentArea);
        }
    }

    /// <summary>
    /// The room a margin takes outside a box's border on its line. A negative one takes room back:
    /// the box around starts where the margin does, and the box's border sticks out before it, as an
    /// icon hung before a link's text by a negative margin does in browsers.
    /// </summary>
    private static double OuterMargin(double margin) => double.IsNaN(margin) ? 0 : margin;

    /// <summary>
    /// Projects this line's per-box rectangles onto the boxes, as the per-line map
    /// the paint walker and <c>FragmentTreeBuilder</c> read back.
    /// </summary>
    /// <remarks>
    /// The write overwrites rather than inserts, because the projection has to be
    /// idempotent: a line box can reach this twice. <c>CreateLineBoxes</c> empties
    /// <see cref="CssBox.LineBoxes"/> when it starts, so a layout pass that lands
    /// inside another one for the same block — a host callback made from inside the
    /// flow (text measurement, an image that completes synchronously) that re-enters
    /// <c>PerformLayout</c> — leaves the inner pass's already-assigned lines in the
    /// list, and the outer pass then walks the same list and re-projects them. An
    /// insert throws <see cref="ArgumentException"/> on the second one, which
    /// <c>PerformLayout</c> catches as a layout error, so the block loses the rest of
    /// its lines and everything below it lays out from a half-finished pass. The
    /// outer pass has just recomputed each line's rectangles (BubbleRectangles and
    /// vertical alignment run immediately before this), so overwriting is also what
    /// leaves the boxes agreeing with the line boxes they came from.
    /// </remarks>
    internal void AssignRectanglesToBoxes()
    {
        foreach (CssBox b in Rectangles.Keys)
            b.Rectangles[this] = Rectangles[b];
    }

    internal void SetBaseLine(CssBox b, double baseline)
    {
        //TODO: Aqui me quede, checar poniendo "by the" con un font-size de 3em
        List<CssRect> ws = WordsOf(b);

        if (!Rectangles.TryGetValue(b, out RectangleF r))
            return;

        // CSS 2.1 §10.8.1: For inline-block boxes, vertical-align adjusts
        // the position of the entire atomic box.  Move the box's rectangle
        // and its Location/ActualBottom directly.
        //
        // An inline flex, grid or table container is an atomic box too, moved here as a whole. It
        // was left where the flow put it, at the top of the line, whatever its alignment.
        if (b.Display is CssConstants.InlineBlock or "inline-flex" or "inline-grid" or CssConstants.InlineTable)
        {
            bool usesDefaultBaseline = string.IsNullOrEmpty(b.VerticalAlign)
                || b.VerticalAlign == CssConstants.Baseline;

            // The default `baseline` case used to return here, on the premise — written down in
            // the <img> branch below — that an inline-block's flow position is already on the
            // baseline. It is not: the flow puts it at the top of the line, exactly like an image.
            // That is only harmless while the box's baseline really is at its top, so the boxes
            // this skipped came out top-aligned; two inline-blocks of different heights on one
            // line, and an inline <svg> beside a taller one, sat with their tops flush instead of
            // their bottoms. A box whose baseline is its bottom margin edge
            // (CssBox.UsesBottomMarginEdgeBaseline) is moved here, and so is one with a line of
            // text in it, whose baseline is that line's (CssLayoutEngine.LastLineBaseline). Any
            // other keeps the position it has always had.
            if (b.Display == CssConstants.InlineBlock
                && usesDefaultBaseline && !b.UsesBottomMarginEdgeBaseline && CssLayoutEngine.LastLineBaseline(b) == null)
            {
                return;
            }

            double inlineBlockShift = baseline - r.Top;
            if (Math.Abs(inlineBlockShift) > 0.01)
            {
                // Moving the box has to move what is in it. Descendant positions are absolute, so
                // rewriting this box's own rectangle and Location alone left its content behind —
                // which no existing case noticed only because an inline-block being aligned had
                // nothing inside it to leave. OffsetTop walks the subtree; Location and
                // ActualBottom are then set to the aligned position, as they were before, because
                // the box's own placement is what the rest of the pass reads back.
                b.OffsetTop(inlineBlockShift);
                Rectangles[b] = new RectangleF(r.X, (float)baseline, r.Width, r.Height);
                b.Location = new PointF(b.Location.X, (float)baseline);
                b.ActualBottom = baseline + r.Height;
            }
            return;
        }

        // CSS2.1 §10.8: an inline replaced element is an atomic box too, and aligning it means
        // moving the box — paint reads an <img>'s geometry off the box (FragmentTreeBuilder reads
        // Location/ActualBottom and only the *source* rect off the word), so moving the word alone
        // moved nothing on screen. An image is placed by the flow at the line's top and always
        // needs this: a 30px and a 90px image on one line came out top-aligned rather than
        // standing on a shared baseline. The same is true of an atomic inline-block, which the
        // branch above now moves for the same reason. Re-running the alignment is
        // idempotent, because an atomic box that has been moved reports the same baseline it was
        // aligned to.
        if (b.IsImage)
        {
            double shift = baseline - r.Top;
            if (Math.Abs(shift) > 0.01)
            {
                Rectangles[b] = new RectangleF(r.X, (float)baseline, r.Width, r.Height);
                foreach (var word in ws)
                    word.Top += shift;
            }

            // The box stands where its rectangle does, moved or not, and is as wide and as tall.
            // It was put there only when the alignment moved it, so an image the flow had already
            // stood on the baseline, one beside text or alone on its line, kept the place the box
            // had before its line was laid out, the page's top-left corner. Only its top and
            // height were set: script, which reads the box in preference to the line's rectangle
            // once the box has a size, found an image beside text at the left edge of the page,
            // 0px wide.
            b.Location = new PointF(r.X, (float)baseline);
            b.Size = new SizeF(r.Width, r.Height);
            return;
        }

        //Save top of words related to the top of rectangle
        double gap = 0f;

        if (ws.Count > 0)
        {
            gap = ws[0].Top - r.Top;
        }
        else
        {
            CssRect firstw = CssBoxHelper.FirstWordOccourence(b, this);

            if (firstw != null)
                gap = firstw.Top - r.Top;
        }

        // The `baseline` parameter is the desired word.Top (visual text
        // top coordinate) already computed by ApplyVerticalAlignment.
        double newtop = baseline;

        // An inline box's rectangle on this line goes where its words go. It was moved only for a
        // box inside a taller inline box, so one in the block itself stayed at the top of the line
        // when the baseline brought its words down, and getBoundingClientRect, which unions these
        // rectangles, reported the line's top: beside an empty 30px inline-block, a span holding
        // "a" was 0px down its line, where its "a" was drawn 15.15px down and browsers report 15.
        // An atomic box has no words on this line to go with, and its place is not set here.
        if (b.Display == CssConstants.Inline
            || (b.ParentBox != null && b.ParentBox.Rectangles.ContainsKey(this) && r.Height < b.ParentBox.Rectangles[this].Height))
        {
            double recttop = newtop - gap;
            Rectangles[b] = new RectangleF(r.X, (float)recttop, r.Width, r.Height);
        }

        foreach (var word in ws)
        {
            if (!word.IsImage)
                word.Top = newtop;
        }
    }

    public override string ToString()
    {
        string[] ws = new string[Words.Count];

        for (int i = 0; i < ws.Length; i++)
            ws[i] = Words[i].Text;

        return string.Join(" ", ws);
    }
}

/// <summary>
/// An inline box the flow put on a line with nothing in it (<see cref="CssLineBox.EmptyInlineBoxes"/>).
/// </summary>
/// <param name="Box">The box.</param>
/// <param name="SpaceBefore">
/// The collapsible white space the flow put on the line between the word or box placed whole before
/// the box and the box (<see cref="CssLineBox.TrailingSpace"/>).
/// </param>
/// <param name="ContentBefore">
/// How many words and boxes placed whole the flow had put on the line before the box; when it had
/// put no more by the end of the line, nothing but white space and empty boxes follows the box there.
/// </param>
/// <param name="SpaceAfter">
/// The collapsible white space the flow put on the line between the box and the word or box it
/// placed whole after it (<see cref="CssLineBox.ReportContent"/>).
/// </param>
internal readonly record struct EmptyInlineBox(CssBox Box, double SpaceBefore, int ContentBefore, double SpaceAfter = 0);

/// <summary>An inline box the flow opened on a line (<see cref="CssLineBox.Openings"/>).</summary>
/// <param name="Box">The box.</param>
/// <param name="X">Where its margin box starts.</param>
/// <param name="ContentBefore">The line's <see cref="CssLineBox.ContentCount"/> when it opened.</param>
/// <param name="SpaceBefore">The line's <see cref="CssLineBox.TrailingSpace"/> when it opened.</param>
/// <param name="EmptyBefore">How many empty inline boxes the flow had put on the line when it opened.</param>
internal readonly record struct InlineBoxOpening(CssBox Box, double X, int ContentBefore, double SpaceBefore, int EmptyBefore);
