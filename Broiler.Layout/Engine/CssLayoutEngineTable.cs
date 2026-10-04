using Broiler.CSS;
using Broiler.Layout.Diagnostics;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace Broiler.Layout.Engine;

internal sealed class CssLayoutEngineTable
{
    private readonly CssBox _tableBox;

    private CssBox _headerBox;
    private CssBox _footerBox;

    private readonly List<CssBox> _bodyrows = [];
    private readonly List<CssBox> _columns = [];
    private readonly List<CssBox> _allRows = [];

    // CSS2.1 §17.4.1: table-caption boxes are laid out as block boxes above
    // (caption-side:top, the default) or below the table box, spanning the
    // table's width. Broiler previously dropped them entirely, so caption text
    // never rendered; collect them here to lay out in LayoutCells.
    private readonly List<CssBox> _captions = [];

    // The widest of the captions' min-content contributions, which the table is at least as wide
    // as (see WidenToCaptions).
    private double _captionMinWidth;

    private int _columnCount;

    private bool _widthSpecified;

    private double[] _columnWidths;
    private double[] _columnMinWidths;
    private double[] _columnMaxWidths = [];

    // The widths the columns were given by their cells or their <col>s, NaN for the others, as
    // CalculateCountAndWidth found them before the others were given theirs.
    private double[] _specifiedColumnWidths = [];

    private CssLayoutEngineTable(CssBox tableBox) => _tableBox = tableBox;

    public static double GetTableSpacing(CssBox tableBox)
    {
        int count = 0;
        int columns = 0;

        foreach (var box in tableBox.Boxes)
        {
            if (box.Display == CssConstants.TableColumn)
            {
                columns += GetSpan(box);
            }
            else if (box.Display == CssConstants.TableRowGroup)
            {
                foreach (CssBox cr in tableBox.Boxes)
                {
                    count++;
                    if (cr.Display == CssConstants.TableRow)
                        columns = Math.Max(columns, cr.Boxes.Count);
                }
            }
            else if (box.Display == CssConstants.TableRow)
            {
                count++;
                columns = Math.Max(columns, box.Boxes.Count);
            }

            // limit the amount of rows to process for performance
            if (count > 30)
                break;
        }

        // +1 columns because padding is between the cell and table borders
        return (columns + 1) * GetHorizontalSpacing(tableBox);
    }

