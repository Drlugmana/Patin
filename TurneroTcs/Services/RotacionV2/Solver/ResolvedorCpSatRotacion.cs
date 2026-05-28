using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Solver;

/// <summary>
/// Ejecuta el motor de optimización por satisfacción de restricciones sobre un modelo ya construido,
/// configura sus parámetros de búsqueda y traduce el resultado al modelo de dominio.
/// </summary>
public sealed class ResolvedorCpSatRotacion
{
    private readonly MapeadorSolucionCp _mapeadorSolucion = new();

    /// <summary>
    /// Resuelve el modelo de optimización contenido en el contexto y devuelve la solución obtenida.
    /// </summary>
    /// <param name="contexto">Contexto con el modelo y las variables de decisión ya configurados.</param>
    /// <param name="opciones">Parámetros de ejecución del motor (tiempo, semilla, hilos, etc.). Si es <see langword="null"/>, se usan los valores predeterminados.</param>
    /// <param name="detenerEnPrimeraSolucion">
    /// Cuando <see langword="true"/>, el motor se detiene en cuanto encuentra la primera solución factible,
    /// sin continuar optimizando. Útil para la fase de búsqueda rápida de factibilidad.
    /// </param>
    /// <returns>Solución de dominio con el estado de resolución, asignaciones y métricas.</returns>
    public SolucionRotacionCp Resolver(
        ContextoModeloCp contexto,
        OpcionesSolverRotacion? opciones = null,
        bool detenerEnPrimeraSolucion = false)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var opcionesFinales = opciones ?? new OpcionesSolverRotacion();
        var solver = new CpSolver
        {
            StringParameters = ConstruirParametros(opcionesFinales, detenerEnPrimeraSolucion)
        };

        var estado = solver.Solve(contexto.Modelo);
        return _mapeadorSolucion.Mapear(contexto, solver, estado);
    }

    /// <summary>
    /// Construye la cadena de parámetros que controla el comportamiento interno del motor,
    /// incluyendo tiempo máximo, número de hilos paralelos y semilla aleatoria.
    /// </summary>
    private static string ConstruirParametros(
        OpcionesSolverRotacion opciones,
        bool detenerEnPrimeraSolucion)
    {
        var parametros = new List<string>
        {
            $"max_time_in_seconds:{Math.Max(1, (int)Math.Ceiling(opciones.TiempoMaximoResolucion.TotalSeconds))}",
            $"num_search_workers:{Math.Max(1, opciones.CantidadWorkers)}",
            $"random_seed:{opciones.SemillaAleatoria}"
        };

        if (detenerEnPrimeraSolucion)
        {
            parametros.Add("stop_after_first_solution:true");
        }

        if (opciones.RegistrarProgresoBusqueda)
        {
            parametros.Add("log_search_progress:true");
        }

        return string.Join(" ", parametros);
    }
}
