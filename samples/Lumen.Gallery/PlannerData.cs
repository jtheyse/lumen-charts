using Lumen.Charts;

namespace Lumen.Gallery;

/// <summary>An invented season of events by invented organizers, with South Africa's provinces and its 2027 public holidays,
/// for the home page's planner.</summary>
public static class PlannerData
{
    public static PlannerSpec Season(ChartTheme theme, int width = 1100) => PlannerSpec.ForYear(2027) with
    {
        Title = "Planning a season", Description = "Invented organizers' events in Gauteng and the Western Cape",
        Theme = theme, Width = width,
        Regions = [new("ZA", "South Africa"), new("ZA-GP", "Gauteng", "ZA"), new("ZA-WC", "Western Cape", "ZA")],
        Periods =
        [
            new(new(2027, 1, 1), null, "New Year's Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 3, 22), null, "Human Rights Day (observed)", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 3, 26), null, "Good Friday", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 3, 29), null, "Family Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 4, 27), null, "Freedom Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 5, 1), null, "Workers' Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 6, 16), null, "Youth Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 8, 9), null, "National Women's Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 9, 24), null, "Heritage Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 12, 16), null, "Day of Reconciliation", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 12, 25), null, "Christmas Day", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 12, 26), null, "Day of Goodwill", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 12, 27), null, "Day of Goodwill (observed)", PeriodKind.PublicHoliday, "ZA"),
            new(new(2027, 3, 26), new DateOnly(2027, 4, 5), "Invented school holiday", PeriodKind.SchoolHoliday, "ZA")
        ],
        Events =
        [
            new("hx1", "Hilltop XCO #1", new(2027, 2, 13)) { Region = "ZA-GP", Category = "XCO", Audience = "Kids", Relevance = PlannerRelevance.Clash },
            new("cs", "Coast Stage Race", new(2027, 3, 12)) { End = new DateOnly(2027, 3, 14), Region = "ZA-WC", Category = "Stage", Audience = "Open", Status = PlannerStatus.Provisional },
            new("hx2", "Hilltop XCO #2", new(2027, 3, 13)) { Region = "ZA-GP", Category = "XCO", Audience = "Kids", Relevance = PlannerRelevance.Clash },
            new("cr", "Invented Club Ride", new(2027, 3, 13)) { Region = "ZA-GP", Category = "Road", Audience = "Open", Relevance = PlannerRelevance.Near },
            new("mine", "Our Spring Enduro", new(2027, 9, 18)) { Region = "ZA-GP", Category = "Enduro", Audience = "Juniors", Mine = true },
            new("cx", "Cancelled Night Race", new(2027, 5, 8)) { Region = "ZA-GP", Category = "XCO", Status = PlannerStatus.Cancelled, Relevance = PlannerRelevance.Near, Note = "Moved to 2028" },
        ]
    };
}
