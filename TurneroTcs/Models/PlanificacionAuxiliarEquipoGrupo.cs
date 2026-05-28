using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Tabla de unión que autoriza a un <see cref="Grupo"/> para participar
/// en una <see cref="PlanificacionAuxiliarEquipo"/> específica.
/// Solo los grupos registrados en esta tabla pueden aportar personas
/// al turno auxiliar compartido del equipo.
/// </summary>
[Table("planificacion_auxiliar_equipo_grupo")]
public class PlanificacionAuxiliarEquipoGrupo
{
    /// <summary>Identificador único del registro de autorización grupo–auxiliar.</summary>
    [Key]
    [Required]
    [Column("planificacion_auxiliar_equipo_grupo_id")]
    public string PlanificacionAuxiliarEquipoGrupoId { get; set; } = null!;

    /// <summary>Identificador de la <see cref="PlanificacionAuxiliarEquipo"/> a la que se autoriza el grupo.</summary>
    [Required]
    [Column("planificacion_auxiliar_equipo_id")]
    public string PlanificacionAuxiliarEquipoId { get; set; } = null!;

    /// <summary>Planificación auxiliar del equipo en la que participa el grupo.</summary>
    [ForeignKey(nameof(PlanificacionAuxiliarEquipoId))]
    public PlanificacionAuxiliarEquipo? PlanificacionAuxiliarEquipo { get; set; }

    /// <summary>Identificador del <see cref="Grupo"/> autorizado.</summary>
    [Required]
    [Column("grupo_id")]
    public string GrupoId { get; set; } = null!;

    /// <summary>Grupo autorizado para cubrir el turno auxiliar del equipo.</summary>
    [ForeignKey(nameof(GrupoId))]
    public Grupo? Grupo { get; set; }
}
