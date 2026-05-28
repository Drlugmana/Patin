using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Calcula el crédito de días feriados que reduce el objetivo semanal de turnos de cada empleado.
/// Solo los feriados que caen en días laborables (lunes a viernes) generan crédito,
/// ya que los feriados de fin de semana no afectan el cómputo semanal normal.
/// </summary>
internal static class CalculadoraCreditoFeriado
{
    /// <summary>
    /// Calcula cuántos días de crédito por feriado laborable tiene una semana del horizonte.
    /// Este valor se resta del objetivo base de turnos para reducir la carga semanal de cada empleado.
    /// </summary>
    /// <param name="problema">Problema de rotación que contiene el conjunto de feriados.</param>
    /// <param name="indiceSemana">Índice de la semana a evaluar (base cero).</param>
    /// <returns>Número de días feriados laborables que caen dentro de la semana indicada.</returns>
    public static int CalcularDiasCreditoSemana(ProblemaRotacion problema, int indiceSemana)
    {
        var fechaInicioSemana = problema.FechaInicio.AddDays(indiceSemana * 7);
        var fechaFinSemana = fechaInicioSemana.AddDays(6);

        return problema.Feriados.Count(fecha =>
            fecha >= fechaInicioSemana &&
            fecha <= fechaFinSemana &&
            EsFeriadoLaborable(fecha));
    }

    /// <summary>
    /// Indica si una fecha es un feriado que cae en día laborable (lunes a viernes).
    /// </summary>
    /// <param name="problema">Problema de rotación con el conjunto de feriados.</param>
    /// <param name="fecha">Fecha a verificar.</param>
    /// <returns>
    /// <see langword="true"/> si la fecha está marcada como feriado y corresponde a un día de lunes a viernes.
    /// </returns>
    public static bool EsFeriadoLaborable(ProblemaRotacion problema, DateOnly fecha)
    {
        return problema.Feriados.Contains(fecha) && EsFeriadoLaborable(fecha);
    }

    private static bool EsFeriadoLaborable(DateOnly fecha)
    {
        return fecha.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }
}
