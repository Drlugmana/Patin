using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Calcula el límite efectivo de slots de fin de semana que un empleado puede acumular en un mes,
/// ajustando el límite base cuando el mes tiene cinco fines de semana completos
/// para evitar penalizar injustamente a quienes trabajan en esos meses más largos.
/// </summary>
internal static class CalculadoraLimiteSlotsFinSemanaMensual
{
    /// <summary>
    /// Valor mínimo garantizado del límite cuando un mes contiene cinco fines de semana completos,
    /// independientemente del límite base configurado.
    /// </summary>
    private const int MaximoSlotsMesConCincoFinesDeSemana = 6;

    /// <summary>
    /// Devuelve el límite efectivo de slots de fin de semana permitidos para un mes y año dados,
    /// elevando el límite base si el mes tiene cinco fines de semana completos.
    /// </summary>
    /// <param name="problema">Problema de rotación con las reglas configurables del equipo.</param>
    /// <param name="anio">Año del mes a evaluar.</param>
    /// <param name="mes">Mes a evaluar (1–12).</param>
    /// <returns>
    /// El límite efectivo de slots de fin de semana, o <see langword="null"/> si no aplica restricción mensual.
    /// </returns>
    public static int? ObtenerMaximoSlotsFinSemanaPorMes(ProblemaRotacion problema, int anio, int mes)
    {
        var maximoBase = problema.Reglas.Configurables.MaximoSlotsFinSemanaPorMes;
        if (maximoBase is null || maximoBase <= 0)
        {
            return maximoBase;
        }

        if (!TieneCincoFinesDeSemanaCompletos(anio, mes))
        {
            return maximoBase.Value;
        }

        return Math.Max(maximoBase.Value, MaximoSlotsMesConCincoFinesDeSemana);
    }

    /// <summary>
    /// Determina si un mes tiene al menos cinco fines de semana completos (sábado + domingo dentro del mismo mes).
    /// </summary>
    /// <param name="anio">Año del mes a evaluar.</param>
    /// <param name="mes">Mes a evaluar (1–12).</param>
    /// <returns><see langword="true"/> si el mes contiene cinco o más fines de semana completos.</returns>
    public static bool TieneCincoFinesDeSemanaCompletos(int anio, int mes)
    {
        var diasMes = DateTime.DaysInMonth(anio, mes);
        var finesDeSemanaCompletos = 0;

        for (var dia = 1; dia < diasMes; dia++)
        {
            var fecha = new DateOnly(anio, mes, dia);
            if (fecha.DayOfWeek != DayOfWeek.Saturday)
            {
                continue;
            }

            if (fecha.AddDays(1).Month == mes)
            {
                finesDeSemanaCompletos++;
            }
        }

        return finesDeSemanaCompletos >= 5;
    }
}
