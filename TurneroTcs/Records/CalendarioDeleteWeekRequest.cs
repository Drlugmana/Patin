namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para eliminar todos los registros de turnos correspondientes a una semana
/// específica del calendario de un equipo.
/// </summary>
/// <param name="EquipoId">
/// Identificador del equipo cuya semana se elimina.
/// <see langword="null"/> si la operación aplica a todos los equipos.
/// </param>
/// <param name="WeekStart">Fecha del primer día (lunes) de la semana que se desea eliminar.</param>
public sealed record CalendarioDeleteWeekRequest(string? EquipoId, DateOnly WeekStart);
