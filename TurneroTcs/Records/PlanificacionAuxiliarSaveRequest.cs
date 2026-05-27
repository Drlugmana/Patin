namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para guardar la configuración de turnos auxiliares aplicable a un rango de días
/// y a un conjunto de grupos específicos.
/// </summary>
/// <param name="TipoTurnoId">Identificador del tipo de turno auxiliar a configurar.</param>
/// <param name="DesdeDia">Primer día del rango de planificación auxiliar (por ejemplo, <c>"Lunes"</c>).</param>
/// <param name="HastaDia">Último día del rango de planificación auxiliar (por ejemplo, <c>"Viernes"</c>).</param>
/// <param name="MaxPorDia">Número máximo de personas auxiliares permitidas por día en este tipo de turno.</param>
/// <param name="GrupoIds">Colección de identificadores de grupos a los que aplica esta configuración auxiliar.</param>
public sealed record PlanificacionAuxiliarSaveRequest(
    string TipoTurnoId,
    string DesdeDia,
    string HastaDia,
    int MaxPorDia,
    IReadOnlyCollection<string> GrupoIds
);
