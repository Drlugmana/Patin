using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Agrupación de variables indicadoras de descanso de exactamente 7 horas,
/// organizada como colección global y por empleado para uso en restricciones y objetivos.
/// </summary>
internal sealed record IndicadoresDescanso7Horas(
    IReadOnlyList<BoolVar> Todos,
    IReadOnlyDictionary<string, IReadOnlyList<BoolVar>> PorEmpleado);

/// <summary>
/// Crea variables indicadoras binarias que detectan cuándo dos turnos consecutivos de un mismo empleado
/// tienen un descanso de exactamente 7 horas (≥7 h y &lt;8 h) entre ellos.
/// Estas variables son consumidas por la restricción de presupuesto de descansos cortos
/// y por el objetivo de minimización de descansos de 7 horas.
/// </summary>
internal static class CalculadoraDescanso7Horas
{
    private static readonly TimeSpan Descanso7Horas = TimeSpan.FromHours(7);
    private static readonly TimeSpan Descanso8Horas = TimeSpan.FromHours(8);

    /// <summary>
    /// Crea y registra en el modelo las variables indicadoras de descanso de 7 horas
    /// para todos los pares de slots que cumplen el criterio, incluyendo la transición
    /// desde el último turno de la semana anterior (si existe estado acumulado).
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema, el modelo interno y el estado acumulado.</param>
    /// <param name="prefijo">Prefijo de nombre usado para las variables internas del motor, para evitar colisiones.</param>
    /// <returns>
    /// Objeto con la lista global de indicadores y el subconjunto de indicadores por empleado.
    /// </returns>
    public static IndicadoresDescanso7Horas CrearIndicadores(ContextoModeloCp contexto, string prefijo)
    {
        var todos = new List<BoolVar>();
        var porEmpleado = contexto.Problema.Empleados
            .ToDictionary(
                empleado => empleado.Id,
                _ => new List<BoolVar>(),
                StringComparer.OrdinalIgnoreCase);

        var slotsOrdenados = contexto.Problema.Slots
            .OrderBy(slot => slot.InicioLocal)
            .ToArray();

        for (var i = 0; i < slotsOrdenados.Length; i++)
        {
            var slotActual = slotsOrdenados[i];
            for (var j = i + 1; j < slotsOrdenados.Length; j++)
            {
                var slotSiguiente = slotsOrdenados[j];
                if (!EsDescanso7Horas(slotActual.FinLocal, slotSiguiente.InicioLocal))
                {
                    continue;
                }

                foreach (var empleado in contexto.Problema.Empleados)
                {
                    var variableA = contexto.ObtenerVariableAsignacion(empleado.Id, slotActual.Id);
                    var variableB = contexto.ObtenerVariableAsignacion(empleado.Id, slotSiguiente.Id);
                    var indicador = contexto.Modelo.NewBoolVar($"{prefijo}_{empleado.Numero}_{slotActual.Id}_{slotSiguiente.Id}");

                    contexto.Modelo.Add(indicador <= variableA);
                    contexto.Modelo.Add(indicador <= variableB);
                    contexto.Modelo.Add(indicador >= variableA + variableB - 1);

                    todos.Add(indicador);
                    porEmpleado[empleado.Id].Add(indicador);
                }
            }
        }

        AgregarIndicadoresDesdeEstadoPrevio(contexto, prefijo, todos, porEmpleado);

        return new IndicadoresDescanso7Horas(
            todos,
            porEmpleado.ToDictionary(
                item => item.Key,
                item => (IReadOnlyList<BoolVar>)item.Value,
                StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determina si el intervalo entre el fin de un turno y el inicio del siguiente
    /// cae en la ventana de descanso corto (≥7 horas y &lt;8 horas).
    /// </summary>
    /// <param name="finTurnoAnterior">Fecha y hora de fin del primer turno.</param>
    /// <param name="inicioTurnoSiguiente">Fecha y hora de inicio del segundo turno.</param>
    /// <returns><see langword="true"/> si el descanso entre ambos turnos está en la ventana [7h, 8h).</returns>
    public static bool EsDescanso7Horas(DateTime finTurnoAnterior, DateTime inicioTurnoSiguiente)
    {
        if (inicioTurnoSiguiente < finTurnoAnterior)
        {
            return false;
        }

        var descanso = inicioTurnoSiguiente - finTurnoAnterior;
        return descanso >= Descanso7Horas && descanso < Descanso8Horas;
    }

    private static void AgregarIndicadoresDesdeEstadoPrevio(
        ContextoModeloCp contexto,
        string prefijo,
        List<BoolVar> todos,
        Dictionary<string, List<BoolVar>> porEmpleado)
    {
        var estado = contexto.EstadoSemanalAcumulado;
        if (estado is null || estado.UltimoFinTurnoPorEmpleado.Count == 0)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            if (!estado.UltimoFinTurnoPorEmpleado.TryGetValue(empleado.Id, out var ultimoFinTurno))
            {
                continue;
            }

            foreach (var slot in contexto.Problema.Slots.Where(slot => EsDescanso7Horas(ultimoFinTurno, slot.InicioLocal)))
            {
                var variable = contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id);
                var indicador = contexto.Modelo.NewBoolVar($"{prefijo}_prev_{empleado.Numero}_{slot.Id}");

                contexto.Modelo.Add(indicador == variable);

                todos.Add(indicador);
                porEmpleado[empleado.Id].Add(indicador);
            }
        }
    }
}
