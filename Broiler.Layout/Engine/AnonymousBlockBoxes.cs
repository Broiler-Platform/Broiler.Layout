using Broiler.CSS;

namespace Broiler.Layout.Engine;

/// <summary>
/// CSS 2.1 §9.2.1.1: a block container that holds a block-level box holds only block-level
/// boxes, so each run of inline-level content between them goes into an anonymous block box.
/// </summary>
/// <remarks>
/// <para>
/// <c>DomParser.CorrectInlineBoxesParent</c> makes those anonymous blocks, but not everywhere. It
/// leaves an inline-block's children as they are, and it goes down into a box's children only
/// when they are not all inline-level, so it never reaches what is inside an inline-level box: an
/// inline-block, an inline-flex or inline-grid container's items, or a block anywhere under one.
/// A box holding text beside a block there was laid out child by child, as blocks, and the text,
/// which a box of text laid out that way gets no line box for, was not painted at all:
/// <c>&lt;li style="display: inline-block"&gt;Menu&lt;ul style="position: absolute"&gt;</c>,
/// a dropdown menu's usual markup, lost its "Menu".
/// </para>
/// <para>
/// This pass makes the anonymous blocks the parser left out, the way it makes them: a run is the
/// inline-level children between two others, and a float or a <c>display: none</c> child neither
/// counts toward it nor makes the box need one. It runs once the parser is done, at the start of
/// the document's layout, because the parser's own passes, which split an inline box around a
/// block inside it, are what leave some of these boxes holding both. A box whose inline-level
/// content is broken only by <c>&lt;br&gt;</c>s keeps it: the inline-block path lays that out as
/// lines of its own (see <see cref="CssLayoutEngine.InlineContentWithBrsOnly"/>). It does nothing
/// to a tree it has run over already.
/// </para>
/// </remarks>
internal static class AnonymousBlockBoxes
{
    /// <summary>
    /// Wraps each run of inline-level children of every block container under
    /// <paramref name="root"/> that also holds a block-level child in an anonymous block.
    /// </summary>
    internal static void Generate(CssBox root)
    {
        if (root == null)
            return;

        if (HoldsBlockAndInlineLevelBoxes(root))
            WrapInlineRuns(root);

        foreach (var child in root.Boxes)
            Generate(child);
    }

    /// <summary>
    /// Whether <paramref name="box"/> lays its children out as a block container, and holds both an
    /// inline-level and a block-level one besides floats and <c>display: none</c> ones.
    /// </summary>
    private static bool HoldsBlockAndInlineLevelBoxes(CssBox box)
    {
        // A flex or grid container's children are its items, and a table's are its rows and
        // captions: neither lays out a run of inline-level content as a block container does.
        if (box.Display is not (CssConstants.Block or CssConstants.InlineBlock or CssConstants.ListItem
            or CssConstants.TableCell or CssConstants.TableCaption or "flow-root"))
        {
            return false;
        }

        if (CssLayoutEngine.InlineContentWithBrsOnly(box))
            return false;

        bool inlineLevel = false;
        bool blockLevel = false;

        foreach (var child in box.Boxes)
        {
            if (child.Display == CssConstants.None || child.Float != CssConstants.None)
                continue;

            if (child.IsInline)
                inlineLevel = true;
            else
                blockLevel = true;
        }

        return inlineLevel && blockLevel;
    }

    /// <summary>
    /// Moves each run of <paramref name="box"/>'s inline-level children into an anonymous block
    /// box put where the run began.
    /// </summary>
    private static void WrapInlineRuns(CssBox box)
    {
        for (int i = 0; i < box.Boxes.Count; i++)
        {
            if (!box.Boxes[i].IsInline)
                continue;

            // The block goes in before the run's first box, which it then takes, and the rest of
            // the run follows it in: each moves out of box.Boxes, so the next is at i again.
            var anonymousBlock = CssBoxHelper.CreateBlock(box, box.BaseUrl, null, box.Boxes[i++]);

            while (i < box.Boxes.Count && box.Boxes[i].IsInline)
                box.Boxes[i].ParentBox = anonymousBlock;
        }
    }
}
