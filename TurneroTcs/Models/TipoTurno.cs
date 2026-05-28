
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace TurneroTcs.Models;

/// <summary>
/// Define un tipo de turno con su nombre y rango horario.
/// Es el catálogo base de horarios disponibles en la empresa (ej. Mañana, Tarde, Noche).
/// </summary>
[Table("tipo_turno")]
public class TipoTurno
{
    /// <summary>Identificador único del tipo de turno.</summary>
    [Key]
    [Required]
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = null!;

    /// <summary>Nombre descriptivo del turno (ej. "Turno Mañana"). Máximo 50 caracteres.</summary>
    [Required]
    [Column("nombre_turno", TypeName = "varchar(50)")]
    public string NombreTurno { get; set; } = string.Empty;

    /// <summary>Hora en que inicia el turno.</summary>
    [Required]
    [Column("hora_inicio", TypeName = "time")]
    public TimeOnly HoraInicio { get; set; }

    /// <summary>
    /// Hora en que finaliza el turno.
    /// Cuando es menor que <see cref="HoraInicio"/>, el turno cruza medianoche.
    /// </summary>
    [Required]
    [Column("hora_fin", TypeName = "time")]
    public TimeOnly HoraFin { get; set; }

    /// <summary>
    /// Indica si el tipo de turno está disponible para ser asignado.
    /// Los tipos inactivos no aparecen en la planificación de nuevos períodos.
    /// </summary>
    [Required]
    [Column("activo")]
    public bool Activo { get; set; } = true;

    /// <summary>Equipos que tienen habilitado este tipo de turno.</summary>
    public ICollection<EquipoTipoTurno> EquipoTipoTurnos { get; set; } = new List<EquipoTipoTurno>();
}
