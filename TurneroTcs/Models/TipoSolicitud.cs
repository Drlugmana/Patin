using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Catálogo de tipos de solicitud disponibles en el sistema
/// (vacación, permiso, cambio de turno, calamidad, etc.).
/// </summary>
public class TipoSolicitud
{
    /// <summary>Identificador único del tipo de solicitud.</summary>
    [Key]
    [Required]
    [Column("tipo_solicitud_id")]
    public string TipoSolicitudId { get; set; } = string.Empty;

    /// <summary>
    /// Nombre descriptivo del tipo de solicitud.
    /// Máximo 20 caracteres (ej. "Vacacion", "Permiso", "CambioTurno").
    /// </summary>
    [Required]
    [Column("nombre_solicitud", TypeName = "varchar(20)")]
    public string NombreSolicitud { get; set; } = string.Empty;
}
