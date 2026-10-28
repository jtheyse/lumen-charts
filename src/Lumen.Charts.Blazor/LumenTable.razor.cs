namespace Lumen.Charts.Blazor;

/// <summary>Draws a read-only data table by writing the HTML fragment <see cref="TableHtml"/> writes, and lets the reader sort by a
/// column and page through the rows in an interactive render mode; a prerender shows the table in its opening state with buttons
/// that do nothing yet. The status line names each sort and each page for assistive technology. It is styled from the spec's own
/// style, else the style cascaded by <c>LumenBrand</c>, else <see cref="ChartStyle.Light"/>.</summary>
public partial class LumenTable { }
