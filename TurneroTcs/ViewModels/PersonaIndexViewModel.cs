using Microsoft.AspNetCore.Mvc.Rendering;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para la página de listado de personas.
/// Contiene la lista de personas y los filtros de búsqueda aplicados.
/// </summary>
public class PersonaIndexViewModel
{
    /// <summary>Lista de personas que coinciden con los filtros aplicados.</summary>
    public IReadOnlyList<PersonaListViewModel> Personas { get; set; } = Array.Empty<PersonaListViewModel>();

    /// <summary>Lista de equipos disponibles para el filtro de equipo.</summary>
    public IEnumerable<SelectListItem> Equipos { get; set; } = Enumerable.Empty<SelectListItem>();

    /// <summary>Lista de grupos disponibles para el filtro de grupo.</summary>
    public IEnumerable<SelectListItem> Grupos { get; set; } = Enumerable.Empty<SelectListItem>();

    /// <summary>Texto de búsqueda libre ingresado por el usuario; <see langword="null"/> si no se aplicó búsqueda.</summary>
    public string? Search { get; set; }

    /// <summary>Identificador del equipo seleccionado como filtro; <see langword="null"/> si no se filtra por equipo.</summary>
    public string? EquipoId { get; set; }

    /// <summary>Identificador del grupo seleccionado como filtro; <see langword="null"/> si no se filtra por grupo.</summary>
    public string? GrupoId { get; set; }
}
