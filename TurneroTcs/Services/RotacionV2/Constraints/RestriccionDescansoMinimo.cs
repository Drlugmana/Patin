using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Garantiza que ningún empleado trabaje en dos turnos cuyo intervalo de descanso sea inferior
/// al mínimo obligatorio configurado en las reglas del equipo.
/// <para>
/// Para cada par de slots que violarían el descanso mínimo, se añade una restricción que impide
/// que el mismo empleado sea asignado a ambos simultáneamente.
/// Adicionalmente, si las reglas incluyen un presupuesto de descansos de 7 horas
/// (máximo global o por empleado), se crean los indicadores correspondientes y se imponen
/// sus límites de uso en el modelo.
/// </para>
/// </summary>
public static class RestriccionDescansoMinimo
{
    /// <summary>
    /// Aplica al modelo las restricciones de descanso mínimo entre turnos y,
    /// si corresponde, el presupuesto de descansos de exactamente 7 horas.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        var minimoDescanso = TimeSpan.FromMinutes(contexto.Problema.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos);
        if (minimoDescanso <= TimeSpan.Zero)
        {
            return;
        }

        var slotsOrdenados = contexto.Problema.Slots
            .OrderBy(slot => slot.InicioLocal)
            .ToArray();

        for (var i = 0; i < slotsOrdenados.Length; i++)
        {
            var slotActual = slotsOrdenados[i];
            for (var j = i + 1; j < slotsOrdenados.Length; j++)
            {
                var slotSiguiente = slotsOrdenados[j];
                if (!ViolaDescansoMinimo(slotActual, slotSiguiente, minimoDescanso))
                {
                    continue;
                }

                foreach (var empleado in contexto.Problema.Empleados)
                {
                    var variableA = contexto.ObtenerVariableAsignacion(empleado.Id, slotActual.Id);
                    var variableB = contexto.ObtenerVariableAsignacion(empleado.Id, slotSiguiente.Id);
                    contexto.Modelo.Add(variableA + variableB <= 1);
                }
            }
        }

        AplicarPresupuestoDescanso7Horas(contexto);
    }

    private static void AplicarPresupuestoDescanso7Horas(ContextoModeloCp contexto)
    {
        var maximoGlobal = contexto.Problema.Reglas.Configurables.MaximoDescansos7HorasGlobal;
        var maximoPorEmpleado = contexto.Problema.Reglas.Configurables.MaximoDescansos7HorasPorEmpleado;
        if ((maximoGlobal is null || maximoGlobal < 0) && (maximoPorEmpleado is null || maximoPorEmpleado < 0))
        {
            return;
        }

        var indicadores = CalculadoraDescanso7Horas.CrearIndicadores(contexto, "limite_descanso7h");
        var estado = contexto.EstadoSemanalAcumulado;

        if (maximoGlobal is >= 0)
        {
            var usadosGlobal = estado?.Descansos7HorasAcumuladosTotal ?? 0;
            contexto.Modelo.Add(usadosGlobal + LinearExpr.Sum(indicadores.Todos.ToArray()) <= maximoGlobal.Value);
        }

        if (maximoPorEmpleado is >= 0)
        {
            foreach (var empleado in contexto.Problema.Empleados)
            {
                var usados = estado is not null &&
                             estado.Descansos7HorasAcumuladosPorEmpleado.TryGetValue(empleado.Id, out var previos)
                    ? previos
                    : 0;

                contexto.Modelo.Add(usados + LinearExpr.Sum(indicadores.PorEmpleado[empleado.Id].ToArray()) <= maximoPorEmpleado.Value);
            }
        }
    }

    private static bool ViolaDescansoMinimo(Domain.SlotTurno slotA, Domain.SlotTurno slotB, TimeSpan minimoDescanso)
    {
        if (slotB.InicioLocal < slotA.FinLocal)
        {
            return true;
        }

        var descansoReal = slotB.InicioLocal - slotA.FinLocal;
        return descansoReal < minimoDescanso;
    }
}
