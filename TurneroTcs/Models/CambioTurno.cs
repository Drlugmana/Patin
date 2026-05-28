using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Detalle de una solicitud de cambio de turno entre dos registros de turno.
/// Permite que un empleado intercambie su turno de origen con otro turno destino,
/// sujeto al flujo de aprobación de la <see cref="Solicitud"/> asociada.
/// </summary>
[Table("cambio_turno")]
public class CambioTurno
{
    /// <summary>Identificador único del registro de cambio de turno.</summary>
    [Key]
    [Required]
    [Column("cambio_turno_id")]
    public string CambioTurnoId { get; set; } = string.Empty;

    /// <summary>Identificador de la <see cref="Solicitud"/> a la que pertenece este detalle.</summary>
    [Required]
    [Column("solicitud_id")]
    public string SolicitudId { get; set; } = string.Empty;

    /// <summary>Solicitud padre que controla el estado de aprobación.</summary>
    [ForeignKey(nameof(SolicitudId))]
    public Solicitud? Solicitud { get; set; }

    /// <summary>Identificador del turno que el solicitante cede (turno de origen).</summary>
    [Required]
    [Column("turno_origen_id")]
    public string TurnoOrigenId { get; set; } = string.Empty;

    /// <summary>Turno que el solicitante cede en el intercambio.</summary>
    [ForeignKey(nameof(TurnoOrigenId))]
    public RegistroTurno? TurnoOrigen { get; set; }

    /// <summary>Identificador del turno que el solicitante recibirá (turno destino).</summary>
    [Required]
    [Column("turno_destino_id")]
    public string TurnoDestinoId { get; set; } = string.Empty;

    /// <summary>Turno que el solicitante recibirá en el intercambio.</summary>
    [ForeignKey(nameof(TurnoDestinoId))]
    public RegistroTurno? TurnoDestino { get; set; }

    /// <summary>Justificación del cambio de turno. Máximo 120 caracteres.</summary>
    [Required]
    [Column("motivo", TypeName = "varchar(120)")]
    public string Motivo { get; set; } = string.Empty;
}
