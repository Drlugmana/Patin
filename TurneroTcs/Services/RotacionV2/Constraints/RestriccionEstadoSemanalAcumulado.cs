using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Proyecta el estado acumulado de semanas anteriores sobre el modelo de la semana actual,
/// cerrando el hueco de continuidad que existe entre semanas resueltas de forma independiente.
/// <para>
/// Aplica cinco tipos de restricciones cross-semana:
/// <list type="number">
///   <item>Bloqueo de fin de semana para empleados que ya alcanzaron el máximo de fines de semana consecutivos.</item>
///   <item>Bloqueo de turno nocturno el lunes para empleados que trabajaron nocturno el domingo anterior.</item>
///   <item>Respeto del descanso mínimo desde el último turno de la semana anterior.</item>
///   <item>Aplicación del límite mensual acumulado de slots de fin de semana.</item>
///   <item>Aplicación del límite mensual acumulado de turnos nocturnos.</item>
/// </list>
/// </para>
/// </summary>
public static class RestriccionEstadoSemanalAcumulado
{
    /// <summary>
    /// Aplica al modelo todas las restricciones derivadas del estado acumulado de semanas anteriores.
    /// </summary>
    /// <param name="contexto">Contexto del modelo de la semana actual.</param>
    /// <param name="estado">Estado acumulado desde el inicio del horizonte hasta la semana anterior.</param>
    public static void Aplicar(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        AplicarBloqueoFinSemanaConsecutivo(contexto, estado);
        AplicarBloqueoNocturnoConsecutivoEntreSemanas(contexto, estado);
        AplicarDescansoMinimoDesdeSemanaAnterior(contexto, estado);
        AplicarLimiteSlotsFinSemanaAcumulados(contexto, estado);
        AplicarLimiteTurnosNocturnosAcumulados(contexto, estado);
    }

    private static void AplicarBloqueoFinSemanaConsecutivo(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        if (!contexto.Problema.Reglas.Configurables.EvitarFinesSemanaConsecutivos)
        {
            return;
        }

        var maximoConsecutivos = Math.Max(1, contexto.Problema.Reglas.Configurables.MaximoFinesSemanaConsecutivos);
        var slotsFinSemana = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == 0 && slot.IndiceDia is 5 or 6)
            .ToArray();

