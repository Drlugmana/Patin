using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Detalle de una solicitud de ausencia por calamidad doméstica o emergencia personal.
/// Define el período de ausencia y la causa, vinculado al flujo de aprobación
/// de la <see cref="Solicitud"/> asociada.
/// </summary>
[Table("calamidad")]
public class Calamidad
{
    /// <summary>Identificador único del registro de calamidad.</summary>
    [Key]
    [Required]
    [Column("calamidad_id")]
    public string CalamidadId { get; set; } = string.Empty;

    /// <summary>Identificador de la <see cref="Solicitud"/> a la que pertenece este detalle.</summary>
    [Required]
    [Column("solicitud_id")]
    public string SolicitudId { get; set; } = string.Empty;

    /// <summary>Solicitud padre que controla el estado de aprobación.</summary>
    [ForeignKey(nameof(SolicitudId))]
    public Solicitud? Solicitud { get; set; }

    /// <summary>Primer día de la ausencia por calamidad (inclusive).</summary>
    [Required]
    [Column("fecha_inicio", TypeName = "date")]
    public DateOnly FechaInicio { get; set; }

    /// <summary>Último día de la ausencia por calamidad (inclusive).</summary>
    [Required]
    [Column("fecha_fin", TypeName = "date")]
    public DateOnly FechaFin { get; set; }

    /// <summary>Descripción de la calamidad o emergencia que motiva la ausencia. Máximo 240 caracteres.</summary>
    [Required]
    [Column("motivo", TypeName = "varchar(240)")]
    public string Motivo { get; set; } = string.Empty;
}
