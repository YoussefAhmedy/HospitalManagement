using Hospital.DAL.Entities;
using Hospital.DAL.Entities.OwnedTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hospital.Tests;

[TestClass]
public sealed class AppointmentRulesTests
{
    [TestMethod]
    public void IsBookableDate_RequiresADayAfterClinicToday()
    {
        var today = new DateTime(2026, 10, 2);

        Assert.IsTrue(AppointmentRules.IsBookableDate(today.AddDays(1), today));
        Assert.IsFalse(AppointmentRules.IsBookableDate(today, today));
        Assert.IsFalse(AppointmentRules.IsBookableDate(today.AddDays(-1), today));
        Assert.IsFalse(AppointmentRules.IsBookableDate(default, today));
    }

    [TestMethod]
    public void HasDailyCapacity_RejectsTheFiftiethExistingBooking()
    {
        Assert.IsTrue(AppointmentRules.HasDailyCapacity(49));
        Assert.IsFalse(AppointmentRules.HasDailyCapacity(50));
        Assert.IsFalse(AppointmentRules.HasDailyCapacity(-1));
        Assert.IsFalse(AppointmentRules.HasDailyCapacity(0, 0));
    }

    [TestMethod]
    public void IsScheduledForDate_MatchesClinicWeekday()
    {
        var saturday = new DateTime(2026, 10, 3);

        Assert.IsTrue(AppointmentRules.IsScheduledForDate(saturday, DayOfWeek.Saturday));
        Assert.IsFalse(AppointmentRules.IsScheduledForDate(saturday, DayOfWeek.Sunday));
    }

    [TestMethod]
    public void IsValidStatus_RejectsUndefinedValues()
    {
        Assert.IsTrue(AppointmentRules.IsValidStatus((int)AppointStatus.Pending));
        Assert.IsTrue(AppointmentRules.IsValidStatus((int)AppointStatus.Cancelled));
        Assert.IsFalse(AppointmentRules.IsValidStatus(0));
        Assert.IsFalse(AppointmentRules.IsValidStatus(999));
    }
}
