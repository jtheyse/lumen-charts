namespace Lumen.Charts.Blazor;

/// <summary>Draws a vertical event timeline by writing the HTML fragment <see cref="EventTimelineHtml"/> writes, and, with
/// <see cref="PageSize"/>, lets the reader show more events at either end in an interactive render mode; a prerender shows the opening
/// window with buttons that do nothing yet. The status line names the events shown after each press for assistive technology. It is styled
/// from the spec's own style, else the style cascaded by <c>LumenBrand</c>, else <see cref="ChartStyle.Light"/>.</summary>
public partial class LumenEvents { }
