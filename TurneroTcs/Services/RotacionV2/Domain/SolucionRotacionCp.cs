namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Estado devuelto por el motor de optimización al terminar la búsqueda de solución.
/// </summary>
public enum EstadoSolucionRotacion
{
    /// <summary>El motor no encontró ninguna solución dentro del tiempo permitido.</summary>
    NoResuelta = 0,

    /// <summary>El motor encontró la solución óptima que minimiza la función objetivo.</summary>
    Optima = 1,

    /// <summary>El motor encontró una solución válida pero no pudo garantizar optimalidad en el tiempo dado.</summary>
    Factible = 2,

    /// <summary>El modelo no tiene ninguna solución que satisfaga todas las restricciones duras.</summary>
    Infactible = 3,

    /// <summary>El modelo construido contiene errores que impiden la resolución.</summary>
    ModeloInvalido = 4,

    /// <summary>Ocurrió un error inesperado durante el proceso de resolución.</summary>
    Error = 5
}

/// <summary>
/// Representa la asignación de un empleado a un slot de turno concreto dentro de la solución de rotación.
/// </summary>
public sealed record AsignacionSlot
{
    /// <summary>Identificador del slot de turno asignado.</summary>
    public required string IdSlot { get; init; }

    /// <summary>Identificador del empleado asignado al slot.</summary>
    public required string EmpleadoId { get; init; }
}

/// <summary>
/// Métricas de alto nivel que resumen la calidad y cobertura de la solución de rotación obtenida.
/// </summary>
public sealed record MetricasSolucionRotacion
{
    /// <summary>Número total de pares (empleado, slot) asignados en la solución.</summary>
    public int SlotsAsignados { get; init; }

    /// <summary>Número de slots que quedaron sin cubrir con el mínimo de empleados requeridos.</summary>
    public int SlotsSinAsignar { get; init; }

    /// <summary>Número de asignaciones que corresponden a slots auxiliares.</summary>
    public int AsignacionesAuxiliares { get; init; }

    /// <summary>Número de asignaciones que corresponden a slots de turno nocturno.</summary>
    public int AsignacionesNocturnas { get; init; }
}

/// <summary>
/// Resultado completo devuelto por el motor de optimización tras resolver un problema de rotación.
/// Contiene el estado de resolución, las asignaciones obtenidas y métricas de calidad.
/// </summary>
public sealed record SolucionRotacionCp
{
    /// <summary>Estado final reportado por el motor de optimización.</summary>
    public required EstadoSolucionRotacion Estado { get; init; }

    /// <summary>Texto de detalle del estado, útil para trazabilidad y diagnóstico de semanas individuales.</summary>
    public string DetalleEstado { get; init; } = string.Empty;

    /// <summary>Lista de asignaciones (empleado → slot) que conforman la solución.</summary>
    public List<AsignacionSlot> Asignaciones { get; init; } = [];

    /// <summary>Métricas de resumen de cobertura y tipo de turnos asignados.</summary>
    public MetricasSolucionRotacion Metricas { get; init; } = new();
}
