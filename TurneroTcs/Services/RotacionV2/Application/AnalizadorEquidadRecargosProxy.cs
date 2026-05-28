using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Application;

/// <summary>
/// Estadísticas de recargo ponderado calculadas para un empleado individual,
/// usadas para evaluar la equidad en la distribución de turnos especiales.
/// </summary>
public sealed record EstadisticaRecargoPersonaProxy
{
    /// <summary>Identificador del empleado.</summary>
    public required string EmpleadoId { get; init; }

    /// <summary>Nombre del empleado.</summary>
    public required string NombreEmpleado { get; init; }

    /// <summary>Total de minutos trabajados en la solución (solo slots computables).</summary>
    public required int MinutosTotales { get; init; }

    /// <summary>Suma de puntos de recargo ponderado acumulados por el empleado (nocturnos + feriados + fines de semana).</summary>
    public required long PuntosRecargoPonderado { get; init; }

    /// <summary>Índice de recargo por hora: puntos ponderados divididos entre los minutos totales × 100.</summary>
    public required double IndiceRecargoPorHoraProxy { get; init; }
}

/// <summary>
/// Resultado agregado del análisis de equidad de recargos para todos los empleados de la solución.
/// Incluye estadísticas descriptivas que permiten valorar cuán balanceada es la distribución.
/// </summary>
public sealed record ResultadoEquidadRecargosProxy
{
    /// <summary>Número de empleados incluidos en el análisis (aquellos con al menos un minuto trabajado).</summary>
    public int EmpleadosConsiderados { get; init; }

    /// <summary>Promedio del índice de recargo por hora entre todos los empleados considerados.</summary>
    public double PromedioIndiceRecargoPorHoraProxy { get; init; }

    /// <summary>Desviación estándar del índice de recargo por hora.</summary>
    public double DesviacionEstandarIndiceRecargoPorHoraProxy { get; init; }

    /// <summary>Coeficiente de variación expresado en porcentaje (desviación / promedio × 100).</summary>
    public double CoeficienteVariacionPorcentaje { get; init; }

    /// <summary>
    /// Clasificación cualitativa del coeficiente de variación.
    /// Valores posibles: <c>"muy_alta_equidad"</c>, <c>"equidad_buena"</c>, <c>"aceptable_revisar"</c>,
    /// <c>"desbalance_relevante"</c>, <c>"desigualdad_alta"</c>, <c>"sin_datos"</c>.
    /// </summary>
    public string ClasificacionCv { get; init; } = "sin_datos";

    /// <summary>Estadísticas individuales por empleado, ordenadas de menor a mayor índice de recargo.</summary>
    public List<EstadisticaRecargoPersonaProxy> Personas { get; init; } = [];
}

