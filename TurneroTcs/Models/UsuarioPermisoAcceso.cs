using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace TurneroTcs.Models;

/// <summary>
/// Asignación o denegación explícita de un <see cref="PermisoAcceso"/> para un usuario específico.
/// Permite sobrescribir los permisos heredados por rol: si <see cref="EsDenegado"/> es
/// <see langword="true"/>, el permiso queda bloqueado para el usuario aunque su rol lo conceda.
/// </summary>
[Table("usuario_permiso_acceso")]
public class UsuarioPermisoAcceso
{
    /// <summary>Identificador del usuario de ASP.NET Identity (<see cref="IdentityUser"/>) al que aplica.</summary>
    [Required]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Usuario de ASP.NET Identity al que se asigna o deniega el permiso.</summary>
    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    /// <summary>Identificador del <see cref="PermisoAcceso"/> que se asigna o deniega.</summary>
    [Required]
    [Column("permiso_acceso_id", TypeName = "varchar(12)")]
    public string PermisoAccesoId { get; set; } = string.Empty;

    /// <summary>Permiso de acceso que se asigna o deniega al usuario.</summary>
    [ForeignKey(nameof(PermisoAccesoId))]
    public PermisoAcceso? PermisoAcceso { get; set; }

    /// <summary>
    /// Cuando es <see langword="true"/>, el permiso está explícitamente denegado para este usuario,
    /// ignorando cualquier concesión que provenga de sus roles.
    /// Cuando es <see langword="false"/>, el permiso está explícitamente concedido.
    /// </summary>
    [Required]
    [Column("es_denegado")]
    public bool EsDenegado { get; set; } = false;

    /// <summary>Fecha y hora en que se registró la asignación usuario–permiso (UTC).</summary>
    [Required]
    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
