namespace Hospital.BLL.ModelVM;

public sealed class MainVm
{
    public IReadOnlyList<PublicDoctorVm> Doctors { get; init; } = [];
}
