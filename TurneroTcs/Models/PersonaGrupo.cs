using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Relación de pertenencia entre una <see cref="Persona"/> y un <see cref="Grupo"/>.
/// Una persona puede pertenecer a varios grupos, pero solo uno puede ser su grupo principal.
/// Los grupos secundarios habilitan a la persona como candidata para cubrir
/// turnos auxiliares en esos grupos.
/// </summary>
[Table("persona_grupo")]
public class PersonaGrupo
{
    /// <summary>Identificador único de la relación persona–grupo.</summary>
    [Key]
    [Required]
    [Column("persona_grupo_id")]
    public string PersonaGrupoId { get; set; } = null!;

    /// <summary>Identificador de la <see cref="Persona"/> vinculada.</summary>
    [Required]
    [Column("persona_id")]
    public string PersonaId { get; set; } = null!;

    /// <summary>Persona vinculada a este grupo.</summary>
    [ForeignKey(nameof(PersonaId))]
    public Persona? Persona { get; set; }

    /// <summary>Identificador del <see cref="Grupo"/> vinculado.</summary>
    [Required]
    [Column("grupo_id")]
    public string GrupoId { get; set; } = null!;

    /// <summary>Grupo al que pertenece la persona.</summary>
    [ForeignKey(nameof(GrupoId))]
    public Grupo? Grupo { get; set; }

    /// <summary>
    /// Indica si este grupo es el grupo primario de la persona.
    /// Solo debe existir un registro con <c>true</c> por persona.
    /// El grupo primario determina la rotación base del empleado.
    /// </summary>
    [Required]
    [Column("es_principal")]
    public bool EsPrincipal { get; set; } = true;
}
