using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Crea una variable de penalización que representa el rango (máximo − mínimo) de puntos
/// de recargo compuesto acumulados por los empleados elegibles.
/// Los puntos de recargo combinan en una sola métrica la exposición nocturna, en feriado
/// y en fin de semana, ponderada por los porcentajes configurados en las reglas del equipo.
/// Minimizar este rango promueve que la carga de turnos especialmente gravosos
/// se distribuya equitativamente entre todos los empleados.
/// Solo se activa si la política <see cref="Domain.PoliticasConfigurablesEquipo.BalancearRecargosCompuestos"/>
/// está habilitada y existen al menos dos empleados elegibles con slots de recargo.
/// </summary>
public static class ObjetivoBalanceRecargosCompuestos
{
    /// <summary>
    /// Crea la variable de penalización de rango de puntos de recargo compuesto entre empleados.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema, las variables de decisión y el estado acumulado.</param>
    /// <returns>
    /// Variable entera de penalización (rango de puntos de recargo), o <see langword="null"/>
    /// si la política no está habilitada, no hay slots con recargo o hay menos de dos empleados elegibles.
    /// </returns>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        if (!contexto.Problema.Reglas.Configurables.BalancearRecargosCompuestos)
        {
            return null;
        }

        if (contexto.Problema.Excepciones.Count > 0)
        {
            return null;
        }

        var slotsConRecargo = contexto.Problema.Slots
            .Select(slot => new
            {
                Slot = slot,
                PuntosRecargo = CalcularPuntosRecargoCompuesto(contexto, slot)
            })
            .Where(item => item.PuntosRecargo > 0)
            .ToArray();

        if (slotsConRecargo.Length == 0 || contexto.Problema.Empleados.Count == 0)
        {
            return null;
        }

        var empleadosElegibles = contexto.Problema.Empleados
            .Where(empleado => slotsConRecargo.Any(item => PuedeCubrirGrupo(empleado, item.Slot.GrupoId)))
            .ToArray();

        if (empleadosElegibles.Length <= 1)
        {
            return null;
        }

        var estadoAcumulado = contexto.EstadoSemanalAcumulado;
        var puntosRecargoMaximosVentana = slotsConRecargo.Sum(item => item.PuntosRecargo);

        var puntosRecargoPorEmpleado = new List<IntVar>();
        foreach (var empleado in empleadosElegibles)
        {
            var puntosAcumuladosPrevios =
                ObtenerPuntosRecargoAcumuladosPrevios(contexto, estadoAcumulado, empleado.Id);

            var variables = slotsConRecargo
                .Select(item => contexto.ObtenerVariableAsignacion(empleado.Id, item.Slot.Id))
                .ToArray();

            var coeficientes = slotsConRecargo
                .Select(item => item.PuntosRecargo)
                .ToArray();

            var puntosEmpleado = contexto.Modelo.NewIntVar(
                puntosAcumuladosPrevios,
                puntosAcumuladosPrevios + puntosRecargoMaximosVentana,
                $"puntos_recargo_compuesto_{empleado.Numero}");

            contexto.Modelo.Add(puntosEmpleado ==
                                puntosAcumuladosPrevios + LinearExpr.WeightedSum(variables, coeficientes));
            puntosRecargoPorEmpleado.Add(puntosEmpleado);
        }

        var puntosPreviosPorEmpleado = empleadosElegibles
            .Select(empleado => ObtenerPuntosRecargoAcumuladosPrevios(contexto, estadoAcumulado, empleado.Id))
            .ToArray();

        var minimoPosible = puntosPreviosPorEmpleado.Min();
        var maximoPosible = puntosPreviosPorEmpleado.Max() + puntosRecargoMaximosVentana;

        var maximo = contexto.Modelo.NewIntVar(minimoPosible, maximoPosible, "maximo_puntos_recargo_compuesto");
        var minimo = contexto.Modelo.NewIntVar(minimoPosible, maximoPosible, "minimo_puntos_recargo_compuesto");
        var rango = contexto.Modelo.NewIntVar(0, maximoPosible - minimoPosible, "rango_puntos_recargo_compuesto");

        contexto.Modelo.AddMaxEquality(maximo, puntosRecargoPorEmpleado);
        contexto.Modelo.AddMinEquality(minimo, puntosRecargoPorEmpleado);
        contexto.Modelo.Add(rango == maximo - minimo);

        return rango;
    }

    private static long CalcularPuntosRecargoCompuesto(ContextoModeloCp contexto, Domain.SlotTurno slot)
    {
        var reglas = contexto.Problema.Reglas.Configurables;
        var pesoNocturno = Math.Max(0, reglas.PesoRecargoNocturnoPorcentaje);
        var pesoFeriado = Math.Max(0, reglas.PesoRecargoFeriadoPorcentaje);
        var pesoFinSemana = Math.Max(0, reglas.PesoRecargoFinSemanaPorcentaje);

        long total = (long)Math.Max(0, slot.MinutosVentanaNocturna) * pesoNocturno;

        if (contexto.Problema.Feriados.Contains(slot.Fecha))
        {
            total += (long)Math.Max(0, slot.MinutosTrabajoComputables) * pesoFeriado;
        }

        if (slot.IndiceDia is 5 or 6)
        {
            total += (long)Math.Max(0, slot.MinutosTrabajoComputables) * pesoFinSemana;
        }

        return total;
    }

    private static bool PuedeCubrirGrupo(Domain.Empleado empleado, string grupoId)
    {
        if (string.IsNullOrWhiteSpace(grupoId))
        {
            return true;
        }

        return string.Equals(empleado.GrupoPrimarioId, grupoId, StringComparison.OrdinalIgnoreCase)
               || empleado.GruposSecundariosIds.Contains(grupoId);
    }

    private static long ObtenerPuntosRecargoAcumuladosPrevios(
        ContextoModeloCp contexto,
        Domain.EstadoResolucionSemanal? estadoAcumulado,
        string empleadoId)
    {
        if (estadoAcumulado is null)
        {
            return 0;
        }

        var reglas = contexto.Problema.Reglas.Configurables;
        var pesoNocturno = Math.Max(0, reglas.PesoRecargoNocturnoPorcentaje);
        var pesoFeriado = Math.Max(0, reglas.PesoRecargoFeriadoPorcentaje);
        var pesoFinSemana = Math.Max(0, reglas.PesoRecargoFinSemanaPorcentaje);

        var nocturnos = estadoAcumulado.MinutosNocturnosAcumuladosPorEmpleado.TryGetValue(empleadoId, out var previosNocturnos)
            ? (long)Math.Max(0, previosNocturnos) * pesoNocturno
            : 0;
        var feriados = estadoAcumulado.MinutosFeriadoAcumuladosPorEmpleado.TryGetValue(empleadoId, out var previosFeriados)
            ? (long)Math.Max(0, previosFeriados) * pesoFeriado
            : 0;
        var finesSemana = estadoAcumulado.MinutosFinSemanaAcumuladosPorEmpleado.TryGetValue(empleadoId, out var previosFinesSemana)
            ? (long)Math.Max(0, previosFinesSemana) * pesoFinSemana
            : 0;

        return nocturnos + feriados + finesSemana;
    }
}
