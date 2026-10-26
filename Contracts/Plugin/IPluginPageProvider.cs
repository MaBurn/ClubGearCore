namespace ClubGear.Plugin.Contracts;

public interface IPluginPageProvider
{
    Task<PluginPageDefinition> GetPageDefinitionAsync(
        IPluginHostContext context,
        CancellationToken ct = default);

    Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> GetRowsAsync(
        IPluginHostContext context,
        string? filterValue,
        string? entityKey,
        CancellationToken ct = default);

    Task<PluginCommandResult> ExecuteCommandAsync(
        IPluginHostContext context,
        string commandKey,
        string? entityKey,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken ct = default);

    /// <summary>
    /// Contract 1.11.0. Returns one page of rows plus the total number of matches, so the host
    /// can render a pager without asking the plugin how many rows exist.
    /// </summary>
    /// <remarks>
    /// The default implementation delegates to <see cref="GetRowsAsync"/> and then sorts and
    /// pages in memory. A plugin written against 1.10.0 therefore keeps working unchanged and
    /// gains sorting and paging for free. Override this when the underlying store can do the
    /// work itself — an inventory with 20.000 items should not materialise every row per
    /// request just to show fifty of them.
    /// </remarks>
    async Task<PluginPageRows> GetRowsPageAsync(
        IPluginHostContext context,
        PluginPageQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await GetRowsAsync(context, query.FilterValue, query.EntityKey, ct)
            .ConfigureAwait(false);

        return PluginPageRows.SortAndPageInMemory(rows, query);
    }
}

public enum PluginSortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Contract 1.11.0. What the host asks a page for: the existing filter and entity key, plus
/// sorting and paging.
/// </summary>
public sealed record PluginPageQuery
{
    public string? FilterValue { get; init; }

    public string? EntityKey { get; init; }

    /// <summary>Column key to sort by. <c>null</c> leaves the plugin's own order untouched.</summary>
    public string? SortKey { get; init; }

    public PluginSortDirection SortDirection { get; init; } = PluginSortDirection.Ascending;

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary><c>null</c> requests every row, which is the pre-1.11.0 behaviour.</summary>
    public int? PageSize { get; init; }
}

/// <summary>
/// Contract 1.11.0. One page of rows and the total number of rows matching the filter —
/// <see cref="TotalCount"/> counts all matches, not the length of <see cref="Rows"/>.
/// </summary>
public sealed record PluginPageRows(
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    int TotalCount)
{
    /// <summary>
    /// Applies <paramref name="query"/> to an already materialised, unsorted, unpaged result.
    /// Lives in the contract so the host and every plugin that falls back to it order rows
    /// identically.
    /// </summary>
    /// <remarks>
    /// Ordering is ordinal and case-insensitive over the raw cell text, with empty and missing
    /// values sorted last in both directions — cell values are strings, so a plugin that needs
    /// numeric or culture-aware ordering has to override
    /// <see cref="IPluginPageProvider.GetRowsPageAsync"/> and sort in its own store.
    /// </remarks>
    public static PluginPageRows SortAndPageInMemory(
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        PluginPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<IReadOnlyDictionary<string, string?>> ordered = rows;

        if (!string.IsNullOrWhiteSpace(query.SortKey))
        {
            static string? Cell(IReadOnlyDictionary<string, string?> row, string key)
                => row.TryGetValue(key, out var value) ? value : null;

            var key = query.SortKey;

            // Missing and empty cells sort last whichever direction is asked for: a column of
            // mostly-filled values reads better than one that opens with a block of blanks.
            ordered = query.SortDirection == PluginSortDirection.Descending
                ? rows.OrderBy(r => string.IsNullOrEmpty(Cell(r, key)))
                      .ThenByDescending(r => Cell(r, key), StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(r => string.IsNullOrEmpty(Cell(r, key)))
                      .ThenBy(r => Cell(r, key), StringComparer.OrdinalIgnoreCase);
        }

        if (query.PageSize is not > 0)
        {
            var all = ordered as IReadOnlyList<IReadOnlyDictionary<string, string?>> ?? ordered.ToList();
            return new PluginPageRows(all, rows.Count);
        }

        var pageSize = query.PageSize.Value;
        var page = query.Page < 1 ? 1 : query.Page;

        var window = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PluginPageRows(window, rows.Count);
    }
}

public sealed record PluginPageDefinition(
    string PageKey,
    string Title,
    string EntityKeyColumn,
    IReadOnlyList<PluginPageColumn> Columns,
    IReadOnlyList<PluginPageCommand> Commands,
    string? ListPermission,
    string? FilterPlaceholder)
{
    /// <summary>
    /// Contract 1.11.0. Rows per page the host should request. <c>null</c> keeps the pre-1.11.0
    /// behaviour of putting every row on one page. Declared as an init-only property rather than
    /// a constructor parameter so the seven-argument constructor an already-compiled plugin
    /// calls still exists.
    /// </summary>
    public int? DefaultPageSize { get; init; }
}

public sealed record PluginPageColumn(
    string Key,
    string Label)
{
    /// <summary>
    /// Contract 1.11.0. Offers this column as a sort option in the host's table header.
    /// Init-only for the same binary-compatibility reason as
    /// <see cref="PluginPageDefinition.DefaultPageSize"/>.
    /// </summary>
    public bool Sortable { get; init; }

    /// <summary>
    /// Contract 1.15.0. When set, the host renders this column's cell value as a Bootstrap Icon
    /// (<c>&lt;i class="bi {value}"&gt;&lt;/i&gt;</c>) instead of plain text, in both the list and
    /// detail views — the same rendering already used for <see cref="PluginPageCommand.Icon"/> and
    /// <see cref="PluginNavEntry.Icon"/>, extended to a per-row column. Defaults to <c>false</c> so
    /// every already-compiled plugin keeps rendering this column as text, unchanged.
    /// </summary>
    public bool IsIconColumn { get; init; }
}

public sealed record PluginPageCommand(
    string Key,
    string Label,
    string? Icon,
    string? RequiredPermission,
    IReadOnlyList<PluginFieldSchema>? Schema,
    bool RequiresEntityKey);
