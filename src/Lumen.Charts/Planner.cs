namespace Lumen.Charts;

/// <summary>What kind of day or run of days a <see cref="PlannerPeriod"/> is.</summary>
public enum PeriodKind
{
    /// <summary>A public holiday: marked on its day and counted towards long weekends.</summary>
    PublicHoliday,
    /// <summary>A school holiday: a band along the top of its days.</summary>
    SchoolHoliday,
    /// <summary>Anything else worth seeing when choosing a date, such as exam weeks or a large external event.</summary>
    Other
}

/// <summary>How settled an event's date is.</summary>
public enum PlannerStatus
{
    /// <summary>Confirmed: drawn solid.</summary>
    Confirmed,
    /// <summary>Provisional, pencilled in: drawn hatched and said "provisional".</summary>
    Provisional,
    /// <summary>Cancelled: drawn struck through and said "cancelled".</summary>
    Cancelled
}

/// <summary>How strongly an event competes with the viewer's plans, as the host decides it; the planner draws it by weight and
/// dash and says it in words, never by colour alone.</summary>
public enum PlannerRelevance
{
    /// <summary>Unrelated: drawn thin and muted.</summary>
    Other,
    /// <summary>Close: a soft clash, drawn medium and dashed and said "close".</summary>
    Near,
    /// <summary>A clash: drawn bold and said "clash".</summary>
    Clash
}

/// <summary>The level a planner is drawn at.</summary>
public enum PlannerZoom
{
    /// <summary>The whole period, a row per month.</summary>
    Year,
    /// <summary>One month as a grid of weeks.</summary>
    Month,
    /// <summary>One day as a list.</summary>
    Day
}

/// <summary>How a planner is laid out: wide for a desktop, narrow for a phone (hosts switch below 640 pixels).</summary>
public enum PlannerLayout
{
    /// <summary>The year as weekday-aligned month rows; a month as a grid of weeks.</summary>
    Wide,
    /// <summary>The year as compact month bars; a month as a list of the days that hold something.</summary>
    Narrow
}

/// <summary>A place events and holidays belong to. <paramref name="Parent"/> makes a hierarchy: a country, its provinces, their
/// districts. A region includes itself and every region below it.</summary>
/// <param name="Code">A short unique code, such as <c>ZA</c> or <c>ZA-GP</c>, compared ordinally.</param>
/// <param name="Name">The name written and said, such as <c>Gauteng</c>.</param>
/// <param name="Parent">The code of the region it lies in, or null at the top.</param>
public sealed record PlannerRegion(string Code, string Name, string? Parent = null);

/// <summary>A holiday or other day or run of days to see when choosing a date.</summary>
/// <param name="From">Its first day.</param>
/// <param name="To">Its last day; null for one day.</param>
/// <param name="Name">What it is called, such as <c>Freedom Day</c>.</param>
/// <param name="Kind">Public holiday, school holiday or other.</param>
/// <param name="Region">The region it applies to, and every region below it; null for everywhere.</param>
public sealed record PlannerPeriod(DateOnly From, DateOnly? To, string Name, PeriodKind Kind, string? Region = null);

/// <summary>An event on the planner: someone's race, ride or other occasion.</summary>
/// <param name="Id">A unique identifier, returned to the host when the event is selected.</param>
/// <param name="Name">The event's name.</param>
/// <param name="Start">Its first day.</param>
public sealed record PlannerEvent(string Id, string Name, DateOnly Start)
{
    /// <summary>Its last day, for an event over several days; null for one day.</summary>
    public DateOnly? End { get; init; }
    /// <summary>The region it is held in; null shows it under every region.</summary>
    public string? Region { get; init; }
    /// <summary>A free-text category, such as a discipline, filtered on and said in its name.</summary>
    public string? Category { get; init; }
    /// <summary>A free-text audience, such as an age group or level, filtered on and said in its name.</summary>
    public string? Audience { get; init; }
    /// <summary>How settled its date is: confirmed by default.</summary>
    public PlannerStatus Status { get; init; }
    /// <summary>How strongly it competes with the viewer's plans, as the host decides: unrelated by default.</summary>
    public PlannerRelevance Relevance { get; init; }
    /// <summary>The viewer's own event: drawn outlined and said "yours".</summary>
    public bool Mine { get; init; }
    /// <summary>A short note, at most 120 characters, said in its name and shown in the day view.</summary>
    public string? Note { get; init; }
    /// <summary>A link for the host to open; the static drawing does not follow it.</summary>
    public string? Url { get; init; }
}

