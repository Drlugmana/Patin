using TurneroTcs.Models;
using TurneroTcs.Records;
using TurneroTcs.Services;

namespace TurneroTcs.Services.Interfaces;

public interface IPlanificacionService
{
    Task<IReadOnlyList<Planificacion>> GetByGrupoIdAsync(string grupoId);
    Task<IReadOnlyList<Planificacion>> GetByEquipoIdAsync(string equipoId);
    Task<IReadOnlyList<PlanificacionApoyoGrupo>> GetApoyosByGrupoIdAsync(string grupoId);
    Task<IReadOnlyList<PlanificacionTurnoOpcionalVacacionGrupo>> GetTurnosOpcionalesVacacionByGrupoIdAsync(string grupoId);
    Task<IReadOnlyList<PlanificacionAuxiliarEquipo>> GetAuxiliaresByEquipoIdAsync(string equipoId);
    Task<Result> SavePlanificacionAsync(
        IEnumerable<PlanificacionSaveRequest> requests,
        IEnumerable<PlanificacionAuxiliarSaveRequest> auxiliares,
        IEnumerable<PlanificacionApoyoSaveRequest> apoyos,
        IEnumerable<PlanificacionTurnoOpcionalVacacionSaveRequest> turnosOpcionalesVacacion,
        string grupoId,
        string equipoId,
        int? maximoSlotsFinSemanaPorMes,
        int? maximoTurnosNocturnosPorMes,
        int? maximoTurnosNocturnosPorSemana,
        bool usaSoloSecundarios = false,
        string? grupoFuenteSecundariosId = null,
        bool usarPersonaUnicaPorSemana = false);
    Task<IReadOnlyList<TipoTurno>> GetTipoTurnosAsync();
    Task<EquipoPlanificacionConfigResult> GetEquipoPlanificacionConfigAsync(string equipoId);
    Task<Result> SaveEquipoPlanificacionConfigAsync(string equipoId, int? maximoSlotsFinSemanaPorMes, int? maximoTurnosNocturnosPorMes, int? maximoTurnosNocturnosPorSemana);
    Task<int> GetMaximoTurnosNocturnosPorMesAsync(string equipoId);
    Task<Result> ValidarGeneracionRotacionPreviewAsync(List<string> gruposIds);
    //Task<Result> GenerarTurnosRotacionGeneradorAsync(List<string> gruposIds, int numeroSemanas, string tipoGeneracion);
    Task<Result<List<TurnoGeneradoPreview>>> GenerarTurnosRotacionGeneradorPreviewAsync(
        List<string> gruposIds,
        int numeroSemanas,
        DateTime fechaInicio,
        Action<GenerationProgressUpdate>? reportProgress = null,
        bool autorizarSobrecupoSemanalFeriado = false,
        string nivelUsoDescanso7Horas = "low",
        string nivelEvitarFinesSemanaConsecutivos = "low",
        bool balancearHorasSemanales = true);
    //Task<Result> GuardarTurnosGeneradosAsync(List<TurnoGeneradoPreview> turnos);
}
