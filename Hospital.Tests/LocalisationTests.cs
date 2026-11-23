using System.Globalization;
using Hospital.BLL.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hospital.Tests;

/// <summary>
/// Interface language coverage. Arabic is a supported locale with right-to-left
/// presentation, so an untranslated key is a defect rather than a cosmetic gap.
/// </summary>
[TestClass]
public sealed class LocalisationTests
{
    [TestMethod]
    public void EveryKey_IsTranslatedIntoArabic()
    {
        foreach (var key in UiText.Keys)
        {
            var english = UiText.For(key, "en");
            var arabic = UiText.For(key, "ar");

            Assert.AreNotEqual(key, english, $"'{key}' is missing an English value.");
            Assert.AreNotEqual(key, arabic, $"'{key}' is missing an Arabic value.");
            Assert.AreNotEqual(english, arabic, $"'{key}' is not actually translated.");
            Assert.IsTrue(arabic.Any(c => c >= 0x0600 && c <= 0x06FF), $"'{key}' does not contain Arabic script.");
        }
    }

    [TestMethod]
    public void UnknownKeys_FallBackToTheKeyItself()
    {
        Assert.AreEqual("missing.key", UiText.For("missing.key", "ar"));
    }

    [TestMethod]
    public void ArabicCulture_IsRightToLeft()
    {
        Assert.IsTrue(UiText.IsRightToLeft("ar"), "Generic Arabic must be RTL.");
        Assert.IsTrue(UiText.IsRightToLeft("ar-EG"), "Regional Arabic must be RTL.");
        Assert.IsFalse(UiText.IsRightToLeft("en"), "English must be LTR.");
        Assert.IsFalse(UiText.IsRightToLeft("en-GB"));
        Assert.IsFalse(UiText.IsRightToLeft(null));
    }

    [TestMethod]
    public void ArabicRegionCulture_ResolvesArabicStrings()
    {
        Assert.AreEqual("تسجيل الخروج", UiText.For("shell.signout", "ar-EG"));
        Assert.AreEqual("Sign out", UiText.For("shell.signout", "en-US"));
    }
}
