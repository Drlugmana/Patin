using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace TurneroTcs.Models;

/// <summary>
/// Empleado registrado en el sistema. Combina la información personal con la cuenta
/// de autenticación de ASP.NET Identity (<see cref="IdentityUser"/>) y los datos
/// de auditoría y estado de baja lógica.
/// </summary>
[Table("persona")]
public class Persona
{
    /// <summary>Identificador único de la persona.</summary>
    [Key]
    [Required]
    public string PersonaId { get; set; } = null!;

    /// <summary>Identificador del usuario de autenticación (<see cref="IdentityUser"/>) vinculado.</summary>
    [Required]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Cuenta de autenticación ASP.NET Identity asociada a esta persona.</summary>
    public IdentityUser? User { get; set; }

    /// <summary>
    /// Identificador del <see cref="Equipo"/> al que pertenece la persona.
    /// Puede ser <see langword="null"/> si la persona no está asignada a ningún equipo.
    /// </summary>
    [ForeignKey(nameof(EquipoId))]
    [Column("equipo_id")]
    public string? EquipoId { get; set; }

    /// <summary>Equipo al que pertenece la persona.</summary>
    public Equipo? Equipo { get; set; }

    #region Informacion Persona

    /// <summary>Primer nombre de la persona. Entre 2 y 50 caracteres.</summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    [Column("nombre", TypeName = "varchar(50)")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Segundo nombre de la persona. Opcional, máximo 50 caracteres.</summary>
    [StringLength(50)]
    [Column("segundo_nombre", TypeName = "varchar(50)")]
    public string? SegundoNombre { get; set; }

    /// <summary>Primer apellido de la persona. Entre 2 y 50 caracteres.</summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    [Column("apellido", TypeName = "varchar(50)")]
    public string Apellido { get; set; } = string.Empty;

    /// <summary>Segundo apellido de la persona. Opcional, máximo 50 caracteres.</summary>
    [StringLength(50)]
    [Column("segundo_apellido", TypeName = "varchar(50)")]
    public string? SegundoApellido { get; set; }

    /// <summary>
    /// Color de identificación visual del empleado en la UI, en formato RGB (ej. <c>"255,100,50"</c>).
    /// Máximo 11 caracteres.
    /// </summary>
    [Column("color_usuario", TypeName = "varchar(11)")]
    public string? ColorUsuario { get; set; }

    /// <summary>
    /// Nombre completo calculado concatenando los campos de nombre y apellido no nulos.
    /// No se persiste en base de datos.
    /// </summary>
    [NotMapped]
    public string NombreCompleto =>
        string.Join(" ", new[] { Nombre, SegundoNombre, Apellido, SegundoApellido }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    #endregion

    #region Autenticacion Persona

    /// <summary>
    /// Código de empleado Ultimatix. Exactamente 7 dígitos numéricos.
    /// Se usa como identificador corporativo del empleado.
    /// </summary>
    [Required]
    [StringLength(7, MinimumLength = 7)]
    [RegularExpression(@"^\d{7}$")]
    [Column("ultimatix", TypeName = "varchar(7)")]
    public string Ultimatix { get; set; } = string.Empty;

    #endregion

    #region Status

    /// <summary>
    /// Indica si la persona fue dada de baja lógicamente del sistema.
    /// Las personas borradas no aparecen en las planificaciones activas.
    /// </summary>
    [Required]
    [Column("borrado")]
    [DefaultValue(false)]
    public bool Borrado { get; set; } = false;

    /// <summary>Identificador de la persona o proceso que registró la baja. <see langword="null"/> si no ha sido dada de baja.</summary>
    [Column("borrado_por")]
    public string? BorradoPor { get; set; }

    /// <summary>Fecha y hora en que se registró la baja lógica (UTC). <see langword="null"/> si no ha sido dada de baja.</summary>
    [Column("borrado_en")]
    public DateTime? BorradoEn { get; set; }

    #endregion

    #region Campos auditoria

    /// <summary>Fecha y hora en que se insertó el registro por primera vez (UTC).</summary>
    [Required]
    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha y hora de la última modificación del registro (UTC).</summary>
    [Required]
    [Column("actualizado_en")]
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    #endregion
}
