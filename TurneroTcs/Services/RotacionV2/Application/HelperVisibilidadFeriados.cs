using TurneroTcs.Services.RotacionV2.Domain;

namespace TurneroTcs.Services.RotacionV2.Application;

/// <summary>
/// Configura qué asignaciones de la solución son visibles en la presentación de feriados laborables
/// para un equipo. Define los criterios de cobertura y cuántas personas se muestran por regla.
/// </summary>
public sealed record ConfiguracionVisibilidadFeriado
{
    /// <summary>Identificador del equipo al que aplica esta configuración.</summary>
    public required string EquipoId { get; init; }

    /// <summary>Lista de reglas de cobertura que determinan qué asignaciones se hacen visibles.</summary>
    public List<CoberturaVisibilidadFeriado> Coberturas { get; init; } = [];
}

/// <summary>
/// Regla de cobertura que filtra asignaciones por tipo de turno y grupo, y controla
/// cuántas personas se muestran en la vista pública del feriado.
/// </summary>
public sealed record CoberturaVisibilidadFeriado
{
    /// <summary>Identificador único de la regla de cobertura.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// Tipos de turno que aplican a esta regla.
    /// Si está vacío, aplica a todos los tipos de turno.
    /// </summary>
    public HashSet<string> TiposTurnoIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Grupos incluidos en esta regla.
    /// Si está vacío, aplica a todos los grupos.
    /// </summary>
    public HashSet<string> GruposIncluidos { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Número máximo de personas que se muestran como visibles para esta regla en cada feriado.</summary>
    public int PersonasVisibles { get; init; } = 1;
}

/// <summary>
/// Asignación de un empleado que ha sido seleccionada como visible en la vista pública de un feriado.
/// </summary>
public sealed record AsignacionVisibleFeriado
{
    /// <summary>Índice de la semana dentro del horizonte en la que cae el feriado.</summary>
    public required int IndiceSemana { get; init; }

    /// <summary>Fecha del feriado.</summary>
    public required DateOnly Fecha { get; init; }

    /// <summary>Identificador de la regla de cobertura que seleccionó esta asignación.</summary>
    public required string CoberturaId { get; init; }

    /// <summary>Identificador del slot de turno de la asignación visible.</summary>
    public required string IdSlot { get; init; }

    /// <summary>Identificador del empleado asignado.</summary>
    public required string EmpleadoId { get; init; }

    /// <summary>Nombre del empleado asignado.</summary>
    public required string NombreEmpleado { get; init; }

    /// <summary>Número de orden del empleado dentro del grupo.</summary>
    public required int NumeroEmpleado { get; init; }

    /// <summary>Identificador del tipo de turno del slot visible.</summary>
    public required string TipoTurnoId { get; init; }

    /// <summary>Código corto del turno visible.</summary>
    public required string CodigoTurno { get; init; }
}

/// <summary>
/// Registro de una asignación que fue ocultada de la vista pública del feriado
/// porque otra asignación del mismo empleado ya fue seleccionada por una regla de cobertura.
/// </summary>
public sealed record OcultamientoFeriado
{
    /// <summary>Índice de la semana del feriado.</summary>
    public required int IndiceSemana { get; init; }

    /// <summary>Fecha del feriado.</summary>
    public required DateOnly Fecha { get; init; }

    /// <summary>Identificador del empleado ocultado.</summary>
    public required string EmpleadoId { get; init; }

    /// <summary>Nombre del empleado ocultado.</summary>
    public required string NombreEmpleado { get; init; }

    /// <summary>Número de orden del empleado ocultado.</summary>
    public required int NumeroEmpleado { get; init; }

    /// <summary>Identificador del tipo de turno del slot ocultado.</summary>
    public required string TipoTurnoId { get; init; }

    /// <summary>Código corto del turno ocultado.</summary>
    public required string CodigoTurno { get; init; }

    /// <summary>Identificador del grupo al que pertenece el slot ocultado.</summary>
    public required string GrupoSlot { get; init; }
}

/// <summary>
/// Resultado del cálculo de visibilidad de feriados, que indica qué asignaciones
/// son públicamente visibles y cuáles fueron ocultadas por las reglas de cobertura.
/// </summary>
public sealed record ResultadoVisibilidadFeriados
{
    /// <summary>Conjunto de pares (SlotId, EmpleadoId) cuyas asignaciones son visibles en la vista pública.</summary>
    public HashSet<(string SlotId, string EmpleadoId)> AsignacionesVisibles { get; init; } = [];

