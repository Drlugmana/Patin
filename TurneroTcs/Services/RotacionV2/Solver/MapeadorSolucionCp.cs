using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Solver;

/// <summary>
/// Traduce el resultado devuelto por el motor de optimización al modelo de dominio de la aplicación.
/// Recorre las variables binarias de asignación, extrae las que tomaron valor 1 y construye
/// la lista de asignaciones junto con métricas de cobertura.
/// </summary>
public sealed class MapeadorSolucionCp
{
    /// <summary>
    /// Convierte el estado del motor y los valores de sus variables en una <see cref="SolucionRotacionCp"/> de dominio.
    /// </summary>
    /// <param name="contexto">Contexto del modelo que contiene el problema y las variables de decisión.</param>
    /// <param name="solver">Instancia del motor después de ejecutar la resolución.</param>
    /// <param name="estadoSolver">Estado final reportado por el motor al terminar la búsqueda.</param>
    /// <returns>
    /// Solución de dominio con el estado de resolución, las asignaciones extraídas y las métricas de cobertura.
    /// Si el estado no es óptimo ni factible, devuelve una solución vacía con el estado correspondiente.
    /// </returns>
    public SolucionRotacionCp Mapear(ContextoModeloCp contexto, CpSolver solver, CpSolverStatus estadoSolver)
    {
        var estado = MapearEstado(estadoSolver);
        if (estado is not EstadoSolucionRotacion.Optima and not EstadoSolucionRotacion.Factible)
        {
            return new SolucionRotacionCp
            {
                Estado = estado,
                DetalleEstado = estadoSolver.ToString()
            };
        }

        var asignaciones = contexto.Variables.EnumerarTodas()
            .Where(par => solver.Value(par.Value) == 1)
            .Select(par => new AsignacionSlot
            {
                IdSlot = par.Key.SlotId,
                EmpleadoId = par.Key.EmpleadoId
            })
            .ToList();

        var asignacionesPorSlot = asignaciones
            .GroupBy(asignacion => asignacion.IdSlot)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());

        var demandaTotal = contexto.Problema.Slots.Sum(slot => slot.EmpleadosRequeridos);
        var totalAsignaciones = asignaciones.Count;
        var faltantes = contexto.Problema.Slots.Sum(slot =>
        {
            asignacionesPorSlot.TryGetValue(slot.Id, out var asignados);
            return Math.Max(0, slot.EmpleadosRequeridos - asignados);
        });

        var metricas = new MetricasSolucionRotacion
        {
            SlotsAsignados = totalAsignaciones,
            SlotsSinAsignar = demandaTotal > totalAsignaciones ? faltantes : 0,
            AsignacionesAuxiliares = asignaciones.Count(asignacion => contexto.SlotPorId[asignacion.IdSlot].EsAuxiliar),
            AsignacionesNocturnas = asignaciones.Count(asignacion => contexto.SlotPorId[asignacion.IdSlot].EsTurnoNocturno)
        };

        return new SolucionRotacionCp
        {
            Estado = estado,
            DetalleEstado = estadoSolver.ToString(),
            Asignaciones = asignaciones,
            Metricas = metricas
        };
    }

    /// <summary>
    /// Traduce el estado interno del motor al enumerado de dominio <see cref="EstadoSolucionRotacion"/>.
    /// </summary>
    private static EstadoSolucionRotacion MapearEstado(CpSolverStatus estadoSolver)
    {
        return estadoSolver switch
        {
            CpSolverStatus.Optimal => EstadoSolucionRotacion.Optima,
            CpSolverStatus.Feasible => EstadoSolucionRotacion.Factible,
            CpSolverStatus.Infeasible => EstadoSolucionRotacion.Infactible,
            CpSolverStatus.ModelInvalid => EstadoSolucionRotacion.ModeloInvalido,
            CpSolverStatus.Unknown => EstadoSolucionRotacion.NoResuelta,
            _ => EstadoSolucionRotacion.Error
        };
    }
}
