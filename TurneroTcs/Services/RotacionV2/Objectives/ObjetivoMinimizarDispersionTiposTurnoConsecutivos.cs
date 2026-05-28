using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Penaliza cambios de codigo de turno entre dias consecutivos trabajados por la misma persona.
/// Busca reducir secuencias muy dispersas como noche-manana-tarde-noche cuando el modelo
/// puede mantener bloques mas estables del mismo tipo de turno.
/// </summary>
public static class ObjetivoMinimizarDispersionTiposTurnoConsecutivos
{
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        if (contexto.Problema.Reglas.Configurables.NivelAgruparTiposTurnoConsecutivos == NivelAgruparTiposTurnoConsecutivos.NoUsar)
        {
            return null;
        }

        if (contexto.Problema.Empleados.Count == 0 || contexto.Problema.Slots.Count == 0)
        {
            return null;
        }

        var fechas = contexto.Problema.Slots
            .Select(slot => slot.Fecha)
            .Distinct()
            .OrderBy(fecha => fecha)
            .ToArray();

        if (fechas.Length < 2)
        {
            return null;
        }

        var indicadores = new List<BoolVar>();
        foreach (var empleado in contexto.Problema.Empleados)
        {
            for (var indiceFecha = 1; indiceFecha < fechas.Length; indiceFecha++)
            {
                var fechaAnterior = fechas[indiceFecha - 1];
                var fechaActual = fechas[indiceFecha];
                if (fechaActual != fechaAnterior.AddDays(1))
                {
                    continue;
                }

                var codigosAnterior = ObtenerIndicadoresPorCodigo(contexto, empleado.Id, empleado.Numero, fechaAnterior);
                var codigosActual = ObtenerIndicadoresPorCodigo(contexto, empleado.Id, empleado.Numero, fechaActual);
                if (codigosAnterior.Count == 0 || codigosActual.Count == 0)
                {
                    continue;
                }

                foreach (var codigoAnterior in codigosAnterior)
                {
                    foreach (var codigoActual in codigosActual)
                    {
                        if (string.Equals(codigoAnterior.Key, codigoActual.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var indicador = contexto.Modelo.NewBoolVar(
                            $"obj_dispersion_tipo_turno_{empleado.Numero}_{fechaAnterior:yyyyMMdd}_{codigoAnterior.Key}_{fechaActual:yyyyMMdd}_{codigoActual.Key}");
                        contexto.Modelo.Add(indicador <= codigoAnterior.Value);
                        contexto.Modelo.Add(indicador <= codigoActual.Value);
                        contexto.Modelo.Add(indicador + 1 >= codigoAnterior.Value + codigoActual.Value);
                        indicadores.Add(indicador);
                    }
                }
            }
        }

        if (indicadores.Count == 0)
        {
            return null;
        }

        var penalizacion = contexto.Modelo.NewIntVar(0, indicadores.Count, "penalizacion_dispersion_tipos_turno_consecutivos");
        contexto.Modelo.Add(penalizacion == LinearExpr.Sum(indicadores));
        return penalizacion;
    }

    private static Dictionary<string, BoolVar> ObtenerIndicadoresPorCodigo(
        ContextoModeloCp contexto,
        string empleadoId,
        int numeroEmpleado,
        DateOnly fecha)
    {
        var slotsFecha = contexto.Problema.Slots
            .Where(slot => slot.Fecha == fecha)
            .GroupBy(slot => slot.CodigoTurno, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Select(slot => contexto.ObtenerVariableAsignacion(empleadoId, slot.Id)).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var resultado = new Dictionary<string, BoolVar>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in slotsFecha)
        {
            var indicador = contexto.Modelo.NewBoolVar($"obj_codigo_turno_{numeroEmpleado}_{fecha:yyyyMMdd}_{item.Key}");
            if (item.Value.Length == 0)
            {
                contexto.Modelo.Add(indicador == 0);
                resultado[item.Key] = indicador;
                continue;
            }

            contexto.Modelo.Add(LinearExpr.Sum(item.Value) >= 1).OnlyEnforceIf(indicador);
            contexto.Modelo.Add(LinearExpr.Sum(item.Value) == 0).OnlyEnforceIf(indicador.Not());
            resultado[item.Key] = indicador;
        }

        return resultado;
    }
}