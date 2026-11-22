using Hospital.BLL.Services.Abstraction;

namespace HospitalManagement.Services;

/// <summary>
/// Kept for compatibility with existing constructors; the contract lives in the business
/// layer so every new workspace depends on the same abstraction.
/// </summary>
public interface IAuditLogger : Hospital.BLL.Services.Abstraction.IAuditLogger
{
}
