using Hospital.BLL.Common;
using Hospital.BLL.Common.Rules;
using Hospital.BLL.Helpers;
using Hospital.BLL.Permissions;
using Hospital.DAL.Entities.Clinical;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hospital.Tests;

/// <summary>
/// Encounter lifecycle, permission boundaries and query normalisation. The permission tests
/// are the regression guard against privilege creep: they assert that non-clinical roles
/// cannot sign clinical records and that patient-facing roles cannot read the audit trail.
/// </summary>
[TestClass]
public sealed class EncounterAndPermissionTests
{
    [TestMethod]
    public void EncounterTransitions_RequireCompletionBeforeSigning()
    {
        Assert.IsTrue(EncounterRules.CanTransition(EncounterStatus.InConsultation, EncounterStatus.Completed));
        Assert.IsTrue(EncounterRules.CanTransition(EncounterStatus.Completed, EncounterStatus.Signed));
        Assert.IsFalse(EncounterRules.CanTransition(EncounterStatus.InConsultation, EncounterStatus.Signed),
            "An encounter cannot be signed before the consultation is completed.");
        Assert.IsFalse(EncounterRules.CanTransition(EncounterStatus.Registered, EncounterStatus.Signed));
    }

    [TestMethod]
    public void EncounterTransitions_AreTerminalAfterSigning()
    {
        Assert.IsFalse(EncounterRules.CanTransition(EncounterStatus.Signed, EncounterStatus.Completed));
        Assert.IsFalse(EncounterRules.CanTransition(EncounterStatus.Signed, EncounterStatus.Cancelled));
        Assert.IsFalse(EncounterRules.IsEditable(EncounterStatus.Signed),
            "Signed records are legally closed and must be amended, not edited.");
    }

    [TestMethod]
    public void IsEditable_OnlyBeforeCompletion()
    {
        Assert.IsTrue(EncounterRules.IsEditable(EncounterStatus.Registered));
        Assert.IsTrue(EncounterRules.IsEditable(EncounterStatus.Triaged));
        Assert.IsTrue(EncounterRules.IsEditable(EncounterStatus.InConsultation));
        Assert.IsFalse(EncounterRules.IsEditable(EncounterStatus.Completed));
        Assert.IsFalse(EncounterRules.IsEditable(EncounterStatus.Cancelled));
    }

    [TestMethod]
    public void CanSign_RequiresADiagnosisAndASignedNotePresent()
    {
        Assert.IsTrue(EncounterRules.CanSign(EncounterStatus.Completed, hasDiagnosis: true, hasNote: true));
        Assert.IsFalse(EncounterRules.CanSign(EncounterStatus.Completed, hasDiagnosis: false, hasNote: true));
        Assert.IsFalse(EncounterRules.CanSign(EncounterStatus.Completed, hasDiagnosis: true, hasNote: false));
        Assert.IsFalse(EncounterRules.CanSign(EncounterStatus.InConsultation, true, true));
    }

    [TestMethod]
    public void CanEditNote_IsFalseOnceA_NoteIsSigned()
    {
        Assert.IsTrue(EncounterRules.CanEditNote(new ClinicalNote { Status = NoteStatus.Draft }));
        Assert.IsFalse(EncounterRules.CanEditNote(new ClinicalNote { Status = NoteStatus.Signed }));
    }

    [TestMethod]
    public void ReceptionCannot_SignOrEnterResults()
    {
        var permissions = RolePermissions.For(Role.Receptionist);

        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.EncounterSign);
        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.LabEnterResult);
        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.LabRelease);
        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.BillingManage);
    }

    [TestMethod]
    public void PharmacistCannot_PrescribeOrReleaseLaboratoryResults()
    {
        var permissions = RolePermissions.For(Role.Pharmacist);

        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.PrescriptionCreate);
        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.LabRelease);
        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.EncounterSign);
    }

    [TestMethod]
    public void LaboratoryTechnicianCanReleaseButCannotPrescribe()
    {
        var permissions = RolePermissions.For(Role.LabTechnician);

        CollectionAssert.Contains(permissions.ToList(), Permission.LabRelease);
        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.PrescriptionCreate);
        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.BillingManage);
    }

    [TestMethod]
    public void AuditorCanReadTheTrailButCannotWriteClinicalData()
    {
        var permissions = RolePermissions.For(Role.Auditor);

        CollectionAssert.Contains(permissions.ToList(), Permission.AuditView);
        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.EncounterDocument);
        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.PatientEdit);
        CollectionAssert.DoesNotContain(permissions.ToList(), Permission.BillingManage);
    }

    [TestMethod]
    public void PatientRole_HasNoStaffPermissions()
    {
        Assert.AreEqual(0, RolePermissions.For(Role.Patient).Count);
    }

    [TestMethod]
    public void EveryRolePermission_IsInTheCatalogue()
    {
        var catalogue = Permission.All.ToHashSet();
        foreach (var role in Enum.GetValues<Role>())
        {
            foreach (var permission in RolePermissions.For(role))
            {
                Assert.IsTrue(catalogue.Contains(permission), $"{role} grants unknown permission '{permission}'.");
            }
        }
    }

    [TestMethod]
    public void ListQuery_NormalisesPagingAndTrimsTheSearchTerm()
    {
        var query = new ListQuery { Page = 0, PageSize = 5, Term = "  nadia  " }.Normalised();

        Assert.AreEqual(1, query.Page);
        Assert.AreEqual(20, query.PageSize, "Out-of-range page sizes fall back to the default.");
        Assert.AreEqual("nadia", query.Term);
    }

    [TestMethod]
    public void ListQuery_KeepsExplicitBlankTermAsNull()
    {
        Assert.IsNull(new ListQuery { Term = "   " }.Normalised().Term);
    }

    [TestMethod]
    public void ListQuery_RouteValues_PreserveActiveFiltersForPaging()
    {
        var values = new ListQuery
        {
            Term = "heba",
            Status = "Released",
            From = new DateOnly(2026, 11, 1),
            To = new DateOnly(2026, 11, 22),
            PageSize = 50
        }.ToRouteValues();

        Assert.AreEqual("heba", values["term"]);
        Assert.AreEqual("Released", values["status"]);
        Assert.AreEqual("2026-11-01", values["from"]);
        Assert.AreEqual("2026-11-22", values["to"]);
        Assert.AreEqual("50", values["pageSize"]);
    }
}
