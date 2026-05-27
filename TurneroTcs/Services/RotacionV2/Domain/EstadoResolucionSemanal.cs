namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Mantiene el estado acumulado entre semanas durante la resolución secuencial del horizonte de planificación.
/// Permite que cada semana individual tenga en cuenta lo que ocurrió en semanas anteriores,
/// garantizando la continuidad de restricciones cross-semana (descanso mínimo, rachas de fines de semana, etc.).
/// </summary>
public sealed class EstadoResolucionSemanal
{
    /// <summary>Empleados que trabajaron en fin de semana durante la semana inmediatamente anterior.</summary>
    public HashSet<string> EmpleadosConFinSemanaAnterior { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Número de fines de semana consecutivos acumulados hasta la semana anterior, por empleado.
    /// Se usa para aplicar la restricción de máximo de fines de semana consecutivos en la semana siguiente.
    /// </summary>
    public Dictionary<string, int> RachaFinesSemanaConsecutivosPorEmpleado { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Empleados que tuvieron un turno nocturno el último día (domingo) de la semana anterior.</summary>
    public HashSet<string> EmpleadosConNocturnoUltimoDiaAnterior { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Fecha y hora de fin del último turno trabajado por cada empleado en la semana anterior.
    /// Se usa para verificar el descanso mínimo al inicio de la semana siguiente.
    /// </summary>
    public Dictionary<string, DateTime> UltimoFinTurnoPorEmpleado { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Conteo acumulado de turnos nocturnos por empleado y mes, para verificar el límite mensual de nocturnos.
    /// La clave es <c>(EmpleadoId, Año, Mes)</c>.
    /// </summary>
    public Dictionary<(string EmpleadoId, int Anio, int Mes), int> TurnosNocturnosPorEmpleadoMes { get; } = [];

    /// <summary>
    /// Conteo acumulado de slots de fin de semana por empleado y mes, para verificar el límite mensual de slots de fin de semana.
    /// La clave es <c>(EmpleadoId, Año, Mes)</c>.
    /// </summary>
    public Dictionary<(string EmpleadoId, int Anio, int Mes), int> SlotsFinSemanaPorEmpleadoMes { get; } = [];

    /// <summary>Minutos nocturnos acumulados por empleado en todo el horizonte resuelto hasta el momento.</summary>
    public Dictionary<string, int> MinutosNocturnosAcumuladosPorEmpleado { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Minutos de fin de semana acumulados por empleado en todo el horizonte resuelto hasta el momento.</summary>
    public Dictionary<string, int> MinutosFinSemanaAcumuladosPorEmpleado { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Minutos totales trabajados acumulados por empleado en todo el horizonte resuelto hasta el momento.</summary>
    public Dictionary<string, int> MinutosTotalesAcumuladosPorEmpleado { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Minutos trabajados en días feriados acumulados por empleado en todo el horizonte resuelto hasta el momento.</summary>
    public Dictionary<string, int> MinutosFeriadoAcumuladosPorEmpleado { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Número de transiciones con descanso de exactamente 7 horas acumuladas por empleado.</summary>
    public Dictionary<string, int> Descansos7HorasAcumuladosPorEmpleado { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Número total de transiciones con descanso de exactamente 7 horas en todo el equipo.</summary>
    public int Descansos7HorasAcumuladosTotal { get; set; }

    /// <summary>
    /// Empleados que estuvieron asignados a cada grupo especial durante la semana anterior.
    /// Permite evitar que el mismo empleado repita el grupo especial en semanas consecutivas.
    /// </summary>
    public Dictionary<string, HashSet<string>> EmpleadosGrupoEspecialSemanaAnterior { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Número de veces que cada empleado ha sido asignado a cada grupo especial a lo largo del horizonte.
    /// La clave es <c>(GrupoId, EmpleadoId)</c> y se usa para penalizar la reasignación al mismo grupo especial.
    /// </summary>
    public Dictionary<(string GrupoId, string EmpleadoId), int> UsosGrupoEspecialPorEmpleado { get; } = new();
}
