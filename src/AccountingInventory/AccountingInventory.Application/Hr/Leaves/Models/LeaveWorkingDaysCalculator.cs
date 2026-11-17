namespace AccountingInventory.Application.Hr.Leaves.Models;

public static class LeaveWorkingDaysCalculator
{
    private static readonly HashSet<DayOfWeek> WeekendDays = new()
    {
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    };

    // Standard official gazetted national & mandatory festival holidays
    public static readonly HashSet<DateOnly> StandardHolidays = new()
    {
        new DateOnly(2026, 1, 26),  // Republic Day
        new DateOnly(2026, 3, 4),   // Holi
        new DateOnly(2026, 4, 3),   // Good Friday
        new DateOnly(2026, 5, 1),   // Maharashtra Day / May Day
        new DateOnly(2026, 8, 15),  // Independence Day
        new DateOnly(2026, 10, 2),  // Gandhi Jayanti
        new DateOnly(2026, 10, 20), // Dussehra
        new DateOnly(2026, 11, 8),  // Diwali (Laxmi Pujan)
        new DateOnly(2026, 11, 10), // Diwali (Bhai Dooj)
        new DateOnly(2026, 12, 25), // Christmas
    };

    public static (decimal TotalWorkingDays, int WeekendDaysCount, int HolidayDaysCount) CalculateNetLeaveDays(
        DateOnly startDate,
        DateOnly endDate,
        IEnumerable<DateOnly>? customHolidays = null)
    {
        if (endDate < startDate)
            return (0m, 0, 0);

        var holidaysSet = new HashSet<DateOnly>(StandardHolidays);
        if (customHolidays != null)
        {
            foreach (var h in customHolidays)
                holidaysSet.Add(h);
        }

        var weekends = 0;
        var holidays = 0;
        var workingDays = 0;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var isWeekend = WeekendDays.Contains(date.DayOfWeek);
            var isHoliday = holidaysSet.Contains(date);

            if (isWeekend)
            {
                weekends++;
            }
            else if (isHoliday)
            {
                holidays++;
            }
            else
            {
                workingDays++;
            }
        }

        return (workingDays, weekends, holidays);
    }
}
