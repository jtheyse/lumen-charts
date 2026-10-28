namespace Lumen.Charts;

/// <summary>A table's rows in a chosen sort, and the pages they fall on.</summary>
internal sealed class TableView
{
    private readonly int _rows;
    private readonly int? _pageSize;

    private TableView(IReadOnlyList<int> order, int? pageSize, bool startAtEnd, int? sortBy, bool descending)
    {
        Order = order;
        SortBy = sortBy;
        Descending = descending;
        _rows = order.Count;
        _pageSize = pageSize;
        Pages = pageSize is { } size ? Math.Max(1, (_rows + size - 1) / size) : 1;
        DefaultPage = startAtEnd ? Pages - 1 : 0;
    }

    /// <summary>The column the rows are sorted by; null when the spec's row order is kept.</summary>
    internal int? SortBy { get; }
    /// <summary>Whether the sort runs from the largest down.</summary>
    internal bool Descending { get; }
    /// <summary>Row indexes in sorted order.</summary>
    internal IReadOnlyList<int> Order { get; }
    /// <summary>The number of pages; 1 without a page size.</summary>
    internal int Pages { get; }
    /// <summary>The page the table opens on: the last with <see cref="TableSpec.StartAtEnd"/>, else the first.</summary>
    internal int DefaultPage { get; }

    /// <summary>Whether a column's first sort runs from the largest down: when a higher value is better in a number, bar or change column. Text and date columns start ascending.</summary>
    internal static bool NaturalDescending(TableColumn c) => c.Better == Better.Higher && c.Kind is TableColumnKind.Number or TableColumnKind.Bar or TableColumnKind.Change;

    /// <summary>Validates the spec, then sorts by <paramref name="sortBy"/> and <paramref name="descending"/>; with no <paramref name="sortBy"/>
    /// the spec's own <see cref="TableSpec.SortBy"/> and <see cref="TableSpec.SortDescending"/> apply, and with neither the spec's row order is kept.</summary>
    internal static TableView Of(TableSpec spec, int? sortBy, bool descending)
    {
        TableValidation.Spec(spec);
        TableValidation.SortColumn(spec, sortBy);
        if (sortBy is null) { sortBy = spec.SortBy; descending = spec.SortDescending; }

        var order = Enumerable.Range(0, spec.Rows.Count).ToList();
        if (sortBy is { } column)
        {
            var kind = spec.Columns[column].Kind;
            var keys = order.Select(i => Key(spec.Rows[i].Cells[column], kind)).ToArray();
            // A total order: missing values last in both directions, and equal keys in the spec's row order in both directions.
            order.Sort((a, b) =>
            {
                var x = keys[a]; var y = keys[b];
                if (x is null || y is null) return x is null && y is null ? a.CompareTo(b) : x is null ? 1 : -1;
                var compare = Compare(x, y);
                if (descending) compare = -compare;
                return compare != 0 ? compare : a.CompareTo(b);
            });
        }
        return new TableView(order, spec.PageSize, spec.StartAtEnd, sortBy, descending);
    }

    /// <summary>The first and last position of a page in <see cref="Order"/>, zero-based and inclusive.</summary>
    internal (int First, int Last) RowsOn(int page)
    {
        if (page < 0 || page >= Pages) throw new ArgumentException($"Page {page} is not a page of this table; the pages are 0 to {Pages - 1}.");
        if (_pageSize is not { } size) return (0, _rows - 1);
        return (page * size, Math.Min(_rows, (page + 1) * size) - 1);
    }

    private static IComparable? Key(TableCell cell, TableColumnKind kind) => kind switch
    {
        TableColumnKind.Text => cell.Text,
        TableColumnKind.Number or TableColumnKind.Bar => cell.Number,
        TableColumnKind.Date => cell.Date,
        TableColumnKind.Change => cell.Number is { } n && cell.Previous is { } p ? n - p : null,
        _ => null
    };

    private static int Compare(IComparable x, IComparable y) =>
        x is string s ? StringComparer.InvariantCultureIgnoreCase.Compare(s, (string)y) : x.CompareTo(y);
}
