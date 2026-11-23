namespace Hospital.BLL.Common;

/// <summary>
/// The interface-language catalogue. English is the invariant default; Arabic is a
/// first-class locale with right-to-left presentation. Strings live in one table so a
/// missing translation is a visible test failure rather than a silent English leak.
/// </summary>
public static class UiText
{
    public const string English = "en";
    public const string Arabic = "ar";

    public static bool IsRightToLeft(string? culture) =>
        culture?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) == true;

    private static readonly Dictionary<string, (string En, string Ar)> Catalogue = new()
    {
        // Navigation and shell
        ["nav.overview"] = ("Overview", "نظرة عامة"),
        ["nav.encounters"] = ("Encounters", "الزيارات"),
        ["nav.appointments"] = ("Appointments", "المواعيد"),
        ["nav.patients"] = ("Patient directory", "سجل المرضى"),
        ["nav.laboratory"] = ("Laboratory", "المختبر"),
        ["nav.radiology"] = ("Radiology", "الأشعة"),
        ["nav.pharmacy"] = ("Pharmacy", "الصيدلية"),
        ["nav.admissions"] = ("Admissions & beds", "الدخول والأسرّة"),
        ["nav.billing"] = ("Billing", "الفوترة"),
        ["nav.reports"] = ("Reports", "التقارير"),
        ["nav.audit"] = ("Audit trail", "سجل التدقيق"),
        ["nav.administration"] = ("Administration", "الإدارة"),
        ["shell.search"] = ("Search patients by name, MRN or phone", "ابحث عن مريض بالاسم أو الرقم الطبي أو الهاتف"),
        ["shell.signout"] = ("Sign out", "تسجيل الخروج"),
        ["shell.account"] = ("Account settings", "إعدادات الحساب"),
        ["shell.language"] = ("Language", "اللغة"),
        ["shell.skip"] = ("Skip to main content", "تخطَّ إلى المحتوى الرئيسي"),
        ["group.workspace"] = ("Workspace", "مساحة العمل"),
        ["group.clinical"] = ("Clinical", "الرعاية السريرية"),
        ["group.admin"] = ("Administration", "الإدارة"),

        // States
        ["state.empty.title"] = ("Nothing to show yet", "لا توجد بيانات بعد"),
        ["state.loading"] = ("Loading", "جارٍ التحميل"),
        ["state.error"] = ("Something went wrong", "حدث خطأ ما"),
        ["state.forbidden"] = ("You do not have permission for this action", "لا تملك صلاحية لهذا الإجراء"),

        // Clinical safety
        ["safety.unverified"] = ("Not yet verified", "غير مُعتمد بعد"),
        ["safety.final"] = ("Final", "نهائي"),
        ["safety.demo"] = ("Synthetic demonstration data", "بيانات تجريبية اصطناعية"),
        ["safety.allergy"] = ("Allergy alert", "تنبيه حساسية"),

        // Common actions
        ["action.apply"] = ("Apply", "تطبيق"),
        ["action.reset"] = ("Reset", "إعادة ضبط"),
        ["action.save"] = ("Save", "حفظ"),
        ["action.cancel"] = ("Cancel", "إلغاء"),
        ["action.open"] = ("Open", "فتح"),
        ["action.print"] = ("Print", "طباعة"),
        ["action.export"] = ("Export", "تصدير"),
        ["action.today"] = ("Today", "اليوم")
    };

    public static string For(string key, string? culture)
    {
        if (!Catalogue.TryGetValue(key, out var entry))
        {
            return key;
        }

        return IsRightToLeft(culture) ? entry.Ar : entry.En;
    }

    public static IReadOnlyCollection<string> Keys => Catalogue.Keys;
}