        foreach (var empleado in contexto.Problema.Empleados)
        {
            estado.RachaFinesSemanaConsecutivosPorEmpleado.TryGetValue(empleado.Id, out var rachaActual);
            if (rachaActual < maximoConsecutivos)
            {
                AplicarPrefijoRachaFinSemana(contexto, empleado.Id, empleado.Numero, rachaActual, maximoConsecutivos);
                continue;
            }

            foreach (var slot in slotsFinSemana)
            {
                contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
            }
        }
    }

    private static void AplicarBloqueoNocturnoConsecutivoEntreSemanas(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        if (estado.EmpleadosConNocturnoUltimoDiaAnterior.Count == 0)
        {
            return;
        }

        var slotsNocturnosPrimerDia = contexto.Problema.Slots
            .Where(slot => slot.IndiceSemana == 0 && slot.IndiceDia == 0 && slot.EsTurnoNocturno)
            .ToArray();

        foreach (var empleadoId in estado.EmpleadosConNocturnoUltimoDiaAnterior)
        {
            foreach (var slot in slotsNocturnosPrimerDia)
            {
                contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleadoId, slot.Id) == 0);
            }
        }
    }

    private static void AplicarDescansoMinimoDesdeSemanaAnterior(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        var descansoMinimo = TimeSpan.FromMinutes(contexto.Problema.Reglas.Obligatorias.MinutosMinimosDescansoEntreTurnos);
        if (descansoMinimo <= TimeSpan.Zero)
        {
            return;
        }

        foreach (var (empleadoId, ultimoFinTurno) in estado.UltimoFinTurnoPorEmpleado)
        {
            foreach (var slot in contexto.Problema.Slots)
            {
                if (slot.InicioLocal - ultimoFinTurno < descansoMinimo)
                {
                    contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleadoId, slot.Id) == 0);
                }
            }
        }
    }

    private static void AplicarLimiteTurnosNocturnosAcumulados(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        var maximoTurnosNocturnosPorMes = contexto.Problema.Reglas.Configurables.MaximoTurnosNocturnosPorMes;
        if (maximoTurnosNocturnosPorMes is null || maximoTurnosNocturnosPorMes <= 0)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            var slotsNocturnosPorMes = contexto.Problema.Slots
                .Where(slot => slot.EsTurnoNocturno)
                .GroupBy(slot => new { slot.Fecha.Year, slot.Fecha.Month });

            foreach (var grupoMes in slotsNocturnosPorMes)
            {
                var clave = (empleado.Id, grupoMes.Key.Year, grupoMes.Key.Month);
                estado.TurnosNocturnosPorEmpleadoMes.TryGetValue(clave, out var usados);

                if (usados >= maximoTurnosNocturnosPorMes)
                {
                    foreach (var slot in grupoMes)
                    {
                        contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
                    }

                    continue;
                }

                var restantes = maximoTurnosNocturnosPorMes.Value - usados;
                var variablesMes = grupoMes
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                if (variablesMes.Length > 0)
                {
                    contexto.Modelo.Add(LinearExpr.Sum(variablesMes) <= restantes);
                }
            }
        }
    }

    private static void AplicarLimiteSlotsFinSemanaAcumulados(ContextoModeloCp contexto, EstadoResolucionSemanal estado)
    {
        if (contexto.Problema.Reglas.Configurables.MaximoSlotsFinSemanaPorMes is null or <= 0)
        {
            return;
        }

        foreach (var empleado in contexto.Problema.Empleados)
        {
            var slotsFinSemanaPorMes = contexto.Problema.Slots
                .Where(slot => slot.IndiceDia is 5 or 6)
                .GroupBy(slot => new { slot.Fecha.Year, slot.Fecha.Month });

            foreach (var grupoMes in slotsFinSemanaPorMes)
            {
                var maximoSlotsFinSemanaMes = CalculadoraLimiteSlotsFinSemanaMensual.ObtenerMaximoSlotsFinSemanaPorMes(
                    contexto.Problema,
                    grupoMes.Key.Year,
                    grupoMes.Key.Month);
                if (maximoSlotsFinSemanaMes is null or <= 0)
                {
                    continue;
                }

                var clave = (empleado.Id, grupoMes.Key.Year, grupoMes.Key.Month);
                estado.SlotsFinSemanaPorEmpleadoMes.TryGetValue(clave, out var usados);

                if (usados >= maximoSlotsFinSemanaMes)
                {
                    foreach (var slot in grupoMes)
                    {
                        contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
                    }

                    continue;
                }

                var restantes = maximoSlotsFinSemanaMes.Value - usados;
                var variablesMes = grupoMes
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                if (variablesMes.Length > 0)
                {
                    contexto.Modelo.Add(LinearExpr.Sum(variablesMes) <= restantes);
                }
            }
        }
    }

    private static void AplicarPrefijoRachaFinSemana(
        ContextoModeloCp contexto,
        string empleadoId,
        int numeroEmpleado,
        int rachaActual,
        int maximoConsecutivos)
    {
        if (rachaActual <= 0)
        {
            return;
        }

        var semanasAdicionalesPermitidas = maximoConsecutivos - rachaActual;
        var semanasPrefijo = Math.Min(contexto.Problema.CantidadSemanas, semanasAdicionalesPermitidas + 1);
        if (semanasPrefijo <= 1)
        {
            return;
        }

        var trabajoFinSemanaPrefijo = Enumerable.Range(0, semanasPrefijo)
            .Select(indiceSemana => CrearTrabajoFinSemana(contexto, empleadoId, numeroEmpleado, indiceSemana))
            .ToArray();

        contexto.Modelo.Add(LinearExpr.Sum(trabajoFinSemanaPrefijo) <= semanasAdicionalesPermitidas);
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

        var trabajoFinSemana = contexto.Modelo.NewBoolVar($"trabajo_fin_semana_prefijo_{numeroEmpleado}_{indiceSemana}");
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
