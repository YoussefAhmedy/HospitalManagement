using System.Text.RegularExpressions;
using Hospital.BLL.Permissions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hospital.Tests;

/// <summary>
/// Static authorisation audit of every controller action. This is the "authz matrix" guard that
/// cannot be a request test in this environment (no host, no database), but is strong enough to
/// catch the failure modes that actually matter:
/// <list type="bullet">
///   <item>a new action added to a controller with no authorisation at all,</item>
///   <item>a method marked public that nobody intended to be public,</item>
///   <item>a controller that carries both [AllowAnonymous] and [Authorize] — ASP.NET Core makes
///         [AllowAnonymous] win, silently disabling the controller's entire policy,</item>
///   <item>an [Authorize] attribute that names a policy by a literal string rather than the
///         <see cref="Permission"/> constants, so a typo silently becomes an always-failing
///         (or, worse, always-passing) policy.</item>
/// </list>
/// </summary>
[TestClass]
public sealed class AuthorizationCoverageTests
{
    /// <summary>
    /// Every action that is deliberately reachable without signing in. Adding a public action
    /// requires editing this list, which forces a reviewer to see the change.
    /// </summary>
    private static readonly HashSet<string> DeliberatelyPublic = new(StringComparer.Ordinal)
    {
        // Public marketing and legal pages. Home has no [Authorize] anywhere; that is the design.
        "HomeController.Index",
        "HomeController.AboutUs",
        "HomeController.Privacy",
        "HomeController.Terms",
        "HomeController.Security",
        "HomeController.Support",
        "HomeController.Error",
        "HomeController.Demo",

        // Reached when authorisation fails, so it cannot itself require authorisation.
        "AccountController.AccessDenied",

        // Authentication endpoints. These must be anonymous by definition.
        "AccountController.Register",
        "AccountController.Registered",
        "AccountController.ConfirmEmail",
        "AccountController.ResendConfirmation",
        "AccountController.LogIn",
        "AccountController.GoogleAuth",
        "AccountController.ExternalAuth",
        "AccountController.ExternalLoginCallback",
        "AccountController.ForgetPassword",
        "AccountController.ResendPasswordReset",
        "AccountController.ResetConfirmation",
        "AccountController.ResetPassword"
    };

    private sealed record ActionRecord(
        string ControllerName,
        string File,
        string Name,
        bool OwnAuthorize,
        bool OwnAllowAnonymous,
        string OwnAuthorizeArgument,
        bool ClassAuthorize,
        bool ClassAllowAnonymous,
        string ClassAuthorizeArgument,
        int Line);

    private static readonly Regex ActionDeclaration =
        new(@"public\s+(?:async\s+)?(?:Task<)?IActionResult>?\s+(\w+)\s*\(", RegexOptions.Compiled);

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

    private static IReadOnlyList<ActionRecord> Actions()
    {
        var root = Path.Combine(RepositoryRoot(), "HospitalManagement", "Controllers");
        var results = new List<ActionRecord>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs"))
        {
            var fileName = Path.GetFileName(file);
            var controller = fileName[..^3];
            var lines = File.ReadAllLines(file);

            var firstActionLine = Array.FindIndex(lines, l => ActionDeclaration.IsMatch(l));
            if (firstActionLine < 0)
            {
                continue;
            }

            // The class level is everything before the first action: its attributes sit there.
            var classAttributes = string.Join("\n", lines.Take(firstActionLine));
            var classAuthorize = Regex.Match(classAttributes, @"\[Authorize(?:\((.*?)\))?\]", RegexOptions.Singleline);
            var classAllowAnonymous = classAttributes.Contains("[AllowAnonymous]");

            for (var i = 0; i < lines.Length; i++)
            {
                var match = ActionDeclaration.Match(lines[i]);
                if (!match.Success)
                {
                    continue;
                }

                var attributes = AttributesAbove(lines, i);
                var ownAuthorize = Regex.Match(attributes, @"\[Authorize(?:\((.*?)\))?\]", RegexOptions.Singleline);

                results.Add(new ActionRecord(
                    controller,
                    fileName,
                    match.Groups[1].Value,
                    ownAuthorize.Success,
                    attributes.Contains("[AllowAnonymous]"),
                    ownAuthorize.Success ? ownAuthorize.Groups[1].Value : string.Empty,
                    classAuthorize.Success,
                    classAllowAnonymous,
                    classAuthorize.Success ? classAuthorize.Groups[1].Value : string.Empty,
                    i + 1));
            }
        }

        return results;
    }