    /// <summary>Asignaciones ocultadas agrupadas por índice de semana.</summary>
    public Dictionary<int, List<OcultamientoFeriado>> OcultamientosPorSemana { get; init; } = [];

    /// <summary>Asignaciones visibles seleccionadas, agrupadas por fecha de feriado.</summary>
    public Dictionary<DateOnly, List<AsignacionVisibleFeriado>> VisiblesPorFecha { get; init; } = [];

    /// <summary>
    /// Indica si la asignación identificada por (slotId, empleadoId) es visible en la vista pública.
    /// </summary>
    public bool DebeMostrar(string slotId, string empleadoId)
    {
        return AsignacionesVisibles.Contains((slotId, empleadoId));
    }
}

/// <summary>
/// Calcula la visibilidad pública de asignaciones en días feriados laborables,
/// aplicando las reglas de cobertura para seleccionar qué empleados aparecen en la vista pública
/// y registrando los ocultamientos para trazabilidad.
/// <para>
/// La selección de empleados visibles sigue el criterio de menor exposición acumulada previa
/// (empleados que han aparecido menos veces en feriados previos tienen prioridad) y evita
/// seleccionar el mismo empleado que fue visible en el feriado inmediatamente anterior.
/// </para>
/// </summary>
public static class HelperVisibilidadFeriados
{
    /// <summary>
    /// Calcula el resultado de visibilidad para todos los feriados laborables de la solución.
    /// Si no hay configuración de cobertura o no hay feriados en la solución, devuelve
    /// un resultado donde todas las asignaciones son visibles.
    /// </summary>
    /// <param name="problema">Problema de rotación con los slots y la lista de feriados.</param>
    /// <param name="solucion">Solución con las asignaciones obtenidas por el motor de optimización.</param>
    /// <param name="configuracion">
    /// Configuración de cobertura con las reglas de visibilidad.
    /// <see langword="null"/> para mostrar todas las asignaciones sin filtrar.
    /// </param>
    /// <returns>Resultado con las asignaciones visibles, ocultas y los detalles por fecha.</returns>
    public static ResultadoVisibilidadFeriados Calcular(
        ProblemaRotacion problema,
        SolucionRotacionCp solucion,
        ConfiguracionVisibilidadFeriado? configuracion)
    {
        ArgumentNullException.ThrowIfNull(problema);
        ArgumentNullException.ThrowIfNull(solucion);

        var asignacionesVisibles = solucion.Asignaciones
            .Select(asignacion => (asignacion.IdSlot, asignacion.EmpleadoId))
            .ToHashSet();

        var resultado = new ResultadoVisibilidadFeriados
        {
            AsignacionesVisibles = asignacionesVisibles,
            OcultamientosPorSemana = [],
            VisiblesPorFecha = []
        };

        if (configuracion is null || configuracion.Coberturas.Count == 0 || problema.Feriados.Count == 0)
        {
            return resultado;
        }

        var slotPorId = problema.Slots.ToDictionary(slot => slot.Id);
        var empleadoPorId = problema.Empleados.ToDictionary(empleado => empleado.Id);

        var asignacionesPorFecha = solucion.Asignaciones
            .Select(asignacion => new AsignacionContexto(
                asignacion,
                slotPorId[asignacion.IdSlot],
                empleadoPorId[asignacion.EmpleadoId]))
            .GroupBy(item => item.Slot.Fecha)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.ToList());

