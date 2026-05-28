namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para guardar la configuración de turnos de apoyo en un día específico.
/// El personal de apoyo cubre necesidades adicionales sin pertenecer a un grupo fijo.
/// </summary>
/// <param name="Dia">Día de la semana para el que se configura el apoyo (por ejemplo, <c>"Lunes"</c>).</param>
/// <param name="TipoTurnoId">Identificador del tipo de turno al que se asigna el apoyo.</param>
/// <param name="CantidadApoyo">Número de personas de apoyo requeridas para ese día y tipo de turno.</param>
public sealed record PlanificacionApoyoSaveRequest(
    string Dia,
    string TipoTurnoId,
    int CantidadApoyo
);
