using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Registro de reemplazo asignado para cubrir la ausencia de un empleado por calamidad.
/// Asocia el turno del empleado ausente con el turno del empleado que lo cubre,
/// indicando el mecanismo de cobertura utilizado.
/// </summary>
[Table("calamidad_reemplazo")]
public class CalamidadReemplazo
{
    /// <summary>Identificador único del registro de reemplazo por calamidad.</summary>
    [Key]
    [Required]
    [Column("calamidad_reemplazo_id")]
    public string CalamidadReemplazoId { get; set; } = string.Empty;

    /// <summary>Identificador de la <see cref="Solicitud"/> de calamidad que origina el reemplazo.</summary>
    [Required]
    [Column("solicitud_id")]
    public string SolicitudId { get; set; } = string.Empty;

    /// <summary>Solicitud de calamidad que origina este reemplazo.</summary>
    [ForeignKey(nameof(SolicitudId))]
    public Solicitud? Solicitud { get; set; }

    /// <summary>Identificador del turno del empleado que se ausentó por calamidad.</summary>
    [Required]
    [Column("turno_ausente_id")]
    public string TurnoAusenteId { get; set; } = string.Empty;

    /// <summary>Turno del empleado que no laboró por calamidad.</summary>
    [ForeignKey(nameof(TurnoAusenteId))]
    public RegistroTurno? TurnoAusente { get; set; }

    /// <summary>Identificador del turno del empleado que cubre la ausencia.</summary>
    [Required]
    [Column("turno_reemplazo_id")]
    public string TurnoReemplazoId { get; set; } = string.Empty;

    /// <summary>Turno del empleado que cubre la ausencia.</summary>
    [ForeignKey(nameof(TurnoReemplazoId))]
    public RegistroTurno? TurnoReemplazo { get; set; }

    /// <summary>
    /// Modo en que se ejecuta el reemplazo (ej. <c>"SWAP"</c> para intercambio directo).
    /// Por defecto es <c>"SWAP"</c>.
    /// </summary>
    [Required]
    [Column("modo_reemplazo")]
    public string ModoReemplazo { get; set; } = "SWAP";
}