/// <summary>Which periods and events a planner shows. An empty list means every value. A region includes the regions below it, and
/// a period or event set for a region above the filtered one still shows (a country's holiday under one of its provinces).</summary>
public sealed record PlannerFilter
{
    /// <summary>Region codes to show; empty for all.</summary>
    public IReadOnlyList<string> Regions { get; init; } = [];
    /// <summary>Categories to show, compared ordinally; empty for all.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];
    /// <summary>Audiences to show, compared ordinally; empty for all.</summary>
    public IReadOnlyList<string> Audiences { get; init; } = [];
    /// <summary>Statuses to show; empty for all.</summary>
    public IReadOnlyList<PlannerStatus> Statuses { get; init; } = [];
    /// <summary>Relevances to show; empty for all.</summary>
    public IReadOnlyList<PlannerRelevance> Relevances { get; init; } = [];
}

/// <summary>What a planner draws: the whole period, a month, or a day.</summary>
/// <param name="Zoom">The level.</param>
/// <param name="Date">For a month, any day in it (the first is used); for a day, the day; ignored for the whole period.</param>
public readonly record struct PlannerView(PlannerZoom Zoom, DateOnly Date)
{
    /// <summary>The whole period.</summary>
    public static PlannerView WholePeriod => new(PlannerZoom.Year, default);
    /// <summary>One month.</summary>
    public static PlannerView Month(int year, int month) => new(PlannerZoom.Month, new DateOnly(year, month, 1));
    /// <summary>One day.</summary>
    public static PlannerView Day(DateOnly day) => new(PlannerZoom.Day, day);
}

/// <summary>A planner: a period of days with weekends, holidays and events by region, drawn by <see cref="PlannerSvg.Render"/>.</summary>
public sealed record PlannerSpec
{
    /// <summary>The heading and the drawing's accessible name.</summary>
    public string Title { get; init; } = "Planner";
    /// <summary>A line under the title, also said.</summary>
    public string Description { get; init; } = "";
    /// <summary>The first day shown.</summary>
    public DateOnly From { get; init; }
    /// <summary>The last day shown, at most 399 days after <see cref="From"/>.</summary>
    public DateOnly To { get; init; }
    /// <summary>The day each week starts on: Monday by default.</summary>
    public DayOfWeek WeekStart { get; init; } = DayOfWeek.Monday;
    /// <summary>The weekend days: Saturday and Sunday by default.</summary>
    public IReadOnlyList<DayOfWeek> Weekend { get; init; } = [DayOfWeek.Saturday, DayOfWeek.Sunday];
    /// <summary>The regions periods and events refer to.</summary>
    public IReadOnlyList<PlannerRegion> Regions { get; init; } = [];
    /// <summary>Holidays and other periods.</summary>
    public IReadOnlyList<PlannerPeriod> Periods { get; init; } = [];
    /// <summary>The events.</summary>
    public IReadOnlyList<PlannerEvent> Events { get; init; } = [];
    /// <summary>Which periods and events to show; null for all.</summary>
    public PlannerFilter? Filter { get; init; }
    /// <summary>The preset drawn with when no <see cref="Style"/> is set.</summary>
    public ChartTheme Theme { get; init; }
    /// <summary>A host's colours and typeface; replaces <see cref="Theme"/>.</summary>
    public ChartStyle? Style { get; init; }
    /// <summary>The drawing's width in SVG units, 320 to 4096: 1100 by default.</summary>
    public int Width { get; init; } = 1100;
    /// <summary>Draws the title and description, the default; off, they stay the accessible name only.</summary>
    public bool DrawTitles { get; init; } = true;
    /// <summary>Paints the background, the default; off, the surface behind shows through (set <see cref="ChartStyle.Background"/> to it).</summary>
    public bool PaintBackground { get; init; } = true;

    /// <summary>A planner of one calendar year, 1 January to 31 December.</summary>
    public static PlannerSpec ForYear(int year) => new() { From = new DateOnly(year, 1, 1), To = new DateOnly(year, 12, 31) };
}
