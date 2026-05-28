namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Descripcion completa e inmutable del problema de rotacion de turnos que se entrega al motor de optimizacion.
/// Contiene todos los datos de entrada: empleados, grupos, slots a cubrir, ausencias, feriados y reglas.
/// </summary>
public sealed record ProblemaRotacion
{
    /// <summary>Identificador unico del problema, utilizado para trazabilidad y diagnostico.</summary>
    public required string ProblemaId { get; init; }

    /// <summary>Fecha de inicio del horizonte de planificacion (primer lunes del periodo).</summary>
    public required DateOnly FechaInicio { get; init; }

    /// <summary>Numero de semanas que abarca el horizonte de planificacion.</summary>
    public required int CantidadSemanas { get; init; }

    /// <summary>Lista de empleados que participan en la rotacion.</summary>
    public required List<Empleado> Empleados { get; init; }

    /// <summary>Lista de grupos de trabajo disponibles en el problema.</summary>
    public required List<GrupoTrabajo> Grupos { get; init; }

    /// <summary>Lista de slots de turno que deben ser asignados durante el horizonte.</summary>
    public required List<SlotTurno> Slots { get; init; }

    /// <summary>Lista de ausencias de empleados que bloquean su disponibilidad en fechas concretas.</summary>
    public required List<AusenciaEmpleado> Ausencias { get; init; }

    /// <summary>Lista de excepciones temporales por tipo de turno y rango de fechas.</summary>
    public List<ExcepcionTurno> Excepciones { get; init; } = [];

    /// <summary>
    /// Retornos de vacaciones que obligan a reservar al menos dos dias consecutivos de descanso
    /// en la misma semana del regreso.
    /// </summary>
    public List<DescansoPosteriorVacacion> DescansosPosterioresVacacion { get; init; } = [];

    /// <summary>Conjunto de fechas que corresponden a dias feriados dentro del horizonte de planificacion.</summary>
    public HashSet<DateOnly> Feriados { get; init; } = [];

    /// <summary>Reglas obligatorias y configurables que gobiernan la generacion de la rotacion.</summary>
    public required ReglasRotacion Reglas { get; init; }

    /// <summary>Total de dias que abarca el horizonte de planificacion, equivalente a <see cref="CantidadSemanas"/> x 7.</summary>
    public int CantidadDiasHorizonte => CantidadSemanas * 7;
}