    /// <summary>Collects the attribute block immediately above a declaration, skipping blanks and comments.</summary>
    private static string AttributesAbove(string[] lines, int index)
    {
        var collected = new List<string>();
        for (var j = index - 1; j >= 0; j--)
        {
            var trimmed = lines[j].Trim();
            if (trimmed.StartsWith('['))
            {
                collected.Add(trimmed);
            }
            else if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }
            else
            {
                break;
            }
        }

        return string.Join(" ", collected);
    }

    private static string Key(ActionRecord record) => $"{record.ControllerName}.{record.Name}";

    [TestMethod]
    public void EveryAction_IsAuthorisedOrExplicitlyPublic()
    {
        var unprotected = Actions()
            .Where(a => !a.OwnAuthorize && !a.OwnAllowAnonymous && !a.ClassAuthorize && !a.ClassAllowAnonymous)
            .Where(a => !DeliberatelyPublic.Contains(Key(a)))
            .Select(a => $"{a.File}:{a.Line} {Key(a)}")
            .ToList();

        Assert.AreEqual(0, unprotected.Count,
            "These actions have no authorisation at all and are not on the deliberate-public list: " +
            string.Join(", ", unprotected));
    }

    [TestMethod]
    public void NoController_MixesAllowAnonymousWithAuthorize()
    {
        var offenders = Actions()
            .Where(a => a.ClassAllowAnonymous && a.ClassAuthorize)
            .Select(a => a.File)
            .Distinct()
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "ASP.NET Core ignores [Authorize] on a controller that also carries [AllowAnonymous], " +
            "which silently disables the whole controller's policy. Offenders: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void NoAction_IsBothAnonymousAndAuthorised()
    {
        var offenders = Actions()
            .Where(a => a.OwnAllowAnonymous && a.OwnAuthorize)
            .Select(a => $"{a.File}:{a.Line} {Key(a)}")
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "An action marked both [AllowAnonymous] and [Authorize] is ambiguous; the anonymous marker " +
            "wins, which is almost never intended. Offenders: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void EveryPublicAction_IsOnTheDeliberateList()
    {
        var undeclared = Actions()
            .Where(a => a.OwnAllowAnonymous || (!a.OwnAuthorize && !a.ClassAuthorize))
            .Where(a => !DeliberatelyPublic.Contains(Key(a)))
            .Select(a => $"{a.File}:{a.Line} {Key(a)}")
            .ToList();

        Assert.AreEqual(0, undeclared.Count,
            "These actions are reachable without authentication but are not declared in " +
            "DeliberatelyPublic, so nobody has reviewed them as public: " + string.Join(", ", undeclared));
    }

    [TestMethod]
    public void EveryAuthorizeAttribute_UsesPermissionConstants()
    {
        var literals = Actions()
            .SelectMany(a => new[] { a.OwnAuthorizeArgument, a.ClassAuthorizeArgument })
            .Where(argument => argument.Contains("Policy", StringComparison.Ordinal))
            .Where(argument => !argument.Contains("Permission.", StringComparison.Ordinal))
            .ToList();

        Assert.AreEqual(0, literals.Count,
            "Every policy must be built from Permission constants so a rename cannot silently break " +
            "authorisation. Offending arguments: " + string.Join(" | ", literals));
    }

    /// <summary>
    /// Permissions that are deliberately defined but not yet enforced. Each one is named in the
    /// documentation as a reserved capability, so listing it here is the machine-checkable form of
    /// that promise: if one of them starts being enforced, this list must shrink; if a fifth one
    /// appears, the test fails.
    /// </summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        nameof(Permission.PatientDelete),
        nameof(Permission.PatientViewSensitive),
        nameof(Permission.InsuranceManage),
        nameof(Permission.SettingsManage)
    };

    [TestMethod]
    public void PermissionConstants_AreReferencedUnlessExplicitlyReserved()
    {
        // A defined permission that no attribute ever names is dead weight: it looks like a control
        // but enforces nothing. Reporting them keeps the grant matrix honest.
        var controllers = string.Join(
            "\n",
            Directory.EnumerateFiles(
                    Path.Combine(RepositoryRoot(), "HospitalManagement", "Controllers"),
                    "*.cs")
                .Select(File.ReadAllText)
                // Views check permissions too, so include them: a claim check is enforcement as well.
                .Concat(Directory.EnumerateFiles(
                        Path.Combine(RepositoryRoot(), "HospitalManagement", "Views"),
                        "*.cshtml",
                        SearchOption.AllDirectories)
                    .Select(File.ReadAllText)));

        var constants = typeof(Permission)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (Name: f.Name, Value: (string)f.GetRawConstantValue()!))
            .ToList();

        Assert.IsTrue(constants.Count > 0, "No permission constants were found via reflection.");

        var unreferenced = constants
            .Where(c => !Reserved.Contains(c.Name))
            .Where(c => !controllers.Contains("Permission." + c.Name, StringComparison.Ordinal))
            .Select(c => c.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, unreferenced.Count,
            "These permissions are defined but never referenced, so holding them grants nothing: " +
            string.Join(", ", unreferenced));
    }

    [TestMethod]
    public void ReservedPermissions_AreStillUnenforced()
    {
        // The companion check. If a reserved permission becomes enforced, this fails so that the
        // documentation, the grant matrix and this list are updated together rather than drifting.
        var controllers = string.Join(
            "\n",
            Directory.EnumerateFiles(
                    Path.Combine(RepositoryRoot(), "HospitalManagement"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        var nowEnforced = Reserved
            .Where(name => controllers.Contains("Permission." + name, StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, nowEnforced.Count,
            "These permissions were reserved but are now enforced somewhere; remove them from Reserved " +
            "and update the documentation: " + string.Join(", ", nowEnforced));
    }

    [TestMethod]
    public void PermissionCatalogue_ContainsExactlyTheDocumentedCount()
    {
        // The documentation states a number in several places. Pin it so the prose and the code
        // cannot disagree silently.
        var count = typeof(Permission)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Count(f => f.IsLiteral && f.FieldType == typeof(string));

        Assert.AreEqual(43, count,
            "The permission catalogue changed. Update this number and every document that states it.");
    }

    [TestMethod]
    public void PermissionCatalogue_MatchesDeclaredConstants()
    {
        // Every super-admin grant flows from Permission.All, so a constant missing from that list
        // becomes a permission that even the highest role does not hold.
        var constants = typeof(Permission)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        var missingFromCatalogue = constants.Except(Permission.All).OrderBy(v => v, StringComparer.Ordinal).ToList();
        var strayInCatalogue = Permission.All.Except(constants).OrderBy(v => v, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, missingFromCatalogue.Count,
            "Defined permissions missing from Permission.All, so no role can hold them: " +
            string.Join(", ", missingFromCatalogue));
        Assert.AreEqual(0, strayInCatalogue.Count,
            "Permission.All contains values with no constant behind them: " + string.Join(", ", strayInCatalogue));
        Assert.AreEqual(Permission.All.Count, Permission.All.Distinct(StringComparer.Ordinal).Count(),
            "Permission.All contains duplicates.");
    }

    [TestMethod]
    public void PermissionConstants_HaveNoDuplicateValues()
    {
        var duplicates = typeof(Permission)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .GroupBy(v => v, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.AreEqual(0, duplicates.Count,
            "Duplicate permission values collapse two distinct grants into one. Duplicates: " +
            string.Join(", ", duplicates));
    }
}
