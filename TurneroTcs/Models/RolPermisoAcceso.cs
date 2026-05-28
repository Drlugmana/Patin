using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace TurneroTcs.Models;

/// <summary>
/// Tabla de unión que asigna un <see cref="PermisoAcceso"/> a un rol de ASP.NET Identity.
/// Todos los usuarios con ese rol heredan el permiso asignado.
/// </summary>
[Table("rol_permiso_acceso")]
public class RolPermisoAcceso
{
    /// <summary>Identificador del rol de ASP.NET Identity (<see cref="IdentityRole"/>) al que se asigna el permiso.</summary>
    [Required]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>Rol de ASP.NET Identity que recibe el permiso.</summary>
    [ForeignKey(nameof(RoleId))]
    public IdentityRole? Role { get; set; }

    /// <summary>Identificador del <see cref="PermisoAcceso"/> asignado al rol.</summary>
    [Required]
    [Column("permiso_acceso_id", TypeName = "varchar(12)")]
    public string PermisoAccesoId { get; set; } = string.Empty;

    /// <summary>Permiso de acceso que se concede al rol.</summary>
    [ForeignKey(nameof(PermisoAccesoId))]
    public PermisoAcceso? PermisoAcceso { get; set; }

    /// <summary>Fecha y hora en que se creó la asignación rol–permiso (UTC).</summary>
    [Required]
    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
