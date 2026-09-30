using Broiler.CSS;
using Broiler.Layout.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace Broiler.Layout.Engine;

internal static class CssBoxHelper
{
    public static CssBox CreateBox(HtmlTag tag, Uri baseUrl, CssBox? parent = null)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.Name == HtmlConstants.Img)
        {
            return new CssBoxImage(parent, tag, baseUrl);
        }
        else if (tag.Name.Equals("object", StringComparison.OrdinalIgnoreCase) &&
                 tag.TryGetAttribute("data") is { } data &&
                 data.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            // <object data="data:image/..."> — treat as a replaced image element.
            // Any nested fallback content will be removed by CorrectObjectBoxes.
            return new CssBoxImage(parent, tag, baseUrl);
        }
        else if (tag.Name == HtmlConstants.Iframe)
        {
            return new CssBox(parent, tag, baseUrl);
        }
        else if (tag.Name == HtmlConstants.Hr)
        {
            return new CssBoxHr(parent, tag, baseUrl);
        }
        else
        {
            return new CssBox(parent, tag, baseUrl);
        }
    }

    public static CssBox CreateBox(CssBox parent, Uri baseUrl, HtmlTag? tag = null, CssBox? before = null)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var newBox = new CssBox(parent, tag, baseUrl);
        newBox.InheritStyle();

        // Anonymous boxes (tag == null) are fragments of their parent's inline
        // formatting context — e.g. the wrappers created when content is split
        // around a <br> or a block-level child.  'unicode-bidi' is not inherited,
        // so InheritStyle() does not carry it; without this an anonymous block
        // wrapping a 'unicode-bidi: plaintext' element's content would fall back
        // to 'normal' and mis-resolve per-line bidi direction.  Explicit CSS on
        // pseudo-elements is applied later and still overrides this default.
        if (tag == null)
            newBox.UnicodeBidi = parent.UnicodeBidi;

        if (before != null)
            newBox.SetBeforeBox(before);

        return newBox;
    }

    public static CssBox CreateBlock(Uri baseUrl) => new(null, null, baseUrl) { Display = CssConstants.Block };

    public static CssBox CreateBlock(CssBox parent, Uri baseUrl, HtmlTag? tag = null, CssBox? before = null)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var newBox = CreateBox(parent, baseUrl, tag, before);
        newBox.Display = CssConstants.Block;

        return newBox;
    }

    internal static CssRect FirstWordOccourence(CssBox b, CssLineBox line)
    {
        if (b.Words.Count == 0 && b.Boxes.Count == 0)
            return null;

        if (b.Words.Count > 0)
        {
            foreach (CssRect word in b.Words)
            {
                if (line.Words.Contains(word))
                    return word;
            }

            return null;
        }
        else
        {
            foreach (CssBox bb in b.Boxes)
            {
                CssRect w = FirstWordOccourence(bb, line);

                if (w != null)
                    return w;
            }

            return null;
        }
    }

    /// <summary>
    /// A text box that held nothing but collapsible whitespace: its words were collapsed away, so
    /// it looks empty to the intrinsic passes even though it still separates its siblings on the
    /// line. Preserved whitespace (<c>pre</c>, <c>pre-wrap</c>, <c>break-spaces</c>) keeps its
    /// words and is measured normally, so it is deliberately not matched here.
    /// </summary>
    private static bool IsCollapsedWhitespaceSeparator(CssBox box)
    {
        if (box.Words.Count > 0 || box.Boxes.Count > 0 || box.HtmlTag != null)
            return false;

        if (box.WhiteSpace == CssConstants.Pre || box.WhiteSpace == CssConstants.PreWrap
            || box.WhiteSpace == CssConstants.PreLine)
            return false;

        var text = box.Text.Span;
        if (text.Length == 0)
            return false;

        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c))
                return false;
        }

        return true;
    }

    public static void GetMinimumWidth_LongestWord(CssBox box, ref double maxWidth, ref CssRect maxWidthWord)
    {
        LayoutWorkTrace.Count(LayoutWorkTrace.Counters.IntrinsicVisits);

        // A display:none box generates no boxes at all (CSS 2.1 §9.2.4), so it contributes nothing
        // to an intrinsic size. Without this the UA-hidden elements that carry *text* — <style>,
        // <script>, <title> — were measured, and their source text set the min/max-content width of
        // any shrink-to-fit ancestor. A <div style="display:inline-block"> holding one <li> and a
        // stylesheet measured 861px wide instead of 65px.
        if (box.Display == CssConstants.None)
            return;

        if (box.Words.Count > 0)
        {
            foreach (CssRect cssRect in box.Words)
            {
                if (cssRect.Width > maxWidth)
                {
                    maxWidth = cssRect.Width;
                    maxWidthWord = cssRect;
                }
            }
        }
        else
        {
            foreach (CssBox childBox in box.Boxes)
            {
                // See GetMinMaxSumWords: a positioned child's words are out of flow with it.
                if (IsOutOfFlowPositioned(childBox))
                    continue;

                GetMinimumWidth_LongestWord(childBox, ref maxWidth, ref maxWidthWord);
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="box"/> is absolutely or fixed positioned, and so out of flow: it
    /// takes no room in its parent's content (CSS 2.1 §9.6).
    /// </summary>
    internal static bool IsOutOfFlowPositioned(CssBox box) =>
        box.Position == CssConstants.Absolute || box.Position == CssConstants.Fixed;

    public static double GetWidthMarginDeep(CssBox box)
    {
        double sum = 0f;

        if (box.Size.Width > 90999 || (box.ParentBox != null && box.ParentBox.Size.Width > 90999))
        {
            CssBox? current = box;
            while (current != null)
            {
                sum += current.ActualMarginLeft + current.ActualMarginRight;
                current = current.ParentBox;
            }
        }

        return sum;
    }

    internal static double GetMaximumBottom(CssBox startBox, double currentMaxBottom)
    {
        // CSS2.1 §10.6.1: an inline, non-replaced box's bottom padding and border are not part of
        // its line, so what a table cell centres ends at its words, not at its rectangles.
        if (startBox.IsInlineNonReplaced)
        {
            foreach (var word in startBox.Words)
                currentMaxBottom = Math.Max(currentMaxBottom, word.Bottom);
        }
        else
        {
            foreach (var line in startBox.Rectangles.Keys)
                currentMaxBottom = Math.Max(currentMaxBottom, startBox.Rectangles[line].Bottom);
        }

        foreach (var b in startBox.Boxes)
        {
            currentMaxBottom = Math.Max(currentMaxBottom, b.ActualBottom + b.ActualMarginBottom);
            currentMaxBottom = Math.Max(currentMaxBottom, GetMaximumBottom(b, currentMaxBottom));
        }

        return currentMaxBottom;
    }

    /// <summary>An atomic inline-level box (inline-block / inline-table /
    /// inline-flex / inline-grid): inline-level, so it shares a line with siblings
    /// for max-content sizing rather than starting a new one.</summary>
    internal static bool IsAtomicInlineLevel(string display) =>
        display is "inline-block" or "inline-table" or "inline-flex" or "inline-grid";

    /// <summary>
    /// Whether <paramref name="box"/> begins a fresh max-content line rather than continuing the
    /// running one: block-level boxes do, inline-level content — real inline boxes, the atomic
    /// inline-level displays, floats, and anything under <c>white-space: nowrap</c> — does not.
    /// </summary>
    private static bool StartsNewMaxContentLine(CssBox box) =>
        box.Display != CssConstants.Inline
        && box.Display != CssConstants.TableCell
        && !IsAtomicInlineLevel(box.Display)
        && box.WhiteSpace != CssConstants.NoWrap
        && box.Float == CssConstants.None;

    /// <summary>
    /// Walks <paramref name="box"/> for its min- and max-content widths: <paramref name="min"/> is
    /// the widest thing that cannot be broken, <paramref name="maxSum"/> the widest line.
    /// </summary>
    /// <remarks>
    /// <paramref name="marginSum"/> and <paramref name="paddingSum"/> are the horizontal margins and
    /// the horizontal border and padding of the boxes on the path to the box being visited: each box
    /// adds its own on the way in and takes them off on the way out. A line starts from both, and a
    /// word's minimum is measured with the padding and border around it.
    /// <para>
    /// <paramref name="paddingSum"/> used to be a running total that nothing took off, added to both
    /// widths by the caller at the end, so the padding of boxes stacked one above another was
    /// counted as if they sat side by side. Ten rows with 100px of left padding made their
    /// shrink-to-fit container 1008px wide around an 8px word. html5test.com's results column, a
    /// padded table cell per feature, came out 12,060px wide in a 900px page.
    /// </para>
    /// </remarks>
    public static void GetMinMaxSumWords(CssBox box, ref double min, ref double maxSum, ref double paddingSum, ref double marginSum, CssBox suppressExplicitWidthFor = null)
    {
        // The box lays out lines of its own, and the first starts with nothing on it.
        bool lineHoldsContent = false;
        GetMinMaxSumWordsOnLine(box, ref min, ref maxSum, ref paddingSum, ref marginSum, ref lineHoldsContent, suppressExplicitWidthFor);
    }

    /// <summary>
    /// <see cref="GetMinMaxSumWords"/>, on a line that holds content in the flow before
    /// <paramref name="box"/> if <paramref name="lineHoldsContent"/>, which is left saying whether it
    /// does after the box.
    /// </summary>
    /// <remarks>
    /// CSS Text 3 §4.1.3: collapsible white space at the start of a line is removed, before the
    /// line's first word or box placed whole, whatever empty inline boxes and boxes out of the flow
    /// come first, and the flow takes it away there (CssLayoutEngine.FlowBox). Measured with that
    /// space all the same, a box laying out lines of its own came out a space wider than what the
    /// flow puts in it: a flex item holding an empty span, a space and "abc" was 28px wide around
    /// 24px of text, where browsers make it 24px wide, and at the end of its row, with the space
    /// gone from its line, it ended "abc" a space short of the row's end.
    /// </remarks>
    private static void GetMinMaxSumWordsOnLine(CssBox box, ref double min, ref double maxSum, ref double paddingSum, ref double marginSum, ref bool lineHoldsContent, CssBox? suppressExplicitWidthFor)
    {
        LayoutWorkTrace.Count(LayoutWorkTrace.Counters.IntrinsicVisits);

        // See GetMinimumWidth_LongestWord: a display:none box generates no boxes, so it adds
        // nothing to the running max-content line. This is the max-content half of the same fix.
        if (box.Display == CssConstants.None)
            return;

        // Where the box starts on the running line and on the path to it: the margins of the boxes
        // it is in are on its line, and on its path too (see HoldToMinWidth).
        double minWidthEdge = AtomicInlineMinWidth(box);
        double lineBeforeBox = maxSum, pathBeforeBox = paddingSum + marginSum;
        double? oldSum = null;

        // Block-level boxes start a new line, so max-content resets the running sum
        // to this line. Inline-*level* content stays on the line and accumulates:
        // real inline boxes (CSS2.1 §10.3.7 floats likewise contribute to the same
        // "line") and — the reason this guard also lists them — **atomic inline-level
        // boxes** (inline-block / inline-table / inline-flex / inline-grid), which are
        // inline-level and sit side by side on one line. Treating them as block reset
        // the line, so N inline-blocks in a row measured as the *widest* one instead
        // of their *sum* (e.g. two 40px inline-blocks → max-content 40, not 80),
        // under-sizing every shrink-to-fit and collapsing max-content/fit-content
        // grid tracks that hold them.
        if (StartsNewMaxContentLine(box))
        {
            oldSum = maxSum;
            maxSum = marginSum + paddingSum;
        }

        // When measuring a grid item's content contribution, its own explicit
        // width is ignored (a percentage width resolves against the track being
        // sized, so it is treated as auto/content — CSS Grid §11.5); descendants'
        // explicit widths still count.
        //
        // CSS Sizing 3 §5.1: more generally, a *percentage* width resolves against
        // the size we are computing (the container's intrinsic width), so it is
        // treated as auto for intrinsic sizing regardless of grid — measuring the
        // box's content instead of the percentage. Without this a `width:100%`
        // child resolves against the containing block and reports the full
        // available width, ballooning a float/inline-block/abspos shrink-to-fit to
        // the container (e.g. a float wrapping a `width:100%` block, or an
        // auto-fill grid item sized 100%, pinned to the viewport not its content).
        bool widthIsPercentage = !string.IsNullOrEmpty(box.Width)
            && box.Width.EndsWith('%')
            && !box.Width.Contains('(');
        bool useExplicitWidth = box != suppressExplicitWidthFor && !widthIsPercentage;

        // CSS2.1 §10.3.5/§10.3.7: When a floated child has an explicit width,
        // use the declared width directly for shrink-to-fit calculation
        // instead of measuring content words.
        if (useExplicitWidth
            && box.Float != CssConstants.None
            && box.Width != CssConstants.Auto
            && !string.IsNullOrEmpty(box.Width))
        {
            double explicitWidth = CssLengthParser.ParseLength(
                box.Width, box.ContainingBlock?.Size.Width ?? 0, box.GetEmHeight());
            double explicitEdges = box.ActualBorderLeftWidth + box.ActualBorderRightWidth
                                 + box.ActualPaddingRight + box.ActualPaddingLeft;
            maxSum += explicitWidth + explicitEdges;
            min = Math.Max(min, paddingSum + explicitWidth + explicitEdges);

            if (oldSum.HasValue)
                maxSum = Math.Max(maxSum, oldSum.Value);
            return;
        }

        // CSS2.1 §17.5.2: Non-floated block-level children (e.g. display:table
        // or display:list-item inside an anonymous table-cell) with explicit
        // width contribute that width to the intrinsic minimum/maximum.
        if (useExplicitWidth
            && box.Display != CssConstants.Inline
            && box.Display != CssConstants.TableCell
            && box.Float == CssConstants.None
            && box.Width != CssConstants.Auto
            && !string.IsNullOrEmpty(box.Width))
        {
            double explicitWidth = CssLengthParser.ParseLength(
                box.Width, box.ContainingBlock?.Size.Width ?? 0, box.GetEmHeight());
            if (explicitWidth > 0)
            {
                double explicitEdges = box.ActualBorderLeftWidth + box.ActualBorderRightWidth
                                     + box.ActualPaddingRight + box.ActualPaddingLeft;
                maxSum += explicitWidth + explicitEdges;
                min = Math.Max(min, paddingSum + explicitWidth + explicitEdges);

                if (oldSum.HasValue)
                    maxSum = Math.Max(maxSum, oldSum.Value);
                HoldToMinWidth(minWidthEdge, lineBeforeBox, pathBeforeBox, ref min, ref maxSum);
                return;
            }
        }

        // This box's own border and padding, and a table's spacing between its cells, are on the
        // path of everything inside it: its line and each word in it are that much wider. They come
        // off again below, once the box is done, so its siblings do not carry them.
        //
        // A table in the collapsing border model and its cells take halves of the borders they
        // share, resolved when the table is laid out; one measured before that is resolved here.
        if (box.Display is CssConstants.Table or CssConstants.InlineTable)
            CssLayoutEngineTable.EnsureCollapsedBorders(box);

        double edges = box.ActualBorderLeftWidth + box.ActualBorderRightWidth + box.ActualPaddingRight + box.ActualPaddingLeft;
        if (box.Display == CssConstants.Table)
            edges += CssLayoutEngineTable.GetTableSpacing(box);

        maxSum += edges;
        paddingSum += edges;
        min = Math.Max(min, paddingSum);

        // CSS Sizing 3 §5.2.1: a replaced box's intrinsic inline size is its own — it has no
        // contents to walk for one. An <img> carries a word to be measured, but a <canvas>, a
        // <video> or an <svg> carries none: this walk found nothing in it and contributed zero, so
        // a shrink-to-fit box around one came out as wide as its other content and no wider. The
        // canvas of css-sizing/intrinsic-percent-replaced-001 sizes itself correctly from its
        // `height: 100%` and its ratio; it was the float around it that collapsed to nothing.
        if (box.Words.Count == 0
            && box.IntrinsicReplacedSize is { Width: > 0, Height: > 0 } natural)
        {
            box.ResolveReplacedContentSize(natural, box.ContainingBlock?.Size.Width ?? 0,
                out double replacedContentWidth, out _);

            maxSum += replacedContentWidth;
            min = Math.Max(min, paddingSum + replacedContentWidth);
            lineHoldsContent = true;
        }
        else if (box.Words.Count > 0)
        {
            // calculate the min and max sum for all the words in the box; the space before the
            // first is left out at the start of a line (see above), unless break-spaces keeps it
            // there, and a forced break starts a line with nothing on it
            foreach (CssRect word in box.Words)
            {
                maxSum += word.FullWidth + (word.HasSpaceBefore && (lineHoldsContent || word.OwnerBox.WhiteSpace == "break-spaces") ? word.OwnerBox.ActualWordSpacing : 0);
                min = Math.Max(min, paddingSum + word.Width);
                lineHoldsContent = !word.IsLineBreak;
            }

            // CSS Text 3 §5.1: the edge of an element inside a word is no place to break a line
            // (SoftWrapOpportunities), so the widest thing that cannot be broken can reach from
            // this box's last word into the boxes after it. Only the words were measured: a float
            // holding "aaa<b>bbb</b>" was as wide as "bbb", where browsers make it as wide as both.
            // The word is measured from where it starts, so each one once.
            //
            // The run takes in the right border and padding of each inline box it walks out of,
            // where they are, so they come off paddingSum, which holds them for every box on the
            // path, and so do the left ones where the word is not the box's first. Counted twice,
            // they made a table cell holding a <code> with 6px of padding on each side, then a
            // full stop, 72.25px wide, where browsers make it 66.27px.
            var last = box.Words[^1];
            if (box.WhiteSpace is not (CssConstants.NoWrap or CssConstants.Pre) && !SoftWrapOpportunities.After(last)
                && (box.Words.Count > 1 || SoftWrapOpportunities.BeforeFirstWord(box)))
            {
                double start = paddingSum - SoftWrapOpportunities.InlinePathEdges(box, left: box.Words.Count > 1);
                min = Math.Max(min, start + last.Width + SoftWrapOpportunities.RunAfterLastWord(null, box, double.PositiveInfinity));
            }
        }
        else if (box.TryGetFlexRowIntrinsicContentWidths(out double flexMin, out double flexMax))
        {
            // CSS Flexbox §9.9.1: a row flex container's items sit side by side on this box's line,
            // so their widths add up. Walked as children below, each blockified item would start a
            // line of its own and the row would measure as its widest item.
            maxSum += flexMax;
            min = Math.Max(min, paddingSum + flexMin);
        }
        else
        {
            // A table row's cells sit side by side and nothing wraps between them, so its minimum is
            // the sum of theirs, where anywhere else a minimum is the widest thing on any one path.
            bool sumsChildMinimums = box.Display == CssConstants.TableRow;
            double rowMin = paddingSum;

            // recursively on all the child boxes
            for (int i = 0; i < box.Boxes.Count; i++)
            {
                CssBox childBox = box.Boxes[i];

                // CSS Sizing 3 §5: a box's intrinsic sizes come from its in-flow content, and an
                // absolutely or fixed positioned child is out of flow: it takes no room on any line
                // or in any word's path. This walk measured it like any other child, so a dropdown
                // menu 300px wide, positioned in an inline-block, a float or a table cell, made the
                // box around it 300px wide, where browsers size that box to the rest of its content.
                if (IsOutOfFlowPositioned(childBox))
                    continue;

                // A <br> forces a line break, so max-content is the widest line:
                // close the running line here and start a fresh one. Otherwise the
                // recursion below (a <br> computes to a block box, which resets and
                // then restores the running sum) leaves the following inline content
                // accumulating on the same line — `A<br>B` would measure as A + B
                // instead of max(A, B), doubling a multi-line item's width.
                if (childBox.IsBrElement)
                {
                    oldSum = oldSum.HasValue ? Math.Max(oldSum.Value, maxSum) : maxSum;
                    maxSum = marginSum + paddingSum;
                    lineHoldsContent = false;
                    continue;
                }

                // A collapsible space *between* two atomic inline-level siblings is a real advance
                // on the line: `<span>A</span> <span>B</span>` is one space wider than the same
                // markup with no space. But that space is a text box of its own, and collapsing
                // clears its words (a space is normally carried as a HasSpaceBefore/After flag on
                // an adjacent word, and here the neighbours are in *other* boxes), so the recursion
                // measured it as zero. The shrink-to-fit container then came out exactly one space
                // too narrow and the last item wrapped — two 10px inline-blocks measured 20px and
                // stacked, where they need 24px to sit side by side. Layout itself lays the space
                // out; only the intrinsic measurement was missing it. One that starts a line is
                // removed there (see above), and takes no room, unless break-spaces keeps it.
                if (IsCollapsedWhitespaceSeparator(childBox))
                {
                    double space = childBox.ActualWordSpacing;
                    if (double.IsNaN(space))
                        space = box.ActualWordSpacing;
                    if (!double.IsNaN(space) && (lineHoldsContent || childBox.WhiteSpace == "break-spaces"))
                        maxSum += space;
                    continue;
                }

                marginSum += childBox.ActualMarginLeft + childBox.ActualMarginRight;

                // CSS Sizing 3 §5: an inline-level child sits on the running line, so its own
                // horizontal margins advance that line and belong in the sum. Only the
                // block-level case was covered — a block child restarts its line at marginSum,
                // which carries them — so the margins of an inline box, and of an inline
                // *replaced* box in particular, contributed nothing. A shrink-to-fit container
                // around one then came out exactly those margins too narrow: MediaWiki wraps
                // every thumbnail in a `display: table` figure whose image carries `margin: 3px`,
                // so the figure measured 6px under, and `max-width` scaled the photo down to fit
                // a box that should have fitted it exactly.
                if (!StartsNewMaxContentLine(childBox) && childBox.Display != CssConstants.None)
                    maxSum += childBox.ActualMarginLeft + childBox.ActualMarginRight;

                // A box laying out lines of its own starts them with nothing on them. After it, the
                // line it sits on holds it if it sits there whole, as an inline-block does, holds
                // what it did before if it floats (or is a table cell), and starts again after a
                // block.
                bool floatsOrCell = childBox.Float != CssConstants.None || childBox.Display == CssConstants.TableCell;
                bool ownLines = childBox.Display != CssConstants.None
                    && (floatsOrCell || IsAtomicInlineLevel(childBox.Display) || StartsNewMaxContentLine(childBox));
                bool contentBefore = lineHoldsContent;

                if (ownLines)
                    lineHoldsContent = false;

                if (sumsChildMinimums)
                {
                    // The cell's minimum is measured with this row's path; what the cell adds to it
                    // is its share of the row.
                    double childMin = 0;
                    GetMinMaxSumWordsOnLine(childBox, ref childMin, ref maxSum, ref paddingSum, ref marginSum, ref lineHoldsContent, null);
                    rowMin += Math.Max(0, childMin - paddingSum);
                }
                else
                {
                    GetMinMaxSumWordsOnLine(childBox, ref min, ref maxSum, ref paddingSum, ref marginSum, ref lineHoldsContent, null);
                }

                if (ownLines)
                    lineHoldsContent = floatsOrCell ? contentBefore : IsAtomicInlineLevel(childBox.Display);

                marginSum -= childBox.ActualMarginLeft + childBox.ActualMarginRight;
            }

            if (sumsChildMinimums)
                min = Math.Max(min, rowMin);
        }

        paddingSum -= edges;

        // max sum is max of all the lines in the box
        if (oldSum.HasValue)
            maxSum = Math.Max(maxSum, oldSum.Value);

        HoldToMinWidth(minWidthEdge, lineBeforeBox, pathBeforeBox, ref min, ref maxSum);
    }

    /// <summary>
    /// The width from border edge to border edge that the <c>min-width</c> of
    /// <paramref name="box"/>, an atomic inline-level box, holds it to, or 0 where it holds nothing:
    /// for any other box, and for a percentage.
    /// </summary>
    /// <remarks>
    /// CSS Sizing 3 §5.2: a box's min- and max-content contributions are held to its min and max
    /// sizes. The layout of an inline-block holds it to its <c>min-width</c>, but this walk did not,
    /// so a box around one was measured too narrow for it. The icon-only buttons of Wikipedia's
    /// header are inline-flex labels with <c>min-width: 44px</c> around a 20px icon: measured
    /// around the icon alone, each dropdown holding one came out 22px narrower than browsers make
    /// it, and its icon was drawn over the link after it. A percentage resolves against the width
    /// being measured, so it holds nothing here (§5.2.1).
    /// <para>
    /// Form controls are measured as before. Broiler.HTML's default style gives an
    /// <c>&lt;input&gt;</c>, a <c>&lt;select&gt;</c> and a <c>&lt;textarea&gt;</c> placeholder
    /// minimums (173px, 60px and 170px) where browsers size them from their content and
    /// attributes; held to those, the boxes around an <c>&lt;input size=2&gt;</c> or a short
    /// <c>&lt;select&gt;</c> came out 50-150px wider than browsers make them.
    /// </para>
    /// </remarks>
    private static double AtomicInlineMinWidth(CssBox box)
    {
        string minWidth = box.MinWidth;
        if (!IsAtomicInlineLevel(box.Display) || string.IsNullOrEmpty(minWidth) || minWidth == "0"
            || minWidth == CssConstants.Auto || minWidth.Contains('%') || IsFormControlWithDefaultMinWidth(box))
            return 0;

        double length = CssLengthParser.ParseLength(
            minWidth, box.ContainingBlock?.Size.Width ?? 0, box.GetEmHeight());
        if (!(length > 0))
            return 0;

        double edges = box.ActualBorderLeftWidth + box.ActualBorderRightWidth
                     + box.ActualPaddingLeft + box.ActualPaddingRight;
        return box.BoxSizing.Equals("border-box", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(length, edges)
            : length + edges;
    }

    /// <summary>
    /// Whether <paramref name="box"/> is an <c>&lt;input&gt;</c>, a <c>&lt;select&gt;</c> or a
    /// <c>&lt;textarea&gt;</c>, the form controls that the default style gives a placeholder
    /// minimum width (see <see cref="AtomicInlineMinWidth"/>).
    /// </summary>
    private static bool IsFormControlWithDefaultMinWidth(CssBox box)
    {
        string? name = box.HtmlTag?.Name;
        return name != null
            && (name.Equals("input", StringComparison.OrdinalIgnoreCase)
                || name.Equals("select", StringComparison.OrdinalIgnoreCase)
                || name.Equals("textarea", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Holds what a box adds to the running line, from <paramref name="lineBeforeBox"/>, and its
    /// minimum, past <paramref name="pathBeforeBox"/>, to at least <paramref name="minWidthEdge"/>
    /// (<see cref="AtomicInlineMinWidth"/>).
    /// </summary>
    /// <remarks>
    /// CSS Sizing 3 §5.2: a box's contributions include its margins, and a negative margin takes
    /// room off. The path is measured with the margins of the boxes the box is in, as its line is.
    /// Vector's main menu dropdown is a block with <c>margin: 0 -12px</c> around a button with
    /// <c>min-width: 44px</c>, and browsers make the flex item holding it 20px wide. Held past the
    /// padding alone, the button made that item's minimum 44px, 24px more than its line, and the
    /// item, which cannot be narrower than its minimum, came out 44px wide: the logo after it
    /// stood 24px right of where browsers put it.
    /// </remarks>
    private static void HoldToMinWidth(
        double minWidthEdge, double lineBeforeBox, double pathBeforeBox, ref double min, ref double maxSum)
    {
        if (minWidthEdge <= 0)
            return;

        maxSum = Math.Max(maxSum, lineBeforeBox + minWidthEdge);
        min = Math.Max(min, pathBeforeBox + minWidthEdge);
    }

    /// <summary>
    /// CSS Text 3 §4.1.1: collapsible white space at the very end of a
    /// formatting context's last line is removed.  Broiler carries a collapsed
    /// space as word-spacing on the neighbouring word (<c>HasSpaceAfter</c>),
    /// so the preferred-width sum in <see cref="GetMinMaxSumWords"/> counts
    /// the trailing space-after of <paramref name="box"/>'s last word.  Returns
    /// that spacing (in CSS px) so the caller can subtract it from the
    /// shrink-to-fit width; a box that ends on a real word (no edge space)
    /// contributes nothing.  The walk itself leaves out the white space that
    /// starts a line, the space before the first word among it.
    /// </summary>
    internal static double EdgeWhitespaceSpacing(CssBox box)
    {
        double sum = 0;

        var last = LastContentWord(box);
        if (last != null && last.HasSpaceAfter && !last.IsImage && last.OwnerBox != null)
            sum += last.OwnerBox.ActualWordSpacing;

        return sum > 0 ? sum : 0;
    }

    /// <summary>
    /// Returns the last word in document order within <paramref name="box"/>'s
    /// in-flow inline content, or <c>null</c> if it has none.
    /// </summary>
    private static CssRect LastContentWord(CssBox box)
    {
        if (box.Words.Count > 0)
            return box.Words[^1];

        for (int i = box.Boxes.Count - 1; i >= 0; i--)
        {
            if (box.Boxes[i].Display == CssConstants.None)
                continue;
            var w = LastContentWord(box.Boxes[i]);
            if (w != null)
                return w;
        }

        return null;
    }

    /// <summary>
    /// CSS2.1 §9.5.2: Returns the maximum bottom outer edge of preceding
    /// floats that the given box needs to clear, considering the box's
    /// <c>clear</c> direction (<c>left</c>, <c>right</c>, or <c>both</c>).
    /// </summary>
    public static double GetMaxFloatBottom(CssBox box)
    {
        double maxBottom = 0;
        List<(string tag, double bottom)> considered = null;

        if (box.ParentBox == null)
            return maxBottom;

        string clearDir = box.Clear;

        // Walk up the ancestor chain to find floats in the same block
        // formatting context (BFC).  Floats from ancestor-level siblings
        // are relevant for clearance even when the cleared element is
        // nested deeper (CSS2.1 §9.5.2).
        CssBox current = box;
        while (current.ParentBox != null)
        {
            foreach (var sibling in current.ParentBox.Boxes)
            {
                if (sibling == current) break;
                CollectMaxFloatBottom(sibling, clearDir, ref maxBottom, ref considered);
            }

            // Stop at BFC boundaries — floats in an outer BFC don't
            // participate in clearance for elements in an inner BFC.
            if (EstablishesBfc(current.ParentBox))
                break;

            current = current.ParentBox;
        }

        if (considered != null && considered.Count > 0)
        {
            Debug.WriteLine($"[ClearFloat] Clearance for <{box.HtmlTag?.Name ?? "?"}> clear={box.Clear}: " +
                $"considered {considered.Count} float(s), maxBottom={maxBottom:F1}");
            foreach (var (tag, bottom) in considered)
                Debug.WriteLine($"  - <{tag}> bottom={bottom:F1}");
        }

        return maxBottom;
    }

    /// <summary>
    /// Collects the maximum bottom coordinate of floats in the same
    /// block formatting context (BFC) that match the <paramref name="clearDir"/>
    /// direction.  Floated elements establish a new BFC, so their descendant
    /// floats are excluded from clearance calculations outside.
    /// </summary>
    private static void CollectMaxFloatBottom(CssBox box, string clearDir, ref double maxBottom, ref List<(string tag, double bottom)> considered)
    {
        if (box.Float != CssConstants.None)
        {
            // CSS2.1 §9.5.2: Only consider floats in the matching direction.
            // clear:left → only left floats, clear:right → only right floats,
            // clear:both → all floats.
            bool matchesDirection = clearDir == "both"
                || string.Equals(box.Float, clearDir, StringComparison.OrdinalIgnoreCase);

            if (!matchesDirection)
                return;

            // Compute the float's margin-box bottom ("bottom outer edge"
            // per CSS2.1 §9.5.2) so that clearance positions the cleared
            // element below the float's full margin box.
            // CSS2.1 §10.5: Percentage heights resolve to auto when the
            // containing block's height is not explicitly specified —
            // use ActualBottom (layout-computed) in that case.
            double bottom;
            bool hasExplicitHeight = box.Height != CssConstants.Auto && !string.IsNullOrEmpty(box.Height);

            if (hasExplicitHeight && !box.HeightPercentageResolvesToAuto())
                bottom = box.Location.Y + box.ActualHeight
                    + box.ActualPaddingTop + box.ActualPaddingBottom
                    + box.ActualBorderTopWidth + box.ActualBorderBottomWidth
                    + box.ActualMarginBottom;
            else
                bottom = box.ActualBottom
                    + box.ActualMarginBottom;

            maxBottom = Math.Max(maxBottom, bottom);

            considered ??= [];
            considered.Add((box.HtmlTag?.Name ?? box.Display, bottom));

            // Float establishes a new BFC – don't recurse into descendants.
            return;
        }

        foreach (var child in box.Boxes)
            CollectMaxFloatBottom(child, clearDir, ref maxBottom, ref considered);
    }

    /// <summary>
    /// Whether <paramref name="box"/> is the document's root element box — the
    /// <c>&lt;html&gt;</c> box the anonymous document box wraps.
    /// </summary>
    /// <remarks>
    /// CSS2.1 §8.3.1 exempts the root element from margin collapsing, which is what keeps
    /// the body's bottom margin inside the document's height: the canvas is the root's
    /// margin box (§11.1.1), so a collapsed-through body margin shortens the whole page.
    /// Acid1's <c>--full-page</c> capture came out 405px tall instead of 420 for exactly
    /// that reason — the black body border ended up flush with the bottom edge of the image
    /// with none of the blue canvas below it.
    /// </remarks>
    internal static bool IsRootElement(CssBox box) =>
        box.HtmlTag is { } tag && tag.Name.Equals("html", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="box"/> has top padding or a top border, which separates its top
    /// margin from its first in-flow child's (CSS2.1 §8.3.1).
    /// </summary>
    /// <remarks>
    /// Margins are adjoining only where "no line boxes, no clearance, no padding and no border
    /// separate them", and any padding or border does, however thin. Padding and borders under
    /// 0.1px were taken for none: www.mediawiki.org's <c>.mw-page-container</c> has
    /// <c>padding-top: 0.05px</c> to keep its site notice's 24px top margin inside it, and began
    /// 24px below the header, where browsers begin it right below.
    /// </remarks>
    internal static bool HasTopPaddingOrBorder(CssBox box) =>
        box.ActualPaddingTop > 0 || box.ActualBorderTopWidth > 0;

    /// <summary>
    /// Whether <paramref name="box"/> has bottom padding or a bottom border, which separates its
    /// bottom margin from its last in-flow child's (CSS2.1 §8.3.1), however thin, as <see
    /// cref="HasTopPaddingOrBorder"/> has it at the top.
    /// </summary>
    internal static bool HasBottomPaddingOrBorder(CssBox box) =>
        box.ActualPaddingBottom > 0 || box.ActualBorderBottomWidth > 0;

    /// <summary>
    /// Returns the effective bottom margin for a box, accounting for
    /// parent-child bottom-margin collapse (CSS 2.1 §8.3.1).
    /// When a box has no bottom border, no bottom padding, and auto height,
    /// the last in-flow block-level child's bottom margin collapses with
    /// the box's own bottom margin.  This is applied recursively.
    /// </summary>
    internal static double GetPropagatedMarginBottom(CssBox box)
    {
        double mb = box.ActualMarginBottom;

        if (HasBottomPaddingOrBorder(box))
            return mb;

        // CSS2.1 §8.3.1: "Margins of the root element's box do not collapse." The root
        // keeps its own margin and its last child's margin stays inside its height, so
        // nothing propagates out. See IsRootElement.
        if (IsRootElement(box))
            return mb;

        // Nor do those of a box that establishes a new block formatting context with its
        // children's: its last child's margin stays inside it (see MarginBottomCollapse).
        if (EstablishesBfc(box))
            return mb;

        if (box.Height != CssConstants.Auto && !string.IsNullOrEmpty(box.Height))
        {
            bool resolvedToAuto = box.Height.Contains('%')
                && (box.ContainingBlock.Height == CssConstants.Auto
                    || string.IsNullOrEmpty(box.ContainingBlock.Height));
            if (!resolvedToAuto)
                return mb;
        }

        // Find last in-flow block-level child (CSS 2.1 §8.3.1).
        CssBox? lastInFlow = null;
        foreach (var child in box.Boxes)
        {
            if (child.Float != CssConstants.None
                || child.Position == CssConstants.Absolute
                || child.Position == CssConstants.Fixed)
                continue;

            if (child.Display == CssConstants.Inline
                || child.Display == CssConstants.InlineBlock)
                continue;

            // CSS2.1 §9.2.4 again: a box that is not generated cannot be the last in-flow child
            // whose bottom margin propagates out of its parent.
            if (child.Display == CssConstants.None)
                continue;

            lastInFlow = child;
        }

        if (lastInFlow == null)
            return mb;

        // An empty last child's margins collapse through it (§8.3.1), and with the margins before
        // it, so this box's margin joins the whole set: the margins the child stands below, its own
        // and its children's. Only its own were taken: after a 30px block, an empty last child with
        // margin-top: 20px handed on nothing, where browsers put the 20px below this box.
        // CssBox.MarginBottomCollapse ends the box where the set begins. The set of a first child
        // begins above this box, which is then empty itself and hands the set on as one, and one
        // that collapses with a top margin that has clearance stays inside this box (§8.3.1; see
        // CssBox.ClearsFloats).
        if (IsEmptyCollapsible(lastInFlow) && !lastInFlow.MarginTopCollapsesWithParent)
        {
            if (lastInFlow.ClearsFloats)
                return mb;

            double setPositive = Math.Max(Math.Max(mb, 0), lastInFlow.CollapsedMarginTop);
            double setNegative = Math.Min(Math.Min(mb, 0), lastInFlow.NegativeMarginTopAbove);
            CollectEmptyBoxMargins(lastInFlow, ref setPositive, ref setNegative);
            return setPositive + setNegative;
        }

        double childMb = GetPropagatedMarginBottom(lastInFlow);

        // Collapse: max(positives,0) + min(negatives,0)
        double maxPos = Math.Max(Math.Max(mb, 0), Math.Max(childMb, 0));
        double minNeg = Math.Min(Math.Min(mb, 0), Math.Min(childMb, 0));
        return maxPos + minNeg;
    }

    /// <summary>
    /// Computes the vertical offset applied by <c>position: relative</c>.
    /// CSS2.1 §9.4.3: <c>top</c> takes precedence over <c>bottom</c>.
    /// Returns 0 if the element is not relatively positioned or has no offset.
    /// </summary>
    internal static double GetRelativeOffsetY(CssBoxProperties box)
    {
        bool hasTop = box.Top != null && box.Top != CssConstants.Auto;
        bool hasBottom = box.Bottom != null && box.Bottom != CssConstants.Auto;

        if (hasTop)
            return CssLengthParser.ParseLength(box.Top, box.Size.Height, box.GetEmHeight());

        if (hasBottom)
            return -CssLengthParser.ParseLength(box.Bottom, box.Size.Height, box.GetEmHeight());

        return 0;
    }

    /// <summary>
    /// Computes the horizontal offset applied by <c>position: relative</c>.
    /// CSS2.1 §9.4.3: <c>left</c> takes precedence over <c>right</c> (in LTR).
    /// Returns 0 if the element is not relatively positioned or has no offset.
    /// </summary>
    internal static double GetRelativeOffsetX(CssBoxProperties box)
    {
        bool hasLeft = box.Left != null && box.Left != CssConstants.Auto;
        bool hasRight = box.Right != null && box.Right != CssConstants.Auto;

        if (hasLeft)
            return CssLengthParser.ParseLength(box.Left, box.Size.Width, box.GetEmHeight());

        if (hasRight)
            return -CssLengthParser.ParseLength(box.Right, box.Size.Width, box.GetEmHeight());

        return 0;
    }

    /// <summary>
    /// Collects all float boxes in the same block formatting context that
    /// precede <paramref name="box"/> in the DOM tree. This includes floats
    /// nested inside non-BFC siblings (e.g., floated <c>li</c> elements
    /// inside a non-floated <c>ul</c>) and floats that are siblings of
    /// ancestor elements when those ancestors do not establish a new BFC
    /// (CSS2.1 §9.4.1).
    /// </summary>
    internal static List<CssBox> CollectPrecedingFloatsInBfc(CssBox box)
    {
        var result = new List<CssBox>();
        if (box.ParentBox == null) return result;

        // Collect preceding sibling floats (and their non-BFC subtrees).
        foreach (var sibling in box.ParentBox.Boxes)
        {
            if (sibling == box) break;
            CollectFloatsInSubtree(sibling, result);
        }

        // Walk up ancestor chain: collect floats from each ancestor's
        // preceding siblings while the ancestor does not establish a BFC.
        var current = box.ParentBox;
        while (current != null && current.ParentBox != null)
        {
            if (EstablishesBfc(current))
                break;

            foreach (var sibling in current.ParentBox.Boxes)
            {
                if (sibling == current) break;
                CollectFloatsInSubtree(sibling, result);
            }

            current = current.ParentBox;
        }

        return result;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="box"/> establishes a new
    /// block formatting context (CSS2.1 §9.4.1, CSS Box Alignment §5.4).
    /// <para>
    /// The single source of truth for the question. Four near-copies of this
    /// list had drifted apart across the engine (one had never gained the
    /// flex/grid displays), so a display that establishes a BFC was recognised
    /// in some float calculations and not others — which is how
    /// <c>display: flow-root</c>, whose <em>only</em> job is to establish one,
    /// came to establish none. The callers that guard on <c>float</c>/
    /// <c>position</c> themselves before asking still do; those conditions are
    /// simply redundant there, not wrong.
    /// </para>
    /// </summary>
    internal static bool EstablishesBfc(CssBox box)
    {
        return box.Float != CssConstants.None
            || box.Display == CssConstants.InlineBlock
            || box.Display == CssConstants.TableCell
            // CSS2.1 §9.4.1 names table captions with table cells. A caption's first child's
            // top margin collapsed through the caption's top, and the table put the caption
            // back where it belongs and the child with it: <caption><p style="margin-top:
            // 16px"> lost the 16px, where browsers keep it inside the caption.
            || box.Display == CssConstants.TableCaption
            // CSS2.1 §17.4: the table wrapper box, which the table's box stands for here,
            // establishes a block formatting context of its own.
            || box.Display == CssConstants.Table
            || box.Display == CssConstants.InlineTable
            // CSS Display 3 §2.5: `flow-root` is exactly "block box that
            // establishes a new block formatting context".
            || box.Display == "flow-root"
            || box.Display is "flex" or "inline-flex" or "grid" or "inline-grid"
            || box.Position == CssConstants.Absolute
            || box.Position == CssConstants.Fixed
            || (box.Overflow != null && box.Overflow != CssConstants.Visible)
            || (box.AlignContent != null && box.AlignContent != "normal")
            || IsFlexOrGridItem(box);
    }

    /// <summary>
    /// Whether <paramref name="box"/> is an item of a flex or grid container, which establishes an
    /// independent formatting context for its contents (CSS Flexbox §4, CSS Grid §6.2) whatever its
    /// own <c>display</c>, so its children's margins do not collapse through its edges.
    /// </summary>
    /// <remarks>
    /// Without it, a block item's first child's top margin collapsed through the item's top: in a
    /// column, an unpadded item holding a paragraph with 16px margins sat 16px low with the margin
    /// outside it, and in a row or a grid, where the item is placed at the top of its line or area
    /// whatever its margins, the paragraph's margin was lost.
    /// </remarks>
    private static bool IsFlexOrGridItem(CssBox box) =>
        box.ParentBox is { Display: "flex" or "inline-flex" or "grid" or "inline-grid" }
        && box.Display != CssConstants.None
        && box.Position is not (CssConstants.Absolute or CssConstants.Fixed);

    private static void CollectFloatsInSubtree(CssBox root, List<CssBox> result)
    {
        if (root.Float != CssConstants.None && root.Display != CssConstants.None)
        {
            result.Add(root);
            // Float establishes a new BFC – don't recurse into descendants.
            return;
        }

        // CSS2.1 §9.5: Don't recurse into elements that establish a new
        // block formatting context — their inner floats don't participate
        // in the parent BFC's float list.
        if (EstablishesBfc(root))
            return;

        foreach (var child in root.Boxes)
            CollectFloatsInSubtree(child, result);
    }

    /// <summary>
    /// CSS2.1 §8.3.1: Returns <c>true</c> if a box is "empty" — its own
    /// top and bottom margins are adjoining and collapse through.
    /// Conditions: min-height is zero, no top/bottom borders or padding,
    /// height is 0 or auto (or percentage that resolves to auto), no line
    /// boxes, and all in-flow children's margins also collapse.
    /// </summary>
    internal static bool IsEmptyCollapsible(CssBox box)
    {
        // CSS2.1 §8.3.1: a box that establishes a new block formatting context
        // (float, inline-block, grid/flex, overflow≠visible, abspos, table-cell)
        // does not collapse its top and bottom margins through itself — even with
        // no content — so an empty such box still separates its neighbours by both
        // its margins. Chromium keeps an empty overflow:hidden / grid container's
        // top and bottom margins distinct; only a plain in-flow block collapses
        // through. (Floats/abspos are already excluded from flow collapsing, but
        // covering them here keeps the predicate self-consistent.)
        if (EstablishesBfc(box))
            return false;

        if (HasTopPaddingOrBorder(box) || HasBottomPaddingOrBorder(box))
            return false;

        // Check if height resolves to zero/auto
        if (box.Height != CssConstants.Auto && !string.IsNullOrEmpty(box.Height))
        {
            bool resolvedToAuto = box.Height.Contains('%')
                && (box.ContainingBlock.Height == CssConstants.Auto
                    || string.IsNullOrEmpty(box.ContainingBlock.Height));

            if (!resolvedToAuto)
            {
                double h = CssLengthParser.ParseLength(box.Height, box.Size.Height, box.GetEmHeight());
                if (h > 0.1)
                    return false;
            }
        }

        // Zero content height — ActualBottom should equal Location.Y
        // (tolerance 0.5 accounts for sub-pixel rounding in layout)
        if (Math.Abs(box.ActualBottom - box.Location.Y) > 0.5)
            return false;

        // Must not contain any line boxes with actual content.
        // CreateLineBoxes always creates at least one CssLineBox for any
        // element that enters the inline-formatting path, even if the
        // element is empty.  An empty line box (no words) does not
        // constitute "content" for margin-through-collapse purposes.
        //
        // CSS2.1 §8.3.1: When height is explicitly 0, line boxes contain
        // overflowing content that doesn't prevent margin collapse.  Only
        // check for line-box content when height is auto.
        bool hasExplicitZeroHeight = box.Height != CssConstants.Auto
            && !string.IsNullOrEmpty(box.Height);

        if (!hasExplicitZeroHeight)
        {
            foreach (var lb in box.LineBoxes)
            {
                if (lb.Words.Count > 0)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Collects the maximum positive and minimum negative margins from an
    /// empty collapsible box and all its in-flow children (recursively for
    /// children that are also empty and collapsible).
    /// </summary>
    internal static void CollectEmptyBoxMargins(CssBox box, ref double maxPos, ref double maxNeg)
    {
        maxPos = Math.Max(maxPos, Math.Max(box.ActualMarginTop, 0));
        maxPos = Math.Max(maxPos, Math.Max(box.ActualMarginBottom, 0));
        maxNeg = Math.Min(maxNeg, Math.Min(box.ActualMarginTop, 0));
        maxNeg = Math.Min(maxNeg, Math.Min(box.ActualMarginBottom, 0));

        foreach (var child in box.Boxes)
        {
            if (child.Float != CssConstants.None
                || child.Position == CssConstants.Absolute
                || child.Position == CssConstants.Fixed)
                continue;

            // CSS2.1 §9.2.4: a display:none element generates no box, so it has no margins to
            // collapse through. Collecting them anyway hands a hidden element's margin to the
            // next visible sibling: www.mediawiki.org's empty .vector-column-start holds two
            // display:none pinned containers with `margin-bottom: 32px`, and that 32px was
            // separating the site notice from the article.
            if (child.Display == CssConstants.None)
                continue;

            maxPos = Math.Max(maxPos, Math.Max(child.ActualMarginTop, 0));
            maxPos = Math.Max(maxPos, Math.Max(child.ActualMarginBottom, 0));
            maxNeg = Math.Min(maxNeg, Math.Min(child.ActualMarginTop, 0));
            maxNeg = Math.Min(maxNeg, Math.Min(child.ActualMarginBottom, 0));

            if (IsEmptyCollapsible(child))
                CollectEmptyBoxMargins(child, ref maxPos, ref maxNeg);
        }
    }

    /// <summary>
    /// Returns the effective bottom margin for a box, accounting for margins
    /// that collapse through the box when it is "empty" per CSS2.1 §8.3.1.
    /// For non-empty boxes returns <see cref="CssBoxProperties.ActualMarginBottom"/>.
    /// </summary>
    internal static double GetEffectiveMarginBottom(CssBox box)
    {
        if (!IsEmptyCollapsible(box))
            return box.ActualMarginBottom;

        // The empty box's own margins join the set collapsed above it, as CssBox.MarginTopCollapse
        // takes it for the box after the empty one.
        double maxPos = box.CollapsedMarginTop, maxNeg = box.NegativeMarginTopAbove;
        CollectEmptyBoxMargins(box, ref maxPos, ref maxNeg);

        double collapsed = maxPos + maxNeg;
        return collapsed - box.MarginSpentAboveTop;
    }
}
