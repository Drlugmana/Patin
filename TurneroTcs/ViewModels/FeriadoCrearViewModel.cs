using System.ComponentModel.DataAnnotations;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para el formulario de creacion de un feriado.
/// Implementa <see cref="IValidatableObject"/> para validar que la fecha de fin
/// no sea anterior a la fecha de inicio.
/// </summary>
public class FeriadoCrearViewModel : IValidatableObject
{
    /// <summary>Nombre descriptivo del feriado. Maximo 120 caracteres.</summary>
    [Required(ErrorMessage = "El nombre del feriado es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre del feriado no puede exceder 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string NombreFeriado { get; set; } = string.Empty;

    /// <summary>Fecha de inicio del feriado. Por defecto se inicializa con la fecha actual.</summary>
    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Inicio")]
    public DateOnly InicioFeriado { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Fecha de fin del feriado. Debe ser mayor o igual a <see cref="InicioFeriado"/>.</summary>
    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fin")]
    public DateOnly FinFeriado { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// Valida que la fecha de fin no sea anterior a la fecha de inicio.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FinFeriado < InicioFeriado)
        {
            yield return new ValidationResult(
                "La fecha fin no puede ser menor a la fecha inicio.",
                new[] { nameof(FinFeriado) });
        }
    }
}
