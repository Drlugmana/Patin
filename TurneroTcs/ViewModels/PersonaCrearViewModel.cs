using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para el formulario de creación de una nueva persona en el sistema.
/// Incluye los datos personales, credenciales de acceso y asignación de rol, equipo y grupos.
/// </summary>
public class PersonaCrearViewModel
{
    /// <summary>Primer nombre de la persona. Solo letras y espacios, entre 2 y 50 caracteres.</summary>
    [Required(ErrorMessage = "El primer nombre es obligatorio.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 50 caracteres.")]
    [RegularExpression(@"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ\s]+$", ErrorMessage = "El nombre solo puede contener letras.")]
    [Display(Name = "Primer Nombre")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Segundo nombre de la persona. Opcional; solo letras y espacios, máximo 50 caracteres.</summary>
    [StringLength(50, ErrorMessage = "El segundo nombre no puede exceder 50 caracteres.")]
    [RegularExpression(@"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ\s]*$", ErrorMessage = "El segundo nombre solo puede contener letras.")]
    [Display(Name = "Segundo Nombre")]
    public string? SegundoNombre { get; set; }

    /// <summary>Apellido paterno de la persona. Solo letras y espacios, entre 2 y 50 caracteres.</summary>
    [Required(ErrorMessage = "El apellido paterno es obligatorio.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 50 caracteres.")]
    [RegularExpression(@"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ\s]+$", ErrorMessage = "El apellido solo puede contener letras.")]
    [Display(Name = "Apellido Paterno")]
    public string Apellido { get; set; } = string.Empty;

    /// <summary>Apellido materno de la persona. Opcional; solo letras y espacios, máximo 50 caracteres.</summary>
    [StringLength(50, ErrorMessage = "El apellido materno no puede exceder 50 caracteres.")]
    [RegularExpression(@"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ\s]*$", ErrorMessage = "El apellido materno solo puede contener letras.")]
    [Display(Name = "Apellido Materno")]
    public string? SegundoApellido { get; set; }

    /// <summary>Número Ultimatix del empleado. Debe ser exactamente 7 dígitos numéricos.</summary>
    [Required(ErrorMessage = "El número Ultimatix es obligatorio.")]
    [StringLength(7, MinimumLength = 7, ErrorMessage = "El número Ultimatix debe tener exactamente 7 caracteres.")]
    [RegularExpression(@"^\d{7}$", ErrorMessage = "El Ultimatix debe contener solo 7 dígitos numéricos.")]
    [Display(Name = "Número Ultimatix")]
    public string Ultimatix { get; set; } = string.Empty;

    /// <summary>Color hexadecimal de identificación visual del usuario en la interfaz de calendario. Opcional.</summary>
    [Display(Name = "Color de usuario")]
    public string? ColorUsuario { get; set; }

    /// <summary>Contraseña de acceso al sistema. Mínimo 15 caracteres.</summary>
    [Required(ErrorMessage = "Ingresa una contrasena valida.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    [MinLength(15, ErrorMessage = "La contraseña dee tener 15 caractares min.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Confirmación de la contraseña. Debe coincidir exactamente con <see cref="Password"/>.</summary>
    [Required(ErrorMessage = "Ingresa una contrasena valida.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
    [Display(Name = "Confirmar Contraseña")]
    public string ConfirmarPassword { get; set; } = string.Empty;

    /// <summary>Nombre del rol de ASP.NET Identity asignado al usuario.</summary>
    [Required(ErrorMessage = "El rol es obligatorio.")]
    [Display(Name = "Rol de usuario")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Lista de roles disponibles para poblar el selector en el formulario.</summary>
    public IEnumerable<SelectListItem> Roles { get; set; } = Enumerable.Empty<SelectListItem>();

    /// <summary>Identificador del equipo al que se asigna la persona. Opcional.</summary>
    [Display(Name = "Nombre de equipo")]
    public string? EquipoId { get; set; }

    /// <summary>Lista de equipos disponibles para poblar el selector en el formulario.</summary>
    public IEnumerable<SelectListItem> Equipos { get; set; } = Enumerable.Empty<SelectListItem>();

    /// <summary>Identificadores de los grupos primarios seleccionados para la persona.</summary>
    [Display(Name = "Grupos")]
    public List<string> Grupos { get; set; } = new();

    /// <summary>Lista de grupos disponibles para poblar el selector múltiple en el formulario.</summary>
    public IEnumerable<SelectListItem> GruposNombres { get; set; } = Enumerable.Empty<SelectListItem>();
}
