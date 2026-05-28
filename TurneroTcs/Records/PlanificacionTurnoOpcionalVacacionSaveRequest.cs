namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para guardar la configuración de un turno opcional de vacaciones en un día específico.
/// Define qué tipo de turno está disponible como opción de vacación para el día indicado.
/// </summary>
/// <param name="Dia">Día de la semana al que aplica el turno opcional de vacación (por ejemplo, <c>"Lunes"</c>).</param>
/// <param name="TipoTurnoId">Identificador del tipo de turno que actúa como turno opcional de vacaciones.</param>
public sealed record PlanificacionTurnoOpcionalVacacionSaveRequest(
    string Dia,
    string TipoTurnoId);
