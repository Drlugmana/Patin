using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Penaliza cuando un mismo empleado trabaja fines de semana consecutivos.
/// Considera tanto pares consecutivos dentro de la ventana actual como el cruce
/// con la semana inmediatamente anterior ya resuelta.
/// </summary>
public static class ObjetivoEvitarFinesSemanaConsecutivos
{
    /// <summary>
    /// Crea una variable de penalizacion que cuenta los pares de fines de semana consecutivos trabajados.
    /// </summary>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        if (contexto.Problema.Reglas.Configurables.NivelEvitarFinesSemanaConsecutivos == NivelEvitarFinesSemanaConsecutivos.NoUsar)
        {
            return null;
        }

        if (contexto.Problema.CantidadSemanas <= 0 || contexto.Problema.Empleados.Count == 0)
        {
            return null;
        }

        var indicadores = new List<BoolVar>();
        foreach (var empleado in contexto.Problema.Empleados)
        {
            var trabajoFinSemanaPorSemana = Enumerable.Range(0, contexto.Problema.CantidadSemanas)
                .Select(indiceSemana => CrearTrabajoFinSemana(contexto, empleado.Id, empleado.Numero, indiceSemana))
                .ToArray();

            if (contexto.EstadoSemanalAcumulado?.EmpleadosConFinSemanaAnterior.Contains(empleado.Id) == true &&
                trabajoFinSemanaPorSemana.Length > 0)
            {
                indicadores.Add(trabajoFinSemanaPorSemana[0]);
            }

            for (var indiceSemana = 1; indiceSemana < trabajoFinSemanaPorSemana.Length; indiceSemana++)
            {
                indicadores.Add(CrearIndicadorParConsecutivo(
                    contexto,
                    empleado.Numero,
                    indiceSemana,
                    trabajoFinSemanaPorSemana[indiceSemana - 1],
                    trabajoFinSemanaPorSemana[indiceSemana]));
            }
        }

        if (indicadores.Count == 0)
        {
            return null;
        }

        var penalizacion = contexto.Modelo.NewIntVar(0, indicadores.Count, "penalizacion_fines_semana_consecutivos");
        contexto.Modelo.Add(penalizacion == LinearExpr.Sum(indicadores));
        return penalizacion;
    }

    private static BoolVar CrearTrabajoFinSemana(
        ContextoModeloCp contexto,
        string empleadoId,
        int numeroEmpleado,
        int indiceSemana)
    {
        var slotsFinSemana = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == indiceSemana && slot.IndiceDia is 5 or 6)
            .Select(slot => contexto.ObtenerVariableAsignacion(empleadoId, slot.Id))
            .ToArray();

        var trabajoFinSemana = contexto.Modelo.NewBoolVar($"obj_trabajo_fin_semana_{numeroEmpleado}_{indiceSemana}");
        if (slotsFinSemana.Length == 0)
        {
            contexto.Modelo.Add(trabajoFinSemana == 0);
            return trabajoFinSemana;
        }

        contexto.Modelo.Add(LinearExpr.Sum(slotsFinSemana) >= 1).OnlyEnforceIf(trabajoFinSemana);
        contexto.Modelo.Add(LinearExpr.Sum(slotsFinSemana) == 0).OnlyEnforceIf(trabajoFinSemana.Not());
        return trabajoFinSemana;
    }

    private static BoolVar CrearIndicadorParConsecutivo(
        ContextoModeloCp contexto,
        int numeroEmpleado,
        int indiceSemana,
        BoolVar semanaAnterior,
        BoolVar semanaActual)
    {
        var indicador = contexto.Modelo.NewBoolVar($"obj_fds_consecutivos_{numeroEmpleado}_{indiceSemana}");
        contexto.Modelo.Add(indicador <= semanaAnterior);
        contexto.Modelo.Add(indicador <= semanaActual);
        contexto.Modelo.Add(indicador + 1 >= semanaAnterior + semanaActual);
        return indicador;
    }
}
