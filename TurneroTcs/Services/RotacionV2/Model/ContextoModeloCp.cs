using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Model;

/// <summary>
/// Contexto completo del modelo de optimización por satisfacción de restricciones para una ventana semanal.
/// Agrupa el problema de entrada, el modelo interno del motor, las variables de decisión
/// y los índices de acceso rápido necesarios durante la aplicación de restricciones y objetivos.
/// </summary>
public sealed class ContextoModeloCp
{
    /// <summary>Definición del problema de rotación que este modelo debe resolver.</summary>
    public required ProblemaRotacion Problema { get; init; }

    /// <summary>Modelo interno del motor de optimización al que se añaden restricciones y la función objetivo.</summary>
    public required CpModel Modelo { get; init; }

    /// <summary>Repositorio de variables de decisión registradas en el modelo.</summary>
    public required VariablesDecision Variables { get; init; }

    /// <summary>Índice de acceso rápido de empleados por identificador, con comparación insensible a mayúsculas.</summary>
    public required Dictionary<string, Empleado> EmpleadoPorId { get; init; }

    /// <summary>Índice de acceso rápido de slots de turno por identificador.</summary>
    public required Dictionary<string, SlotTurno> SlotPorId { get; init; }

    /// <summary>
    /// Estado acumulado de semanas anteriores que se inyecta al modelo para garantizar
    /// la continuidad de restricciones cross-semana.
    /// <see langword="null"/> cuando se resuelve la primera semana o en resolución no secuencial.
    /// </summary>
    public EstadoResolucionSemanal? EstadoSemanalAcumulado { get; init; }

    /// <summary>
    /// Acceso conveniente a la variable binaria de asignación del par (empleado, slot).
    /// </summary>
    /// <param name="empleadoId">Identificador del empleado.</param>
    /// <param name="slotId">Identificador del slot de turno.</param>
    /// <returns>Variable binaria del motor que vale 1 si el empleado es asignado al slot.</returns>
    public BoolVar ObtenerVariableAsignacion(string empleadoId, string slotId)
    {
        return Variables.ObtenerAsignacion(empleadoId, slotId);
    }
}
