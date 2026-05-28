using Microsoft.AspNetCore.Mvc.Rendering;
using TurneroTcs.Models;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para la página de configuración de planificación semanal de un equipo.
/// Expone la estructura de grupos, tipos de turno y planificaciones existentes
/// necesarias para renderizar la grilla de planificación por día de la semana.
/// </summary>
public class PlanificacionViewModel
{
    /// <summary>Identificador del equipo cuya planificación se está configurando.</summary>
    public string EquipoId { get; set; } = string.Empty;

    /// <summary>Nombre del equipo, para mostrar en el encabezado de la vista.</summary>
    public string EquipoNombre { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de generación de turnos del equipo (por ejemplo, <c>"Rotacion"</c>, <c>"Manual"</c>).
    /// Determina qué controles se muestran en la interfaz de planificación.
    /// </summary>
    public string TipoGeneracion { get; set; } = "Rotacion";

    /// <summary>Identificador del grupo actualmente seleccionado para editar su planificación.</summary>
    public string GrupoIdSeleccionado { get; set; } = string.Empty;

    /// <summary>Total de registros de turno generados para el equipo en el período vigente.</summary>
    public int RegistroTurnosCount { get; set; }

    /// <summary>Total de personas-semana asignadas en la planificación del equipo.</summary>
    public int TotalPersonasSemanaEquipo { get; set; }

    /// <summary>Lista de grupos del equipo disponibles para el selector de grupo.</summary>
    public IEnumerable<SelectListItem> Grupos { get; set; } = Enumerable.Empty<SelectListItem>();

    /// <summary>Tipos de turno disponibles para asignar en la grilla de planificación.</summary>
    public IReadOnlyList<TipoTurno> TipoTurnos { get; set; } = new List<TipoTurno>();

    /// <summary>Planificaciones existentes del grupo seleccionado, organizadas por día y tipo de turno.</summary>
    public IReadOnlyList<Planificacion> Planificaciones { get; set; } = new List<Planificacion>();

    /// <summary>
    /// Nombres de los días de la semana en el orden utilizado por la grilla de planificación.
    /// El valor <c>"Miercoles"</c> está sin tilde para compatibilidad con los datos almacenados.
    /// </summary>
    public static readonly string[] DiasSemana = { "Lunes", "Martes", "Miercoles", "Jueves", "Viernes", "Sabado", "Domingo" };
}
