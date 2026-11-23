using Hospital.BLL.Common.Rules;
using Hospital.DAL.Entities.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hospital.Tests;

/// <summary>
/// Bed occupancy and admission lifecycle. Two patients can never hold the same bed; these
/// rules are the in-process guard that runs before the filtered unique index fires.
/// </summary>
[TestClass]
public sealed class BedRulesTests
{
    private static Bed BedWith(params DateTime?[] releasedTimestamps)
    {
        var bed = new Bed { Id = 1, Label = "B-01", RoomId = 1, IsActive = true };
        bed.Assignments = [];
        foreach (var released in releasedTimestamps)
        {
            bed.Assignments.Add(new BedAssignment
            {
                Id = bed.Assignments.Count + 1,
                BedId = bed.Id,
                AdmissionId = bed.Assignments.Count + 1,
                AssignedAtUtc = new DateTime(2026, 11, 1),
                ReleasedAtUtc = released
            });
        }

        return bed;
    }

    [TestMethod]
    public void IsOccupied_IsFalseForAFreshBed()
    {
        Assert.IsFalse(BedRules.IsOccupied(BedWith().Assignments));
    }

    [TestMethod]
    public void IsOccupied_IsTrueWhileAnyAssignmentIsUnreleased()
    {
        Assert.IsTrue(BedRules.IsOccupied(BedWith(null).Assignments));
        Assert.IsFalse(BedRules.IsOccupied(BedWith(new DateTime(2026, 11, 5)).Assignments));
        Assert.IsTrue(BedRules.IsOccupied(BedWith(new DateTime(2026, 11, 5), null).Assignments));
    }

    [TestMethod]
    public void OccupiedCount_CountsEachBedOnceEvenWithHistoricalAssignments()
    {
        var beds = new[]
        {
            BedWith(null),
            BedWith(new DateTime(2026, 11, 2)),
            BedWith(new DateTime(2026, 11, 1), new DateTime(2026, 11, 4), null)
        };

        Assert.AreEqual(2, BedRules.OccupiedCount(beds));
    }

    [TestMethod]
    public void OccupancyRate_IsZeroForAnEmptyWard()
    {
        Assert.AreEqual(0m, BedRules.OccupancyRate([]));
    }

    [TestMethod]
    public void OccupancyRate_RoundsToOneDecimalPlace()
    {
        var beds = new[] { BedWith(null), BedWith(null), BedWith(), };
        // 2 of 3 beds occupied.
        Assert.AreEqual(66.7m, BedRules.OccupancyRate(beds));
    }

    [TestMethod]
    public void OccupancyRate_IgnoresInactiveBeds()
    {
        var inactive = BedWith(null);
        inactive.IsActive = false;
        var beds = new[] { BedWith(null), inactive };

        Assert.AreEqual(100m, BedRules.OccupancyRate(beds));
    }

    [TestMethod]
    public void Transitions_PreventDischargingAnAdmissionThatWasNeverAdmitted()
    {
        Assert.IsTrue(BedRules.CanTransition(AdmissionStatus.Requested, AdmissionStatus.Admitted));
        Assert.IsFalse(BedRules.CanTransition(AdmissionStatus.Requested, AdmissionStatus.Discharged));
        Assert.IsTrue(BedRules.CanTransition(AdmissionStatus.Admitted, AdmissionStatus.Discharged));
    }

    [TestMethod]
    public void Transitions_AreTerminalAfterDischargeOrCancellation()
    {
        Assert.IsFalse(BedRules.CanTransition(AdmissionStatus.Discharged, AdmissionStatus.Admitted));
        Assert.IsFalse(BedRules.CanTransition(AdmissionStatus.Cancelled, AdmissionStatus.Admitted));
        Assert.IsFalse(BedRules.CanTransition(AdmissionStatus.Admitted, AdmissionStatus.Requested));
    }

    [TestMethod]
    public void CanAssignBed_OnlyWhenTheAdmissionIsActive()
    {
        Assert.IsTrue(BedRules.CanAssignBed(AdmissionStatus.Admitted));
        Assert.IsFalse(BedRules.CanAssignBed(AdmissionStatus.Requested));
        Assert.IsFalse(BedRules.CanAssignBed(AdmissionStatus.Discharged));
    }
}
