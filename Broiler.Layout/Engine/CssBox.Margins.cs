using Broiler.CSS;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;


namespace Broiler.Layout.Engine;

internal partial class CssBox : CssBoxProperties, IDisposable
{
    /// <summary>
    /// CSS Box Model 4 §6.2: Applies <c>margin-trim</c> to this box by zeroing
    /// the block-start margin of its first in-flow block-level child and/or the
    /// block-end margin of its last in-flow block-level child, as requested by
    /// the property value (<c>block</c>, <c>block-start</c>, <c>block-end</c>).
    /// Inline-axis trimming is not yet supported.
    /// </summary>
    private void ApplyMarginTrim()
    {
        if (string.IsNullOrEmpty(MarginTrim) || MarginTrim == CssConstants.None)
            return;

        bool trimBlockStart = false;
        bool trimBlockEnd = false;

        foreach (var token in MarginTrim.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "block":
                    trimBlockStart = true;
                    trimBlockEnd = true;
                    break;

                case "block-start":
                    trimBlockStart = true;
                    break;

                case "block-end":
                    trimBlockEnd = true;
                    break;
            }
        }

        if (!trimBlockStart && !trimBlockEnd)
            return;

        CssBox first = null;
        CssBox last = null;

        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None
                || child.Position == CssConstants.Absolute
                || child.Position == CssConstants.Fixed
                || child.Float != CssConstants.None
                || child.IsInline)
                continue;

            first ??= child;
            last = child;
        }

        if (trimBlockStart && first != null)
            first.MarginTop = "0";

        if (trimBlockEnd && last != null)
            last.MarginBottom = "0";
    }

    /// <summary>
    /// Clears the margin-collapse result this box carries from a previous layout pass, at the top of
    /// its next one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a layout needs this at all.</b> <see cref="CssBoxProperties.CollapsedMarginTop"/> is
    /// not an input; it is what <see cref="MarginTopCollapse"/> decided last time, kept so that a
    /// following sibling can subtract the part of a collapsed margin already spent and so that a
    /// first child can tell how much of its top margin the parent has already absorbed. Every writer
    /// sets it during a layout pass — and nothing cleared it between passes, so a <em>second</em>
    /// layout of the same box tree read the first pass's answer as if it were the parent's own
    /// margin.
    /// </para>
    /// <para>
    /// <b>What that cost.</b> The first in-flow child's top margin propagates by shifting the parent
    /// down (the branch below), and it does that only when the child's margin exceeds
    /// <c>max(parent margin, parent CollapsedMarginTop)</c>. On the first pass that comparison is
    /// against zero and the shift happens; on the second, the stale value equals the child's own
    /// margin, the comparison fails, and the shift does not — so the document lays out two pixels
    /// shorter the second time, for
    /// <c>&lt;div style="margin-top:2px"&gt;</c> directly inside <c>&lt;body&gt;</c>. Nothing in this
    /// repository laid the same tree out twice before item #14, which is why a pass-dependent result
    /// could sit here: every relayout rebuilt the tree from scratch and got a fresh box. The moment
    /// a rebuild can be skipped, "lay out the same tree again" has to mean what "build it and lay it
    /// out" meant, and <c>--relayout-parity</c> is what caught that it did not.
    /// </para>
    /// <para>
    /// Resetting at the top of the box's own layout is the point where no writer has run yet for this
    /// pass: a box's own value is written while it is being positioned, and the value a first child
    /// writes onto its <em>parent</em> is written after the parent's own layout has begun. Both are
    /// downstream of this.
    /// </para>
    /// </remarks>
    protected void ResetCollapsedMarginState()
    {
        CollapsedMarginTop = 0;
        _marginTopCollapsesWithParent = false;
        _negativeMarginTopAbove = 0;
    }

    /// <summary>
    /// The most negative of the margins that <see cref="MarginTopCollapse"/> collapsed together
    /// above this box's top edge on this layout pass, or zero. CSS2.1 §8.3.1 collapses adjoining
    /// margins to the largest positive one plus the most negative one; <see
    /// cref="CssBoxProperties.CollapsedMarginTop"/> is the positive side of the same set, and this
    /// the negative side.
    /// </summary>
    private double _negativeMarginTopAbove;

    /// <summary>
    /// The negative side of the set of margins collapsed above this box's top edge; see <see
    /// cref="_negativeMarginTopAbove"/>.
    /// </summary>
    internal double NegativeMarginTopAbove => _negativeMarginTopAbove;

    /// <summary>
    /// The margin spent above this box's top edge when it was placed: what the set of margins
    /// collapsed above it comes to, its positive side, <see
    /// cref="CssBoxProperties.CollapsedMarginTop"/>, plus its negative side. The box stands that
    /// far below where the set begins. An empty box hands the set on to the box after it, which
    /// goes as far as the set comes to with its own margins in it, less this.
    /// </summary>
    internal double MarginSpentAboveTop => CollapsedMarginTop + _negativeMarginTopAbove;

    /// <summary>
    /// The boxes a first child's top margin collapses through: its <paramref name="parent"/>, and
    /// the ancestors above it whose own top margins collapse with their parents' in turn, as far as
    /// a box <see cref="MarginTopCollapse"/> may move. The last is the one that moves.
    /// </summary>
    private static IEnumerable<CssBox> FirstChildMarginRun(CssBox parent)
    {
        var box = parent;
        yield return box;

        while (box._marginTopCollapsesWithParent && box.ParentBox is { ParentBox.ParentBox: not null } next)
        {
            box = next;
            yield return box;
        }
    }

    /// <summary>
    /// Moves the run of boxes that a first child's top margin collapses through, <see
    /// cref="FirstChildMarginRun"/> of <paramref name="parent"/>, by <paramref name="growth"/>: how
    /// much the child changes what the set of margins above the run comes to. Each box of the run
    /// gets the set's new <paramref name="positive"/> and <paramref name="negative"/> sides.
    /// </summary>
    private static void MoveFirstChildMarginRun(CssBox parent, double positive, double negative, double growth)
    {
        var moved = parent;

        foreach (var box in FirstChildMarginRun(parent))
        {
            box.CollapsedMarginTop = positive;
            box._negativeMarginTopAbove = negative;
            moved = box;
        }

        // Move what is already inside the parent with it. Only the parent's own origin used to
        // move, and anything positioned before this box got there — a preceding float, or a whole
        // subtree on a second layout pass — stayed where the old origin had put it, so the box's
        // content rendered outside its own border box. www.mediawiki.org's site notice did exactly
        // that: its border box moved down by the margin its first block child propagated, and the
        // notice text stayed above it.
        moved.OffsetTop(growth);
    }

    /// <summary>
    /// Whether this box's parent moves for a margin that collapses through its top: every parent
    /// but the root element's box and what is above it, which keep their established position.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_parentBox))]
    private bool ParentMovesWithItsMargin => _parentBox is { ParentBox.ParentBox: not null };

    /// <summary>
    /// Whether this box follows <paramref name="sibling"/> among their parent's children with
    /// nothing between them but boxes that generate nothing (<c>display: none</c>): no float and no
    /// absolutely positioned box.
    /// </summary>
    private bool FollowsWithNothingBetween(CssBox sibling)
    {
        if (_parentBox == null)
            return false;

        var siblings = _parentBox.Boxes;

        for (int i = siblings.IndexOf(this) - 1; i >= 0; i--)
        {
            if (siblings[i] == sibling)
                return true;

            if (siblings[i].Display != CssConstants.None)
                return false;
        }

        return false;
    }

    /// <summary>
    /// Whether <see cref="MarginTopCollapse"/> found this box's top margin collapsing with its
    /// parent's on this layout pass: the box is the parent's first in-flow child, or follows empty
    /// ones whose margins do, and nothing separates their top margins (CSS2.1 §8.3.1). A first child
    /// of this box whose margin is larger moves the topmost box of such a run rather than this one.
    /// </summary>
    private bool _marginTopCollapsesWithParent;

    protected double MarginTopCollapse(CssBoxProperties prevSibling)
    {
        double value;

        if (prevSibling != null)
        {
            // CSS2.1 §8.3.1: When the previous sibling is an "empty" box
            // (zero content height, no borders/padding, height auto/0), its
            // own top and bottom margins — and its children's margins —
            // collapse through.  The resulting collapsed margin participates
            // in collapsing with this element's top margin.
            //
            // Only a block-level box collapses through. An inline box, such as the text before a
            // float in its block, takes no part in margin collapsing and has no block position to
            // measure from: its ActualBottom stays 0, and the floor below, taken from it, lowered
            // the float by the whole of its block's distance from the top of the page.
            if (prevSibling is CssBox prevBox
                && prevBox.Display != CssConstants.Inline
                && CssBoxHelper.IsEmptyCollapsible(prevBox))
            {
                // The empty box's margins adjoin the ones collapsed above it as well as this box's,
                // so all of them are one set, and it starts where the set above the empty box
                // starts. They were left out: after a block with margin-bottom: 20px and an empty
                // <div>, a block began right below the first, where browsers begin it 20px below.
                // The set above includes what the empty box collapsed with its parent's margin,
                // so that is not cancelled either: www.mediawiki.org's empty #centralNotice sits
                // under its container's 24px margin, and the block after it at the container's top.
                double maxPos = Math.Max(prevBox.CollapsedMarginTop, Math.Max(ActualMarginTop, 0));
                double maxNeg = Math.Min(prevBox._negativeMarginTopAbove, Math.Min(ActualMarginTop, 0));
                CssBoxHelper.CollectEmptyBoxMargins(prevBox, ref maxPos, ref maxNeg);

                // The empty box stands as far below the start of the set as the part above it
                // came to, so this box goes the rest of the way. The rest is negative whenever
                // the set comes to less than was spent placing the empty box, which is how this
                // box lands *above* the empty one's border edge: Acid2's `.empty` (margin 6.25em,
                // one child with `margin-bottom: -6em`) spends 75px placing itself and collapses to
                // 3px, so the `.smile` after it belongs 72px higher — high enough for its `clear:
                // both` to take effect, without which the face was cut in half by a 15px gap.
                value = maxPos + maxNeg - prevBox.MarginSpentAboveTop;

                // A first in-flow child of *this* box collapses with the whole set, not with what
                // was left to add, or a margin one level down reappears as a gap.
                CollapsedMarginTop = maxPos;
                _negativeMarginTopAbove = maxNeg;

                // When the empty box's margins collapse with its parent's top margin, as the first
                // child's or after empty ones that do, so do this box's: the set is the one above
                // the parent, which moves by the rest, with this box at its top, as for a first
                // child. The rest was spent inside the parent instead: in <div><div
                // style="margin-bottom: 16px"></div><p>Text</p></div>, the outer <div> kept its
                // place and held the <p> 16px down, where browsers begin the outer <div> 16px lower
                // with the <p> at its top.
                //
                // Only an in-flow block that follows the empty box directly joins: a float's or an
                // absolutely positioned box's margins do not collapse (CSS2.1 §8.3.1), and one that
                // stands between the two is placed below the margins handed on already, where
                // browsers place it too, so moving the parent under it as well would move it twice.
                if (prevBox._marginTopCollapsesWithParent
                    && Float == CssConstants.None
                    && Position is not (CssConstants.Absolute or CssConstants.Fixed)
                    && FollowsWithNothingBetween(prevBox))
                {
                    _marginTopCollapsesWithParent = true;

                    if (ParentMovesWithItsMargin)
                    {
                        if (Math.Abs(value) > 0.1)
                            MoveFirstChildMarginRun(_parentBox, maxPos, maxNeg, value);

                        value = 0;
                    }
                }
            }
            else
            {
                // CSS2.1 §8.3.1: Adjoining vertical margins collapse.
                // When both are positive → max(m1, m2).
                // When one is negative  → max(positives,0) + min(negatives,0).
                // When both are negative → 0 + min(m1,m2) = most-negative.
                // The general formula covers all three cases.
                // Use GetPropagatedMarginBottom so that a last-child's
                // bottom margin propagates through its parent when the
                // parent has no bottom border/padding and auto height
                // (CSS 2.1 §8.3.1 parent-child bottom-margin collapse).
                double prevMb = (prevSibling is CssBox prevSibBox)
                    ? CssBoxHelper.GetPropagatedMarginBottom(prevSibBox)
                    : prevSibling.ActualMarginBottom;
                double maxPos = Math.Max(
                    Math.Max(prevMb, 0),
                    Math.Max(ActualMarginTop, 0));
                double minNeg = Math.Min(
                    Math.Min(prevMb, 0),
                    Math.Min(ActualMarginTop, 0));

                value = maxPos + minNeg;
                CollapsedMarginTop = maxPos;
                _negativeMarginTopAbove = minNeg;
            }
        }
        // CSS2.1 §8.3.1: "Margins of absolutely positioned boxes do not collapse." This branch is
        // reached by any box with no previous *in-flow* sibling — GetPreviousSibling already skips
        // out-of-flow ones — so an absolutely positioned first child was collapsing with its parent
        // and, worse, propagating its excess margin into the parent's Location below, which drags
        // every in-flow sibling down with it. A page whose first child is a fixed backdrop or an
        // abspos panel therefore laid its real content out at the wrong offset.
        else if (_parentBox != null && Position is not (CssConstants.Absolute or CssConstants.Fixed)
            // CSS2.1 §8.3.1 again, for the other kind of out-of-flow box: "margins of floating
            // boxes do not collapse". A float's margin was propagating into its parent's position
            // exactly as an abspos child's used to, so a block whose first child is a floated
            // element was drawn at the float's margin rather than its own — which is what left
            // css-flexbox/flexbox_item-bottom-float's *reference* an em below its test.
            && Float == CssConstants.None
            && _parentBox.ActualPaddingTop < 0.1 && _parentBox.ActualPaddingBottom < 0.1 && _parentBox.ActualBorderTopWidth < 0.1 && _parentBox.ActualBorderBottomWidth < 0.1
            // CSS2.1 §8.3.1: "margins of elements that establish new block formatting contexts
            // do not collapse with their in-flow children" — so a parent that establishes one
            // contains its first child's top margin instead of taking it as its own. The two
            // triggers spelled out here before, `overflow` other than `visible` (§9.4.1, e.g.
            // css-anchor-position anchor-center-scroll-001's scroller) and CSS Box Alignment
            // §5.4's `align-content`, are two of the set `EstablishesBfc` already answers for;
            // the ones it adds are what the rest of this run needed. A **float** parent: the
            // reference of css-flexbox/flex-lines/multi-line-wrap-with-column-reverse is three
            // floated columns of `margin-top: 10px` paragraphs, and each float was drawn at its
            // first paragraph's margin — then the next float below that, so the three columns
            // stepped 10px further down each. And a **flex or grid container**, which CSS
            // Flexbox §3 / CSS Grid §6 make an independent formatting context: that test itself
            // drew its whole flex container 10px below where it belongs.
            && !CssBoxHelper.EstablishesBfc(_parentBox))
        {
            _marginTopCollapsesWithParent = true;

            // CSS2.1 §8.3.1: First in-flow child's top margin collapses with the parent's top
            // margin when the parent has no top border and no top padding, so it joins the set
            // collapsed above the parent. The set's positive side is the largest positive margin
            // in it and its negative side the most negative one, each recorded on its own. What
            // the set came to after the parent's previous sibling stood for its positive side, so
            // a negative margin in it made a first child's margin look larger by as much: after a
            // block with margin-bottom: 10px, <div style="margin-top: -4px"><p style="margin-top:
            // 8px"> began 8px below it, where browsers begin it 6px below.
            double parentPositive = Math.Max(_parentBox.CollapsedMarginTop, Math.Max(_parentBox.ActualMarginTop, 0));
            double parentNegative = _parentBox._negativeMarginTopAbove;
            double positive = Math.Max(parentPositive, Math.Max(ActualMarginTop, 0));
            double negative = Math.Min(parentNegative, Math.Min(ActualMarginTop, 0));

            // How much this box's margin changes what the set comes to: as much as it is larger
            // than the positive side, or smaller than the negative side, and nothing when it is
            // neither.
            double growth = positive - parentPositive + (negative - parentNegative);

            // The parent moves by as much, except the root element's box and what is above it,
            // which keep their established position and take it inside instead.
            bool parentMoves = ParentMovesWithItsMargin;

            // The margin collapses through every ancestor whose own top margin collapses with its
            // parent's in turn, so what moves is the topmost of them that the condition above
            // would move, carrying the rest inside it, and each box of the run has the whole set
            // above its top edge. Moving the parent alone kept the margin inside the grandparent:
            // after a 10 px block, <div><div><p>Text</p></div></div> began right below it and held
            // the paragraph 16 px down, where browsers begin it 16 px down with the paragraph at
            // its top. A negative margin moves the run up the same way: after a 10 px block,
            // <div><p style="margin-top: -4px">Text</p></div> begins 4 px higher, over the block.
            if (parentMoves && Math.Abs(growth) > 0.1)
                MoveFirstChildMarginRun(_parentBox, positive, negative, growth);

            value = parentMoves ? 0 : growth;

            // Record the whole set above this box's top edge — not merely this box's own margin,
            // since the set is what positioned the parent. An empty-collapsible box hands its
            // margins on to the next sibling (the prevSibling branch above), and that sibling
            // subtracts what the set already spent, or it is applied twice. www.mediawiki.org
            // opens its article body with exactly that box — an empty <p> holding only a
            // <style> and two abspos spans, `margin: 0.5em 0 1em` — and its 1em collapse-through
            // was landing on top of the 1em already applied, pushing the whole article down.
            CollapsedMarginTop = positive;
            _negativeMarginTopAbove = negative;
        }
        else
        {
            // Nothing above this box's top edge collapses with its margin, so the margin is the
            // set, spent whole between the parent's content top and this box. It was recorded
            // only when the parent establishes a formatting context, and the empty first child
            // of a block with top padding handed its margin on to the block after it as though
            // none of it was spent: with a 10px margin, that block began 20px down.
            value = ActualMarginTop;
            CollapsedMarginTop = Math.Max(ActualMarginTop, 0);
            _negativeMarginTopAbove = Math.Min(ActualMarginTop, 0);
        }

        // fix for hr tag
        if (value < 0.1 && HtmlTag != null && HtmlTag.Name == "hr")
            value = GetEmHeight() * 1.1f;

        return value;
    }

    public bool BreakPage()
    {
        var container = LayoutEnvironment;

        if (Size.Height >= container.PageSize.Height)
            return false;

        var remTop = (Location.Y - container.MarginTop) % container.PageSize.Height;
        var remBottom = (ActualBottom - container.MarginTop) % container.PageSize.Height;

        if (remTop > remBottom)
        {
            var diff = container.PageSize.Height - remTop;
            Location = new PointF(Location.X, (float)(Location.Y + diff + 1));
            
            return true;
        }

        return false;
    }

    private double CalculateActualRight()
    {
        if (ActualRight <= 90999)
            return ActualRight;

        var maxRight = 0d;

        foreach (var box in Boxes)
            maxRight = Math.Max(maxRight, box.ActualRight + box.ActualMarginRight);

        return maxRight + ActualPaddingRight + ActualMarginRight + ActualBorderRightWidth;
    }

    private double MarginBottomCollapse()
    {
        double margin = 0;

        // NOTE: When the last in-flow child's bottom margin collapses through
        // this box (computed below, once the last child is known) the collapsed
        // margin is NOT included in this box's height — it is external spacing
        // propagated to the parent via GetPropagatedMarginBottom().  The
        // `margin` variable stays 0.

        // CSS2.1 §10.6.3 / §10.6.7: Floated children contribute to the
        // height of their parent only when the parent establishes a new
        // block formatting context (BFC).  Non-BFC blocks (e.g. a plain
        // <ul> inside a floated <dd>) must not include descendant floats
        // in their height calculation.
        bool isBfc = CssBoxHelper.EstablishesBfc(this);

        // Use the maximum ActualBottom across all children to handle
        // floated children that may not be the last in source order.
        // Initialize to the content-area top so that padding is preserved
        // even when all children are floated (CSS2.1 §10.6.3: content
        // height is zero but padding is additive).
        double maxChildBottom = Location.Y + ActualBorderTopWidth + ActualPaddingTop;
        CssBox lastInFlowChild = null;
        
        foreach (var child in Boxes)
        {
            // CSS2.1 §10.6.3: Only children in the normal flow are taken
            // into account.  Absolutely positioned and fixed-position boxes
            // are out of flow and must not influence the parent's auto height.
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;

            if (!isBfc && child.Float != CssConstants.None)
                continue;

            // CSS2.1 §9.4.3: Relative positioning is visual-only and
            // does not affect the flow position used for auto-height
            // calculation.  Undo the relative offset so the parent
            // measures the child's normal-flow bottom.
            double childBottom = child.ActualBottom;

            if (child.Position == CssConstants.Relative)
                childBottom -= CssBoxHelper.GetRelativeOffsetY(child);

            // An inline box is laid out in this box's lines, whose extent CreateLineBoxes has
            // already made this box's height, and it has no bottom of its own: its Location is not
            // kept by line layout, and OffsetTop moves it with every shift of the boxes around it.
            // Reading it gave a flex item holding one word, which a row centred 33.5px down, the
            // bottom of a text box shifted there twice, 67px down.
            if (child.Display != CssConstants.Inline)
                maxChildBottom = Math.Max(maxChildBottom, childBottom);

            lastInFlowChild = child;
        }

        // CSS2.1 §10.6.7: When a BFC root auto-sizes its height it must
        // extend to contain all descendant floats — not only direct-child
        // floats.  Walk the subtree (stopping at nested BFC boundaries)
        // to find the maximum float bottom.
        if (isBfc)
        {
            double maxFloatDesc = maxChildBottom;

            FindMaxDescendantFloatBottom(this, ref maxFloatDesc);
            maxChildBottom = Math.Max(maxChildBottom, maxFloatDesc);
        }

        // CSS2.1 §8.3.1 / §10.6.3: The auto height extends to the bottom
        // margin-edge of the last in-flow child unless that child's bottom
        // margin collapses through this box.  Collapse-through happens when
        // this box has no bottom border or padding, an auto (or
        // auto-resolved) height, and a block-level last in-flow child.  This
        // must match the condition used by GetPropagatedMarginBottom() (which
        // propagates the same margin to the parent): otherwise the child's
        // margin is double-counted — once inside this box's height and once as
        // external spacing.  Note this does NOT depend on whether this box is
        // its own parent's last child, nor on this box's own bottom margin.
        bool autoHeight = Height == CssConstants.Auto || string.IsNullOrEmpty(Height)
            || (Height.Contains('%')
                && (ContainingBlock == null || ContainingBlock.Height == CssConstants.Auto
                    || string.IsNullOrEmpty(ContainingBlock.Height)));

        bool collapseThrough = lastInFlowChild != null
            // CSS2.1 §8.3.1: the margins of a box that establishes a new block formatting context
            // do not collapse with its in-flow children's, so its last child's bottom margin stays
            // inside it, as its first child's top margin does.
            && !isBfc
            && ActualPaddingBottom < 0.1 && ActualBorderBottomWidth < 0.1
            && autoHeight
            // CSS2.1 §8.3.1: margins of the root element's box do not collapse, so the
            // body's bottom margin stays inside the root's height instead of propagating
            // out of it (where nothing would ever contain it, shortening the canvas).
            && !CssBoxHelper.IsRootElement(this)
            && lastInFlowChild.Float == CssConstants.None
            && lastInFlowChild.Display != CssConstants.Inline
            && lastInFlowChild.Display != CssConstants.InlineBlock;

        // Wherever the last child's bottom margin stays inside this box, for any of the reasons
        // above, what stays inside is that margin as it has collapsed with those of the child's
        // own last children (CSS2.1 §8.3.1), as GetPropagatedMarginBottom gives it: a paragraph's
        // margin that collapsed through an unpadded wrapper is the wrapper's margin here, not the
        // wrapper's own zero, as its top margin is the wrapper's at the top. Taking the wrapper's
        // own lost the paragraph's at a block with bottom padding: <div style="padding-bottom:
        // 1px"><div><p>Text</p></div></div> ended 1 px below the paragraph, where browsers end it
        // 17 px below.
        if (!collapseThrough && lastInFlowChild != null)
        {
            maxChildBottom += lastInFlowChild.Float == CssConstants.None
                && lastInFlowChild.Display is not (CssConstants.Inline or CssConstants.InlineBlock or CssConstants.None)
                    ? CssBoxHelper.GetPropagatedMarginBottom(lastInFlowChild)
                    : lastInFlowChild.ActualMarginBottom;
        }

        return Math.Max(ActualBottom, maxChildBottom + margin + ActualPaddingBottom + ActualBorderBottomWidth);
    }
}
