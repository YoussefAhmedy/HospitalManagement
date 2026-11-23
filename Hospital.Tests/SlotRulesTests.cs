using Hospital.BLL.Common.Rules;
using Hospital.DAL.Entities;
using Hospital.DAL.Entities.OwnedTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hospital.Tests;

/// <summary>
/// Scheduling invariants. These are the rules that prevent double-booked slots and
/// unverified check-ins; they run without a database so they can gate every commit.
/// </summary>
[TestClass]
public sealed class SlotRulesTests
{
    [TestMethod]
    public void GenerateSlots_RespectsGranularityAndClinicClose()
    {
        var slots = SlotRules.GenerateSlots(TimeSpan.FromHours(9), TimeSpan.FromHours(10), 20);

        CollectionAssert.AreEqual(new[] { 540, 560, 580 }, slots.ToArray());
    }

    [TestMethod]
    public void GenerateSlots_ReturnsNothingForImpossibleSchedules()
    {
        Assert.AreEqual(0, SlotRules.GenerateSlots(TimeSpan.FromHours(10), TimeSpan.FromHours(9), 20).Count);
        Assert.AreEqual(0, SlotRules.GenerateSlots(TimeSpan.FromHours(9), TimeSpan.FromHours(10), 4).Count);
        Assert.AreEqual(0, SlotRules.GenerateSlots(TimeSpan.FromHours(9), TimeSpan.FromHours(9), 20).Count);
    }

    [TestMethod]
    public void AvailableSlots_RemovesAnySlotAlreadyTaken()
    {
        var generated = new[] { 540, 560, 580, 600 };
        var taken = new[] { 560, 600 };

        CollectionAssert.AreEqual(new[] { 540, 580 }, SlotRules.AvailableSlots(generated, taken).ToArray());
    }

    [TestMethod]
    public void Overlaps_DetectsPartialAndNestedIntervals()
    {
        Assert.IsTrue(SlotRules.Overlaps(540, 20, 550, 20));
        Assert.IsTrue(SlotRules.Overlaps(550, 20, 540, 20));
        Assert.IsFalse(SlotRules.Overlaps(540, 20, 560, 20));
        Assert.IsTrue(SlotRules.Overlaps(540, 60, 560, 20));
    }

    [TestMethod]
    public void OccupiesSlot_IsTrueForEveryStatusExceptCancelledAndDeclined()
    {
        Assert.IsTrue(SlotRules.OccupiesSlot(AppointStatus.Pending));
        Assert.IsTrue(SlotRules.OccupiesSlot(AppointStatus.Approved));
        Assert.IsTrue(SlotRules.OccupiesSlot(AppointStatus.CheckedIn));
        Assert.IsTrue(SlotRules.OccupiesSlot(AppointStatus.Completed));
        Assert.IsTrue(SlotRules.OccupiesSlot(AppointStatus.NoShow));
        Assert.IsFalse(SlotRules.OccupiesSlot(AppointStatus.Cancelled));
        Assert.IsFalse(SlotRules.OccupiesSlot(AppointStatus.Declined));
    }

    [TestMethod]
    public void CanCheckIn_RequiresConfirmedAppointmentOnTheSameDay()
    {
        var today = new DateTime(2026, 11, 22);

        Assert.IsTrue(SlotRules.CanCheckIn(AppointStatus.Approved, today, today));
        Assert.IsFalse(SlotRules.CanCheckIn(AppointStatus.Pending, today, today), "Unconfirmed requests cannot be checked in.");
        Assert.IsFalse(SlotRules.CanCheckIn(AppointStatus.Approved, today.AddDays(1), today), "Arriving before the booked day is rejected.");
        Assert.IsFalse(SlotRules.CanCheckIn(AppointStatus.Cancelled, today, today), "A cancelled slot is not a valid check-in.");
    }

    [TestMethod]
    public void CanMarkNoShow_RequiresAMissedConfirmedDay()
    {
        var today = new DateTime(2026, 11, 22);

        Assert.IsTrue(SlotRules.CanMarkNoShow(AppointStatus.Approved, today.AddDays(-1), today));
        Assert.IsFalse(SlotRules.CanMarkNoShow(AppointStatus.Approved, today, today), "A same-day appointment is not a no-show.");
        Assert.IsFalse(SlotRules.CanMarkNoShow(AppointStatus.Pending, today.AddDays(-1), today));
    }

    [TestMethod]
    public void CanCancel_OnlyAppliesToUnresolvedAppointments()
    {
        Assert.IsTrue(SlotRules.CanCancel(AppointStatus.Pending));
        Assert.IsTrue(SlotRules.CanCancel(AppointStatus.Approved));
        Assert.IsFalse(SlotRules.CanCancel(AppointStatus.Completed));
        Assert.IsFalse(SlotRules.CanCancel(AppointStatus.Cancelled));
    }
}
