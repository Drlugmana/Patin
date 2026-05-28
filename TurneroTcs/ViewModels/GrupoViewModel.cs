using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para la creación y edición de un grupo de trabajo.
/// Un grupo pertenece a un equipo y agrupa personas para la planificación de turnos.
/// </summary>
public class GrupoViewModel
{
    /// <summary>
    /// Nombre del grupo. Debe tener entre 2 y 50 caracteres.
    /// </summary>
    [Required(ErrorMessage = "El nombre del grupo es obligatorio.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "El nombre del grupo debe tener entre 2 y 50 caracteres.")]
    [Display(Name = "Nombre del grupo")]
    public string NombreGrupo { get; set; } = string.Empty;

    /// <summary>Identificador del equipo al que pertenece el grupo.</summary>
    [Required(ErrorMessage = "Selecciona un equipo.")]
    [Display(Name = "Equipo")]
    public string EquipoId { get; set; } = string.Empty;

    /// <summary>Lista de equipos disponibles para poblar el selector en el formulario.</summary>
    public IEnumerable<SelectListItem> Equipos { get; set; } = Enumerable.Empty<SelectListItem>();
}
