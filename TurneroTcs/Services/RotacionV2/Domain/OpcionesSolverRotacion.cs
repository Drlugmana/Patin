namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Controla el nivel de uso de la relajacion del descanso minimo a 7 horas
/// como estrategia de fallback cuando el modelo resulta infactible.
/// </summary>
public enum NivelUsoDescanso7Horas
{
    /// <summary>No se aplica la relajacion a 7 horas en ningun caso.</summary>
    NoUsar,

    /// <summary>
    /// La relajacion a 7 horas se activa solo como reintento unico cuando una semana resulta infactible.
    /// Una vez que resuelve con 7 h, vuelve al descanso normal en la semana siguiente.
    /// </summary>
    Bajo,

    /// <summary>
    /// Si la relajacion a 7 horas resuelve una semana infactible, se mantiene activa
    /// para las semanas siguientes de ese horizonte.
    /// </summary>
    Medio,

    /// <summary>La relajacion a 7 horas se aplica de forma preventiva desde la primera semana.</summary>
    Alto
}

/// <summary>
/// Define cuan fuerte debe ser la preferencia del solver por evitar que un mismo empleado
/// trabaje fines de semana consecutivos dentro del horizonte generado.
/// </summary>
public enum NivelEvitarFinesSemanaConsecutivos
{
    /// <summary>No agrega una penalizacion extra por fines de semana consecutivos.</summary>
    NoUsar,

    /// <summary>Aplica una preferencia suave por cortar repeticiones consecutivas.</summary>
    Bajo,

    /// <summary>Aplica una preferencia intermedia por cortar repeticiones consecutivas.</summary>
    Medio,

    /// <summary>Aplica la preferencia mas fuerte por evitar fines de semana consecutivos.</summary>
    Alto
}

/// <summary>
/// Parametros de ejecucion que controlan el comportamiento del motor de optimizacion
/// durante la resolucion del problema de rotacion.
/// </summary>
public sealed record OpcionesSolverRotacion
{
    /// <summary>
    /// Tiempo maximo que se le concede al motor de optimizacion para encontrar una solucion por semana.
    /// Valor predeterminado: 2 minutos.
    /// </summary>
    public TimeSpan TiempoMaximoResolucion { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Semilla aleatoria utilizada por el motor para reproducibilidad de resultados.
    /// El resolvedor secuencial prueba un portafolio de semillas derivadas de este valor base.
    /// </summary>
    public int SemillaAleatoria { get; init; } = 1;

    /// <summary>
    /// Numero de hilos paralelos que puede utilizar el motor durante la busqueda.
    /// Por defecto se usa el numero de procesadores logicos disponibles.
    /// </summary>
    public int CantidadWorkers { get; init; } = Environment.ProcessorCount;

    /// <summary>Indica si el motor debe emitir trazas del progreso de busqueda en la salida estandar.</summary>
    public bool RegistrarProgresoBusqueda { get; init; }

    /// <summary>Nivel de agresividad con que se aplica la relajacion del descanso minimo a 7 horas como estrategia de fallback.</summary>
    public NivelUsoDescanso7Horas NivelUsoDescanso7Horas { get; init; } = NivelUsoDescanso7Horas.Bajo;

    /// <summary>Nivel de prioridad con que se evita asignar fines de semana consecutivos al mismo empleado.</summary>
    public NivelEvitarFinesSemanaConsecutivos NivelEvitarFinesSemanaConsecutivos { get; init; } = NivelEvitarFinesSemanaConsecutivos.NoUsar;

    /// <summary>
    /// Cuando <see langword="true"/>, si una semana con feriado laborable resulta infactible,
    /// se reintenta permitiendo que los empleados superen el objetivo semanal en esa semana.
    /// </summary>
    public bool AutorizarSobrecupoSemanalEnFeriado { get; init; }

    /// <summary>
    /// Accion opcional invocada con mensajes de diagnostico durante la resolucion.
    /// Permite al llamador capturar o mostrar el progreso de cada semana en tiempo real.
    /// </summary>
    public Action<string>? ReportarDiagnostico { get; init; }
}
