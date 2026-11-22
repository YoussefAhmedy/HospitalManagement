namespace Hospital.BLL.Common;

/// <summary>Document number prefixes. Kept together so identifiers stay recognisable.</summary>
public static class ClinicalNumber
{
    public const string Patient = "PT";
    public const string Encounter = "ENC";
    public const string Appointment = "APT";
    public const string LabOrder = "LAB";
    public const string Specimen = "SPC";
    public const string Imaging = "IMG";
    public const string Prescription = "RX";
    public const string Admission = "ADM";
    public const string Invoice = "INV";
    public const string Receipt = "RCP";

    public static string Format(string prefix, long value) =>
        $"{prefix}-{value:D6}";
}
