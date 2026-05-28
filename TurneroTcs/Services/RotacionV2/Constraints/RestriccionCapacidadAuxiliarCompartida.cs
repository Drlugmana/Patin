using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Limita la capacidad total de asignaciones en slots auxiliares que comparten un pool común entre grupos.
/// <para>
/// Cuando varios slots auxiliares de diferentes grupos tienen la misma
/// <see cref="Domain.SlotTurno.LlaveCompartidaAuxiliar"/> en la misma fecha y semana,
/// el total de empleados asignados a todos ellos no puede superar el límite
/// <see cref="Domain.SlotTurno.MaximoCompartidoAuxiliar"/> del pool.
/// Si la política <see cref="Domain.PoliticasConfigurablesEquipo.PermiteTurnosAuxiliares"/> está deshabilitada,
/// todos los slots auxiliares se fijan en cero.
/// </para>
/// </summary>
public static class RestriccionCapacidadAuxiliarCompartida
{
    /// <summary>
    /// Registra en el modelo la restricción de capacidad compartida para todos los pools de auxiliares.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        var slotsAuxiliares = contexto.Problema.Slots
            .Where(slot => slot.EsAuxiliar)
            .ToArray();

        if (slotsAuxiliares.Length == 0)
        {
            return;
        }

        if (!contexto.Problema.Reglas.Configurables.PermiteTurnosAuxiliares)
        {
            foreach (var slot in slotsAuxiliares)
            {
                var variablesSlot = contexto.Variables.ObtenerAsignacionesPorSlot(contexto.Problema, slot.Id);
                contexto.Modelo.Add(LinearExpr.Sum(variablesSlot) == 0);
            }

            return;
        }

        var gruposCompartidos = slotsAuxiliares
            .Where(slot => !string.IsNullOrWhiteSpace(slot.LlaveCompartidaAuxiliar) && slot.MaximoCompartidoAuxiliar > 0)
            .GroupBy(
                slot => new
                {
                    slot.IndiceSemana,
                    slot.Fecha,
                    Llave = slot.LlaveCompartidaAuxiliar.Trim().ToUpperInvariant()
                });

        foreach (var grupoCompartido in gruposCompartidos)
        {
            var slots = grupoCompartido.ToArray();
            if (slots.Length <= 1)
            {
                continue;
            }

            var capacidadCompartida = slots
                .Where(slot => slot.MaximoCompartidoAuxiliar > 0)
                .Select(slot => slot.MaximoCompartidoAuxiliar)
                .DefaultIfEmpty(0)
                .Min();

            if (capacidadCompartida <= 0)
            {
                continue;
            }

            var variables = slots
                .SelectMany(slot => contexto.Problema.Empleados.Select(empleado => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id)))
                .ToArray();

            contexto.Modelo.Add(LinearExpr.Sum(variables) <= capacidadCompartida);
        }
    }
}
