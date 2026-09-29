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
        ClearsFloats = false;
        _placedBelowHandedOnSet = false;
        _marginSeparation = null;
        PrecedingFloatsBottom = double.PositiveInfinity;
        FloatHeldDown = true;
        FloatsBesideBottom = double.NegativeInfinity;
    }

    /// <summary>
    /// Whether <c>clear</c> found floats for this box to clear on this layout pass (CSS2.1 §9.5.2)
    /// that reach below its parent's content top, whether or not it had to move the box down past
    /// them.
    /// </summary>
    /// <remarks>
    /// An empty box with clearance does not hand its margins on to its parent's bottom margin
    /// (§8.3.1): they stay inside the parent. Chromium has it so wherever the box has floats to
    /// clear that reach into its parent, even ones that end above the box: www.mediawiki.org's
    /// <c>#mw-content-text</c> ends with a clearfix's empty <c>::after</c>, below the 16px bottom
    /// margin of the article's last paragraph, and its thumbnail floats well above that. Where
    /// the floats to clear end above the parent, or there are none, the box's margins collapse
    /// through it as any empty box's do.
    /// </remarks>
    internal bool ClearsFloats { get; private set; }

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
    /// much <paramref name="child"/> changes what the set of margins above the run comes to. Each box
    /// of the run gets the set's new <paramref name="positive"/> and <paramref name="negative"/>
    /// sides.
    /// </summary>
    /// <remarks>
    /// What the run holds before the child goes with it, but for the out-of-flow boxes whose place
    /// does not follow the run's (CSS2.1 §9.5.1, §10.6.4). A float there is one the set places,
    /// where it ends, at the top of the box it is in. The floats after an empty block whose margins
    /// collapse with its parent's (see <see cref="_placedBelowHandedOnSet"/>), such as those
    /// between it and the box after it, were placed below the part of the set it handed on, so they
    /// are placed again where the set ends now, by the float rules (see <see
    /// cref="PlaceFloatAtParentTop"/>), and so are the floats after them, which they may hold down
    /// or aside. The floats before them were placed where the set ended, and the run's move takes
    /// them where the float rules place them now, unless a float before the run, outside it,
    /// reaches down to where the run is or goes: that one stays where it is and may hold them down
    /// or aside, so then they are placed again, in document order, from the first that the move
    /// takes out of the reach of a float that held it down or stood beside it, or all of them when
    /// the run moves up or one of them has <c>clear</c>. A float on a line of inline content, or in
    /// a box that <c>position: relative</c> has shifted already, goes with the run: the rules place
    /// it from where the line or the box was before, which the float no longer knows. Placed again
    /// from where the box was shifted to, after a 100 × 50px float, a float in
    /// <c>&lt;div&gt;&lt;div style="position: relative; top:
    /// 20px"&gt;&lt;div&gt;&lt;/div&gt;&lt;div style="float: left"&gt;&lt;/div&gt;&lt;/div&gt;&lt;p
    /// style="margin-top: 30px"&gt;</c> went to the left edge, where browsers place it beside the
    /// big float, 100px across, before they shift it 20px down. Moved with the run, a float after
    /// an empty block kept the part of the set it had been placed below: after a 10px block, in
    /// <c>&lt;div&gt;&lt;div&gt;&lt;div style="margin-bottom: 10px"&gt;&lt;/div&gt;&lt;div
    /// style="float: left"&gt;&lt;/div&gt;&lt;/div&gt;&lt;p&gt;</c> it went 10px below the outer
    /// <c>&lt;div&gt;</c>'s top, where browsers put it at the top, with the <c>&lt;p&gt;</c>. An
    /// absolutely positioned box that its <c>top</c> or <c>bottom</c> place in a containing block
    /// outside the run stays where they put it, and a fixed box at its static position goes with
    /// the run (see <see cref="OffsetWithBoxesApart"/>). Moved with the run, a float that a float
    /// outside it held below it went down by the margin as well, and so did a box with <c>top:
    /// 5px</c> in the page: after a 300 × 40px float, in <c>&lt;div&gt;&lt;div style="float:
    /// left"&gt;&lt;/div&gt;&lt;p style="margin-top: 30px"&gt;</c> the float went 30px below the
    /// big one, and <c>&lt;div&gt;&lt;div style="position: absolute; top: 5px"&gt;&lt;/div&gt;&lt;p
    /// style="margin-top: 30px"&gt;</c> put the box 35px down the page, where browsers put them
    /// right below the big float and 5px down.
    /// </remarks>
    private static void MoveFirstChildMarginRun(CssBox parent, CssBox child, double positive, double negative,
        double growth)
    {
        List<CssBox> run = [];

        foreach (var box in FirstChildMarginRun(parent))
        {
            box.CollapsedMarginTop = positive;
            box._negativeMarginTopAbove = negative;
            run.Add(box);
        }

        var moved = run[^1];
        List<CssBox> floats = [];
        List<CssBox> apart = [];

        foreach (var box in BoxesBeforeInRun(run, child))
        {
            CssBoxHelper.CollectFloatsInSubtree(box, floats);
            CollectBoxesApart(box, moved, apart);
        }

        // The first float the run holds, in document order: the floats before it are those before
        // the run (see FloatBeforeReachesBelow).
        var first = floats.Count > 0 ? floats[0] : null;

        // The floats that go with the run in any case (see above).
        floats.RemoveAll(box => IsPlacedByOffsets(box) || box.InlineFloatTopFloor is not null
            || IsInShiftedBox(box, run));

        // The first float placed below a part of the set that was handed on; it and the floats
        // after it are placed again.
        int again = floats.FindIndex(box => box._placedBelowHandedOnSet);

        if (again < 0)
            again = floats.Count;

        // The highest the float rules may place a float in the run, where the run is and where it
        // goes: at its top, less a negative top margin of the float's.
        double lowestMargin = 0;

        foreach (var box in floats)
            lowestMargin = Math.Min(lowestMargin, box.ActualMarginTop);

        double highest = Math.Min(moved.Location.Y, moved.Location.Y + growth) + lowestMargin;
        bool clears = floats.FindIndex(0, again, box => box.Clear is not (null or CssConstants.None)) >= 0;

        if (again > 0 && FloatBeforeReachesBelow(moved, highest, first!, clears))
        {
            // Moved down, the floats that keep their places among the floats around them go with
            // the run (see KeepsItsPlaceMovedDown), up to the first that does not.
            int held = growth >= 0 && !clears
                ? floats.FindIndex(0, again, box => !box.KeepsItsPlaceMovedDown(growth))
                : 0;

            if (held >= 0)
                again = held;
        }

        // Move what is already inside the parent with it. Only the parent's own origin used to
        // move, and anything positioned before this box got there — a preceding float, or a whole
        // subtree on a second layout pass — stayed where the old origin had put it, so the box's
        // content rendered outside its own border box. www.mediawiki.org's site notice did exactly
        // that: its border box moved down by the margin its first block child propagated, and the
        // notice text stayed above it.
        moved.OffsetWithBoxesApart(0, growth, apart);

        for (int i = again; i < floats.Count; i++)
            floats[i].PlaceFloatAtParentTop();
    }

    /// <summary>
    /// Whether <paramref name="box"/> is in a box that <c>position: relative</c> has shifted already
    /// (CSS2.1 §9.4.3), inside the boxes of the <paramref name="run"/>, which are still being laid
    /// out and are not shifted yet.
    /// </summary>
    private static bool IsInShiftedBox(CssBox box, List<CssBox> run)
    {
        for (var ancestor = box.ParentBox; ancestor != null && !run.Contains(ancestor);
            ancestor = ancestor.ParentBox)
        {
            var (dx, dy) = ancestor.RelativePositionOffset();

            if (dx != 0 || dy != 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a float before <paramref name="box"/> in its block formatting context, outside it,
    /// reaches below <paramref name="top"/>, where it may hold a float in <paramref name="box"/>
    /// placed below that down or aside (CSS2.1 §9.5.1): with its border box, which is what the float
    /// rules keep floats off (see <see cref="PlaceFloat"/>), or with its margin box, below which a
    /// float with <c>clear</c> goes (§9.5.2), when one of the floats in the box <paramref
    /// name="clears"/>.
    /// </summary>
    /// <remarks>
    /// The floats before <paramref name="first"/>, the first float in <paramref name="box"/>, are
    /// those before the box, and they stay where they are while the box is laid out, so their border
    /// boxes reach as far down as they did when <paramref name="first"/> was first placed, <see
    /// cref="PrecedingFloatsBottom"/>. Walking them for every move of the box made a page of 2000
    /// articles, each a floated thumbnail and a heading with a top margin, take 16.6s to lay out,
    /// where it took 14.3s before; and as each thumbnail's bottom margin reached past the top of the
    /// next article, each thumbnail after it was placed again.
    /// </remarks>
    private static bool FloatBeforeReachesBelow(CssBox box, double top, CssBox first, bool clears)
    {
        if (!clears && !double.IsPositiveInfinity(first.PrecedingFloatsBottom))
            return first.PrecedingFloatsBottom > top + 0.01;

        return CssBoxHelper.CollectPrecedingFloatsInBfc(box).Exists(floatBox =>
            FloatBottom(floatBox, withMargin: clears) > top + 0.01);
    }

    /// <summary>
    /// How far down <paramref name="floatBox"/> reaches: the bottom of its border box, as <see
    /// cref="PlaceFloat"/> measures it, or <paramref name="withMargin"/> of its margin box, as <see
    /// cref="CssBoxHelper.GetMaxFloatBottom"/> does.
    /// </summary>
    private static double FloatBottom(CssBox floatBox, bool withMargin) => withMargin
        ? Math.Max(floatBox.ActualBottom, floatBox.Location.Y + floatBox.ActualHeight + floatBox.ActualPaddingTop
            + floatBox.ActualPaddingBottom + floatBox.ActualBorderTopWidth + floatBox.ActualBorderBottomWidth)
            + Math.Max(floatBox.ActualMarginBottom, 0)
        : floatBox.ActualBottom;

    /// <summary>
    /// How far down the floats before this float in its block formatting context reached with their
    /// border boxes when it was first placed on this layout pass (see <see cref="FloatBottom"/>):
    /// nothing when there were none, and further than anything until it is placed.
    /// </summary>
    private double PrecedingFloatsBottom { get; set; } = double.PositiveInfinity;

    /// <summary>
    /// Whether the floats before this float held it lower than where it stood when <see
    /// cref="PlaceFloat"/> last placed it, at its containing block's top, the top of the float
    /// before it or the line the content before it reached (CSS2.1 §9.5.1).
    /// </summary>
    private bool FloatHeldDown { get; set; } = true;

    /// <summary>
    /// How far down the border boxes of the floats beside this float reached when <see
    /// cref="PlaceFloat"/> last placed it, those before it whose band it overlapped: the nearest
    /// bottom of them, further down than anything when there were none, and nothing until it is
    /// placed.
    /// </summary>
    private double FloatsBesideBottom { get; set; } = double.NegativeInfinity;

    /// <summary>
    /// Whether the float rules would place this float, laid out already, <paramref name="down"/>
    /// further down when the boxes it is in and the floats in them move so far, with the floats
    /// before them staying where they are (CSS2.1 §9.5.1): when no float held it down, and every
    /// float beside it reaches below where it goes. Moved down, a float's band can only lose the
    /// floats before it that overlap it, since none of them begins below its top (rule 6), and with
    /// the same ones beside it, it goes as far across and stands where its containing block's top,
    /// or the float before it, puts it, as before.
    /// </summary>
    private bool KeepsItsPlaceMovedDown(double down) =>
        !FloatHeldDown && FloatsBesideBottom > Location.Y - RelativePositionOffset().Y + down + 0.01;

    /// <summary>
    /// What the <paramref name="run"/> of boxes a first child's top margin collapses through (<see
    /// cref="FirstChildMarginRun"/>) holds before <paramref name="child"/>, laid out already, in
    /// document order: in each box of the run, from the topmost down, the boxes before the next box
    /// of the run, or before the child.
    /// </summary>
    private static IEnumerable<CssBox> BoxesBeforeInRun(List<CssBox> run, CssBox child)
    {
        for (int i = run.Count - 1; i >= 0; i--)
        {
            var next = i > 0 ? run[i - 1] : child;

            foreach (var box in run[i].Boxes)
            {
                if (box == next)
                    break;

                if (box.Display != CssConstants.None)
                    yield return box;
            }
        }
    }

    /// <summary>
    /// Adds to <paramref name="apart"/> <paramref name="box"/>, or the out-of-flow boxes in it, whose
    /// place does not simply follow <paramref name="moving"/>'s when it moves (see <see
    /// cref="OffsetWithBoxesApart"/>): the absolutely positioned ones whose containing block is
    /// outside it, and the fixed ones. What is in such a box goes with it, so it is not looked into.
    /// </summary>
    private static void CollectBoxesApart(CssBox box, CssBox moving, List<CssBox> apart)
    {
        if (box.Display == CssConstants.None)
            return;

        if (box.Position is CssConstants.Absolute or CssConstants.Fixed)
        {
            if (box.Position == CssConstants.Fixed || !IsWithin(box.FindPositionedContainingBlock(), moving))
                apart.Add(box);

            return;
        }

        foreach (var child in box.Boxes)
            CollectBoxesApart(child, moving, apart);
    }

    /// <summary>
    /// Moves this box <paramref name="across"/> and <paramref name="down"/> with what is in it, as
    /// <see cref="OffsetLeft"/> and <see cref="OffsetTop"/> do, and puts the out-of-flow boxes in it
    /// that <paramref name="apart"/> lists (see <see cref="CollectBoxesApart"/>) where they belong
    /// then (CSS2.1 §10.3.7, §10.6.4): as far across or down in an axis in which they are at their
    /// static position, and where they are in one in which their offsets place them in their
    /// containing block, which does not move (see <see cref="KeepsItsPlaceDown"/>). OffsetLeft and
    /// OffsetTop move an absolutely positioned box in both axes, and a fixed one in neither.
    /// </summary>
    private void OffsetWithBoxesApart(double across, double down, List<CssBox> apart)
    {
        var places = apart.ConvertAll(box => (
            X: IsPlacedAcrossByOffsets(box) ? box.Location.X : box.Location.X + across,
            Y: KeepsItsPlaceDown(box) ? box.Location.Y : box.Location.Y + down));

        if (Math.Abs(across) > 0.01)
            OffsetLeft(across);

        if (Math.Abs(down) > 0.01)
            OffsetTop(down);

        for (int i = 0; i < apart.Count; i++)
        {
            double shiftX = places[i].X - apart[i].Location.X;
            double shiftY = places[i].Y - apart[i].Location.Y;

            if (Math.Abs(shiftX) > 0.01)
                apart[i].OffsetLeft(shiftX);

            if (Math.Abs(shiftY) > 0.01)
                apart[i].OffsetTop(shiftY);
        }
    }

    /// <summary>
    /// Whether <paramref name="box"/> is <paramref name="ancestor"/> or a box in it.
    /// </summary>
    private static bool IsWithin(CssBox? box, CssBox ancestor)
    {
        for (var b = box; b != null; b = b.ParentBox)
        {
            if (b == ancestor)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether this box's parent moves for a margin that collapses through its top: every parent
    /// but the root element's box and what is above it, which keep their established position.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_parentBox))]
    private bool ParentMovesWithItsMargin => _parentBox is { ParentBox.ParentBox: not null };

    /// <summary>
    /// The floats and absolutely positioned boxes between <paramref name="sibling"/> and this box
    /// among their parent's children, in document order, when nothing else stands between them but
    /// boxes that generate nothing (<c>display: none</c>), or <c>null</c> when something in the flow
    /// does.
    /// </summary>
    private List<CssBox>? OutOfFlowBoxesSince(CssBox sibling)
    {
        if (_parentBox == null)
            return null;

        var siblings = _parentBox.Boxes;
        List<CssBox> between = [];

        for (int i = siblings.IndexOf(this) - 1; i >= 0; i--)
        {
            var box = siblings[i];

            if (box == sibling)
                return between;

            if (box.Display == CssConstants.None)
                continue;

            if (box.Float == CssConstants.None && box.Position is not (CssConstants.Absolute or CssConstants.Fixed))
                return null;

            between.Insert(0, box);
        }

        return null;
    }

    /// <summary>
    /// Whether a box in the flow before this one among its parent's children has clearance (see
    /// <see cref="ClearsFloats"/>).
    /// </summary>
    private bool FollowsBoxWithClearance()
    {
        if (_parentBox == null)
            return false;

        foreach (var box in _parentBox.Boxes)
        {
            if (box == this)
                return false;

            if (box.ClearsFloats && box.Float == CssConstants.None
                && box.Position is not (CssConstants.Absolute or CssConstants.Fixed))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The floats placed where the set of margins above this box's parent ends: those the run of
    /// boxes the set collapses through (<see cref="FirstChildMarginRun"/>) holds before this box, in
    /// the boxes of the run or in the empty blocks there.
    /// </summary>
    private List<CssBox> FloatsAtSetEnd()
    {
        List<CssBox> floats = [];

        if (_parentBox != null)
        {
            foreach (var box in BoxesBeforeInRun([.. FirstChildMarginRun(_parentBox)], this))
                CssBoxHelper.CollectFloatsInSubtree(box, floats);
        }

        return floats;
    }

    /// <summary>
    /// Whether this box's <c>clear</c> takes it past any of the floats in <paramref name="boxes"/>
    /// (CSS2.1 §9.5.2).
    /// </summary>
    private bool ClearsAnyFloatOf(List<CssBox> boxes)
    {
        if (Clear is null or CssConstants.None)
            return false;

        return boxes.Exists(box => box.Float != CssConstants.None
            && (Clear == "both" || string.Equals(box.Float, Clear, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Whether this box's <c>clear</c> takes it past floats that reach below <paramref name="top"/>,
    /// where its top border edge would be if it did not clear them (CSS2.1 §9.5.2).
    /// </summary>
    private bool ClearsFloatsBelow(double top)
    {
        if (Clear is null or CssConstants.None)
            return false;

        double floatsBottom = CssBoxHelper.GetMaxFloatBottom(this);

        return floatsBottom > 0 && floatsBottom > top + 0.01;
    }

    /// <summary>
    /// Whether <paramref name="box"/>, absolutely positioned or fixed, is placed by its <c>top</c>
    /// or <c>bottom</c> rather than at its static position (CSS2.1 §10.6.4).
    /// </summary>
    private static bool IsPlacedByOffsets(CssBox box) =>
        box.Position is CssConstants.Absolute or CssConstants.Fixed
        && (box.Top is not (null or CssConstants.Auto) || box.Bottom is not (null or CssConstants.Auto));

    /// <summary>
    /// Whether <paramref name="box"/>, absolutely positioned or fixed, keeps the place down its
    /// containing block that its offsets give it while the boxes around it move (CSS2.1 §10.6.4):
    /// one that a <c>top</c> of a length places, or a <c>bottom</c> or a percentage <c>top</c> in a
    /// containing block whose height is known.
    /// </summary>
    /// <remarks>
    /// Placed against a containing block that its content has not finished sizing, a box is placed
    /// against the height it has so far (a separate issue), and that height grows as the content in
    /// it goes down, so the box goes down with it. Kept where it was, a box with <c>bottom: 5px</c> in
    /// a <c>&lt;section style="position: relative"&gt;</c>, before a <c>&lt;p&gt;</c> with
    /// <c>margin-top: 30px</c> that moved the <c>&lt;div&gt;</c> holding both, stayed 60px above
    /// where browsers put it, 30px further than when it went down with the <c>&lt;div&gt;</c>.
    /// </remarks>
    private static bool KeepsItsPlaceDown(CssBox box)
    {
        if (!IsPlacedByOffsets(box))
            return false;

        if (box.Position == CssConstants.Fixed
            || box.Top is { } top && top != CssConstants.Auto && !top.Contains('%'))
        {
            return true;
        }

        var containing = box.FindPositionedContainingBlock();

        return containing.ParentBox == null
            || containing.Height is { Length: > 0 } height && height != CssConstants.Auto
                && !containing.HeightPercentageResolvesToAuto();
    }

    /// <summary>
    /// Whether <paramref name="box"/>, absolutely positioned or fixed, is placed across by its
    /// <c>left</c> or <c>right</c> rather than at its static position (CSS2.1 §10.3.7).
    /// </summary>
    private static bool IsPlacedAcrossByOffsets(CssBox box) =>
        box.Position is CssConstants.Absolute or CssConstants.Fixed
        && (box.Left is not (null or CssConstants.Auto) || box.Right is not (null or CssConstants.Auto));

    /// <summary>
    /// Moves the run of boxes a first child's top margin collapses through, as <see
    /// cref="MoveFirstChildMarginRun"/> does, for a box that joins the set of margins an empty
    /// sibling handed on across the floats and absolutely positioned boxes <paramref
    /// name="between"/> them, and puts those boxes where the set ends.
    /// </summary>
    /// <remarks>
    /// The boxes between were placed below the set the empty sibling hands on, where it ends if
    /// nothing joins it. It ends <paramref name="growth"/> below where the run began instead, at the
    /// parent's new content top. An absolutely positioned or fixed box at its static position (auto
    /// <c>top</c> and <c>bottom</c>, CSS2.1 §10.6.4) goes there, with its own top margin below it,
    /// which does not collapse with the set (§8.3.1); the run's move places the floats again, and
    /// leaves a box placed by its offsets in a containing block outside the run where they put it
    /// (see <see cref="MoveFirstChildMarginRun"/>).
    /// </remarks>
    private static void MoveFirstChildMarginRunOver(List<CssBox> between, CssBox parent, CssBox child,
        double positive, double negative, double growth)
    {
        MoveFirstChildMarginRun(parent, child, positive, negative, growth);

        // The run's move took it along, but it kept the place where its own margin had collapsed
        // with the set: with margin-top: 20px, 20px below where the outer <div> began, 4px down it
        // once the <div> had moved 16px, where browsers put it 20px down.
        foreach (var box in between)
        {
            if (box.Float != CssConstants.None || IsPlacedByOffsets(box))
                continue;

            double shift = parent.ClientTop + box.ActualMarginTop - box.Location.Y;

            if (Math.Abs(shift) > 0.01)
                box.OffsetTop(shift);
        }
    }

    /// <summary>
    /// Whether this float was placed on this layout pass below the part of a set of margins that an
    /// empty block before it hands on (CSS2.1 §8.3.1), whose margins collapse with its parent's top
    /// margin: where the set ends while nothing after the empty block joins it, and not where it ends
    /// once something does and the parent moves by it (see <see cref="MoveFirstChildMarginRun"/>).
    /// </summary>
    private bool _placedBelowHandedOnSet;

    /// <summary>
    /// How this box's top margin comes back out of the set of margins above its parent if the
    /// floats leave the box no room where the margin put it (see <see cref="SeparateMarginFromSet"/>):
    /// set when <see cref="MarginTopCollapse"/> collapses the margin with the parent's on this layout
    /// pass and moves the parent by it, and kept until the box is placed.
    /// </summary>
    private MarginSeparation? _marginSeparation;

    /// <summary>
    /// A move of the run of boxes a margin collapses through (see <see
    /// cref="MoveFirstChildMarginRun"/>), from <paramref name="Parent"/> up, by <paramref
    /// name="Shift"/>, which leaves the set of margins above it the <paramref name="Positive"/> and
    /// <paramref name="Negative"/> sides it has without the margin.
    /// </summary>
    private sealed record MarginSeparation(CssBox Parent, double Positive, double Negative, double Shift);

    /// <summary>
    /// Whether this box is one in the flow that goes beside floats where they leave it room, or below
    /// them, and not over them (CSS2.1 §9.5; see <see cref="PlaceBesideFloats"/>), with no
    /// <c>clear</c> of its own. A <c>clear</c> takes a box below the floats it clears from where its
    /// margin put it (§9.5.2), and placed from where the set ends without the margin, it could go
    /// beside one of them.
    /// </summary>
    private bool AvoidsFloats =>
        Float == CssConstants.None
        && Position is not (CssConstants.Absolute or CssConstants.Fixed)
        && Clear is (null or CssConstants.None)
        && CssBoxHelper.EstablishesBfc(this);

    /// <summary>
    /// CSS2.1 §9.5: the border box of a table or of a block that establishes a block formatting
    /// context may not overlap the margin box of a float, and "if necessary, implementations should
    /// clear the said element by placing it below any preceding floats". Where the floats leave this
    /// box no room at the place its top margin gave it, collapsed with its parent's, the margin
    /// leaves the set of margins above the parent, as a margin with clearance does (§8.3.1): the run
    /// of boxes it moved goes back to where the set ends without it, with the out-of-flow boxes the
    /// set places, and the box goes on from there to where the floats leave it room. Returns how far
    /// the run went, and the box's place with it, or <c>null</c> when its margin moved no run.
    /// </summary>
    /// <remarks>
    /// Browsers place such a box with its margin in the set, and again without it once the floats
    /// push it down. Kept in the set, the margin moved the parent, the floats it places and the box
    /// below them further down: after a 10px block, <c>&lt;div&gt;&lt;div style="margin-bottom:
    /// 16px"&gt;&lt;/div&gt;&lt;div style="float: left; height: 12px"&gt;&lt;/div&gt;&lt;div
    /// style="display: flow-root; width: 300px; margin-top: 20px"&gt;</c> began the outer
    /// <c>&lt;div&gt;</c> and the float 20px below it and the flow root 32px below, where browsers
    /// begin them 16px and 28px below; and after an empty <c>&lt;div&gt;</c> and a right float, a
    /// <c>&lt;table style="width: 100%; margin: 16px 0"&gt;</c> put the float and everything after
    /// it 16px lower than browsers do.
    /// </remarks>
    private double? SeparateMarginFromSet()
    {
        if (_marginSeparation is not { } separation)
            return null;

        _marginSeparation = null;
        MoveFirstChildMarginRun(separation.Parent, this, separation.Positive, separation.Negative,
            separation.Shift);

        return separation.Shift;
    }

    /// <summary>
    /// Whether <see cref="MarginTopCollapse"/> found this box's top margin collapsing with its
    /// parent's on this layout pass: the box is the parent's first in-flow child, or follows empty
    /// ones whose margins do, and nothing separates their top margins (CSS2.1 §8.3.1). A first child
    /// of this box whose margin is larger moves the topmost box of such a run rather than this one.
    /// </summary>
    private bool _marginTopCollapsesWithParent;

    /// <summary>
    /// Whether this box's top margin collapses with its parent's on this layout pass; see <see
    /// cref="_marginTopCollapsesWithParent"/>.
    /// </summary>
    internal bool MarginTopCollapsesWithParent => _marginTopCollapsesWithParent;

    protected double MarginTopCollapse(CssBoxProperties prevSibling)
    {
        double value;

        // Whether this box's top margin went into the set above its parent, across floats or
        // absolutely positioned boxes, and moved the parent by it (below).
        bool movedParentAcross = false;

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

                // A float goes below the part of the set the empty box hands on, which is not where
                // the set ends once a block after it joins the set and moves the parent (below).
                _placedBelowHandedOnSet = Float != CssConstants.None && prevBox._marginTopCollapsesWithParent;

                // When the empty box's margins collapse with its parent's top margin, as the first
                // child's or after empty ones that do, so do this box's: the set is the one above
                // the parent, which moves by the rest, with this box at its top, as for a first
                // child. The rest was spent inside the parent instead: in <div><div
                // style="margin-bottom: 16px"></div><p>Text</p></div>, the outer <div> kept its
                // place and held the <p> 16px down, where browsers begin the outer <div> 16px lower
                // with the <p> at its top.
                //
                // Only an in-flow block joins: a float's or an absolutely positioned box's margins
                // do not collapse (CSS2.1 §8.3.1). But one between the two keeps nothing apart,
                // being out of flow (§9.5, §9.6), and it goes where the set ends, at the parent's
                // top, where browsers put it once the block after it has joined the set. It was
                // placed below the margins handed on, and the block after it kept the parent in
                // its place: after a 10px block, <div><div style="margin-bottom: 16px"></div><div
                // style="float: left"></div><p>Text</p></div> began right below it, 36px tall,
                // where browsers begin it 16px lower, 20px tall, with the float and the <p> at its
                // top.
                //
                // Not past an empty box with clearance, though, whose margins do not collapse with
                // the parent's (§8.3.1): after a float reaching into the outer <div>, a first child
                // with clear: both, a float and a <p> with margin-top: 20px moved the outer <div>
                // and the float 20px down, where browsers keep them in place. (With nothing between,
                // the <p> still joins, a separate issue.)
                if (prevBox._marginTopCollapsesWithParent
                    && Float == CssConstants.None
                    && Position is not (CssConstants.Absolute or CssConstants.Fixed)
                    && OutOfFlowBoxesSince(prevBox) is { } between
                    && (between.Count == 0 || !FollowsBoxWithClearance()))
                {
                    // §9.5.2: a block whose clear takes it past a float the set places has
                    // clearance, and a margin with clearance does not collapse with the ones above
                    // it (§8.3.1), so the set ends where the empty box hands it on, and this box's
                    // top margin does not collapse with its parent's. With clear: both and
                    // margin-top: 20px on the <p> above, browsers begin the outer <div> 16px lower,
                    // not 20px, with the float at its top and the <p> below the float; and the
                    // margins of what is in the <p> or after it do not move the outer <div> again.
                    // The set places the floats before the empty box too, in the outer <div> or in
                    // what the set collapses through: clear: left past a left float before the
                    // empty box, with a right float between, moved the outer <div> 20px down. So
                    // does a clear that takes the block past a float outside the set reaching below
                    // the parent's top as far down as the block would move it: after a 100 x 50px
                    // float, clear: both and margin-top: 30px on the <p> moved the outer <div> and
                    // an absolutely positioned box between 30px down, where browsers move them 16px
                    // down and put the <p> below the float.
                    bool clearance = between.Count > 0 && Clear is not (null or CssConstants.None)
                        && (ClearsAnyFloatOf(FloatsAtSetEnd())
                            || (_parentBox is { } parentBox && ClearsFloatsBelow(parentBox.ClientTop + value)));
                    _marginTopCollapsesWithParent = !clearance;

                    if (ParentMovesWithItsMargin)
                    {
                        // What the set the empty box hands on comes to below it, where the boxes
                        // between were placed.
                        double handedPos = prevBox.CollapsedMarginTop, handedNeg = prevBox._negativeMarginTopAbove;
                        CssBoxHelper.CollectEmptyBoxMargins(prevBox, ref handedPos, ref handedNeg);
                        double handedOn = handedPos + handedNeg - prevBox.MarginSpentAboveTop;
                        double growth = clearance ? handedOn : value;

                        // The boxes between go where the set ends even when the parent does not
                        // move, the set coming to nothing: with margin-top: -16px on the <p>, the
                        // float stayed 16px down the outer <div>, where browsers put it at the top.
                        if (Math.Abs(growth) > 0.1 || (between.Count > 0 && Math.Abs(handedOn) > 0.1))
                        {
                            MoveFirstChildMarginRunOver(between, _parentBox, this,
                                clearance ? handedPos : maxPos, clearance ? handedNeg : maxNeg, growth);
                        }

                        // Unless the floats leave no room for this box there (see
                        // SeparateMarginFromSet).
                        if (!clearance && AvoidsFloats && Math.Abs(handedOn - growth) > 0.1)
                        {
                            _marginSeparation = new(_parentBox, handedPos, handedNeg, handedOn - growth);
                        }

                        movedParentAcross = between.Count > 0 && Math.Abs(growth) > 0.1;
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
            // CSS2.1 §8.3.1: only what separates the two top margins keeps them apart, the
            // parent's top padding and border. Its bottom ones lie at the other end of its content,
            // and asking for them too kept the margin of a padded box's first child inside it: a
            // block with `padding-bottom: 10px` holding a paragraph with `margin-top: 20px` began
            // where the box before it ended and held the paragraph 20px down, where browsers
            // begin the block 20px down with the paragraph at its top.
            && !CssBoxHelper.HasTopPaddingOrBorder(_parentBox)
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
            {
                MoveFirstChildMarginRun(_parentBox, this, positive, negative, growth);

                // Unless the floats leave no room for this box there (see SeparateMarginFromSet).
                if (AvoidsFloats)
                    _marginSeparation = new(_parentBox, parentPositive, parentNegative, -growth);
            }

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
        //
        // Not for an <hr> whose margin moved its parent past an out-of-flow box (above): the space
        // above it is there. After a 10px block, <div><div style="margin-bottom: 16px"></div><div
        // style="position: absolute"></div><hr></div> put the <hr> 17.6px down the outer <div>,
        // where browsers put it at the top. (One right after the empty box still gets the 1.1em, a
        // separate issue.)
        if (value < 0.1 && !movedParentAcross && HtmlTag != null && HtmlTag.Name == "hr")
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
        // CSS2.1 §10.6.3 / §10.6.7: Floated children contribute to the
        // height of their parent only when the parent establishes a new
        // block formatting context (BFC).  Non-BFC blocks (e.g. a plain
        // <ul> inside a floated <dd>) must not include descendant floats
        // in their height calculation.
        bool isBfc = CssBoxHelper.EstablishesBfc(this);

        // CSS2.1 §10.6.3: the height ends at the last in-flow child, not at the lowest one. The
        // lowest bottom among the children stood for it, so a last child that a negative margin
        // pulls up over the one before it ended this box where that one ends: after a 30px block,
        // one with margin-top: -25px and height: 10px ended its parent 30px down, where browsers
        // end it 15px down.
        //
        // Only children in the normal flow are taken into account. Not an absolutely positioned
        // box, nor a float: a formatting-context root takes its floats in below. Nor a box that is
        // not generated (display: none), whose margin was added below a block with bottom padding.
        // Nor an inline box: it is laid out in this box's lines, whose extent CreateLineBoxes has
        // already made this box's height, and it has no bottom of its own: its Location is not
        // kept by line layout, and OffsetTop moves it with every shift of the boxes around it.
        // Reading it gave a flex item holding one word, which a row centred 33.5px down, the
        // bottom of a text box shifted there twice, 67px down.
        CssBox lastInFlowChild = null;

        foreach (var child in Boxes)
        {
            if (child.Position is CssConstants.Absolute or CssConstants.Fixed
                || child.Float != CssConstants.None
                || child.Display is CssConstants.None or CssConstants.Inline)
                continue;

            lastInFlowChild = child;
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
            && !CssBoxHelper.HasBottomPaddingOrBorder(this)
            && autoHeight
            // CSS2.1 §8.3.1: margins of the root element's box do not collapse, so the
            // body's bottom margin stays inside the root's height instead of propagating
            // out of it (where nothing would ever contain it, shortening the canvas).
            && !CssBoxHelper.IsRootElement(this)
            && lastInFlowChild.Float == CssConstants.None
            && lastInFlowChild.Display != CssConstants.Inline
            && lastInFlowChild.Display != CssConstants.InlineBlock
            // CSS2.1 §8.3.1: nor with the margins of an empty child that collapse with a top margin
            // that has clearance, which stay inside this box (see ClearsFloats): a clearfix's empty
            // ::after ends it below the floats the ::after clears, and below the margin above it.
            && !(lastInFlowChild.ClearsFloats && CssBoxHelper.IsEmptyCollapsible(lastInFlowChild));

        // The content-area top, so that padding is preserved even when all children are floated
        // (CSS2.1 §10.6.3: content height is zero but padding is additive), and so that a last
        // child pulled up above it leaves the content zero tall.
        double contentBottom = Location.Y + ActualBorderTopWidth + ActualPaddingTop;

        if (lastInFlowChild != null)
            contentBottom = Math.Max(contentBottom, LastInFlowChildEdge(lastInFlowChild, collapseThrough));

        // CSS2.1 §10.6.7: When a BFC root auto-sizes its height it must
        // extend to contain all descendant floats — not only direct-child
        // floats.  Walk the subtree (stopping at nested BFC boundaries)
        // to find the maximum float bottom.
        if (isBfc)
            FindMaxDescendantFloatBottom(this, ref contentBottom);

        return Math.Max(ActualBottom, contentBottom + ActualPaddingBottom + ActualBorderBottomWidth);
    }

    /// <summary>
    /// Where <paramref name="child"/>, its parent's last in-flow child, ends the parent's content
    /// (CSS2.1 §10.6.3): at the bottom edge of the child's bottom margin, or, where that margin
    /// collapses through the parent (<paramref name="collapsesThrough"/>), at the bottom border
    /// edge of the last in-flow child whose top margin does not collapse with the parent's bottom
    /// margin.
    /// </summary>
    internal static double LastInFlowChildEdge(CssBox child, bool collapsesThrough)
    {
        // An empty child's margins collapse through it (§8.3.1), and with the margins before it,
        // which it stands below by as much as the part of them above it comes to. Where they
        // collapse through the parent as well, the content ends where they begin, below the
        // last child that is not empty. The empty child's own top edge stood for it: after a 30px
        // block, an empty one with margin-top: 20px ended its parent 50px down, where browsers end
        // it 30px down and put the 20px margin below it. Where they do not, the content ends
        // where the whole set of them does, which is as far below the empty child as what is left
        // of the set after the part above it.
        if (CssBoxHelper.IsEmptyCollapsible(child))
        {
            if (child.ClearsFloats)
                return child.Location.Y + child.ActualMarginBottom;

            if (!collapsesThrough)
                return child.Location.Y + CssBoxHelper.GetEffectiveMarginBottom(child);

            // A first child's set begins above its parent, which it moved down, or kept inside it
            // where the parent keeps its place, as the root element's box does.
            return child.MarginTopCollapsesWithParent
                ? child.Location.Y
                : child.Location.Y - child.MarginSpentAboveTop;
        }

        // CSS2.1 §9.4.3: Relative positioning is visual-only and
        // does not affect the flow position used for auto-height
        // calculation.  Undo the relative offset so the parent
        // measures the child's normal-flow bottom.
        double bottom = child.ActualBottom;

        if (child.Position == CssConstants.Relative)
            bottom -= CssBoxHelper.GetRelativeOffsetY(child);

        if (collapsesThrough)
            return bottom;

        // Wherever the last child's bottom margin stays inside this box, for any of the reasons
        // above, what stays inside is that margin as it has collapsed with those of the child's
        // own last children (CSS2.1 §8.3.1), as GetPropagatedMarginBottom gives it: a paragraph's
        // margin that collapsed through an unpadded wrapper is the wrapper's margin here, not the
        // wrapper's own zero, as its top margin is the wrapper's at the top. Taking the wrapper's
        // own lost the paragraph's at a block with bottom padding: <div style="padding-bottom:
        // 1px"><div><p>Text</p></div></div> ended 1 px below the paragraph, where browsers end it
        // 17 px below.
        return bottom + (child.Display != CssConstants.InlineBlock
            ? CssBoxHelper.GetPropagatedMarginBottom(child)
            : child.ActualMarginBottom);
    }
}
