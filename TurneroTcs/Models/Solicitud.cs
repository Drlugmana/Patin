using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Encabezado de una solicitud laboral (vacación, permiso, cambio de turno, calamidad).
/// Centraliza el flujo de aprobación de dos niveles y los metadatos de auditoría comunes
/// a todos los tipos de solicitud.
/// </summary>
[Table("solicitud")]
public class Solicitud
{
    /// <summary>Identificador único de la solicitud.</summary>
    [Key]
    [Required]
    [Column("solicitud_id")]
    public string SolicitudId { get; set; } = string.Empty;

    /// <summary>Identificador de la <see cref="Persona"/> que realiza la solicitud.</summary>
    [Required]
    [Column("persona_solicitante_id")]
    public string PersonaSolicitanteId { get; set; } = string.Empty;

    /// <summary>Persona que realiza la solicitud.</summary>
    [ForeignKey(nameof(PersonaSolicitanteId))]
    public Persona? PersonaSolicitante { get; set; }

    /// <summary>Identificador del <see cref="TipoSolicitud"/> que clasifica esta solicitud.</summary>
    [Required]
    [Column("tipo_solicitud_id")]
    public string TipoSolicitudId { get; set; } = string.Empty;

    /// <summary>Tipo que clasifica esta solicitud (vacación, permiso, etc.).</summary>
    [ForeignKey(nameof(TipoSolicitudId))]
    public TipoSolicitud? TipoSolicitud { get; set; }

    /// <summary>
    /// Estado actual dentro del flujo de aprobación.
    /// El valor inicial es <see cref="SolicitudEstado.Pendiente"/>.
    /// </summary>
    [Required]
    [Column("estado_solicitud", TypeName = "varchar(20)")]
    public SolicitudEstado EstadoSolicitud { get; set; } = SolicitudEstado.Pendiente;

    /// <summary>Fecha y hora en que se creó la solicitud.</summary>
    [Required]
    [Column("fecha_solicitud")]
    public DateTime FechaSolicitud { get; set; }

    /// <summary>
    /// Fecha y hora de la primera aprobación (líder directo).
    /// Es <see langword="null"/> mientras la solicitud no haya pasado el primer nivel.
    /// </summary>
    [Column("fecha_aprobacion_1")]
    public DateTime? FechaAprobacion1 { get; set; }

    /// <summary>
    /// Fecha y hora de la aprobación final.
    /// Es <see langword="null"/> mientras la solicitud no haya sido aprobada definitivamente.
    /// </summary>
    [Column("fecha_aprobacion_2")]
    public DateTime? FechaAprobacion2 { get; set; }

    /// <summary>Identificador del primer aprobador (líder directo). Puede ser <see langword="null"/> si aún no se aprobó.</summary>
    [Column("persona_aprobador_1_id")]
    public string? PersonaAprobador1Id { get; set; }

    /// <summary>Persona que actuó como primer aprobador.</summary>
    [ForeignKey(nameof(PersonaAprobador1Id))]
    public Persona? PersonaAprobador1 { get; set; }

    /// <summary>Identificador del aprobador final. Puede ser <see langword="null"/> si aún no se otorgó la aprobación final.</summary>
    [Column("persona_aprobador_2_id")]
    public string? PersonaAprobador2Id { get; set; }

    /// <summary>Persona que actuó como aprobador final.</summary>
    [ForeignKey(nameof(PersonaAprobador2Id))]
    public Persona? PersonaAprobador2 { get; set; }

    /// <summary>Fecha y hora de la última modificación del registro (UTC).</summary>
    [Required]
    [Column("actualizado_en")]
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha y hora en que se insertó el registro por primera vez (UTC).</summary>
    [Required]
    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
