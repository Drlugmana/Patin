using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Detalle de una solicitud de vacaciones.
/// Está vinculada a una <see cref="Solicitud"/> que gestiona el flujo de aprobación.
/// </summary>
[Table("vacacion")]
public class Vacacion
{
    /// <summary>Identificador único del registro de vacación.</summary>
    [Key]
    [Required]
    [Column("vacacion_id")]
    public string VacacionId { get; set; } = string.Empty;

    /// <summary>Identificador de la <see cref="Solicitud"/> a la que pertenece este detalle.</summary>
    [Required]
    [Column("solicitud_id")]
    public string SolicitudId { get; set; } = string.Empty;

    /// <summary>Solicitud padre que controla el estado de aprobación.</summary>
    [ForeignKey(nameof(SolicitudId))]
    public Solicitud? Solicitud { get; set; }

    /// <summary>Primer día del período de vacaciones (inclusive).</summary>
    [Required]
    [Column("fecha_inicio")]
    public DateOnly FechaInicio { get; set; }

    /// <summary>Último día del período de vacaciones (inclusive).</summary>
    [Required]
    [Column("fecha_fin")]
    public DateOnly FechaFin { get; set; }
}
