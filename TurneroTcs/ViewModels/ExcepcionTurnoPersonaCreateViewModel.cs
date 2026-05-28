using System.ComponentModel.DataAnnotations;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para crear una nueva excepcion de turnos.
/// </summary>
public class ExcepcionTurnoPersonaCreateViewModel : IValidatableObject
{
    [Required(ErrorMessage = "La persona es obligatoria.")]
    [Display(Name = "Persona")]
    public string PersonaId { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo de turno es obligatorio.")]
    [Display(Name = "Tipo de turno")]
    public string TipoTurnoId { get; set; } = string.Empty;

    [Required(ErrorMessage = "El motivo de la excepcion es obligatorio.")]
    [StringLength(250, ErrorMessage = "El motivo no puede exceder 250 caracteres.")]
    [Display(Name = "Motivo")]
    public string MotivoExcepcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Inicio")]
    public DateOnly FechaInicio { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fin")]
    public DateOnly FechaFin { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// Dias seleccionados para la excepcion (0=Lunes .. 6=Domingo). Lista vacia = todos los dias.
    /// </summary>
    public List<int> SelectedDiasSemana { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FechaFin < FechaInicio)
        {
            yield return new ValidationResult(
                "La fecha fin no puede ser menor a la fecha inicio.",
                new[] { nameof(FechaFin) });
        }
    }
}
