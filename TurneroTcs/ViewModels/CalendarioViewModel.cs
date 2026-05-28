using Microsoft.AspNetCore.Mvc.Rendering;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para la página principal del calendario de turnos.
/// Expone los filtros de selección, la lista de personas disponibles,
/// los tipos de turno y los permisos de acción del usuario actual.
/// </summary>
public class CalendarioViewModel
{
    /// <summary>Identificador del equipo actualmente seleccionado en el filtro del calendario; <see langword="null"/> si no hay selección.</summary>
    public string? SelectedEquipoId { get; set; }

    /// <summary>Identificador de la persona autenticada, usado para determinar la vista personalizada del calendario.</summary>
    public string? CurrentPersonaId { get; set; }

    /// <summary>Indica si el usuario tiene permiso para crear nuevos registros de turno.</summary>
    public bool CanCreateTurno { get; set; }

    /// <summary>Indica si el usuario tiene permiso para editar registros de turno existentes.</summary>
    public bool CanEditTurno { get; set; }

    /// <summary>Indica si el usuario tiene permiso para eliminar registros de turno.</summary>
    public bool CanDeleteTurno { get; set; }

    /// <summary>Indica si el usuario tiene permiso para crear solicitudes de cambio de turno.</summary>
    public bool CanCreateCambioTurno { get; set; }

    /// <summary>Indica si el usuario tiene permiso para crear solicitudes de permiso de ausencia.</summary>
    public bool CanCreatePermiso { get; set; }

    /// <summary>Indica si el usuario tiene permiso para crear solicitudes de calamidad doméstica.</summary>
    public bool CanCreateCalamidad { get; set; }

    /// <summary>Lista de equipos disponibles para poblar el selector de filtro.</summary>
    public IEnumerable<SelectListItem> Equipos { get; set; } = Enumerable.Empty<SelectListItem>();

    /// <summary>Lista de personas del equipo seleccionado, con sus datos de color y grupos para la interfaz.</summary>
    public IEnumerable<CalendarioPersonaItem> Personas { get; set; } = Enumerable.Empty<CalendarioPersonaItem>();

    /// <summary>Lista de grupos disponibles para poblar el selector de filtro por grupo.</summary>
    public IEnumerable<SelectListItem> Grupos { get; set; } = Enumerable.Empty<SelectListItem>();

    /// <summary>Lista de tipos de turno disponibles para los formularios de creación y edición.</summary>
    public IEnumerable<SelectListItem> TiposTurno { get; set; } = Enumerable.Empty<SelectListItem>();
}

/// <summary>
/// Elemento de persona para el selector del calendario, con metadatos de color y grupos
/// necesarios para la renderización visual de la interfaz.
/// </summary>
public class CalendarioPersonaItem
{
    /// <summary>Identificador de la persona (valor del ítem en el selector).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Nombre de la persona (texto visible en el selector).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Color hexadecimal de identificación visual de la persona en el calendario; <see langword="null"/> si no fue configurado.</summary>
    public string? Color { get; set; }

    /// <summary>Identificadores de los grupos a los que pertenece la persona, usados para filtrar slots en la vista.</summary>
    public IEnumerable<string> GrupoIds { get; set; } = Enumerable.Empty<string>();
}
