using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Constraints;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Objectives;

/// <summary>
/// Crea una variable de penalización que cuenta el número total de transiciones en las que
/// el descanso entre dos turnos consecutivos de un mismo empleado es de exactamente 7 horas
/// (≥7 h y &lt;8 h). Minimizar este conteo reduce los descansos cortos que, aunque se permiten
/// dentro de ciertos presupuestos, resultan subóptimos para el bienestar del empleado.
/// Solo se activa cuando la política <see cref="Domain.PoliticasConfigurablesEquipo.PenalizarDescansos7Horas"/>
/// está habilitada.
/// </summary>
public static class ObjetivoMinimizarDescansos7Horas
{
    /// <summary>
    /// Crea la variable de penalización del número de descansos de exactamente 7 horas.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    /// <returns>
    /// Variable entera de penalización, o <see langword="null"/> si la política no está habilitada
    /// o no se detectan pares de slots con descanso de 7 horas.
    /// </returns>
    public static IntVar? CrearPenalizacion(ContextoModeloCp contexto)
    {
        if (!contexto.Problema.Reglas.Configurables.PenalizarDescansos7Horas)
        {
            return null;
        }

        var indicadores = CalculadoraDescanso7Horas.CrearIndicadores(contexto, "obj_descanso7h");
        if (indicadores.Todos.Count == 0)
        {
            return null;
        }

        var penalizacion = contexto.Modelo.NewIntVar(0, indicadores.Todos.Count, "penalizacion_descanso7h");
        contexto.Modelo.Add(penalizacion == LinearExpr.Sum(indicadores.Todos.ToArray()));
        return penalizacion;
    }
}