/// <summary>
/// Analiza la equidad en la distribución de recargos de turno (nocturnos, feriados y fines de semana)
/// entre los empleados de una solución de rotación, usando un índice de recargo por hora como proxy.
/// <para>
/// El índice de recargo por hora se calcula como la suma ponderada de minutos especiales
/// dividida entre los minutos totales trabajados, permitiendo comparar la intensidad de
/// carga especial independientemente de la cantidad total de horas asignadas.
/// </para>
/// </summary>
public static class AnalizadorEquidadRecargosProxy
{
    /// <summary>
    /// Calcula las estadísticas de equidad de recargos para la solución dada.
    /// </summary>
    /// <param name="problema">Problema de rotación con los pesos de recargo y la lista de slots.</param>
    /// <param name="solucion">Solución con las asignaciones efectuadas por el motor de optimización.</param>
    /// <returns>
    /// Resultado con las estadísticas descriptivas de equidad. Si ningún empleado tiene minutos trabajados,
    /// se devuelve un resultado vacío con clasificación <c>"sin_datos"</c>.
    /// </returns>
    public static ResultadoEquidadRecargosProxy Calcular(ProblemaRotacion problema, SolucionRotacionCp solucion)
    {
        ArgumentNullException.ThrowIfNull(problema);
        ArgumentNullException.ThrowIfNull(solucion);

        var slotPorId = problema.Slots.ToDictionary(slot => slot.Id, StringComparer.OrdinalIgnoreCase);
        var empleadoPorId = problema.Empleados.ToDictionary(empleado => empleado.Id, StringComparer.OrdinalIgnoreCase);
        var acumulado = new Dictionary<string, (int Total, long RecargoPonderado)>(StringComparer.OrdinalIgnoreCase);

        foreach (var asignacion in solucion.Asignaciones)
        {
            if (!slotPorId.TryGetValue(asignacion.IdSlot, out var slot) ||
                !empleadoPorId.TryGetValue(asignacion.EmpleadoId, out var empleado))
            {
                continue;
            }

            var total = Math.Max(0, slot.MinutosTrabajoComputables);
            var recargoPonderado = CalcularPuntosRecargoPonderado(problema, slot);
            acumulado[empleado.Id] = acumulado.TryGetValue(empleado.Id, out var actual)
                ? (actual.Total + total, actual.RecargoPonderado + recargoPonderado)
                : (total, recargoPonderado);
        }

        var personas = acumulado
            .Where(item => item.Value.Total > 0)
            .Select(item =>
            {
                var empleado = empleadoPorId[item.Key];
                var indice = (double)item.Value.RecargoPonderado / (100d * item.Value.Total);
                return new EstadisticaRecargoPersonaProxy
                {
                    EmpleadoId = empleado.Id,
                    NombreEmpleado = empleado.Nombre,
                    MinutosTotales = item.Value.Total,
                    PuntosRecargoPonderado = item.Value.RecargoPonderado,
                    IndiceRecargoPorHoraProxy = indice
                };
            })
            .OrderBy(item => item.IndiceRecargoPorHoraProxy)
            .ThenBy(item => item.NombreEmpleado, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (personas.Count == 0)
        {
            return new ResultadoEquidadRecargosProxy();
        }

        var promedio = personas.Average(item => item.IndiceRecargoPorHoraProxy);
        var desviacion = Math.Sqrt(personas.Average(item =>
        {
            var delta = item.IndiceRecargoPorHoraProxy - promedio;
            return delta * delta;
        }));
        var cv = promedio > 0 ? (desviacion / promedio) * 100d : 0d;

        return new ResultadoEquidadRecargosProxy
        {
            EmpleadosConsiderados = personas.Count,
            PromedioIndiceRecargoPorHoraProxy = promedio,
            DesviacionEstandarIndiceRecargoPorHoraProxy = desviacion,
            CoeficienteVariacionPorcentaje = cv,
            ClasificacionCv = ClasificarCv(cv),
            Personas = personas
        };
    }

    private static long CalcularPuntosRecargoPonderado(ProblemaRotacion problema, SlotTurno slot)
    {
        var reglas = problema.Reglas.Configurables;
        var pesoNocturno = Math.Max(0, reglas.PesoRecargoNocturnoPorcentaje);
        var pesoFeriado = Math.Max(0, reglas.PesoRecargoFeriadoPorcentaje);
        var pesoFinSemana = Math.Max(0, reglas.PesoRecargoFinSemanaPorcentaje);

        long total = (long)Math.Max(0, slot.MinutosVentanaNocturna) * pesoNocturno;

        if (problema.Feriados.Contains(slot.Fecha))
        {
            total += (long)Math.Max(0, slot.MinutosTrabajoComputables) * pesoFeriado;
        }

        if (slot.IndiceDia is 5 or 6)
        {
            total += (long)Math.Max(0, slot.MinutosTrabajoComputables) * pesoFinSemana;
        }

        return total;
    }

    private static string ClasificarCv(double cv)
    {
        if (cv <= 3d) return "muy_alta_equidad";
        if (cv <= 5d) return "equidad_buena";
        if (cv <= 8d) return "aceptable_revisar";
        if (cv <= 12d) return "desbalance_relevante";
        return "desigualdad_alta";
    }
}
