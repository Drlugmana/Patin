using Google.OrTools.Sat;
using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Model;

/// <summary>
/// Repositorio centralizado de todas las variables de decisión del modelo de optimización.
/// Gestiona tres tipos de variables:
/// <list type="bullet">
///   <item>Variables binarias de asignación (empleado × slot).</item>
///   <item>Variables enteras de apoyo cedido por slot.</item>
///   <item>Variables enteras de faltante de cobertura por slot.</item>
/// </list>
/// </summary>
public sealed class VariablesDecision
{
    private readonly Dictionary<(string EmpleadoId, string SlotId), BoolVar> _asignacionPorEmpleadoSlot = [];
    private readonly Dictionary<string, IntVar> _apoyoCedidoPorSlot = [];
    private readonly Dictionary<string, IntVar> _faltanteCoberturaPorSlot = [];

    /// <summary>
    /// Registra la variable binaria de asignación para el par (empleado, slot) dado.
    /// </summary>
    /// <param name="empleadoId">Identificador del empleado.</param>
    /// <param name="slotId">Identificador del slot de turno.</param>
    /// <param name="variable">Variable binaria del motor que vale 1 si el empleado es asignado al slot.</param>
    public void RegistrarAsignacion(string empleadoId, string slotId, BoolVar variable)
    {
        _asignacionPorEmpleadoSlot[(empleadoId, slotId)] = variable;
    }

    /// <summary>
    /// Devuelve la variable binaria de asignación para el par (empleado, slot).
    /// </summary>
    /// <param name="empleadoId">Identificador del empleado.</param>
    /// <param name="slotId">Identificador del slot.</param>
    /// <returns>Variable binaria que indica si el empleado cubre el slot.</returns>
    public BoolVar ObtenerAsignacion(string empleadoId, string slotId)
    {
        return _asignacionPorEmpleadoSlot[(empleadoId, slotId)];
    }

    /// <summary>
    /// Registra la variable entera de apoyo cedido para un slot que permite ceder empleados a otro slot del grupo.
    /// </summary>
    /// <param name="slotId">Identificador del slot cedente.</param>
    /// <param name="variable">Variable entera que representa los empleados cedidos como apoyo.</param>
    public void RegistrarApoyoCedido(string slotId, IntVar variable)
    {
        _apoyoCedidoPorSlot[slotId] = variable;
    }

    /// <summary>
    /// Devuelve la variable de apoyo cedido para un slot, si existe.
    /// </summary>
    /// <param name="slotId">Identificador del slot.</param>
    /// <returns>La variable de apoyo cedido, o <see langword="null"/> si el slot no tiene apoyo cedible.</returns>
    public IntVar? ObtenerApoyoCedidoOpcion(string slotId)
    {
        return _apoyoCedidoPorSlot.TryGetValue(slotId, out var variable) ? variable : null;
    }

    /// <summary>
    /// Registra la variable entera de faltante de cobertura para un slot opcional por vacación.
    /// </summary>
    /// <param name="slotId">Identificador del slot con cobertura flexible.</param>
    /// <param name="variable">Variable entera que representa el déficit de cobertura permitido.</param>
    public void RegistrarFaltanteCobertura(string slotId, IntVar variable)
    {
        _faltanteCoberturaPorSlot[slotId] = variable;
    }

    /// <summary>
    /// Devuelve la variable de faltante de cobertura para un slot, si existe.
    /// </summary>
    /// <param name="slotId">Identificador del slot.</param>
    /// <returns>La variable de faltante de cobertura, o <see langword="null"/> si no aplica para el slot.</returns>
    public IntVar? ObtenerFaltanteCoberturaOpcion(string slotId)
    {
        return _faltanteCoberturaPorSlot.TryGetValue(slotId, out var variable) ? variable : null;
    }

    /// <summary>
    /// Devuelve el arreglo de variables binarias de asignación de todos los empleados para un slot dado.
    /// El orden del arreglo corresponde al orden de <see cref="ProblemaRotacion.Empleados"/>.
    /// </summary>
    /// <param name="problema">Problema que contiene la lista de empleados.</param>
    /// <param name="slotId">Identificador del slot.</param>
    /// <returns>Arreglo de variables binarias, una por cada empleado.</returns>
    public BoolVar[] ObtenerAsignacionesPorSlot(ProblemaRotacion problema, string slotId)
    {
        return problema.Empleados
            .Select(empleado => ObtenerAsignacion(empleado.Id, slotId))
            .ToArray();
    }

    /// <summary>
    /// Devuelve las variables binarias de asignación de un empleado en todos los slots de una fecha específica.
    /// </summary>
    /// <param name="problema">Problema que contiene los slots.</param>
    /// <param name="empleadoId">Identificador del empleado.</param>
    /// <param name="fecha">Fecha por la que se filtran los slots.</param>
    /// <returns>Arreglo de variables binarias correspondientes a los slots de esa fecha.</returns>
    public BoolVar[] ObtenerAsignacionesPorEmpleadoYFecha(ProblemaRotacion problema, string empleadoId, DateOnly fecha)
    {
        return problema.Slots
            .Where(slot => slot.Fecha == fecha)
            .Select(slot => ObtenerAsignacion(empleadoId, slot.Id))
            .ToArray();
    }

    /// <summary>
    /// Devuelve las variables binarias de asignación de un empleado en todos los slots de una semana específica.
    /// </summary>
    /// <param name="problema">Problema que contiene los slots.</param>
    /// <param name="empleadoId">Identificador del empleado.</param>
    /// <param name="indiceSemana">Índice de semana (base cero) dentro del horizonte.</param>
    /// <returns>Arreglo de variables binarias correspondientes a los slots de esa semana.</returns>
    public BoolVar[] ObtenerAsignacionesPorEmpleadoSemana(ProblemaRotacion problema, string empleadoId, int indiceSemana)
    {
        return problema.Slots
            .Where(slot => slot.IndiceSemana == indiceSemana)
            .Select(slot => ObtenerAsignacion(empleadoId, slot.Id))
            .ToArray();
    }

    /// <summary>
    /// Enumera todos los pares (clave, variable) de asignación registrados en el modelo.
    /// Útil para iterar sobre todas las variables al aplicar sugerencias iniciales o al extraer la solución.
    /// </summary>
    /// <returns>Secuencia de pares con la clave (EmpleadoId, SlotId) y su variable binaria.</returns>
    public IEnumerable<KeyValuePair<(string EmpleadoId, string SlotId), BoolVar>> EnumerarTodas()
    {
        return _asignacionPorEmpleadoSlot;
    }
}
