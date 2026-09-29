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
        // An inline flex or grid container is an atomic box too, moved here as a whole. It was
        // left where the flow put it, at the top of the line, whatever its alignment.
        if (b.Display is CssConstants.InlineBlock or "inline-flex" or "inline-grid")
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
