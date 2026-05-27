using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Constraints;
using TurneroTcs.Services.RotacionV2.Domain;
using TurneroTcs.Services.RotacionV2.Objectives;

namespace TurneroTcs.Services.RotacionV2.Model;

/// <summary>
/// Construye el modelo completo de optimización por satisfacción de restricciones
/// para un problema de rotación de turnos.
/// <para>
/// El proceso consiste en crear una variable binaria por cada par (empleado, slot),
/// aplicar todas las restricciones duras y, opcionalmente, registrar la función objetivo
/// de balanceo. Si se proveen asignaciones previas como sugerencia inicial, se inyectan
/// como pistas al motor para acelerar la búsqueda de solución factible.
/// </para>
/// </summary>
public sealed class ConstructorModeloCp
{
    /// <summary>
    /// Construye y devuelve el contexto del modelo de optimización listo para ser entregado al motor de resolución.
    /// </summary>
    /// <param name="problema">Problema de rotación que define empleados, slots, ausencias y reglas.</param>
    /// <param name="incluirObjetivo">
    /// Cuando <see langword="true"/>, se registra la función objetivo de balanceo en el modelo.
    /// Cuando <see langword="false"/>, el motor solo busca una solución factible sin optimizar.
    /// </param>
    /// <param name="sugerenciaInicial">
    /// Asignaciones previas que se inyectan como pistas al motor para guiar la búsqueda
    /// hacia una zona prometedora del espacio de soluciones. Puede ser <see langword="null"/>.
    /// </param>
    /// <param name="estadoSemanalAcumulado">
    /// Estado acumulado de semanas anteriores que alimenta las restricciones cross-semana.
    /// <see langword="null"/> para la primera semana o en resolución no secuencial.
    /// </param>
    /// <returns>Contexto del modelo con todas las restricciones y variables configuradas.</returns>
    public ContextoModeloCp Construir(
        ProblemaRotacion problema,
        bool incluirObjetivo = true,
        IReadOnlyCollection<Domain.AsignacionSlot>? sugerenciaInicial = null,
        EstadoResolucionSemanal? estadoSemanalAcumulado = null)
    {
        ArgumentNullException.ThrowIfNull(problema);

        var modelo = new CpModel();
        var variables = new VariablesDecision();

        foreach (var empleado in problema.Empleados)
        {
            foreach (var slot in problema.Slots)
            {
                var variable = modelo.NewBoolVar($"asignacion_{empleado.Numero}_{slot.Id}");
                variables.RegistrarAsignacion(empleado.Id, slot.Id, variable);
            }
        }

        var contexto = new ContextoModeloCp
        {
            Problema = problema,
            Modelo = modelo,
            Variables = variables,
            EmpleadoPorId = problema.Empleados.ToDictionary(empleado => empleado.Id),
            SlotPorId = problema.Slots.ToDictionary(slot => slot.Id),
            EstadoSemanalAcumulado = estadoSemanalAcumulado
        };

        RestriccionCobertura.Aplicar(contexto);
        RestriccionCapacidadAuxiliarCompartida.Aplicar(contexto);
        RestriccionUnTurnoPorDia.Aplicar(contexto);
        RestriccionVacaciones.Aplicar(contexto);
        RestriccionGruposPermitidos.Aplicar(contexto);
        RestriccionGruposEspecialesSecundarios.Aplicar(contexto);
        RestriccionDescansoMinimo.Aplicar(contexto);
        RestriccionHorasSemanales.Aplicar(contexto);
        RestriccionConteoTurnosSemanalExacto.Aplicar(contexto);
        RestriccionAuxiliaresSemanalesExactos.Aplicar(contexto);
        RestriccionDosDiasConsecutivosDescanso.Aplicar(contexto);
        RestriccionDescansoPosteriorVacacionSemanal.Aplicar(contexto);
        RestriccionFinesSemanaConsecutivos.Aplicar(contexto);
        RestriccionVeladasConsecutivasEntreSemanas.Aplicar(contexto);
        RestriccionMaximoSlotsFinSemanaPorMes.Aplicar(contexto);
        RestriccionMaximoTurnosNocturnosPorMes.Aplicar(contexto);
        RestriccionMaximoTurnosNocturnosPorSemana.Aplicar(contexto);

        if (sugerenciaInicial is not null && sugerenciaInicial.Count > 0)
        {
            AplicarSugerenciaInicial(contexto, sugerenciaInicial);
        }

        if (incluirObjetivo)
        {
            ConstructorObjetivoRotacion.Aplicar(contexto);
        }

        return contexto;
    }

    /// <summary>
    /// Inyecta pistas de solución al motor a partir de asignaciones previas conocidas,
    /// inicializando cada variable binaria con el valor que indica si esa asignación estaba presente.
    /// </summary>
    private static void AplicarSugerenciaInicial(ContextoModeloCp contexto, IReadOnlyCollection<Domain.AsignacionSlot> sugerenciaInicial)
    {
        var asignacionesSugeridas = sugerenciaInicial
            .Select(asignacion => (asignacion.EmpleadoId, asignacion.IdSlot))
            .ToHashSet();

        foreach (var variable in contexto.Variables.EnumerarTodas())
        {
            contexto.Modelo.AddHint(
                variable.Value,
                asignacionesSugeridas.Contains((variable.Key.EmpleadoId, variable.Key.SlotId)));
        }
    }
}
