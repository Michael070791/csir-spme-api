namespace Csir.Spme.Application.Leave;

/// <summary>
/// Calculates leave working days and the expected return-to-duty date.
/// Working days are counted inclusively from the start date through the last day of leave,
/// excluding Saturdays, Sundays, and supplied public holidays.
/// </summary>
public static class WorkingDaysCalculator
{
    public static decimal Calculate(DateTime startDate, DateTime endDate, IReadOnlyCollection<DateTime> holidayDates)
    {
        if (endDate.Date < startDate.Date)
        {
            return 0m;
        }

        var holidays = NormalizeHolidays(holidayDates);
        var days = 0m;
        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            if (IsWorkingDay(date, holidays))
            {
                days += 1m;
            }
        }

        return days;
    }

    /// <summary>
    /// First working day strictly after <paramref name="lastLeaveDay"/> — the expected return to duty.
    /// </summary>
    public static DateTime ExpectedReturnDate(DateTime lastLeaveDay, IReadOnlyCollection<DateTime> holidayDates)
    {
        var holidays = NormalizeHolidays(holidayDates);
        var candidate = lastLeaveDay.Date.AddDays(1);
        while (!IsWorkingDay(candidate, holidays))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    private static bool IsWorkingDay(DateTime date, HashSet<DateTime> holidays) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) &&
        !holidays.Contains(date.Date);

    private static HashSet<DateTime> NormalizeHolidays(IReadOnlyCollection<DateTime> holidayDates) =>
        holidayDates.Select(date => date.Date).ToHashSet();
}
