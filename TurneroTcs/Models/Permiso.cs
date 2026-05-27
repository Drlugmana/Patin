using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Detalle de una solicitud de permiso por horas dentro de un turno ya asignado.
/// Especifica el rango horario y el motivo del permiso.
/// </summary>
[Table("permiso")]
public class Permiso
{
    /// <summary>Identificador único del registro de permiso.</summary>
    [Key]
    [Required]
    [Column("permiso_id")]
    public string PermisoId { get; set; } = string.Empty;

    /// <summary>Identificador de la <see cref="Solicitud"/> a la que pertenece este detalle.</summary>
    [Required]
    [Column("solicitud_id")]
    public string SolicitudId { get; set; } = string.Empty;

    /// <summary>Solicitud padre que controla el estado de aprobación.</summary>
    [ForeignKey(nameof(SolicitudId))]
    public Solicitud? Solicitud { get; set; }

    /// <summary>Identificador del <see cref="RegistroTurno"/> durante el cual se solicita el permiso.</summary>
    [Required]
    [Column("registro_turno_id")]
    public string RegistroTurnoId { get; set; } = string.Empty;

    /// <summary>Turno concreto al que aplica el permiso.</summary>
    [ForeignKey(nameof(RegistroTurnoId))]
    public RegistroTurno? RegistroTurno { get; set; }

    /// <summary>Hora de inicio del período de permiso dentro del turno.</summary>
    [Required]
    [Column("hora_inicio", TypeName = "time")]
    public TimeOnly HoraInicio { get; set; }

    /// <summary>Hora de fin del período de permiso dentro del turno.</summary>
    [Required]
    [Column("hora_fin", TypeName = "time")]
    public TimeOnly HoraFin { get; set; }

    /// <summary>Justificación proporcionada por el empleado. Máximo 120 caracteres.</summary>
    [Required]
    [Column("motivo", TypeName = "varchar(120)")]
    public string Motivo { get; set; } = string.Empty;
}
