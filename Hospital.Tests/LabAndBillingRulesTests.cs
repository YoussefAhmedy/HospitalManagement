using Hospital.BLL.Common.Rules;
using Hospital.DAL.Entities.Billing;
using Hospital.DAL.Entities.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hospital.Tests;

/// <summary>
/// Laboratory release gate and billing ledger arithmetic. The laboratory rules are the
/// guarantee that an unverified result can never be presented as final; the billing rules
/// keep the ledger from going negative or double-refunding.
/// </summary>
[TestClass]
public sealed class LabAndBillingRulesTests
{
    [TestMethod]
    public void LabTransitions_FollowTheSpecimenWorkflowInOrder()
    {
        Assert.IsTrue(LabRules.CanTransition(LabOrderStatus.AwaitingCollection, LabOrderStatus.Collected));
        Assert.IsTrue(LabRules.CanTransition(LabOrderStatus.Collected, LabOrderStatus.Received));
        Assert.IsTrue(LabRules.CanTransition(LabOrderStatus.Received, LabOrderStatus.Processing));
        Assert.IsTrue(LabRules.CanTransition(LabOrderStatus.Processing, LabOrderStatus.AwaitingVerification));
        Assert.IsTrue(LabRules.CanTransition(LabOrderStatus.AwaitingVerification, LabOrderStatus.Released));
    }

    [TestMethod]
    public void LabTransitions_CannotSkipVerification()
    {
        Assert.IsFalse(LabRules.CanTransition(LabOrderStatus.Processing, LabOrderStatus.Released),
            "A result must pass through verification before release.");
        Assert.IsFalse(LabRules.CanTransition(LabOrderStatus.Collected, LabOrderStatus.Processing),
            "A specimen must be received before analysis.");
        Assert.IsFalse(LabRules.CanTransition(LabOrderStatus.Requested, LabOrderStatus.Released));
    }

    [TestMethod]
    public void LabTransitions_AreTerminalAfterReleaseOrCancellation()
    {
        Assert.IsFalse(LabRules.CanTransition(LabOrderStatus.Released, LabOrderStatus.Cancelled));
        Assert.IsFalse(LabRules.CanTransition(LabOrderStatus.Released, LabOrderStatus.Processing));
        Assert.IsFalse(LabRules.CanTransition(LabOrderStatus.Cancelled, LabOrderStatus.Collected));
    }

    [TestMethod]
    public void IsClinicallyFinal_IsTrueOnlyForReleasedResults()
    {
        foreach (var status in Enum.GetValues<LabOrderStatus>())
        {
            Assert.AreEqual(status == LabOrderStatus.Released, LabRules.IsClinicallyFinal(status), status.ToString());
        }
    }

    [TestMethod]
    public void Classify_UsesTheReferenceRangeOnly()
    {
        Assert.AreEqual(ResultFlag.Normal, LabRules.Classify(5m, 4m, 6m, 2m, 10m));
        Assert.AreEqual(ResultFlag.Low, LabRules.Classify(3m, 4m, 6m, 2m, 10m));
        Assert.AreEqual(ResultFlag.High, LabRules.Classify(7m, 4m, 6m, 2m, 10m));
        Assert.AreEqual(ResultFlag.Critical, LabRules.Classify(1m, 4m, 6m, 2m, 10m));
        Assert.AreEqual(ResultFlag.Critical, LabRules.Classify(12m, 4m, 6m, 2m, 10m));
    }

    [TestMethod]
    public void Classify_ReturnsNormalWhenNoValueOrNoRangeIsConfigured()
    {
        Assert.AreEqual(ResultFlag.Normal, LabRules.Classify(null, 4m, 6m, 2m, 10m));
        Assert.AreEqual(ResultFlag.Normal, LabRules.Classify(500m, null, null, null, null));
    }

    [TestMethod]
    public void Balance_IsTotalMinusCoverageAndPayment()
    {
        Assert.AreEqual(120m, BillingRules.Balance(500m, 300m, 80m));
        Assert.AreEqual(0m, BillingRules.Balance(100m, 80m, 20m));
    }

    [TestMethod]
    public void IsPaymentValid_RejectsOverpaymentAndNonPositiveAmounts()
    {
        Assert.IsTrue(BillingRules.IsPaymentValid(100m, 100m));
        Assert.IsFalse(BillingRules.IsPaymentValid(100.01m, 100m));
        Assert.IsFalse(BillingRules.IsPaymentValid(0m, 100m));
        Assert.IsFalse(BillingRules.IsPaymentValid(-5m, 100m));
    }

    [TestMethod]
    public void RefundRequiresApproval_UsesTheInclusiveThreshold()
    {
        Assert.IsTrue(BillingRules.RefundRequiresApproval(100m));
        Assert.IsTrue(BillingRules.RefundRequiresApproval(100.01m));
        Assert.IsFalse(BillingRules.RefundRequiresApproval(99.99m));
    }

    [TestMethod]
    public void InsuranceCoverage_ClampsToPolicyLimitsAndTotal()
    {
        Assert.AreEqual(80m, BillingRules.InsuranceCoverage(100m, 80));
        Assert.AreEqual(100m, BillingRules.InsuranceCoverage(100m, 150), "Coverage cannot exceed the invoice total.");
        Assert.AreEqual(0m, BillingRules.InsuranceCoverage(100m, -10), "Negative coverage is not a discount.");
    }

    [TestMethod]
    public void DeriveStatus_ReflectsTheLiveLedger()
    {
        Assert.AreEqual(InvoiceStatus.Void, BillingRules.DeriveStatus(100m, 0m, 0m, true));
        Assert.AreEqual(InvoiceStatus.Issued, BillingRules.DeriveStatus(100m, 0m, 0m, false));
        Assert.AreEqual(InvoiceStatus.PartiallyPaid, BillingRules.DeriveStatus(100m, 80m, 0m, false));
        Assert.AreEqual(InvoiceStatus.Paid, BillingRules.DeriveStatus(100m, 80m, 20m, false));
    }
}
