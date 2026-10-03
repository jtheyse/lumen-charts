using System.Globalization;
using Lumen.Charts;

public static class Probe
{
    public static void Run()
    {
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var kolkata = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        static double At(int year, int month, int day, int hour = 0) =>
            TimeAxis.Value(new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero));

        void Show(string name, Axis axis)
        {
            var ticks = axis.Ticks(6);
            Console.WriteLine($"{name}:");
            foreach (var (value, label) in ticks)
            {
                var utc = TimeAxis.Moment(value).UtcDateTime;
                var local = axis.Zone is null ? utc : TimeZoneInfo.ConvertTimeFromUtc(utc, axis.Zone);
                Console.WriteLine($"   {label,-14} local {local:yyyy-MM-dd HH:mm}  utc {utc:yyyy-MM-dd HH:mm}");
            }
        }

        Show("UTC, four days", new Axis(AxisKind.Time, At(2026, 1, 5), At(2026, 1, 9)));
        Show("New York, four days", new Axis(AxisKind.Time, At(2026, 1, 5), At(2026, 1, 9)) { Zone = newYork });
        // Clocks go forward on 8 March 2026 in the United States.
        Show("New York across a spring change", new Axis(AxisKind.Time, At(2026, 3, 6), At(2026, 3, 11)) { Zone = newYork });
        Show("Kolkata, twelve hours", new Axis(AxisKind.Time, At(2026, 5, 4, 3), At(2026, 5, 4, 15)) { Zone = kolkata });
        Show("New York, one year", new Axis(AxisKind.Time, At(2026, 1, 1), At(2026, 12, 31)) { Zone = newYork });
        Console.WriteLine("Format in New York: " + new Axis(AxisKind.Time, At(2026, 1, 5), At(2026, 1, 5, 12)) { Zone = newYork }.Format(At(2026, 1, 5, 4)));
        Console.WriteLine("Format in UTC:      " + new Axis(AxisKind.Time, At(2026, 1, 5), At(2026, 1, 5, 12)).Format(At(2026, 1, 5, 4)));
    }
}
