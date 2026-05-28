using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Sub-unidad de trabajo dentro de un <see cref="Equipo"/>.
/// Cada grupo tiene su propia plantilla de rotación y puede participar
/// como origen o destino en esquemas de cobertura auxiliar y de apoyo.
/// </summary>
[Table("grupo")]
public class Grupo
{
    /// <summary>Identificador único del grupo.</summary>
    [Key]
    [Required]
    [Column("grupo_id")]
    public string GrupoId { get; set; } = null!;

    /// <summary>Nombre descriptivo del grupo. Máximo 50 caracteres.</summary>
    [Required]
    [Column("nombre_grupo", TypeName = "varchar(50)")]
    public string NombreGrupo { get; set; } = string.Empty;

    /// <summary>Identificador del <see cref="Equipo"/> al que pertenece este grupo.</summary>
    [Required]
    [Column("equipo_id")]
    public string EquipoId { get; set; } = null!;

    /// <summary>Equipo al que pertenece este grupo.</summary>
    [ForeignKey(nameof(EquipoId))]
    public Equipo? Equipo { get; set; }

    /// <summary>
    /// Indica si el grupo está activo.
    /// Los grupos inactivos son excluidos de la planificación de nuevos períodos.
    /// </summary>
    [Required]
    [Column("activo")]
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Grupos fuente cuyos miembros pueden cubrir turnos auxiliares de este grupo.
    /// </summary>
    public ICollection<PlanificacionAuxiliarEquipoGrupo> PlanificacionesAuxiliaresEquipo { get; set; } = new List<PlanificacionAuxiliarEquipoGrupo>();

    /// <summary>Configuraciones de turnos de apoyo que este grupo puede recibir de otros grupos.</summary>
    public ICollection<PlanificacionApoyoGrupo> PlanificacionesApoyo { get; set; } = new List<PlanificacionApoyoGrupo>();

    /// <summary>
    /// Tipos de turno opcionales que pueden asignarse a este grupo cuando algún miembro está de vacaciones.
    /// </summary>
    public ICollection<PlanificacionTurnoOpcionalVacacionGrupo> PlanificacionesTurnosOpcionalesVacacion { get; set; } = new List<PlanificacionTurnoOpcionalVacacionGrupo>();
}
