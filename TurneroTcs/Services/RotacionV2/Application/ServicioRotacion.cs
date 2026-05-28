using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Model;
using TurneroTcs.Services.RotacionV2.Solver;

namespace TurneroTcs.Services.RotacionV2.Application;

/// <summary>
/// Punto de entrada de alto nivel para la generación de rotación de turnos.
/// Coordina la transformación de plantilla a problema de dominio, la construcción del modelo
/// de optimización y la ejecución del resolvedor secuencial semanal.
/// <para>
/// Para la mayoría de los casos de uso, el flujo es:
/// <list type="number">
///   <item>Llamar a <see cref="Resolver"/> con la plantilla y los parámetros de configuración.</item>
///   <item>Inspeccionar el <see cref="SolucionRotacionCp.Estado"/> y las asignaciones del resultado.</item>
/// </list>
/// Los métodos <see cref="CrearProblema"/> y <see cref="CrearModeloBase"/> están disponibles para
/// escenarios más avanzados que requieren acceso directo al modelo antes de resolver.
/// </para>
/// </summary>
public sealed class ServicioRotacion
{
    private readonly MapeadorRotacion _mapeador = new();
    private readonly ConstructorModeloCp _constructorModelo = new();
    private readonly ResolvedorCpSatRotacion _resolvedor = new();
    private readonly ResolvedorSecuencialSemanalRotacion _resolvedorSecuencial = new();

    /// <summary>
    /// Construye el <see cref="ProblemaRotacion"/> a partir de una plantilla y los datos de entrada.
    /// </summary>
    /// <param name="plantilla">Plantilla con la estructura de turnos y personas del grupo.</param>
    /// <param name="cantidadSemanas">Número de semanas del horizonte de planificación.</param>
    /// <param name="fechaInicio">Fecha y hora de inicio del horizonte (se usa solo la parte de fecha).</param>
    /// <param name="vacacionesPorPersonaId">Mapa de fechas de vacación por identificador de persona; <see langword="null"/> si no hay vacaciones.</param>
    /// <param name="feriados">Conjunto de fechas feriadas dentro del horizonte; <see langword="null"/> si no hay feriados.</param>
    /// <param name="reglas">Reglas de rotación personalizadas; <see langword="null"/> para usar los valores predeterminados.</param>
    /// <returns>Problema de rotación listo para ser modelado o resuelto.</returns>
    public ProblemaRotacion CrearProblema(
        Plantilla plantilla,
        int cantidadSemanas,
        DateTime fechaInicio,
        Dictionary<string, HashSet<DateOnly>>? vacacionesPorPersonaId = null,
        HashSet<DateOnly>? feriados = null,
        ReglasRotacion? reglas = null,
        IEnumerable<ExcepcionTurno>? excepciones = null)
    {
        return _mapeador.CrearProblema(plantilla, cantidadSemanas, fechaInicio, vacacionesPorPersonaId, feriados, reglas, excepciones);
    }

    /// <summary>
    /// Construye el modelo de optimización para el problema derivado de la plantilla dada,
    /// sin ejecutar el resolvedor. Útil para inspeccionar el modelo o ejecutar el resolvedor manualmente.
    /// </summary>
    /// <param name="plantilla">Plantilla con la estructura de turnos y personas del grupo.</param>
    /// <param name="cantidadSemanas">Número de semanas del horizonte de planificación.</param>
    /// <param name="fechaInicio">Fecha y hora de inicio del horizonte.</param>
    /// <param name="vacacionesPorPersonaId">Mapa de fechas de vacación por persona; <see langword="null"/> si no aplica.</param>
    /// <param name="feriados">Fechas feriadas; <see langword="null"/> si no aplica.</param>
    /// <param name="reglas">Reglas de rotación; <see langword="null"/> para usar los predeterminados.</param>
    /// <returns>Contexto del modelo con todas las restricciones y la función objetivo registradas.</returns>
    public ContextoModeloCp CrearModeloBase(
        Plantilla plantilla,
        int cantidadSemanas,
        DateTime fechaInicio,
        Dictionary<string, HashSet<DateOnly>>? vacacionesPorPersonaId = null,
        HashSet<DateOnly>? feriados = null,
        ReglasRotacion? reglas = null,
        IEnumerable<ExcepcionTurno>? excepciones = null)
    {
        var problema = CrearProblema(plantilla, cantidadSemanas, fechaInicio, vacacionesPorPersonaId, feriados, reglas, excepciones);
        return _constructorModelo.Construir(problema);
    }

    /// <summary>
    /// Genera la rotación completa para el horizonte dado, resolviendo semana a semana.
    /// </summary>
    /// <param name="plantilla">Plantilla con la estructura de turnos y personas del grupo.</param>
    /// <param name="cantidadSemanas">Número de semanas del horizonte de planificación.</param>
    /// <param name="fechaInicio">Fecha y hora de inicio del horizonte.</param>
    /// <param name="vacacionesPorPersonaId">Mapa de fechas de vacación por persona; <see langword="null"/> si no aplica.</param>
    /// <param name="feriados">Fechas feriadas; <see langword="null"/> si no aplica.</param>
    /// <param name="reglas">Reglas de rotación; <see langword="null"/> para usar los predeterminados.</param>
    /// <param name="opcionesSolver">Parámetros del motor de optimización; <see langword="null"/> para usar los predeterminados.</param>
    /// <returns>Solución de rotación con el estado de resolución, las asignaciones y las métricas.</returns>
    public SolucionRotacionCp Resolver(
        Plantilla plantilla,
        int cantidadSemanas,
        DateTime fechaInicio,
        Dictionary<string, HashSet<DateOnly>>? vacacionesPorPersonaId = null,
        HashSet<DateOnly>? feriados = null,
        ReglasRotacion? reglas = null,
        OpcionesSolverRotacion? opcionesSolver = null,
        IEnumerable<ExcepcionTurno>? excepciones = null,
        EstadoResolucionSemanal? estadoSemanalInicial = null)
    {
        var problema = CrearProblema(plantilla, cantidadSemanas, fechaInicio, vacacionesPorPersonaId, feriados, reglas, excepciones);
        return _resolvedorSecuencial.Resolver(problema, opcionesSolver, estadoSemanalInicial);
    }

    /// <summary>
    /// Genera la rotación a partir de un problema de dominio ya construido.
    /// Útil cuando el llamador necesita preconfigurar el problema antes de resolver.
    /// </summary>
    /// <param name="problema">Problema de rotación con todos los datos ya inicializados.</param>
    /// <param name="opcionesSolver">Parámetros del motor de optimización; <see langword="null"/> para usar los predeterminados.</param>
    /// <returns>Solución de rotación con el estado de resolución, las asignaciones y las métricas.</returns>
    public SolucionRotacionCp ResolverProblema(
        ProblemaRotacion problema,
        OpcionesSolverRotacion? opcionesSolver = null,
        EstadoResolucionSemanal? estadoSemanalInicial = null)
    {
        return _resolvedorSecuencial.Resolver(problema, opcionesSolver, estadoSemanalInicial);
    }
}
