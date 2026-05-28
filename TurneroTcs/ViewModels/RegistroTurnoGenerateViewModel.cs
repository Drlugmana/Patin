using System.ComponentModel.DataAnnotations;

namespace TurneroTcs.ViewModels;

/// <summary>
/// Modelo de vista para el formulario de generación manual de registros de turno.
/// Permite asignar uno o varios tipos de turno a una persona durante un rango de fechas,
/// opcionalmente restringido a un grupo específico.
/// </summary>
public class RegistroTurnoGenerateViewModel
{
    /// <summary>Identificador de la persona a quien se generarán los turnos.</summary>
    [Required(ErrorMessage = "Debe seleccionar una persona.")]
    [Display(Name = "Persona")]
    public string PersonaId { get; set; } = string.Empty;

    /// <summary>
    /// Identificadores de los tipos de turno que se generarán en el rango de fechas.
    /// Debe contener al menos un elemento.
    /// </summary>
    [Required(ErrorMessage = "Debe seleccionar al menos un turno.")]
    [MinLength(1, ErrorMessage = "Debe seleccionar al menos un turno.")]
    [Display(Name = "Turnos")]
    public List<string> TipoTurnoIds { get; set; } = new();

    /// <summary>Fecha de inicio del rango de generación de turnos.</summary>
    [Required(ErrorMessage = "Debe seleccionar la fecha de inicio.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha inicio")]
    public DateOnly FechaInicio { get; set; }

    /// <summary>Fecha de fin del rango de generación de turnos (inclusive).</summary>
    [Required(ErrorMessage = "Debe seleccionar la fecha de fin.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha fin")]
    public DateOnly FechaFin { get; set; }

    /// <summary>
    /// Identificador del grupo al que se asociarán los turnos generados.
    /// <see langword="null"/> si no se restringe a un grupo específico.
    /// </summary>
    [Display(Name = "Grupo")]
    public string? GrupoId { get; set; }
}
