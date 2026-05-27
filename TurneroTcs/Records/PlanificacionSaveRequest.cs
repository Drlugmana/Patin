namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para guardar o actualizar una entrada de planificación de turnos para un grupo en un día específico.
/// Define cuántas personas de qué tipo de turno deben estar asignadas ese día.
/// </summary>
/// <param name="PlanificacionId">
/// Identificador de la planificación existente a actualizar.
/// <see langword="null"/> cuando se crea una nueva entrada de planificación.
/// </param>
/// <param name="GrupoId">Identificador del grupo al que pertenece esta configuración de planificación.</param>
/// <param name="Dia">Día de la semana para el que aplica la configuración (por ejemplo, <c>"Lunes"</c>, <c>"Martes"</c>).</param>
/// <param name="TipoTurnoId">Identificador del tipo de turno planificado para ese día.</param>
/// <param name="NumeroPersonas">Número de personas requeridas para cubrir el turno en ese día.</param>
/// <param name="IsAuxiliar">
/// <see langword="true"/> si la planificación corresponde a personal auxiliar;
/// <see langword="false"/> para personal regular.
/// </param>
public sealed record PlanificacionSaveRequest(
    string? PlanificacionId,
    string GrupoId,
    string Dia,
    string TipoTurnoId,
    int NumeroPersonas,
    bool IsAuxiliar
);
