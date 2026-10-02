using Hospital.BLL.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hospital.Tests;

[TestClass]
public sealed class ResourceOwnershipTests
{
    [TestMethod]
    public void IsOwner_AllowsOnlyExactAuthenticatedOwnerId()
    {
        Assert.IsTrue(ResourceOwnership.IsOwner("patient-a", "patient-a"));
        Assert.IsFalse(ResourceOwnership.IsOwner("patient-a", "patient-b"));
        Assert.IsFalse(ResourceOwnership.IsOwner(null, "patient-a"));
        Assert.IsFalse(ResourceOwnership.IsOwner("patient-a", null));
        Assert.IsFalse(ResourceOwnership.IsOwner(" ", "patient-a"));
    }
}
