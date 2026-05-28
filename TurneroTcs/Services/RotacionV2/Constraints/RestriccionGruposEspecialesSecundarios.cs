using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Model;

namespace TurneroTcs.Services.RotacionV2.Constraints;

/// <summary>
/// Gestiona la elegibilidad y rotación forzada de empleados en grupos especiales secundarios.
/// <para>
/// Un grupo especial es un grupo al que solo pueden acceder empleados de un grupo fuente específico
/// que además tienen ese grupo en su lista de grupos secundarios. Esta restricción aplica dos reglas:
/// <list type="number">
///   <item>
///     <b>Elegibilidad</b>: solo los empleados elegibles pueden ser asignados a slots del grupo especial.
///   </item>
///   <item>
///     <b>Persona única por semana</b>: cuando el grupo está marcado como de persona única,
///     exactamente un empleado elegible diferente cubre el grupo cada semana,
///     evitando que la misma persona sea asignada en semanas consecutivas.
///   </item>
/// </list>
/// </para>
/// </summary>
public static class RestriccionGruposEspecialesSecundarios
{
    /// <summary>
    /// Registra en el modelo las restricciones de elegibilidad y rotación de persona única
    /// para todos los grupos especiales configurados en las reglas del equipo.
    /// </summary>
    /// <param name="contexto">Contexto del modelo con el problema y las variables de decisión.</param>
    public static void Aplicar(ContextoModeloCp contexto)
    {
        var reglas = contexto.Problema.Reglas.Configurables;
        if (reglas.GrupoFuentePorGrupoEspecial.Count == 0)
        {
            return;
        }

        foreach (var (grupoEspecialId, grupoFuenteId) in reglas.GrupoFuentePorGrupoEspecial)
        {
            AplicarElegibilidadFuente(contexto, grupoEspecialId, grupoFuenteId);

            if (reglas.GruposEspecialesPersonaUnicaPorSemana.Contains(grupoEspecialId))
            {
                AplicarPersonaUnicaPorSemana(contexto, grupoEspecialId, grupoFuenteId);
            }
        }
    }

    private static void AplicarElegibilidadFuente(ContextoModeloCp contexto, string grupoEspecialId, string grupoFuenteId)
    {
        var slotsEspeciales = contexto.Problema.Slots
            .Where(slot => string.Equals(slot.GrupoId, grupoEspecialId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var empleado in contexto.Problema.Empleados)
        {
            var esElegible = EsElegible(empleado, grupoEspecialId, grupoFuenteId);
            if (esElegible)
            {
                continue;
            }

            foreach (var slot in slotsEspeciales)
            {
                contexto.Modelo.Add(contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id) == 0);
            }
        }
    }

    private static void AplicarPersonaUnicaPorSemana(ContextoModeloCp contexto, string grupoEspecialId, string grupoFuenteId)
    {
        var empleadosElegibles = contexto.Problema.Empleados
            .Where(empleado => EsElegible(empleado, grupoEspecialId, grupoFuenteId))
            .ToArray();

        if (empleadosElegibles.Length == 0)
        {
            return;
        }

        var usoPorEmpleadoSemana = new Dictionary<(string EmpleadoId, int Semana), BoolVar>();
        var usadosEnCicloPrevio = contexto.EstadoSemanalAcumulado is not null &&
                                  contexto.EstadoSemanalAcumulado.EmpleadosGrupoEspecialCicloActual.TryGetValue(grupoEspecialId, out var usadosPrevios)
            ? new HashSet<string>(usadosPrevios, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hayPendientesEnCiclo = empleadosElegibles.Any(empleado => !usadosEnCicloPrevio.Contains(empleado.Id));

        for (var indiceSemana = 0; indiceSemana < contexto.Problema.CantidadSemanas; indiceSemana++)
        {
            var slotsEspecialesSemana = contexto.Problema.Slots
                .Where(slot => slot.IndiceSemana == indiceSemana &&
                               slot.EmpleadosRequeridos > 0 &&
                               string.Equals(slot.GrupoId, grupoEspecialId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (slotsEspecialesSemana.Length == 0)
            {
                continue;
            }

            var indicadoresSemana = new List<BoolVar>();
            foreach (var empleado in empleadosElegibles)
            {
                var indicador = contexto.Modelo.NewBoolVar($"grupo_especial_{grupoEspecialId}_{empleado.Numero}_{indiceSemana}");
                var variablesEspeciales = slotsEspecialesSemana
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();

                contexto.Modelo.Add(LinearExpr.Sum(variablesEspeciales) >= 1).OnlyEnforceIf(indicador);
                contexto.Modelo.Add(LinearExpr.Sum(variablesEspeciales) == 0).OnlyEnforceIf(indicador.Not());

                var slotsFuenteSemana = contexto.Problema.Slots
                    .Where(slot => slot.IndiceSemana == indiceSemana &&
                                   string.Equals(slot.GrupoId, grupoFuenteId, StringComparison.OrdinalIgnoreCase))
                    .Select(slot => contexto.ObtenerVariableAsignacion(empleado.Id, slot.Id))
                    .ToArray();
                if (slotsFuenteSemana.Length > 0)
                {
                    contexto.Modelo.Add(LinearExpr.Sum(slotsFuenteSemana) == 0).OnlyEnforceIf(indicador);
                }

                indicadoresSemana.Add(indicador);
                usoPorEmpleadoSemana[(empleado.Id, indiceSemana)] = indicador;
            }

            if (indiceSemana == 0 && hayPendientesEnCiclo)
            {
                foreach (var empleado in empleadosElegibles.Where(empleado => usadosEnCicloPrevio.Contains(empleado.Id)))
                {
                    contexto.Modelo.Add(usoPorEmpleadoSemana[(empleado.Id, indiceSemana)] == 0);
                }
            }

            contexto.Modelo.Add(LinearExpr.Sum(indicadoresSemana) == 1);

            if (indiceSemana == 0 &&
                empleadosElegibles.Length > 1 &&
                contexto.EstadoSemanalAcumulado?.EmpleadosGrupoEspecialSemanaAnterior.TryGetValue(grupoEspecialId, out var empleadosPrevios) == true)
            {
                foreach (var empleado in empleadosElegibles.Where(empleado => empleadosPrevios.Contains(empleado.Id)))
                {
                    contexto.Modelo.Add(usoPorEmpleadoSemana[(empleado.Id, indiceSemana)] == 0);
                }
            }
        }

        if (empleadosElegibles.Length <= 1)
        {
            return;
        }

        for (var indiceSemana = 0; indiceSemana < contexto.Problema.CantidadSemanas - 1; indiceSemana++)
        {
            foreach (var empleado in empleadosElegibles)
            {
                if (usoPorEmpleadoSemana.TryGetValue((empleado.Id, indiceSemana), out var actual) &&
                    usoPorEmpleadoSemana.TryGetValue((empleado.Id, indiceSemana + 1), out var siguiente))
                {
                    contexto.Modelo.Add(actual + siguiente <= 1);
                }
            }
        }
    }

    private static bool EsElegible(Domain.Empleado empleado, string grupoEspecialId, string grupoFuenteId)
    {
        return string.Equals(empleado.GrupoPrimarioId, grupoFuenteId, StringComparison.OrdinalIgnoreCase) &&
               empleado.GruposSecundariosIds.Contains(grupoEspecialId);
    }
}