        var conteoVisiblePorEmpleado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var empleadosFeriadoAnterior = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fecha in problema.Feriados
                     .Where(EsFeriadoLaborable)
                     .Where(asignacionesPorFecha.ContainsKey)
                     .OrderBy(fecha => fecha))
        {
            var asignacionesFecha = asignacionesPorFecha[fecha];
            foreach (var item in asignacionesFecha)
            {
                resultado.AsignacionesVisibles.Remove((item.Asignacion.IdSlot, item.Asignacion.EmpleadoId));
            }

            var visiblesFecha = new List<AsignacionVisibleFeriado>();
            var empleadosSeleccionadosFecha = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var cobertura in configuracion.Coberturas)
            {
                var candidatos = asignacionesFecha
                    .Where(item => CoincideCobertura(item.Slot, cobertura))
                    .Where(item => !empleadosSeleccionadosFecha.Contains(item.Asignacion.EmpleadoId))
                    .OrderBy(item => empleadosFeriadoAnterior.Contains(item.Asignacion.EmpleadoId) ? 1 : 0)
                    .ThenBy(item => conteoVisiblePorEmpleado.TryGetValue(item.Asignacion.EmpleadoId, out var conteo) ? conteo : 0)
                    .ThenBy(item => CoincideGrupoPrimario(item.Empleado, item.Slot) ? 0 : 1)
                    .ThenBy(item => item.Empleado.Numero)
                    .ThenBy(item => item.Slot.GrupoId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Slot.Id, StringComparer.OrdinalIgnoreCase)
                    .Take(Math.Max(0, cobertura.PersonasVisibles))
                    .ToArray();

                foreach (var candidato in candidatos)
                {
                    resultado.AsignacionesVisibles.Add((candidato.Asignacion.IdSlot, candidato.Asignacion.EmpleadoId));
                    empleadosSeleccionadosFecha.Add(candidato.Asignacion.EmpleadoId);
                    visiblesFecha.Add(new AsignacionVisibleFeriado
                    {
                        IndiceSemana = candidato.Slot.IndiceSemana,
                        Fecha = fecha,
                        CoberturaId = cobertura.Id,
                        IdSlot = candidato.Asignacion.IdSlot,
                        EmpleadoId = candidato.Asignacion.EmpleadoId,
                        NombreEmpleado = candidato.Empleado.Nombre,
                        NumeroEmpleado = candidato.Empleado.Numero,
                        TipoTurnoId = candidato.Slot.TipoTurnoId,
                        CodigoTurno = candidato.Slot.CodigoTurno
                    });
                }
            }

            foreach (var visible in visiblesFecha)
            {
                conteoVisiblePorEmpleado[visible.EmpleadoId] = conteoVisiblePorEmpleado.TryGetValue(visible.EmpleadoId, out var conteo)
                    ? conteo + 1
                    : 1;
            }

            resultado.VisiblesPorFecha[fecha] = visiblesFecha
                .OrderBy(item => item.CoberturaId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.NumeroEmpleado)
                .ToList();

            var visiblesSet = visiblesFecha
                .Select(item => (item.IdSlot, item.EmpleadoId))
                .ToHashSet();

            foreach (var ocultado in asignacionesFecha
                         .Where(item => !visiblesSet.Contains((item.Asignacion.IdSlot, item.Asignacion.EmpleadoId)))
                         .Select(item => new OcultamientoFeriado
                         {
                             IndiceSemana = item.Slot.IndiceSemana,
                             Fecha = item.Slot.Fecha,
                             EmpleadoId = item.Asignacion.EmpleadoId,
                             NombreEmpleado = item.Empleado.Nombre,
                             NumeroEmpleado = item.Empleado.Numero,
                             TipoTurnoId = item.Slot.TipoTurnoId,
                             CodigoTurno = item.Slot.CodigoTurno,
                             GrupoSlot = item.Slot.GrupoId
                         })
                         .OrderBy(item => item.Fecha)
                         .ThenBy(item => item.NumeroEmpleado))
            {
                if (!resultado.OcultamientosPorSemana.TryGetValue(ocultado.IndiceSemana, out var ocultadosSemana))
                {
                    ocultadosSemana = [];
                    resultado.OcultamientosPorSemana[ocultado.IndiceSemana] = ocultadosSemana;
                }

                ocultadosSemana.Add(ocultado);
            }

            empleadosFeriadoAnterior = visiblesFecha
                .Select(item => item.EmpleadoId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return resultado;
    }

    private static bool CoincideCobertura(SlotTurno slot, CoberturaVisibilidadFeriado cobertura)
    {
        if (cobertura.TiposTurnoIds.Count > 0 && !cobertura.TiposTurnoIds.Contains(slot.TipoTurnoId))
        {
            return false;
        }

        if (cobertura.GruposIncluidos.Count > 0 && !cobertura.GruposIncluidos.Contains(slot.GrupoId))
        {
            return false;
        }

        return true;
    }

    private static bool CoincideGrupoPrimario(Empleado empleado, SlotTurno slot)
    {
        if (string.IsNullOrWhiteSpace(slot.GrupoId))
        {
            return true;
        }

        return string.Equals(empleado.GrupoPrimarioId, slot.GrupoId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EsFeriadoLaborable(DateOnly fecha)
    {
        return fecha.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }

    private sealed record AsignacionContexto(AsignacionSlot Asignacion, SlotTurno Slot, Empleado Empleado);
}
