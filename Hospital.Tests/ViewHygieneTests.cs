using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hospital.Tests;

/// <summary>
/// Guards the properties that are easy to regress silently: that no view emits inline script,
/// that no view hard-codes a colour, and that no view renders unencoded HTML outside the one
/// controlled icon catalogue. These are static checks over the Razor sources, so they run
/// without a browser.
/// </summary>
[TestClass]
public sealed class ViewHygieneTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HospitalManagement.sln")))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "Could not locate the repository root from the test output directory.");
        return directory!.FullName;
    }

    private static IEnumerable<(string Path, string Relative, string Content)> Views()
    {
        var root = Path.Combine(RepositoryRoot(), "HospitalManagement", "Views");
        foreach (var path in Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories))
        {
            yield return (path, Path.GetRelativePath(root, path), File.ReadAllText(path));
        }
    }

    [TestMethod]
    public void NoView_EmitsInlineScript()
    {
        var offenders = Views()
            .Where(v => Regex.IsMatch(v.Content, @"<script(?![^>]*\bsrc=)[^>]*>", RegexOptions.IgnoreCase))
            .Select(v => v.Relative)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Inline <script> blocks break the Content-Security-Policy. Move the code into wwwroot/js. Offenders: "
            + string.Join(", ", offenders));
    }

    [TestMethod]
    public void NoView_ContainsInlineEventHandlers()
    {
        var offenders = Views()
            .Where(v => Regex.IsMatch(v.Content, @"\son(click|change|submit|load|input)\s*=", RegexOptions.IgnoreCase))
            .Select(v => v.Relative)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Inline event handlers are blocked by the CSP and untestable. Offenders: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void NoView_HardCodesAColour()
    {
        var offenders = Views()
            .Where(v => Regex.IsMatch(v.Content, @"#[0-9a-fA-F]{6}\b"))
            .Select(v => v.Relative)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Colours must come from design-system tokens. Offenders: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void RawHtml_IsOnlyUsedForTheIconCatalogue()
    {
        var offenders = Views()
            .Where(v => v.Content.Contains("Html.Raw", StringComparison.Ordinal))
            .Where(v => !v.Relative.Replace('\\', '/').EndsWith("Shared/_Icon.cshtml", StringComparison.Ordinal))
            .Select(v => v.Relative)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Html.Raw bypasses output encoding and must be justified per use. Offenders: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void EveryView_DeclaresAtMostOneScriptsSection()
    {
        var offenders = Views()
            .Where(v => Regex.Matches(v.Content, @"@section\s+Scripts\b").Count > 1)
            .Select(v => v.Relative)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Razor renders only one Scripts section; a second is silently dropped. Offenders: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void NoView_RendersTheRetiredLegacyNavigationPartial()
    {
        var offenders = Views()
            .Where(v => v.Content.Contains("_MainNavigation", StringComparison.Ordinal)
                        || v.Content.Contains("_Layout", StringComparison.Ordinal))
            .Select(v => v.Relative)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "The legacy layout and navigation partials were removed in favour of _AppShell. Offenders: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// A patient's own data must never be assembled by matching a display name. Two patients can
    /// share a surname, so a name match discloses one patient's record on another's chart. This is
    /// a regression guard for defect 16: the chart used to select invoices by surname substring.
    /// </summary>
    [TestMethod]
    public void NoView_ScopesPatientDataByDisplayName()
    {
        var offenders = Views()
            .Where(v => Regex.IsMatch(
                v.Content,
                @"\.(LastName|FirstName|PatientName)\s*\.\s*Contains|\.PatientName\s*==",
                RegexOptions.IgnoreCase))
            .Select(v => v.Relative)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Patient-scoped data must be filtered by identifier, never by a display name. " +
            "Offenders: " + string.Join(", ", offenders));
    }
}
