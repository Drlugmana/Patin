using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Define un permiso de acceso granular a una funcionalidad del sistema.
/// Los permisos se agrupan por módulo y pueden asignarse a roles (<see cref="RolPermisoAcceso"/>)
/// o directamente a usuarios (<see cref="UsuarioPermisoAcceso"/>).
/// </summary>
[Table("permiso_acceso")]
public class PermisoAcceso
{
    /// <summary>Identificador único del permiso de acceso.</summary>
    [Key]
    [Required]
    [Column("permiso_acceso_id", TypeName = "varchar(12)")]
    public string PermisoAccesoId { get; set; } = null!;

    /// <summary>
    /// Clave de código interno del permiso (ej. <c>"turnos.ver"</c>, <c>"solicitudes.aprobar"</c>).
    /// Máximo 120 caracteres.
    /// </summary>
    [Required]
    [Column("codigo_permiso", TypeName = "varchar(120)")]
    public string CodigoPermiso { get; set; } = string.Empty;

    /// <summary>Nombre legible del permiso para mostrar en la UI. Máximo 120 caracteres.</summary>
    [Required]
    [Column("nombre_permiso", TypeName = "varchar(120)")]
    public string NombrePermiso { get; set; } = string.Empty;

    /// <summary>Descripción detallada de qué habilita este permiso. Opcional, máximo 300 caracteres.</summary>
    [Column("descripcion", TypeName = "varchar(300)")]
    public string? Descripcion { get; set; }

    /// <summary>Nombre del módulo al que pertenece el permiso (ej. <c>"Turnos"</c>, <c>"Solicitudes"</c>). Máximo 80 caracteres.</summary>
    [Required]
    [Column("modulo", TypeName = "varchar(80)")]
    public string Modulo { get; set; } = string.Empty;

    /// <summary>
    /// Indica si el permiso es de sistema y no puede ser eliminado ni modificado por el administrador.
    /// </summary>
    [Required]
    [Column("es_sistema")]
    public bool EsSistema { get; set; } = false;

    /// <summary>Fecha y hora en que se creó el permiso (UTC).</summary>
    [Required]
    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha y hora de la última modificación del permiso (UTC).</summary>
    [Required]
    [Column("actualizado_en")]
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Roles a los que se ha asignado este permiso.</summary>
    public ICollection<RolPermisoAcceso> RolesAsignados { get; set; } = new List<RolPermisoAcceso>();

    /// <summary>Usuarios a los que se ha asignado o denegado explícitamente este permiso.</summary>
    public ICollection<UsuarioPermisoAcceso> UsuariosAsignados { get; set; } = new List<UsuarioPermisoAcceso>();
}
