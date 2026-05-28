using System.ComponentModel.DataAnnotations;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para la creación y edición de un tipo de turno.
/// Define el nombre y el rango horario que caracteriza al turno.
/// </summary>
public class TipoTurnoViewModel
{
    /// <summary>
    /// Nombre descriptivo del tipo de turno (por ejemplo, <c>"Mañana"</c>, <c>"Tarde"</c>, <c>"Nocturno"</c>).
    /// Solo admite letras y espacios, con un rango de 2 a 50 caracteres.
    /// </summary>
    [Required(ErrorMessage = "El nombre del tipo de turno es obligatorio.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "El nombre del turno debe tener entre 2 y 50 caracteres.")]
    [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "El nombre solo puede contener letras.")]
    [Display(Name = "Nombre de Turno")]
    public string NombreTurno { get; set; } = string.Empty;

    /// <summary>Hora de inicio del turno.</summary>
    [Required]
    [DataType(DataType.Time)]
    [Display(Name = "Hora de Inicio")]
    public TimeOnly HoraInicio { get; set; }

    /// <summary>Hora de fin del turno. Puede ser menor a <see cref="HoraInicio"/> cuando el turno cruza la medianoche.</summary>
    [Required]
    [DataType(DataType.Time)]
    [Display(Name = "Hora de Fin")]
    public TimeOnly HoraFin { get; set; }
}
