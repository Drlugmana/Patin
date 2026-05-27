using System.ComponentModel.DataAnnotations;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para la creación y edición de un equipo de trabajo.
/// Contiene las validaciones de presentación requeridas por los formularios de la interfaz.
/// </summary>
public class EquipoViewModel
{
    /// <summary>
    /// Nombre del equipo. Solo admite letras (incluyendo caracteres acentuados y ñ)
    /// y espacios, con un rango de 2 a 50 caracteres.
    /// </summary>
    [Required(ErrorMessage = "El nombre de equipo es obligatorio.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "El nombre del equipo debe tener entre 2 y 50 caracteres.")]
    [RegularExpression(@"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ\s]+$", ErrorMessage = "El nombre solo puede contener letras.")]
    [Display(Name = "Nombre de Equipo")]
    public string NombreEquipo { get; set; } = string.Empty;
}