    /// <summary>
    /// Resolves the collapsed borders of a table in the collapsing border model that has not been
    /// laid out yet, so that its intrinsic widths count the halves of the borders its cells and it
    /// take (see <see cref="ResolveCollapsedBorders"/>), not their own borders whole.
    /// </summary>
    internal static void EnsureCollapsedBorders(CssBox tableBox)
    {
        if (tableBox.CollapsedBorders != null
            || !string.Equals(tableBox.BorderCollapse, CssConstants.Collapse, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var table = new CssLayoutEngineTable(tableBox);
        table.AssignBoxKinds(tableBox.BaseUrl);
        table.InsertEmptyBoxes(tableBox.BaseUrl);
        table.ResolveCollapsedBorders();
    }

    public static void PerformLayout(ILayoutEnvironment g, CssBox tableBox, Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(tableBox);

        using var trace = LayoutWorkTrace.Measure(LayoutWorkTrace.Ops.Table);

        try
        {
            var table = new CssLayoutEngineTable(tableBox);
            table.Layout(g, baseUrl);
        }
        catch (Exception ex)
        {
            tableBox.LayoutEnvironment.ReportLayoutError("Failed table layout", ex);
        }
    }


    private void Layout(ILayoutEnvironment g, Uri baseUrl)
    {
        MeasureWords(_tableBox, g);

        // get the table boxes into the proper fields
        AssignBoxKinds(baseUrl);

        // Insert EmptyBoxes for vertical cell spanning.
        InsertEmptyBoxes(baseUrl);

        // CSS2.1 §17.6.2.1: resolve conflicting borders at each shared cell edge
        // before sizing, so the used (winning) border widths drive cell layout.
        ResolveCollapsedBorders();

        // CSS 2.1 §17.6.2: in the collapsing border model a table has no padding. In the separated
        // model it has, between its border and the spacing around its cells, and its `width` counts
        // it as it counts the border. The padding was dropped in both: `padding: 10px` around an x
        // left the table 8px wide and the x at its corner, where browsers make it 28px wide.
        if (_tableBox.BorderCollapse == CssConstants.Collapse)
            _tableBox.PaddingLeft = _tableBox.PaddingTop = _tableBox.PaddingRight = _tableBox.PaddingBottom = "0";

        // Determine Row and Column Count, and ColumnWidths
        var availCellSpace = CalculateCountAndWidth();

        // CSS 2.1 §17.5.2.1: the fixed table layout algorithm sizes the columns from their own
        // widths and the first row's alone, and a cell's content that is wider overflows it. There
        // was no such algorithm: a table with `table-layout: fixed; width: 100px` holding a 400px
        // block was 400px wide, where browsers keep it 100px.
        if (IsFixedLayout)
        {
            DetermineFixedColumnWidths(availCellSpace);
        }
        else
        {
            DetermineMissingColumnWidths(availCellSpace);

            // Check for minimum sizes (increment widths if necessary)
            EnforceMinimumSize();

            // While table width is larger than it should, and width is reducible
            EnforceMaximumSize();
        }

        // However wide that makes it, the table is as wide as its captions need.
        WidenToCaptions();

        //Actually layout cells!
        LayoutCells(g);
    }

    private void AssignBoxKinds(Uri baseUrl)
    {
        // CSS2.1 §17.2.1: Generate anonymous table-row boxes for table-cell
        // children that are direct children of the table without an
        // intermediate table-row wrapper.
        GenerateAnonymousTableRows(baseUrl);
        GenerateAnonymousRowsInRowGroups(baseUrl);

        foreach (var box in _tableBox.Boxes)
        {
            switch (box.Display)
            {
                case CssConstants.TableCaption:
                    _captions.Add(box);
                    break;

                case CssConstants.TableRow:
                    _bodyrows.Add(box);
                    break;

                case CssConstants.TableRowGroup:
                    foreach (CssBox childBox in box.Boxes)
                        if (childBox.Display == CssConstants.TableRow)
                            _bodyrows.Add(childBox);
                    break;
                
                case CssConstants.TableHeaderGroup:
                    if (_headerBox != null)
                        _bodyrows.Add(box);
                    else
                        _headerBox = box;
                    break;
                
                case CssConstants.TableFooterGroup:
                    if (_footerBox != null)
                        _bodyrows.Add(box);
                    else
                        _footerBox = box;
                    break;
                
                case CssConstants.TableColumn:
                    for (int i = 0; i < GetSpan(box); i++)
                        _columns.Add(box);
                    break;
                
                case CssConstants.TableColumnGroup:
                    if (box.Boxes.Count == 0)
                    {
                        int gspan = GetSpan(box);
                        for (int i = 0; i < gspan; i++)
                        {
                            _columns.Add(box);
                        }
                    }
                    else
                    {
                        foreach (CssBox bb in box.Boxes)
                        {
                            int bbspan = GetSpan(bb);
                            for (int i = 0; i < bbspan; i++)
                            {
                                _columns.Add(bb);
                            }
                        }
                    }
                    break;
            }
        }

        if (_headerBox != null)
            _allRows.AddRange(_headerBox.Boxes);

        _allRows.AddRange(_bodyrows);

        if (_footerBox != null)
            _allRows.AddRange(_footerBox.Boxes);

        // CSS2.1 §17.2.1: within each row, a child that is not a table-cell (a
        // block, inline, etc.) is wrapped — together with its consecutive
        // non-cell siblings — in an anonymous table-cell box. GenerateAnonymousTableRows
        // only wraps non-cell children of the *table*; a non-cell child of an
        // explicit <div style="display:table-row"> was left uncelled and never laid
        // out (WPT css-sizing/table-child-percentage-height-with-border-box).
        foreach (var row in _allRows)
            WrapRowNonCellChildrenInAnonymousCells(row, baseUrl);
    }

    /// <summary>
    /// CSS2.1 §17.2.1: wraps each run of consecutive non-<c>table-cell</c> children of
    /// <paramref name="row"/> in a single anonymous <c>table-cell</c> box, preserving
    /// child order. A no-op when every child is already a table-cell.
    /// </summary>
    private void WrapRowNonCellChildrenInAnonymousCells(CssBox row, Uri baseUrl)
    {
        bool needsWrapping = false;
        foreach (var child in row.Boxes)
        {
            if (child.Display != CssConstants.TableCell)
            {
                needsWrapping = true;
                break;
            }
        }

        if (!needsWrapping)
            return;

        var children = new List<CssBox>(row.Boxes);
        row.Boxes.Clear();

        List<CssBox>? pendingNonCell = null;

        void FlushAnonymousCell()
        {
            if (pendingNonCell == null)
                return;

            // The CssBox(parent, tag) constructor appends the new cell to row.Boxes,
            // keeping it in order after any real cells already re-added.
            var anonCell = new CssBox(row, null, baseUrl) { Display = CssConstants.TableCell };
            foreach (var child in pendingNonCell)
                child.ParentBox = anonCell;
            pendingNonCell = null;
        }

        foreach (var child in children)
        {
            if (child.Display == CssConstants.TableCell)
            {
                FlushAnonymousCell();
                row.Boxes.Add(child);
            }
            else
            {
                pendingNonCell ??= [];
                pendingNonCell.Add(child);
            }
        }

        FlushAnonymousCell();
    }

    /// <summary>
    /// CSS2.1 §17.2.1: Generate anonymous table-row boxes for children of a
    /// table element that are not proper table sub-elements (table-row,
    /// table-row-group, table-header-group, table-footer-group, table-caption,
    /// table-column, or table-column-group).  All consecutive non-row children
    /// are wrapped together in a single anonymous table-row.  Within each
    /// anonymous row, children that are not table-cell are additionally wrapped
    /// in anonymous table-cell boxes.
    /// </summary>
    private void GenerateAnonymousTableRows(Uri baseUrl)
    {
        bool needsWrapping = false;
        
        foreach (var box in _tableBox.Boxes)
        {
            if (!IsProperTableChild(box.Display))
            {
                needsWrapping = true;
                break;
            }
        }

        if (!needsWrapping)
            return;

        // Collect children and group consecutive non-row children into
        // anonymous table-row wrappers.
        var children = new List<CssBox>(_tableBox.Boxes);
        _tableBox.Boxes.Clear();

        List<CssBox>? pendingNonRow = null;

        foreach (var child in children)
        {
            if (IsProperTableChild(child.Display))
            {
                if (pendingNonRow != null)
                {
                    FlushAnonymousRow(pendingNonRow, baseUrl);
                    pendingNonRow = null;
                }

                _tableBox.Boxes.Add(child);
            }
            else
            {
                pendingNonRow ??= [];
                pendingNonRow.Add(child);
            }
        }

        if (pendingNonRow != null)
            FlushAnonymousRow(pendingNonRow, baseUrl);
    }

    /// <summary>
    /// Returns true if the display value is a proper direct child of a table
    /// element per CSS2.1 §17.2.1 (table-row, row-group, caption, column, etc.).
    /// </summary>
    private static bool IsProperTableChild(string display)
    {
        return display == CssConstants.TableRow
            || display == CssConstants.TableRowGroup
            || display == CssConstants.TableHeaderGroup
            || display == CssConstants.TableFooterGroup
            || display == CssConstants.TableCaption
            || display == CssConstants.TableColumn
            || display == CssConstants.TableColumnGroup;
    }

    /// <summary>
    /// Creates an anonymous table-row box, re-parents the given children into
    /// it, and appends the row to the table.  Children that are not table-cell
    /// are additionally wrapped in anonymous table-cell boxes (CSS2.1 §17.2.1).
    /// </summary>
    /// <summary>
    /// CSS2.1 §17.2.1, the same rule <see cref="GenerateAnonymousTableRows"/> applies one level
    /// up: a row group may contain only rows, so a run of non-row children has to be wrapped in
    /// an anonymous <c>table-row</c> (and, inside it, anonymous cells).
    /// <para>Without this the collection loop below — which takes a row group's children only when
    /// their display is <c>table-row</c> — silently dropped every other child, so it never laid
    /// out and never painted, and its height did not reach the table. WPT
    /// css/css-page/monolithic-overflow-011-print puts a 350vh block straight inside a
    /// <c>table-row-group</c>: the block vanished and the table collapsed to its text line.</para>
    /// </summary>
    private void GenerateAnonymousRowsInRowGroups(Uri baseUrl)
    {
        foreach (var group in _tableBox.Boxes)
        {
            if (group.Display != CssConstants.TableRowGroup
                && group.Display != CssConstants.TableHeaderGroup
                && group.Display != CssConstants.TableFooterGroup)
                continue;

            bool needsWrapping = false;
            foreach (var child in group.Boxes)
            {
                if (child.Display != CssConstants.TableRow)
                {
                    needsWrapping = true;
                    break;
                }
            }

            if (!needsWrapping)
                continue;

            var children = new List<CssBox>(group.Boxes);
            group.Boxes.Clear();

            List<CssBox>? pendingNonRow = null;

            foreach (var child in children)
            {
                if (child.Display == CssConstants.TableRow)
                {
                    if (pendingNonRow != null)
                    {
                        FlushAnonymousRow(pendingNonRow, baseUrl, group);
                        pendingNonRow = null;
                    }

                    group.Boxes.Add(child);
                }
                else
                {
                    pendingNonRow ??= [];
                    pendingNonRow.Add(child);
                }
            }

            if (pendingNonRow != null)
                FlushAnonymousRow(pendingNonRow, baseUrl, group);
        }
    }

    private void FlushAnonymousRow(List<CssBox> children, Uri baseUrl) =>
        FlushAnonymousRow(children, baseUrl, _tableBox);

    private void FlushAnonymousRow(List<CssBox> children, Uri baseUrl, CssBox container)
    {
        // Create the anonymous row. The CssBox(parent, tag) constructor
        // automatically adds the new box to parent.Boxes.
        var anonRow = new CssBox(container, null, baseUrl) { Display = CssConstants.TableRow };
        
        foreach (var child in children)
        {
            if (child.Display == CssConstants.TableCell)
            {
                // Already a table-cell — re-parent into the anonymous row.
                // Using the ParentBox setter updates _parentBox and adds
                // the child to anonRow.Boxes.
                child.ParentBox = anonRow;
            }
            else
            {
                // CSS2.1 §17.2.1: Wrap non-cell children in an anonymous
                // table-cell box.  The CssBox constructor automatically adds
                // the anonymous cell to anonRow.Boxes.
                var anonCell = new CssBox(anonRow, null, baseUrl) { Display = CssConstants.TableCell };
                child.ParentBox = anonCell;
            }
        }
    }

    private void InsertEmptyBoxes(Uri baseUrl)
    {
        if (_tableBox._tableFixed)
            return;

        int currow = 0;
        List<CssBox> rows = _bodyrows;

        foreach (CssBox row in rows)
        {
            for (int k = 0; k < row.Boxes.Count; k++)
            {
                CssBox cell = row.Boxes[k];
                int rowspan = GetRowSpan(cell);
                int realcol = GetCellRealColumnIndex(row, cell); //Real column of the cell

                for (int i = currow + 1; i < currow + rowspan; i++)
                {
                    if (rows.Count <= i)
                        continue;

                    int colcount = 0;
                    for (int j = 0; j < rows[i].Boxes.Count; j++)
                    {
                        if (colcount == realcol)
                        {
                            rows[i].Boxes.Insert(colcount, new CssSpacingBox(_tableBox, ref cell, currow, baseUrl));
                            break;
                        }

                        colcount++;
                        realcol -= GetColSpan(rows[i].Boxes[j]) - 1;
                    }
                }
            }

            currow++;
        }

        _tableBox._tableFixed = true;
    }

    private double CalculateCountAndWidth()
    {
        // Columns. Count the effective grid columns, not just the number of
        // physical cell boxes, because a one-cell row can span several columns.
        _columnCount = _columns.Count;
        foreach (CssBox row in _allRows)
            _columnCount = Math.Max(_columnCount, GetColumnSpanCount(row));

        //Initialize column widths array with NaNs
        _columnWidths = new double[_columnCount];
        for (int i = 0; i < _columnWidths.Length; i++)
            _columnWidths[i] = double.NaN;

        double availCellSpace = GetAvailableCellWidth();

        if (_columns.Count > 0)
        {
            // Fill ColumnWidths array by scanning column widths
            for (int i = 0; i < _columns.Count; i++)
            {
                CssLength len = new(_columns[i].Width); //Get specified width

                if (len.Number <= 0) //If some width specified
                    continue;

                if (len.IsPercentage) //Get width as a percentage
                {
                    // len.Number already holds the percentage as a fraction (parsed
                    // against a 100%-basis of 1), so scale it here instead of
                    // re-parsing the same string via ParseNumber.
                    _columnWidths[i] = len.Number * availCellSpace;
                }
                else if (len.Unit == CssUnit.Px || len.Unit == CssUnit.None)
                {
                    _columnWidths[i] = len.Number; //Get width as an absolute-pixel value
                }
            }
        }
        else
        {
            // Fill ColumnWidths array by scanning width in table-cell definitions
            foreach (CssBox row in _allRows)
            {
                //Check for column width in table-cell definitions
                int columnIndex = 0;
                foreach (CssBox cell in row.Boxes)
                {
                    int colspan = GetColSpan(cell);
                    int endColumn = Math.Min(_columnWidths.Length, columnIndex + colspan);
                    if (columnIndex >= _columnWidths.Length)
                        break;

                    if (cell.Display != CssConstants.TableCell)
                    {
                        columnIndex += colspan;
                        continue;
                    }

                    if (columnIndex >= 20)
                    {
                        bool spanAlreadyResolved = true;
                        for (int j = columnIndex; j < endColumn; j++)
                        {
                            if (double.IsNaN(_columnWidths[j]))
                            {
                                spanAlreadyResolved = false;
                                break;
                            }
                        }

                        if (spanAlreadyResolved)
                        {
                            columnIndex += colspan;
                            continue;
                        }
                    }

                    double len = CssLengthParser.ParseLength(cell.Width, availCellSpace, cell.GetEmHeight());

                    if (len <= 0) //If some width specified
                    {
                        columnIndex += colspan;
                        continue;
                    }

                    // CSS 2.1 §17.5.2.2: a column is as wide as its cells' border boxes, and a cell's
                    // width is its content box's, as any box's is, unless its box-sizing says
                    // otherwise. It was taken for the border box: a cell 50px wide with 5px of
                    // padding made a 50px column, where browsers make it 60px.
                    len = cell.ResolveSpecifiedWidthToBorderBox(len);

                    len /= Convert.ToSingle(colspan);

                    for (int j = columnIndex; j < endColumn; j++)
                        _columnWidths[j] = double.IsNaN(_columnWidths[j]) ? len : Math.Max(_columnWidths[j], len);

                    columnIndex += colspan;
                }
            }
        }

        _specifiedColumnWidths = (double[])_columnWidths.Clone();

        return availCellSpace;
    }

    private static int GetColumnSpanCount(CssBox row)
    {
        int count = 0;
        
        foreach (CssBox cell in row.Boxes)
            count += GetColSpan(cell);
        
        return count;
    }

    /// <summary>
    /// Whether the table is laid out by the fixed table layout algorithm (CSS 2.1 §17.5.2.1): its
    /// <c>table-layout</c> is <c>fixed</c> and it has a width of its own. Browsers lay a table with
    /// an <c>auto</c> width out by the automatic algorithm, whatever its <c>table-layout</c>.
    /// </summary>
    private bool IsFixedLayout =>
        string.Equals(_tableBox.TableLayout, "fixed", StringComparison.OrdinalIgnoreCase)
        && new CssLength(_tableBox.Width).Number > 0;

    /// <summary>
    /// CSS 2.1 §17.5.2.1, the fixed table layout algorithm: a column whose column element has a
    /// width has that width; any other takes its width from the cell in the first row that starts
    /// in it, divided over the columns the cell spans; and the columns left share the room the
    /// table has left equally. When every column has a width and together they fall short of the
    /// table's, each is widened in proportion to its width. The rows after the first and the
    /// content of the cells take no part: content wider than its column overflows it.
    /// </summary>
    /// <remarks>
    /// A cell's width is its content box's unless its <c>box-sizing</c> says otherwise, and its
    /// padding and borders are added to it for its column, which holds its border box.
    /// </remarks>
    private void DetermineFixedColumnWidths(double availCellSpace)
    {
        for (int i = 0; i < _columnWidths.Length; i++)
            _columnWidths[i] = double.NaN;

        for (int i = 0; i < _columns.Count && i < _columnWidths.Length; i++)
        {
            double width = SpecifiedWidth(_columns[i], availCellSpace);

            if (width > 0)
                _columnWidths[i] = width;
        }

        if (_allRows.Count > 0)
        {
            int column = 0;

            foreach (var cell in _allRows[0].Boxes)
            {
                int span = GetColSpan(cell);
                double width = cell.Display == CssConstants.TableCell ? SpecifiedWidth(cell, availCellSpace) : 0;

                if (width > 0)
                {
                    width = cell.ResolveSpecifiedWidthToBorderBox(width);

                    for (int j = column; j < Math.Min(_columnWidths.Length, column + span); j++)
                    {
                        if (double.IsNaN(_columnWidths[j]))
                            _columnWidths[j] = width / span;
                    }
                }

                column += span;
            }
        }

        double used = 0;
        int unsized = 0;

        foreach (double width in _columnWidths)
        {
            if (double.IsNaN(width))
                unsized++;
            else
                used += width;
        }

        double room = Math.Max(0, availCellSpace - used);

        for (int i = 0; i < _columnWidths.Length; i++)
        {
            if (unsized > 0)
            {
                if (double.IsNaN(_columnWidths[i]))
                    _columnWidths[i] = room / unsized;
            }
            else if (used > 0)
            {
                _columnWidths[i] += room * _columnWidths[i] / used;
            }
        }
    }

    /// <summary>The width <paramref name="box"/> has of its own, a percentage of the table's room; 0 for <c>auto</c>.</summary>
    private static double SpecifiedWidth(CssBox box, double availCellSpace)
    {
        if (string.IsNullOrEmpty(box.Width) || box.Width == CssConstants.Auto)
            return 0;

        double width = CssLengthParser.ParseLength(box.Width, availCellSpace, box.GetEmHeight());
        return double.IsNaN(width) ? 0 : width;
    }

    private void DetermineMissingColumnWidths(double availCellSpace)
    {
        double occupedSpace = 0f;

        // CSS Tables 3: a table with a width of its own shares it out over its columns as it shares
        // out a spanning cell's (see ShareOut), from their minimums: the columns with a width of
        // their own up to it, then the others up to their maximums, then past those, the others in
        // proportion to their maximums. What some columns left once they reached their maximums was
        // split evenly over the columns without a width instead. In a 1024px page, a table with
        // `width: 100%` gave "Some longer text here" and "short" 572.93px and 451.07px, where
        // browsers give them 835.22px and 188.78px, and an empty column beside a y took 96px of
        // 200px, where browsers give it none. A table narrower than its columns' widths took
        // theirs: with `width: 60px`, two 50px columns made it 100px wide, where browsers keep it
        // 60px wide, with 30px columns.
        if (_widthSpecified)
        {
            var widths = (double[])GetColumnMinWidths().Clone();

            if (widths.Length > 0)
                ShareOut(availCellSpace, 0, widths.Length - 1, widths, _columnMaxWidths);

            _columnWidths = widths;
        }
        else
        {
            //Get the minimum and maximum full length of NaN boxes
            GetColumnsMinMaxWidthByContent(true, out double[] minFullWidths, out double[] maxFullWidths);

            for (int i = 0; i < _columnWidths.Length; i++)
            {
                if (double.IsNaN(_columnWidths[i]))
                    _columnWidths[i] = minFullWidths[i];
                occupedSpace += _columnWidths[i];
            }

            // CSS 2.1 §17.5.2.2: the table is as wide as its columns' content, or as the room there is
            // if that is less. CSS Tables 3 shares the room left over the columns in proportion to how
            // much wider their content would have them, each up to its maximum. The room was spread one
            // column at a time instead, each taking what was left divided by the columns still to come,
            // so what a column could not take went unused: in a 1024px page, a table with an auto width
            // holding text and a 600px image was 853.36px wide, where browsers make it 1024px and give
            // the text 424px.
            double room = availCellSpace - occupedSpace;
            double growth = 0;

            for (int i = 0; i < _columnWidths.Length; i++)
                growth += Math.Max(0, maxFullWidths[i] - _columnWidths[i]);

            if (room > 0 && growth > 0)
            {
                double share = Math.Min(1, room / growth);

                for (int i = 0; i < _columnWidths.Length; i++)
                {
                    if (maxFullWidths[i] > _columnWidths[i])
                        _columnWidths[i] += (maxFullWidths[i] - _columnWidths[i]) * share;
                }
            }
        }
    }

    private void EnforceMaximumSize()
    {
        int curCol = 0;
        var widthSum = GetWidthSum();
        
        while (widthSum > GetAvailableTableWidth() && CanReduceWidth())
        {
            while (!CanReduceWidth(curCol))
                curCol++;

            _columnWidths[curCol] -= 1f;

            curCol++;

            if (curCol >= _columnWidths.Length)
                curCol = 0;
        }

        // CSS Tables 3: `max-width` narrows a table, but never below its columns' minimums, and the
        // columns share out the width it leaves them as they share out any width the table is given
        // (see ShareOut), from their minimums. They were narrowed to their minimums, past those where
        // that was not enough, and given what that left evenly. `max-width: 20px` made a table
        // around "xxxxxx" 20px wide, the word running out of it, where browsers make it 48px wide;
        // with `width: 100%` and `max-width: 300px`, "xxxxxx" and "y" had 170px and 130px, where
        // browsers give them 257.14px and 42.86px.
        var maxWidth = GetMaxTableWidth();
        if (double.IsPositiveInfinity(maxWidth))
            return;

        widthSum = GetWidthSum();

        if (maxWidth >= widthSum)
            return;

        double columns = 0;
        foreach (double width in _columnWidths)
            columns += width;

        var widths = (double[])GetColumnMinWidths().Clone();

        if (widths.Length > 0)
            ShareOut(maxWidth - (widthSum - columns), 0, widths.Length - 1, widths, _columnMaxWidths);

        _columnWidths = widths;
    }

    /// <summary>
    /// Check for minimum sizes (increment widths if necessary)
    /// </summary>
    private void EnforceMinimumSize()
    {
        if (_widthSpecified)
        {
            WidenColumnsToMinimumsWithinWidth();
            return;
        }

        foreach (CssBox row in _allRows)
        {
            foreach (CssBox cell in row.Boxes)
            {
                int colspan = GetColSpan(cell);
                int col = GetCellRealColumnIndex(row, cell);
                int affectcol = col + colspan - 1;

                if (_columnWidths.Length <= col || _columnWidths[col] >= GetColumnMinWidths()[col])
                    continue;

                double diff = GetColumnMinWidths()[col] - _columnWidths[col];
                _columnWidths[affectcol] = GetColumnMinWidths()[affectcol];

                if (col < _columnWidths.Length - 1)
                    _columnWidths[col + 1] -= diff;
            }
        }
    }

    /// <summary>
    /// CSS 2.1 §17.5.2.2: a table with a width of its own is that wide, or as wide as its columns'
    /// minimums if they need more. Each column narrower than its minimum is widened to it, and the
    /// columns wider than theirs give that room back, each in proportion to what it can spare: the
    /// columns without a width of their own first, as CSS Tables 3 narrows a table's columns, then
    /// the others. The table grows only by what they cannot spare.
    /// </summary>
    /// <remarks>
    /// Widening a column took the room from the next column alone, so the table grew whenever that
    /// column had none to spare: in a 1024px page, a table with <c>width: 100%</c> holding text and
    /// a 600px image was 1112px wide, the text's column as wide as before, where browsers keep the
    /// table 1024px wide and give the text's column 424px.
    /// </remarks>
    private void WidenColumnsToMinimumsWithinWidth()
    {
        double[] minWidths = GetColumnMinWidths();
        double needed = 0;

        for (int i = 0; i < _columnWidths.Length; i++)
        {
            if (_columnWidths[i] < minWidths[i])
            {
                needed += minWidths[i] - _columnWidths[i];
                _columnWidths[i] = minWidths[i];
            }
        }

        needed = GiveBackRoom(needed, minWidths, specified: false);
        GiveBackRoom(needed, minWidths, specified: true);
    }

    /// <summary>
    /// Narrows the columns with a width of their own, or those without, that are wider than their
    /// minimums, each in proportion to what it can spare, by <paramref name="needed"/> together or
    /// as much as they can spare, and returns what they could not.
    /// </summary>
    private double GiveBackRoom(double needed, double[] minWidths, bool specified)
    {
        if (needed <= 0)
            return 0;

        double spare = 0;

        for (int i = 0; i < _columnWidths.Length; i++)
        {
            if (IsSpecified(i) == specified && _columnWidths[i] > minWidths[i])
                spare += _columnWidths[i] - minWidths[i];
        }

        if (spare <= 0)
            return needed;

        double given = Math.Min(1, needed / spare);

        for (int i = 0; i < _columnWidths.Length; i++)
        {
            if (IsSpecified(i) == specified && _columnWidths[i] > minWidths[i])
                _columnWidths[i] -= (_columnWidths[i] - minWidths[i]) * given;
        }

        return Math.Max(0, needed - spare);
    }

    /// <summary>
    /// CSS Tables 3: a table is at least as wide as the widest of its captions' min-content
    /// contributions, whatever its <c>width</c> and <c>max-width</c>. The columns share that width
    /// out as they share any width the table is given (see <see cref="ShareOut"/>): from their
    /// minimums up to their maximums, then past them in proportion to them.
    /// </summary>
    /// <remarks>
    /// The columns were sized from the cells alone, and the captions laid out across them, so
    /// "Caption" over a cell holding an x made the table 8px wide and the word ran out of it, where
    /// browsers make the table 55.16px wide, as wide as the word. A table holding only a caption was
    /// 0px wide.
    /// </remarks>
    private void WidenToCaptions()
    {
        foreach (var caption in _captions)
            _captionMinWidth = Math.Max(_captionMinWidth, GetCaptionMinContribution(caption));

        // A table with no columns is widened in LayoutCells.
        if (_columnWidths.Length == 0)
            return;

        double columns = 0;
        foreach (double width in _columnWidths)
            columns += width;

        double needed = _captionMinWidth - (GetWidthSum() - columns);
        if (needed <= columns + 0.01)
            return;

        // From the columns' minimums: their widths may lie past their maximums already, shared
        // out evenly over a table's own width, and the share-out would add to that.
        var widths = (double[])GetColumnMinWidths().Clone();
        ShareOut(needed, 0, widths.Length - 1, widths, _columnMaxWidths);
        _columnWidths = widths;
    }

    /// <summary>
    /// A caption's min-content contribution: its min-content width, or its own width where it has
    /// one, with its padding and border (see <see cref="CssBox.GetMinMaxWidth"/>), and its margins.
    /// </summary>
    private static double GetCaptionMinContribution(CssBox caption)
    {
        caption.GetMinMaxWidth(out double min, out _);
        return (double.IsNaN(min) ? 0 : min) + caption.ActualMarginLeft + caption.ActualMarginRight;
    }

    private void LayoutCells(ILayoutEnvironment g)
    {
        // CSS2.1 §17.4.1: lay out top-side captions above the cell grid. They
        // span the table's used width and push the first row (and every later
        // row) down by their combined height. Bottom-side captions are laid out
        // after the rows (see below).
        //
        // CSS 2.1 §17.4: a caption is as wide as the table's border box, which GetWidthSum is, the
        // spacing and the borders counted. The spacing was added again, so a caption ran past the
        // table's right edge by it: with `border-spacing: 4px` and one column, 8px.
        //
        // A table with no columns has no spacing either, and is as wide as its border and padding,
        // its own width, or its captions need, whichever is the widest.
        double captionWidth = _columnCount > 0
            ? GetWidthSum()
            : Math.Max(Math.Max(
                _tableBox.ActualBorderLeftWidth + _tableBox.ActualPaddingLeft + _tableBox.ActualPaddingRight + _tableBox.ActualBorderRightWidth,
                _tableBox.ActualWidth), _captionMinWidth);
        double topCaptionHeight = LayoutTopCaptions(g, captionWidth);

        // CSS2.1 §17.6.1: border spacing lies between the cells, and between them and the
        // table's border, so a table with no cells has none. It was put on both sides of the
        // cells whether there were any or not: an empty table was 4x4px with the default
        // border-spacing: 2px, where browsers make it 0x0px.
        double horizontalSpacing = _columnCount > 0 ? GetHorizontalSpacing() : 0;
        double verticalSpacing = _columnCount > 0 ? GetVerticalSpacing() : 0;

        double startx = Math.Max(_tableBox.ClientLeft + horizontalSpacing, 0);
        double starty = Math.Max(_tableBox.ClientTop + topCaptionHeight + verticalSpacing, 0);
        double cury = starty;
        double maxRight = startx;
        double maxBottom = 0f;
        int currentrow = 0;

        // CSS2.1 §17.5.3: record each laid-out row's natural top/bottom so a
        // specified table height greater than the content height can be
        // distributed across the rows afterwards.
        var rowBounds = new List<(CssBox Row, double Top, double Bottom)>();

        // Rowspan cells are normally sized/aligned at their last spanned row via a
        // CssSpacingBox. When the trailing spanned rows are collapsed or empty no
        // spacer runs, so we track which spanned cells were finalised and fix up the
        // rest in a post-pass below.
        var finalizedSpanCells = new HashSet<CssBox>();

        for (int i = 0; i < _allRows.Count; i++)
        {
            var row = _allRows[i];
            double rowTop = cury;

            // CSS2.1 §17.5.5: Rows with visibility:collapse are hidden and do
            // not contribute height.  Column widths are still affected (handled
            // during column width calculation).
            if (row.Visibility == CssConstants.Collapse)
            {
                currentrow++;
                continue;
            }

            double curx = startx;
            int curCol = 0;
            bool breakPage = false;

            for (int j = 0; j < row.Boxes.Count; j++)
            {
                CssBox cell = row.Boxes[j];
                if (curCol >= _columnWidths.Length)
                    break;

                int rowspan = GetRowSpan(cell);
                var columnIndex = GetCellRealColumnIndex(row, cell);
                double width = GetCellWidth(columnIndex, cell);

                cell.Location = new PointF((float)curx, (float)cury);
                cell.Size = new SizeF((float)width, 0f);
                cell.PerformLayout(g); //That will automatically set the bottom of the cell

                //Alter max bottom only if row is cell's row + cell's rowspan - 1
                if (cell is CssSpacingBox sb)
                {
                    if (sb.EndRow == currentrow)
                        maxBottom = Math.Max(maxBottom, sb.ExtendedBox.ActualBottom);
                }
                else if (rowspan == 1)
                {
                    maxBottom = Math.Max(maxBottom, cell.ActualBottom);
                }

                maxRight = Math.Max(maxRight, cell.ActualRight);
                curCol++;
                curx = cell.ActualRight + GetHorizontalSpacing();
            }

            // CSS2.1 §17.5.3: a row's specified `height` is a minimum. The loop
            // above only grows `maxBottom` from non-row-spanning cell bottoms, so
            // a row whose only cells span into later rows (e.g. rowspan cells with
            // a collapsed/empty following row) would leave the row — and the whole
            // table — at zero height, which `overflow:hidden` then clips away.
            // Floor the row bottom by its explicit height so such rows still take
            // space and overflowing cell content is clipped to the row box.
            maxBottom = Math.Max(maxBottom, rowTop + GetSpecifiedRowHeight(row));

            foreach (CssBox cell in row.Boxes)
            {
                CssSpacingBox spacer = cell as CssSpacingBox;

                if (spacer == null && GetRowSpan(cell) == 1)
                {
                    cell.ActualBottom = maxBottom;
                    // CSS2.1 §17.5.3: Update Size.Height to match the
                    // stretched cell so background painting uses the full
                    // cell height.
                    cell.Size = new SizeF(cell.Size.Width, (float)(maxBottom - cell.Location.Y));
                    CssLayoutEngine.ApplyCellVerticalAlignment(g, cell);
                }
                else if (spacer != null && spacer.EndRow == currentrow)
                {
                    spacer.ExtendedBox.ActualBottom = maxBottom;
                    spacer.ExtendedBox.Size = new SizeF(spacer.ExtendedBox.Size.Width, (float)(maxBottom - spacer.ExtendedBox.Location.Y));
                    CssLayoutEngine.ApplyCellVerticalAlignment(g, spacer.ExtendedBox);
                    finalizedSpanCells.Add(spacer.ExtendedBox);
                }

                // If one cell crosses page borders then don't need to check other cells in the row
                if (_tableBox.PageBreakInside == CssConstants.Avoid)
                {
                    breakPage = cell.BreakPage();
                    if (breakPage)
                    {
                        cury = cell.Location.Y;
                        break;
                    }
                }
            }

            if (breakPage) // go back to move the whole row to the next page
            {
                if (i == 1) // do not leave single row in previous page
                    i = -1; // Start layout from the first row on new page
                else
                    i--;

                maxBottom = 0;
                continue;
            }

            rowBounds.Add((row, rowTop, maxBottom));
            cury = maxBottom + verticalSpacing;

            currentrow++;
        }

        // CSS2.1 §17.5: finalise rowspan cells that no spacer sized (their trailing
        // spanned rows were collapsed/empty). Clamp each to the bottom of its last
        // laid-out spanned row and align its content — so e.g. `align-content:
        // unsafe end` shifts the overflowing content to the cell's end edge instead
        // of leaving it at the start.
        FinalizeUnspacedRowSpanCells(g, rowBounds, finalizedSpanCells);

        // CSS2.1 §17.5.3: when the table's specified height exceeds the height
        // the rows naturally occupy, distribute the surplus over the rows.
        maxBottom = DistributeExtraTableHeight(g, rowBounds, maxBottom, starty);

        // A table's `width` is the width of its border box, as the columns were sized above:
        // GetAvailableCellWidth takes the borders and the spacing off it. The table ends at the
        // greater of that and the columns' own extent. Taking the width for the columns' extent put
        // the right border and the spacing past it: `width: 320px` with a 10px border made a table
        // 330px wide, and 332px with 2px of border spacing, where browsers make it 320px.
        //
        // Its `max-width` wins over its `width`: with `width: 300px` and `max-width: 150px`, the
        // table was 300px wide around 150px of columns, where browsers make it 150px wide.
        _tableBox.ActualRight = Math.Max(
            maxRight + horizontalSpacing + _tableBox.ActualPaddingRight + _tableBox.ActualBorderRightWidth,
            _tableBox.Location.X + Math.Min(_tableBox.ActualWidth, GetMaxTableWidth()));

        if (_columnCount == 0)
            _tableBox.ActualRight = Math.Max(_tableBox.ActualRight, _tableBox.Location.X + captionWidth);

        _tableBox.ActualBottom = Math.Max(maxBottom, starty) + verticalSpacing + _tableBox.ActualPaddingBottom + _tableBox.ActualBorderBottomWidth;

        // CSS2.1 §17.4.1: lay out bottom-side captions below the table box and
        // extend the table's bottom to enclose them.
        double bottomCaptionBottom = LayoutBottomCaptions(g, captionWidth, _tableBox.ActualBottom);
        if (bottomCaptionBottom > _tableBox.ActualBottom)
            _tableBox.ActualBottom = bottomCaptionBottom;

        // CSS2.1 §17.5.2: Update the table box's Size to match the
        // computed layout dimensions so background painting, overflow
        // clipping, and child containing-block queries use the correct
        // table bounds.
        _tableBox.Size = new SizeF(
            (float)(_tableBox.ActualRight - _tableBox.Location.X),
            (float)(_tableBox.ActualBottom - _tableBox.Location.Y));
    }

    /// <summary>
    /// CSS2.1 §17.4.1: A caption's <c>caption-side</c> places it on the block
    /// (top/bottom) side of the table. Broiler exposes it as a raw string
    /// property (default <c>top</c>); only <c>bottom</c> moves it below.
    /// </summary>
    private static bool IsBottomCaption(CssBox caption) =>
        string.Equals(caption.CaptionSide, CssConstants.Bottom, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Lay out a single caption as a block box of the table's used width and
    /// return its outer (margin-box) height. The caption is positioned at
    /// (<paramref name="x"/>, <paramref name="y"/>) at its content-box origin,
    /// inside its own margins.
    /// </summary>
    private static double LayoutCaption(ILayoutEnvironment g, CssBox caption, double x, double y, double width)
    {
        double contentWidth = Math.Max(0,
            width - caption.ActualMarginLeft - caption.ActualMarginRight
                  - caption.ActualBorderLeftWidth - caption.ActualBorderRightWidth
                  - caption.ActualPaddingLeft - caption.ActualPaddingRight);

        // The caption is laid out as a block box whose width is the table's used
        // width (CSS2.1 §17.4). PerformLayoutImp resolves a block's width from its
        // containing block (the table box), whose Size.Width is not finalised
        // until LayoutCells completes — so pin an explicit content-box width here
        // (only when the author left it auto) to avoid the caption shrinking to a
        // stale/zero container width and wrapping every word onto its own line.
        string savedWidth = caption.Width;
        bool pinWidth = string.IsNullOrEmpty(caption.Width) || caption.Width == CssConstants.Auto;

        if (pinWidth)
            caption.Width = contentWidth.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "px";

        double targetX = x + caption.ActualMarginLeft;
        double targetY = y + caption.ActualMarginTop;

        caption.Location = new PointF((float)targetX, (float)targetY);
        caption.Size = new SizeF((float)contentWidth, 0f);
        caption.PerformLayout(g);

        if (pinWidth)
            caption.Width = savedWidth;

        // PerformLayoutImp positions a block's content relative to a flow origin
        // it recomputes, so the caption (and its line boxes) may not honour the
        // provisional Location set above — snap the whole subtree to the target
        // with OffsetLeft/OffsetTop, mirroring the grid item placement path.
        double dx = targetX - caption.Location.X;
        double dy = targetY - caption.Location.Y;

        if (Math.Abs(dx) > 0.01)
            caption.OffsetLeft(dx);

        if (Math.Abs(dy) > 0.01)
            caption.OffsetTop(dy);

        return (caption.ActualBottom - caption.Location.Y)
            + caption.ActualMarginTop + caption.ActualMarginBottom;
    }

    /// <summary>
    /// Where a caption begins: CSS 2.1 §17.4 lays it out across the table's border box, so at the
    /// table's left border edge. It began inside the left border, and so ran past the right one:
    /// with a 5px border, 5px to the right of where browsers put it.
    /// </summary>
    private double CaptionLeft => _tableBox.Location.X;

    /// <summary>
    /// CSS2.1 §17.4.1: lay out all top-side captions stacked from just inside
    /// the table's top border, returning their combined height so the cell grid
    /// can be offset below them.
    /// </summary>
    private double LayoutTopCaptions(ILayoutEnvironment g, double width)
    {
        double total = 0;
        double x = CaptionLeft;

        // Above the table's padding: the caption lies outside the box the padding surrounds.
        double top = _tableBox.Location.Y + _tableBox.ActualBorderTopWidth;

        foreach (var caption in _captions)
        {
            if (IsBottomCaption(caption))
                continue;

            total += LayoutCaption(g, caption, x, top + total, width);
        }

        return total;
    }

    /// <summary>
    /// CSS2.1 §17.4.1: lay out all bottom-side captions stacked below the table
    /// box, returning the bottom edge of the last caption (or the incoming
    /// <paramref name="tableBottom"/> when there are none).
    /// </summary>
    private double LayoutBottomCaptions(ILayoutEnvironment g, double width, double tableBottom)
    {
        double x = CaptionLeft;
        double y = tableBottom;

        foreach (var caption in _captions)
        {
            if (!IsBottomCaption(caption))
                continue;

            y += LayoutCaption(g, caption, x, y, width);
        }

        return y;
    }

    /// <summary>
    /// CSS2.1 §17.5.3: "If the 'table' or 'inline-table' element's height is
    /// specified [and greater than the sum of the row heights], the row heights
    /// are increased so they sum to the specified height." Broiler sizes the
    /// table purely from content, so an explicit table height was ignored
    /// (every CSS2 tables reftest sets <c>height:2in</c>). Distribute the
    /// surplus equally over the in-flow rows: shift each row down by the surplus
    /// already added above it and grow its (non-row-spanning) cells. Returns the
    /// updated <paramref name="naturalBottom"/>. No-op when the height is auto
    /// or the rows already exceed it, so auto-height tables are unchanged.
    /// </summary>
    private double DistributeExtraTableHeight(
        ILayoutEnvironment g, List<(CssBox Row, double Top, double Bottom)> rowBounds,
        double naturalBottom, double rowAreaTop)
    {
        if (rowBounds.Count == 0)
            return naturalBottom;

        // CSS 2.1 §10.5: a percentage resolves against the containing block's content height where
        // that is definite, and is auto otherwise, which leaves the rows as they are. It was read
        // from the containing block's ActualHeight, which resolves the containing block's own
        // declaration against the containing block's own size so far and keeps the result: for a
        // `display: inline-block; height: 100%` still being laid out that is 0px, and the rows of a
        // `height: 100%` table in it stayed as short as their content; for a `height: 50%` one 50px
        // tall it is 25px, and they came to 25px.
        double cbHeight = _tableBox.TryGetPercentageBlockSizeBasis(out double basis) ? basis : 0;
        double em = _tableBox.GetEmHeight();

        double target = naturalBottom;

        // CSS2.1 §17.5.3: an explicit table 'height' greater than the content
        // grows the rows. Target bottom for the row area = table top + specified
        // content height (ClientTop already includes the top border/padding; the
        // bottom border/spacing is added by the caller).
        if (!string.IsNullOrEmpty(_tableBox.Height) && _tableBox.Height != CssConstants.Auto)
        {
            double specHeight = CssLengthParser.ParseLength(_tableBox.Height, cbHeight, em);
            if (!double.IsNaN(specHeight) && specHeight > 0)
            {
                double specBottom = _tableBox.Location.Y + specHeight
                    - _tableBox.ActualBorderBottomWidth - _tableBox.ActualPaddingBottom - GetVerticalSpacing();

                if (specBottom > target)
                    target = specBottom;
            }
        }

        // CSS2.1 §17.5.3 / §10.7: a 'min-height' greater than the content grows
        // the rows the same way. In the table wrapper model min-height applies to
        // the inner table box (the rows), *not* the caption, so measure it from
        // the row-area top (below any top caption) rather than the table origin —
        // this is what vertically centres a `vertical-align:middle` cell whose
        // table has a tall min-height (WPT table-grid-item-dynamic-002).
        if (!string.IsNullOrEmpty(_tableBox.MinHeight) && _tableBox.MinHeight != "0")
        {
            double minH = CssLengthParser.ParseLength(_tableBox.MinHeight, cbHeight, em);
            if (!double.IsNaN(minH) && minH > 0)
            {
                double minBottom = rowAreaTop + minH
                    - _tableBox.ActualBorderTopWidth - _tableBox.ActualBorderBottomWidth;

                if (minBottom > target)
                    target = minBottom;
            }
        }

        double surplus = target - naturalBottom;
        if (surplus <= 0.5)
            return naturalBottom;

        double perRow = surplus / rowBounds.Count;
        double shift = 0;

        foreach (var (row, top, bottom) in rowBounds)
        {
            foreach (var cell in row.Boxes)
            {
                if (cell is CssSpacingBox || GetRowSpan(cell) != 1)
                {
                    // Spanned cells: just shift; their height is governed by the
                    // last spanned row. Conservative for this increment.
                    cell.Location = new PointF(cell.Location.X, (float)(cell.Location.Y + shift));
                    continue;
                }

                cell.Location = new PointF(cell.Location.X, (float)(cell.Location.Y + shift));

                double newBottom = bottom + shift + perRow;
                cell.ActualBottom = newBottom;
                cell.Size = new SizeF(cell.Size.Width, (float)(newBottom - cell.Location.Y));

                ResolveCellPercentageHeightChildren(g, cell);
                CssLayoutEngine.ApplyCellVerticalAlignment(g, cell);
            }

            shift += perRow;
        }

        return naturalBottom + surplus;
    }

    /// <summary>
    /// CSS2.1 §17.5.3 / §10.5: a table cell's used height is only known after the
    /// row-height pass, so a percentage-height in-flow child laid out during the cell's
    /// own <c>PerformLayout</c> (when the cell was still content-sized) resolved its
    /// height against an indefinite base and fell back to its content height. Once the
    /// cell has been stretched to its final height, make that height definite and re-run
    /// the cell layout so such children resolve against — and fill — the cell (WPT
    /// css-sizing/table-child-percentage-height-with-border-box). The original CSS
    /// <c>height</c> is restored afterwards so a later full relayout pass re-derives it
    /// naturally rather than inheriting this pass's pixel value.
    /// </summary>
    private void ResolveCellPercentageHeightChildren(ILayoutEnvironment g, CssBox cell)
    {
        if (cell is CssSpacingBox || !CellHasPercentageHeightChild(cell))
            return;

        double finalBorderBoxHeight = cell.ActualBottom - cell.Location.Y;
        if (finalBorderBoxHeight <= 0)
            return;

        double edges = cell.ActualBorderTopWidth + cell.ActualBorderBottomWidth
            + cell.ActualPaddingTop + cell.ActualPaddingBottom;
        double cssHeight = string.Equals(cell.BoxSizing, "border-box", StringComparison.OrdinalIgnoreCase)
            ? finalBorderBoxHeight
            : Math.Max(0, finalBorderBoxHeight - edges);

        string savedHeight = cell.Height;
        var savedLocation = cell.Location;
        cell.Height = cssHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "px";
        cell.PerformLayout(g);
        // Keep the finalized cell geometry; only the children needed re-resolution.
        cell.Height = savedHeight;
        cell.Location = savedLocation;
        cell.ActualBottom = savedLocation.Y + finalBorderBoxHeight;
        cell.Size = new SizeF(cell.Size.Width, (float)finalBorderBoxHeight);
    }

    /// <summary>Whether <paramref name="cell"/> has an in-flow child whose <c>height</c>
    /// is a percentage — the case that needs re-resolution once the cell height is final.</summary>
    private static bool CellHasPercentageHeightChild(CssBox cell)
    {
        foreach (var child in cell.Boxes)
        {
            if (child.Position == CssConstants.Absolute || child.Position == CssConstants.Fixed)
                continue;
            if (child.Display == CssConstants.None)
                continue;
            var h = child.Height;
            if (!string.IsNullOrEmpty(h) && h.EndsWith("%", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    // CSS2.1 §17.6.2.1 border-conflict-resolution priority of border styles
    // (most → least): hidden (handled separately) > double > solid > dashed >
    // dotted > ridge > outset > groove > inset > none.
    private static readonly Dictionary<string, int> BorderStyleRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["double"] = 8, ["solid"] = 7, ["dashed"] = 6, ["dotted"] = 5,
        ["ridge"] = 4, ["outset"] = 3, ["groove"] = 2, ["inset"] = 1, ["none"] = 0,
    };

    /// <summary>
    /// CSS2.1 §17.6.2.1: resolves the single border that the collapsing border model paints at an
    /// edge two borders meet at. <c>hidden</c> suppresses the edge entirely; otherwise the wider
    /// border wins, then the higher-priority style, then (for an exact tie) the first operand, the
    /// spec's earlier-in-tree-order cell. <c>none</c> and zero width always lose.
    /// </summary>
    private static CollapsedBorder ResolveCollapsedEdge(CollapsedBorder a, CollapsedBorder b)
    {
        bool aHidden = string.Equals(a.Style, CssConstants.Hidden, StringComparison.OrdinalIgnoreCase);
        bool bHidden = string.Equals(b.Style, CssConstants.Hidden, StringComparison.OrdinalIgnoreCase);
        if (aHidden || bHidden)
            return CollapsedBorder.None;

        bool aNone = a.Width <= 0.01 || string.IsNullOrEmpty(a.Style) || string.Equals(a.Style, CssConstants.None, StringComparison.OrdinalIgnoreCase);
        bool bNone = b.Width <= 0.01 || string.IsNullOrEmpty(b.Style) || string.Equals(b.Style, CssConstants.None, StringComparison.OrdinalIgnoreCase);
        if (aNone && bNone)
            return CollapsedBorder.None;

        if (aNone) return b;
        if (bNone) return a;

        if (Math.Abs(a.Width - b.Width) > 0.01)
            return a.Width > b.Width ? a : b;

        int ra = BorderStyleRank.GetValueOrDefault(a.Style, 0);
        int rb = BorderStyleRank.GetValueOrDefault(b.Style, 0);

        if (ra != rb)
            return ra > rb ? a : b;

        return a; // exact tie → the earlier (left/top) cell, per tree order.
    }

    private const int Top = 0, Right = 1, Bottom = 2, Left = 3;

    /// <summary>The border <paramref name="box"/> has of its own on the given side.</summary>
    private static CollapsedBorder AuthoredBorder(CssBox box, int side) => side switch
    {
        Top => new(box.AuthoredBorderTopStyle, box.AuthoredBorderTopWidth, box.BorderTopColor),
        Right => new(box.AuthoredBorderRightStyle, box.AuthoredBorderRightWidth, box.BorderRightColor),
        Bottom => new(box.AuthoredBorderBottomStyle, box.AuthoredBorderBottomWidth, box.BorderBottomColor),
        _ => new(box.AuthoredBorderLeftStyle, box.AuthoredBorderLeftWidth, box.BorderLeftColor),
    };

    /// <summary>
    /// CSS 2.1 §17.6.2: in the collapsing border model, the borders that meet at an edge between two
    /// cells, or between a cell and the table, resolve to one border (§17.6.2.1), centred on the grid
    /// line. Each cell takes half of each of its collapsed borders into its border box, and the table
    /// half of those on its perimeter: its left and right halves from the first row's first and last
    /// cells, its top and bottom from the widest on the first and the last row. There is no spacing
    /// between the cells.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The resolved border was written into the cells' own border properties, whole: the left or
    /// top cell of a shared edge got it and the other none, a cell on the perimeter got its border
    /// collapsed with the table's, and the table kept its own too, while the cells overlapped by a
    /// pixel of spacing. So a cell of a table with a 10px border started 9px in, inside the whole
    /// border, where browsers put it 5px in, and a table of cells with 1px borders was a pixel
    /// narrower than browsers make it for each column after the first.
    /// </para>
    /// <para>
    /// The borders are set in <see cref="CssBoxProperties.CollapsedBorders"/>, which the used border
    /// widths, styles and colours read, and resolved each time from the borders the table and its
    /// cells have of their own, so a second layout resolves the same ones. A cell spanning rows is
    /// resolved against the cells beside it in its first row only.
    /// </para>
    /// </remarks>
    private void ResolveCollapsedBorders()
    {
        if (!string.Equals(_tableBox.BorderCollapse, CssConstants.Collapse, StringComparison.OrdinalIgnoreCase))
        {
            ClearCollapsedBorders();
            return;
        }

        // CSS2.1 §17.6.2.1: an exact tie (same width and style) favours the cell
        // furthest to the top-left in a left-to-right table, but the top-RIGHT
        // cell in a right-to-left table. ResolveCollapsedEdge breaks ties toward
        // its first operand, so for a horizontal edge in an RTL table the right
        // cell must be passed first.
        bool rtl = string.Equals(_tableBox.Direction, "rtl", StringComparison.OrdinalIgnoreCase);

        // Build a column-indexed grid of real cells (a colspan cell occupies
        // every column it spans; row-span placeholders are left null).
        var grid = new List<Dictionary<int, CssBox>>(_allRows.Count);
        foreach (var row in _allRows)
        {
            var cols = new Dictionary<int, CssBox>();
            foreach (var cell in row.Boxes)
            {
                if (cell.Display != CssConstants.TableCell)
                    continue;

                int col = GetCellRealColumnIndex(row, cell);
                int span = GetColSpan(cell);

                for (int k = 0; k < span; k++)
                    cols[col + k] = cell;
            }

            if (cols.Count > 0)
                grid.Add(cols);
        }

        if (grid.Count == 0)
        {
            ClearCollapsedBorders();
            return;
        }

        // The border each cell has on each side once collapsed with what is across the edge; where
        // a side meets several, as a spanning cell's does, the widest.
        var met = new Dictionary<CssBox, CollapsedBorder?[]>();
        void Meet(CssBox cell, int side, CollapsedBorder border)
        {
            if (!met.TryGetValue(cell, out var sides))
                met[cell] = sides = new CollapsedBorder?[4];

            sides[side] = sides[side] is { } current && current.Width >= border.Width ? current : border;
        }

        // Edges between a cell and the one to its right.
        foreach (var cols in grid)
        {
            int maxCol = 0;
            foreach (var c in cols.Keys) if (c > maxCol) maxCol = c;

            for (int c = 0; c < maxCol; c++)
            {
                if (!cols.TryGetValue(c, out var left) || !cols.TryGetValue(c + 1, out var right) || ReferenceEquals(left, right))
                    continue;

                var winner = rtl
                    ? ResolveCollapsedEdge(AuthoredBorder(right, Left), AuthoredBorder(left, Right))
                    : ResolveCollapsedEdge(AuthoredBorder(left, Right), AuthoredBorder(right, Left));

                Meet(left, Right, winner);
                Meet(right, Left, winner);
            }
        }

        // Edges between a cell and the one below it.
        for (int r = 0; r + 1 < grid.Count; r++)
        {
            foreach (var (col, top) in grid[r])
            {
                if (!grid[r + 1].TryGetValue(col, out var bottom) || ReferenceEquals(top, bottom))
                    continue;

                var winner = ResolveCollapsedEdge(AuthoredBorder(top, Bottom), AuthoredBorder(bottom, Top));
                Meet(top, Bottom, winner);
                Meet(bottom, Top, winner);
            }
        }

        // The perimeter: a cell's border there collapses with the table's own. §17.6.2.1 origin
        // priority puts the cell above the table, so on an exact tie the cell wins.
        int lastRow = grid.Count - 1;
        int globalMaxCol = 0;
        foreach (var cols in grid)
            foreach (var c in cols.Keys)
                if (c > globalMaxCol) globalMaxCol = c;

        for (int r = 0; r < grid.Count; r++)
        {
            foreach (var (col, cell) in grid[r])
            {
                if (r == 0) Meet(cell, Top, ResolveCollapsedEdge(AuthoredBorder(cell, Top), AuthoredBorder(_tableBox, Top)));
                if (r == lastRow) Meet(cell, Bottom, ResolveCollapsedEdge(AuthoredBorder(cell, Bottom), AuthoredBorder(_tableBox, Bottom)));
                if (col == 0) Meet(cell, Left, ResolveCollapsedEdge(AuthoredBorder(cell, Left), AuthoredBorder(_tableBox, Left)));
                if (col == globalMaxCol) Meet(cell, Right, ResolveCollapsedEdge(AuthoredBorder(cell, Right), AuthoredBorder(_tableBox, Right)));
            }
        }

        // Each cell takes half of each of its borders; a side that met nothing keeps its own.
        var collapsed = new Dictionary<CssBox, CollapsedBorder[]>();
        foreach (var cols in grid)
        {
            foreach (var cell in cols.Values)
            {
                if (collapsed.ContainsKey(cell))
                    continue;

                met.TryGetValue(cell, out var sides);
                var whole = new CollapsedBorder[4];
                for (int side = 0; side < 4; side++)
                    whole[side] = sides?[side] ?? ResolveCollapsedEdge(AuthoredBorder(cell, side), CollapsedBorder.None);

                collapsed[cell] = whole;
                cell.CollapsedBorders = new CollapsedBorderSides(Half(whole[Top]), Half(whole[Right]), Half(whole[Bottom]), Half(whole[Left]));
            }
        }

        // The table: half of the first row's first and last cells' outer borders, and of the
        // widest on its first and last rows.
        var first = grid[0];
        var last = grid[lastRow];
        var tableTop = Widest(first.Values, Top);
        var tableBottom = Widest(last.Values, Bottom);
        var tableLeft = collapsed[first[MinKey(first)]][Left];
        var tableRight = collapsed[first[MaxKey(first)]][Right];

        _tableBox.CollapsedBorders = new CollapsedBorderSides(Half(tableTop), Half(tableRight), Half(tableBottom), Half(tableLeft));

        CollapsedBorder Widest(IEnumerable<CssBox> cells, int side)
        {
            var widest = CollapsedBorder.None;
            foreach (var cell in cells)
            {
                var border = collapsed[cell][side];
                if (border.Width > widest.Width)
                    widest = border;
            }

            return widest;
        }
    }

    private static CollapsedBorder Half(CollapsedBorder border) =>
        border.Width > 0.01 ? border with { Width = border.Width / 2 } : CollapsedBorder.None;

    private static int MinKey(Dictionary<int, CssBox> cols)
    {
        int min = int.MaxValue;
        foreach (var c in cols.Keys) if (c < min) min = c;
        return min;
    }

    private static int MaxKey(Dictionary<int, CssBox> cols)
    {
        int max = int.MinValue;
        foreach (var c in cols.Keys) if (c > max) max = c;
        return max;
    }

    /// <summary>
    /// Gives the table and its cells their own borders back: outside the collapsing border model,
    /// or with no cells to collapse them with.
    /// </summary>
    private void ClearCollapsedBorders()
    {
        _tableBox.CollapsedBorders = null;

        foreach (var row in _allRows)
        {
            foreach (var cell in row.Boxes)
                cell.CollapsedBorders = null;
        }
    }

    private static int GetCellRealColumnIndex(CssBox row, CssBox cell)
    {
        int i = 0;

        foreach (CssBox b in row.Boxes)
        {
            if (b.Equals(cell))
                break;

            i += GetColSpan(b);
        }

        return i;
    }

    private double GetCellWidth(int column, CssBox b)
    {
        double colspan = Convert.ToSingle(GetColSpan(b));
        double sum = 0f;

        for (int i = column; i < column + colspan; i++)
        {
            if (column >= _columnWidths.Length)
                break;

            if (_columnWidths.Length <= i)
                break;

            sum += _columnWidths[i];
        }

        sum += (colspan - 1) * GetHorizontalSpacing();

        return sum; // -b.ActualBorderLeftWidth - b.ActualBorderRightWidth - b.ActualPaddingRight - b.ActualPaddingLeft;
    }

    private static int GetColSpan(CssBox b)
    {
        string att = b.GetAttribute("colspan", "1");

        if (!int.TryParse(att, out int colspan) || colspan < 1)
            return 1;

        return colspan;
    }

    private static int GetRowSpan(CssBox b)
    {
        string att = b.GetAttribute("rowspan", "1");

        if (!int.TryParse(att, out int rowspan))
            return 1;

        return rowspan;
    }

    /// <summary>
    /// CSS2.1 §17.5.3: the explicit <c>height</c> of a table row is a minimum.
    /// Returns the resolved length for a definite (px/em) row height, or 0 when
    /// the height is <c>auto</c> or a percentage (percentages resolve against the
    /// table height, which is not yet known during the row-height pass).
    /// </summary>
    private static double GetSpecifiedRowHeight(CssBox row)
    {
        string h = row.Height;
        if (string.IsNullOrEmpty(h)
            || h == CssConstants.Auto
            || h.EndsWith('%'))
            return 0;

        double v = CssLengthParser.ParseLength(h, 0, row.GetEmHeight());
        return double.IsNaN(v) || v < 0 ? 0 : v;
    }

    /// <summary>
    /// Sizes and aligns rowspan cells that no <see cref="CssSpacingBox"/> finalised
    /// because their trailing spanned rows were collapsed (<c>visibility:collapse</c>)
    /// or empty. Each such cell is clamped to the bottom of its last laid-out spanned
    /// row, then its content is aligned via
    /// <see cref="CssLayoutEngine.ApplyCellContentAlignment"/> (so <c>align-content</c>
    /// center/end positions overflowing content instead of leaving it at the start).
    /// </summary>
    private void FinalizeUnspacedRowSpanCells(
        ILayoutEnvironment g,
        List<(CssBox Row, double Top, double Bottom)> rowBounds,
        HashSet<CssBox> finalizedSpanCells)
    {
        if (rowBounds.Count == 0)
            return;

        var rowBottom = new Dictionary<CssBox, double>();
        foreach (var (row, _, bottom) in rowBounds)
            rowBottom[row] = bottom;

        for (int r = 0; r < _allRows.Count; r++)
        {
            foreach (var cell in _allRows[r].Boxes)
            {
                if (cell is CssSpacingBox || GetRowSpan(cell) <= 1 || finalizedSpanCells.Contains(cell))
                    continue;

                // Bottom of the last laid-out (non-collapsed) row this cell spans.
                double bottom = double.NaN;
                for (int k = r; k < r + GetRowSpan(cell) && k < _allRows.Count; k++)
                    if (rowBottom.TryGetValue(_allRows[k], out var b))
                        bottom = double.IsNaN(bottom) ? b : Math.Max(bottom, b);

                if (double.IsNaN(bottom) || bottom <= cell.Location.Y)
                    continue;

                cell.ActualBottom = bottom;
                cell.Size = new SizeF(cell.Size.Width, (float)(bottom - cell.Location.Y));

                CssLayoutEngine.ApplyCellContentAlignment(g, cell);
                finalizedSpanCells.Add(cell);
            }
        }
    }

    private static void MeasureWords(CssBox box, ILayoutEnvironment g)
    {
        if (box == null)
            return;

        foreach (var childBox in box.Boxes)
        {
            childBox.MeasureWordsSize(g);
            MeasureWords(childBox, g);
        }
    }

    private bool CanReduceWidth()
    {
        for (int i = 0; i < _columnWidths.Length; i++)
        {
            if (CanReduceWidth(i))
                return true;
        }

        return false;
    }

    private bool CanReduceWidth(int columnIndex)
    {
        if (_columnWidths.Length >= columnIndex || GetColumnMinWidths().Length >= columnIndex)
            return false;

        return _columnWidths[columnIndex] > GetColumnMinWidths()[columnIndex];
    }

    // A table box is always laid out inside its containing box.
    private CssBox TableParent => _tableBox.ParentBox ?? throw new InvalidOperationException("Table box has no parent box.");

    private double GetAvailableTableWidth()
    {
        CssLength tblen = new(_tableBox.Width);

        if (tblen.Number > 0)
        {
            _widthSpecified = true;
            return CssLengthParser.ParseLength(_tableBox.Width, TableParent.AvailableWidth, _tableBox.GetEmHeight());
        }
        else
        {
            // CSS2.1 §9.5: a table placed beside floats has the space they leave it, as a block is
            // narrowed to it. The algorithm took its container's whole width: beside a 100px float
            // in 1024px, a table with an auto width and a long line of text came out 1024px wide
            // and ran 100px past the edge, where browsers make it 924px wide and wrap the text.
            return _tableBox.WidthBesideFloats is double space
                ? Math.Min(TableParent.AvailableWidth, space)
                : TableParent.AvailableWidth;
        }
    }

    private double GetMaxTableWidth()
    {
        var tblen = new CssLength(_tableBox.MaxWidth);
        if (tblen.Number > 0)
        {
            _widthSpecified = true;
            return CssLengthParser.ParseLength(_tableBox.MaxWidth, TableParent.AvailableWidth, _tableBox.GetEmHeight());
        }
        else
        {
            return double.PositiveInfinity;
        }
    }

    private void GetColumnsMinMaxWidthByContent(bool onlyNans, out double[] minFullWidths, out double[] maxFullWidths)
    {
        MeasureColumns();

        minFullWidths = (double[])_columnMinWidths.Clone();
        maxFullWidths = (double[])_columnMaxWidths.Clone();

        if (!onlyNans)
            return;

        // The columns given a width already keep it.
        for (int i = 0; i < _columnWidths.Length; i++)
        {
            if (!double.IsNaN(_columnWidths[i]))
                minFullWidths[i] = maxFullWidths[i] = 0;
        }
    }

    private double GetAvailableCellWidth() => GetAvailableTableWidth() - GetHorizontalSpacing() * (_columnCount + 1) - _tableBox.ActualBorderLeftWidth - _tableBox.ActualBorderRightWidth
        - _tableBox.ActualPaddingLeft - _tableBox.ActualPaddingRight;

    private double GetWidthSum()
    {
        double f = 0f;

        foreach (double t in _columnWidths)
        {
            if (double.IsNaN(t))
                throw new Exception("CssTable Algorithm error: There's a NaN in column widths");
            else
                f += t;
        }

        //Take cell-spacing
        f += GetHorizontalSpacing() * (_columnWidths.Length + 1);

        //Take table borders and padding
        f += _tableBox.ActualBorderLeftWidth + _tableBox.ActualBorderRightWidth + _tableBox.ActualPaddingLeft + _tableBox.ActualPaddingRight;

        return f;
    }

    private static int GetSpan(CssBox b)
    {
        double f = CssLengthParser.ParseNumber(b.GetAttribute("span"), 1);
        return Math.Max(1, Convert.ToInt32(f));
    }

    private double[] GetColumnMinWidths()
    {
        MeasureColumns();
        return _columnMinWidths;
    }

    /// <summary>
    /// Measures each column's minimum and maximum content widths from the cells in it, once a
    /// layout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CSS 2.1 §17.5.2.2: a column is at least as wide as its cells' minimum content width, the
    /// width their content needs not to overflow them, and a cell spanning columns needs them, with
    /// the spacing between them, to be as wide as it together. The cells in one column are measured
    /// first. Then each cell spanning columns, those spanning fewer first, shares out over its
    /// columns what they lack of its widths (see <see cref="ShareOut"/>).
    /// </para>
    /// <para>
    /// A spanning cell's minimum went on its last column alone, less what the columns before it had
    /// so far, and was its longest word alone; its maximum was split evenly over its columns. In a
    /// table with <c>width: 100px</c>, a cell spanning two columns and holding a 40-letter word,
    /// over a row of two cells holding an x, made the columns 50px and 320px wide, where browsers
    /// make each 160px; with a 400px block in it the table stayed 100px wide, and the block ran out
    /// of it.
    /// </para>
    /// </remarks>
    private void MeasureColumns()
    {
        if (_columnMinWidths != null)
            return;

        int count = _columnWidths.Length;
        var mins = new double[count];
        var maxes = new double[count];
        var spanning = new List<(CssBox Cell, int First, int Last)>();
        int widestSpan = 0;

        foreach (CssBox row in _allRows)
        {
            foreach (CssBox cell in row.Boxes)
            {
                int first = GetCellRealColumnIndex(row, cell);
                int last = Math.Min(first + GetColSpan(cell), count) - 1;

                if (first > last)
                    continue;

                if (first < last)
                {
                    spanning.Add((cell, first, last));
                    widestSpan = Math.Max(widestSpan, last - first);
                    continue;
                }

                MeasureCell(cell, out double min, out double max);
                mins[first] = Math.Max(mins[first], min);
                maxes[first] = Math.Max(maxes[first], max);
            }
        }

        // A column given a width of its own is that wide at most, unless its cells need more.
        for (int i = 0; i < count; i++)
            maxes[i] = Math.Max(mins[i], IsSpecified(i) ? _specifiedColumnWidths[i] : maxes[i]);

        for (int span = 1; span <= widestSpan; span++)
        {
            foreach (var (cell, first, last) in spanning)
            {
                if (last - first != span)
                    continue;

                MeasureCell(cell, out double min, out double max);
                double spacing = span * GetHorizontalSpacing();

                ShareOut(min - spacing, first, last, mins, maxes);

                for (int i = first; i <= last; i++)
                    maxes[i] = Math.Max(maxes[i], mins[i]);

                ShareOut(max - spacing, first, last, maxes, maxes);
            }
        }

        _columnMinWidths = mins;
        _columnMaxWidths = maxes;
    }

    /// <summary>
    /// A cell's minimum content width, and its maximum, at least as wide.
    /// </summary>
    /// <remarks>
    /// The minimum is the min-content width, which <see cref="CssBox.GetMinMaxWidth"/> measures with
    /// the widths of the blocks in the cell, or its longest word where that is wider.
    /// <see cref="CssBox.GetMinimumWidth"/> is the longest word alone, so a block with a width of
    /// its own counted for nothing on its own: a table with <c>width: 100px</c> stayed 100px wide
    /// around a 400px block in a cell, and the block ran out of it.
    /// </remarks>
    private static void MeasureCell(CssBox cell, out double min, out double max)
    {
        cell.GetMinMaxWidth(out double minContentWidth, out double maxContentWidth);
        min = Math.Max(cell.GetMinimumWidth(), double.IsNaN(minContentWidth) ? 0 : minContentWidth);
        max = Math.Max(min, double.IsNaN(maxContentWidth) ? 0 : maxContentWidth);
    }

    /// <summary>
    /// Widens the columns <paramref name="first"/> to <paramref name="last"/>, as measured in
    /// <paramref name="widths"/>, until together they are <paramref name="width"/> wide. The width
    /// is shared out as CSS Tables 3 shares a table's width out over its columns: first the columns
    /// given a width of their own, up to it, then the others, up to their maximums in
    /// <paramref name="maxes"/>, each in proportion to what it has yet to take; past that, the
    /// columns without a width of their own, in proportion to their maximums.
    /// </summary>
    private void ShareOut(double width, int first, int last, double[] widths, double[] maxes)
    {
        double current = 0, specifiedGuess = 0, maxGuess = 0;

        for (int i = first; i <= last; i++)
        {
            current += widths[i];
            specifiedGuess += IsSpecified(i) ? maxes[i] : widths[i];
            maxGuess += maxes[i];
        }

        if (width <= current)
            return;

        double[] excessShares = width > maxGuess ? GetExcessShares(first, last, maxes) : [];

        for (int i = first; i <= last; i++)
        {
            double share;

            if (width <= specifiedGuess)
                share = IsSpecified(i) ? widths[i] + (maxes[i] - widths[i]) * (width - current) / (specifiedGuess - current) : widths[i];
            else if (width <= maxGuess)
                share = IsSpecified(i) ? maxes[i] : widths[i] + (maxes[i] - widths[i]) * (width - specifiedGuess) / (maxGuess - specifiedGuess);
            else
                share = maxes[i] + (width - maxGuess) * excessShares[i - first];

            widths[i] = Math.Max(widths[i], share);
        }
    }

    /// <summary>
    /// The shares of the columns <paramref name="first"/> to <paramref name="last"/> in what a
    /// spanning cell needs beyond their maximums: the columns without a width of their own take
    /// it, in proportion to their maximums, or evenly if none has any content; with no such
    /// column, the others take it likewise.
    /// </summary>
    private double[] GetExcessShares(int first, int last, double[] maxes)
    {
        var shares = new double[last - first + 1];

        foreach (bool specified in new[] { false, true })
        {
            double sum = 0;
            int columns = 0;

            for (int i = first; i <= last; i++)
            {
                if (IsSpecified(i) != specified)
                    continue;

                sum += maxes[i];
                columns++;
            }

            if (columns == 0)
                continue;

            for (int i = first; i <= last; i++)
            {
                if (IsSpecified(i) == specified)
                    shares[i - first] = sum > 0 ? maxes[i] / sum : 1.0 / columns;
            }

            break;
        }

        return shares;
    }

    private bool IsSpecified(int column) => !double.IsNaN(_specifiedColumnWidths[column]);

    // CSS 2.1 §17.6.2: the collapsing border model has no spacing between the cells, which share
    // their borders instead (see ResolveCollapsedBorders). It had -1px, which overlapped cells with
    // 1px borders, and each column after the first took a pixel from the table's width.
    private double GetHorizontalSpacing() => _tableBox.BorderCollapse == CssConstants.Collapse ? 0 : _tableBox.ActualBorderSpacingHorizontal;
    private static double GetHorizontalSpacing(CssBox box) => box.BorderCollapse == CssConstants.Collapse ? 0 : box.ActualBorderSpacingHorizontal;
    private double GetVerticalSpacing() => _tableBox.BorderCollapse == CssConstants.Collapse ? 0 : _tableBox.ActualBorderSpacingVertical;
}
