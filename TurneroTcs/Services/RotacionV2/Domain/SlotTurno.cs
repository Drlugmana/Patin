namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Representa una ocurrencia concreta de un turno en una fecha y grupo específicos.
/// Cada slot es la unidad mínima de asignación dentro del problema de rotación:
/// el motor de optimización decide qué empleado(s) cubren cada slot.
/// </summary>
public sealed record SlotTurno
{
    /// <summary>Identificador único del slot dentro del problema de rotación.</summary>
    public required string Id { get; init; }

    /// <summary>Número de turno en la plantilla origen, utilizado para trazabilidad.</summary>
    public required int NumeroTurnoPlantilla { get; init; }

    /// <summary>Índice de la semana dentro del horizonte de planificación (base cero).</summary>
    public required int IndiceSemana { get; init; }

    /// <summary>Índice del día de la semana (0 = lunes, 6 = domingo).</summary>
    public required int IndiceDia { get; init; }

    /// <summary>Fecha calendario en que ocurre el slot.</summary>
    public required DateOnly Fecha { get; init; }

    /// <summary>Nombre del día de la semana en español normalizado (por ejemplo, <c>"lunes"</c>).</summary>
    public required string NombreDia { get; init; }

    /// <summary>Identificador del grupo de trabajo al que pertenece este slot.</summary>
    public required string GrupoId { get; init; }

    /// <summary>Identificador del tipo de turno según la configuración del sistema.</summary>
    public required string TipoTurnoId { get; init; }

    /// <summary>Código corto del turno derivado del tipo de horario (por ejemplo, <c>"M"</c>, <c>"T"</c>, <c>"N"</c>).</summary>
    public required string CodigoTurno { get; init; }

    /// <summary>Fecha y hora de inicio del turno en hora local.</summary>
    public required DateTime InicioLocal { get; init; }

    /// <summary>Fecha y hora de fin del turno en hora local. Puede caer en el día siguiente para turnos nocturnos.</summary>
    public required DateTime FinLocal { get; init; }

    /// <summary>Número mínimo de empleados que deben cubrir este slot para satisfacer la demanda.</summary>
    public required int EmpleadosRequeridos { get; init; }

    /// <summary>
    /// Capacidad total planificada del slot, que puede ser mayor que <see cref="EmpleadosRequeridos"/>
    /// cuando se contempla personal de apoyo adicional.
    /// </summary>
    public int CapacidadPlanificada { get; init; }

    /// <summary>Número máximo de empleados de este slot que pueden cederse como apoyo a otro slot del mismo grupo y fecha.</summary>
    public int MaximoApoyoCedible { get; init; }

    /// <summary>Indica si el slot corresponde a un turno auxiliar (no computado en el objetivo de horas semanales del grupo principal).</summary>
    public bool EsAuxiliar { get; init; }

    /// <summary>Indica si el slot fue creado específicamente para reemplazar a un empleado en vacaciones.</summary>
    public bool EsReemplazoVacacion { get; init; }

    /// <summary>
    /// Indica que el slot puede omitirse (ponerse a cero empleados) cuando hay una vacación primaria
    /// activa en el grupo y la fecha, evitando asignaciones innecesarias.
    /// </summary>
    public bool PuedeOmitirsePorVacacion { get; init; }

    /// <summary>
    /// Número mínimo de personas requerido cuando el slot nocturno flexible opera en modo
    /// de nocturnos consecutivos. Corresponde a <c>min_personas</c> de la planificación.
    /// Puede ser 0 si así lo configura el operador.
    /// </summary>
    public int MinimoFlexible { get; init; }

    /// <summary>
    /// Clave que agrupa slots auxiliares que comparten un límite de capacidad compartido entre grupos.
    /// Vacío si el slot no participa en una capacidad compartida.
    /// </summary>
    public string LlaveCompartidaAuxiliar { get; init; } = string.Empty;

    /// <summary>
    /// Capacidad máxima total de empleados que pueden ser asignados al conjunto de slots
    /// que comparten la misma <see cref="LlaveCompartidaAuxiliar"/> en la misma fecha.
    /// </summary>
    public int MaximoCompartidoAuxiliar { get; init; }

    /// <summary>
    /// Minutos de trabajo efectivo que computan para el objetivo semanal de horas.
    /// Puede diferir de la duración real del turno cuando hay bloques no computables.
    /// </summary>
    public int MinutosTrabajoComputables { get; init; }

    /// <summary>Duración total del turno en minutos, calculada como la diferencia entre <see cref="FinLocal"/> e <see cref="InicioLocal"/>.</summary>
    public int DuracionMinutos => (int)Math.Round((FinLocal - InicioLocal).TotalMinutes);

    /// <summary>
    /// Minutos del turno que caen dentro de la ventana nocturna (18:00–07:00 del día siguiente).
    /// Se usa para cuantificar la carga nocturna con fines de balanceo.
    /// </summary>
    public int MinutosVentanaNocturna => CalcularMinutosVentanaNocturna(InicioLocal, FinLocal);

    /// <summary>
    /// <see langword="true"/> si más del 70 % de la duración del turno cae dentro de la ventana nocturna (18:00–07:00).
    /// </summary>
    public bool EsTurnoNocturno
    {
        get
        {
            if (DuracionMinutos <= 0)
            {
                return false;
            }

            return (double)MinutosVentanaNocturna / DuracionMinutos > 0.70d;
        }
    }

    private static int CalcularMinutosVentanaNocturna(DateTime inicio, DateTime fin)
    {
        var total = 0d;
        var fecha = inicio.Date.AddDays(-1);
        while (fecha <= fin.Date)
        {
            var inicioVentana = fecha.AddHours(18);
            var finVentana = fecha.AddDays(1).AddHours(7);
            var inicioInterseccion = inicio > inicioVentana ? inicio : inicioVentana;
            var finInterseccion = fin < finVentana ? fin : finVentana;

            if (finInterseccion > inicioInterseccion)
            {
                total += (finInterseccion - inicioInterseccion).TotalMinutes;
            }

            fecha = fecha.AddDays(1);
        }

        return (int)Math.Round(total);
    }
}
