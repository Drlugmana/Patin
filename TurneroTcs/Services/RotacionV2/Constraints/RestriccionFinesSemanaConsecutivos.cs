using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Limita el número de fines de semana consecutivos que un empleado puede trabajar,
/// según el máximo configurado en las políticas del equipo.
/// <para>
/// Para cada empleado se crea una variable indicadora binaria por semana que vale 1
/// si el empleado trabaja al menos un slot en el fin de semana (sábado o domingo) de esa semana.
/// Luego se aplica una restricción de ventana deslizante que impide superar el límite
/// de fines de semana consecutivos en cualquier ventana contigua de tamaño (máximo + 1).
/// </para>
/// </summary>
public static class RestriccionFinesSemanaConsecutivos
{
    /// <summary>
    /// Registra en el modelo la restricción de fines de semana consecutivos para cada empleado.
    /// Si la política <see cref="Domain.PoliticasConfigurablesEquipo.EvitarFinesSemanaConsecutivos"/>
    /// no está habilitada, la restricción no se aplica.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        if (!contexto.Problema.Reglas.Configurables.EvitarFinesSemanaConsecutivos)
        {
            return;
        }

        var maximoConsecutivos = Math.Max(1, contexto.Problema.Reglas.Configurables.MaximoFinesSemanaConsecutivos);

        foreach (var empleado in contexto.Problema.Empleados)
        {
            var trabajoFinSemanaPorSemana = new List<BoolVar>();

            for (var indiceSemana = 0; indiceSemana < contexto.Problema.CantidadSemanas; indiceSemana++)
            {
                trabajoFinSemanaPorSemana.Add(CrearTrabajoFinSemana(contexto, empleado.Id, empleado.Numero, indiceSemana));
            }

            var tamanoVentana = maximoConsecutivos + 1;
            for (var indiceSemana = 0; indiceSemana <= trabajoFinSemanaPorSemana.Count - tamanoVentana; indiceSemana++)
            {
                var ventana = trabajoFinSemanaPorSemana
                    .Skip(indiceSemana)
                    .Take(tamanoVentana)
                    .ToArray();
                contexto.Modelo.Add(LinearExpr.Sum(ventana) <= maximoConsecutivos);
            }
        }
    }

    private static BoolVar CrearTrabajoFinSemana(
        ContextoModeloCp contexto,
        string empleadoId,
        int numeroEmpleado,
        int indiceSemana)
    {
        var slotsFinSemana = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == indiceSemana && (slot.IndiceDia == 5 || slot.IndiceDia == 6))
            .Select(slot => contexto.ObtenerVariableAsignacion(empleadoId, slot.Id))
            .ToArray();

        var trabajoFinSemana = contexto.Modelo.NewBoolVar($"trabajo_fin_semana_{numeroEmpleado}_{indiceSemana}");
        if (slotsFinSemana.Length == 0)
        {
            contexto.Modelo.Add(trabajoFinSemana == 0);
            return trabajoFinSemana;
        }

        contexto.Modelo.Add(LinearExpr.Sum(slotsFinSemana) >= 1).OnlyEnforceIf(trabajoFinSemana);
        contexto.Modelo.Add(LinearExpr.Sum(slotsFinSemana) == 0).OnlyEnforceIf(trabajoFinSemana.Not());
        return trabajoFinSemana;
    }
}
