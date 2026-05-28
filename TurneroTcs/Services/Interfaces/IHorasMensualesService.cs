using TurneroTcs.Records;
using TurneroTcs.ViewModels;

namespace TurneroTcs.Services.Interfaces;

public interface IHorasMensualesService
{
    Task<HorasMensualesViewModel> GetHorasMensualesAsync(
        HorasMensualesReportRequest request,
        HorasMensualesActorContext actor);


    Task<HorasMensualesExcelDocument> ExportExcelAsync(
        HorasMensualesReportRequest request,
        HorasMensualesActorContext actor);
}
