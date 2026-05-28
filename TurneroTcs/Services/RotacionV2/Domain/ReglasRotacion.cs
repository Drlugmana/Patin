namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Reglas de cumplimiento obligatorio que aplican a todos los equipos y no pueden ser relajadas.
/// Estas restricciones se codifican como restricciones duras en el modelo de optimización.
/// </summary>
public sealed record ReglasGlobalesObligatorias
{
    /// <summary>Minutos de trabajo que cada empleado debe alcanzar por semana (objetivo semanal de horas).</summary>
    public int MinutosObjetivoSemanales { get; init; } = 40 * 60;

    /// <summary>
    /// Tiempo mínimo de descanso requerido entre el fin de un turno y el inicio del siguiente
    /// para un mismo empleado. Valor predeterminado: 8 horas (480 minutos).
    /// </summary>
    public int MinutosMinimosDescansoEntreTurnos { get; init; } = 8 * 60;

    /// <summary>
    /// Número mínimo de días consecutivos de descanso que cada empleado debe tener por semana.
    /// Valor predeterminado: 2 días consecutivos.
    /// </summary>
    public int MinimoDiasDescansoConsecutivosPorSemana { get; init; } = 2;
}

/// <summary>
/// Políticas de rotación que pueden habilitarse o ajustarse por equipo según sus necesidades operativas.
/// Las políticas habilitadas se incorporan como restricciones o términos de la función objetivo del modelo.
/// </summary>
public sealed record PoliticasConfigurablesEquipo
{
    /// <summary>Número máximo de slots de turno que un empleado puede cubrir en un mismo día calendario.</summary>
    public int MaximoTurnosPorDia { get; init; } = 1;

    /// <summary>Indica si las ausencias por vacaciones deben bloquear la asignación de turnos en esas fechas.</summary>
    public bool AplicarVacaciones { get; init; } = true;

    /// <summary>Indica si se permite asignar empleados a slots auxiliares.</summary>
    public bool PermiteTurnosAuxiliares { get; init; } = true;

    /// <summary>Indica si se debe evitar que un empleado trabaje fines de semana consecutivos.</summary>
    public bool EvitarFinesSemanaConsecutivos { get; init; } = true;

    /// <summary>Número máximo de fines de semana consecutivos permitidos cuando <see cref="EvitarFinesSemanaConsecutivos"/> está activo.</summary>
    public int MaximoFinesSemanaConsecutivos { get; init; } = 1;

    /// <summary>Límite mensual de turnos nocturnos por empleado; <see langword="null"/> si no aplica restricción.</summary>
    public int? MaximoTurnosNocturnosPorMes { get; init; }

    /// <summary>Limite semanal de turnos nocturnos por empleado; <see langword="null"/> si no aplica restriccion.</summary>
    public int? MaximoTurnosNocturnosPorSemana { get; init; }
    /// <summary>
    /// Cuando <see langword="true"/>, los turnos nocturnos pueden distribuirse en bloques de hasta 3 consecutivos
    /// seguidos de al menos un dia de descanso nocturno antes de reiniciar el conteo.
    /// Se aplica ademas del limite semanal configurado.
    /// </summary>
    public bool NocturnosConsecutivos { get; init; } = false;
    /// <summary>Límite mensual de fines de semana trabajados por empleado; <see langword="null"/> si no aplica restricción.</summary>
    public int? MaximoFinesSemanaPorMes { get; init; }

    /// <summary>Límite mensual de slots de fin de semana por empleado; <see langword="null"/> si no aplica restricción.</summary>
    public int? MaximoSlotsFinSemanaPorMes { get; init; }

    /// <summary>Indica si el modelo debe incluir un objetivo de balanceo de minutos nocturnos entre empleados.</summary>
    public bool BalancearTurnosNocturnos { get; init; } = true;

    /// <summary>Indica si el modelo debe incluir un objetivo de balanceo de la carga en días feriados entre empleados.</summary>
    public bool BalancearCargaFeriados { get; init; } = true;

    /// <summary>Indica si el modelo debe incluir un objetivo de balanceo de recargos compuestos (nocturno + feriado + fin de semana).</summary>
    public bool BalancearRecargosCompuestos { get; init; } = true;

    /// <summary>Peso porcentual del recargo nocturno en el cálculo de puntos de recargo compuesto.</summary>
    public int PesoRecargoNocturnoPorcentaje { get; init; } = 50;

    /// <summary>Peso porcentual del recargo por feriado en el cálculo de puntos de recargo compuesto.</summary>
    public int PesoRecargoFeriadoPorcentaje { get; init; } = 100;

    /// <summary>Peso porcentual del recargo por fin de semana en el cálculo de puntos de recargo compuesto.</summary>
    public int PesoRecargoFinSemanaPorcentaje { get; init; } = 100;

    /// <summary>Indica si el modelo debe incluir un objetivo de balanceo de horas totales trabajadas entre empleados.</summary>
    public bool BalancearHorasSemanales { get; init; } = true;

    /// <summary>
    /// Cuando <see langword="true"/>, permite que en semanas con feriado laborable un empleado supere
    /// el objetivo semanal base de turnos para compensar los feriados no trabajados.
    /// </summary>
    public bool PermitirSobrecupoSemanalEnFeriado { get; init; } = false;

    /// <summary>Límite global de transiciones con descanso de exactamente 7 horas en todo el horizonte; <see langword="null"/> si no aplica.</summary>
    public int? MaximoDescansos7HorasGlobal { get; init; }

    /// <summary>Límite por empleado de transiciones con descanso de exactamente 7 horas en todo el horizonte; <see langword="null"/> si no aplica.</summary>
    public int? MaximoDescansos7HorasPorEmpleado { get; init; }

    /// <summary>Indica si se debe penalizar en la función objetivo el uso de descansos de exactamente 7 horas.</summary>
    public bool PenalizarDescansos7Horas { get; init; } = false;

    /// <summary>Nivel de prioridad con que la funcion objetivo evita fines de semana consecutivos para un mismo empleado.</summary>
    public NivelEvitarFinesSemanaConsecutivos NivelEvitarFinesSemanaConsecutivos { get; init; } = NivelEvitarFinesSemanaConsecutivos.NoUsar;

    /// <summary>Nivel de prioridad con que la funcion objetivo intenta mantener el mismo codigo de turno en dias consecutivos trabajados.</summary>
    public NivelAgruparTiposTurnoConsecutivos NivelAgruparTiposTurnoConsecutivos { get; init; } = NivelAgruparTiposTurnoConsecutivos.NoUsar;

    /// <summary>
    /// Mapa que relaciona cada grupo especial con el grupo fuente del que provienen sus empleados elegibles.
    /// Las claves y valores son insensibles a mayúsculas.
    /// </summary>
    public Dictionary<string, string> GrupoFuentePorGrupoEspecial { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Conjunto de identificadores de grupos especiales para los que se aplica la restricción
    /// de una única persona asignada por semana.
    /// </summary>
    public HashSet<string> GruposEspecialesPersonaUnicaPorSemana { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Agrupa las reglas obligatorias y las políticas configurables del equipo que rigen la generación de rotación.
/// </summary>
public sealed record ReglasRotacion
{
    /// <summary>Restricciones de cumplimiento obligatorio que no pueden relajarse.</summary>
    public ReglasGlobalesObligatorias Obligatorias { get; init; } = new();

    /// <summary>Políticas opcionales que pueden habilitarse o ajustarse por equipo.</summary>
    public PoliticasConfigurablesEquipo Configurables { get; init; } = new();
}
