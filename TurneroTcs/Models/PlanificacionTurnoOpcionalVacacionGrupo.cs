using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Registra los slots de turno que un <see cref="Grupo"/> puede omitir
/// cuando alguno de sus miembros está de vacaciones.
/// Permite que el algoritmo de rotación trate el slot como opcional
/// en lugar de forzar la cobertura con personal auxiliar.
/// </summary>
[Table("planificacion_turno_opcional_vacacion_grupo")]
public class PlanificacionTurnoOpcionalVacacionGrupo
{
    /// <summary>Identificador único del registro de turno opcional por vacación.</summary>
    [Key]
    [Required]
    [Column("planificacion_turno_opcional_vacacion_grupo_id", TypeName = "varchar(12)")]
    public string PlanificacionTurnoOpcionalVacacionGrupoId { get; set; } = null!;

    /// <summary>Identificador del <see cref="Grupo"/> al que aplica la opcionalidad.</summary>
    [Required]
    [Column("grupo_id")]
    public string GrupoId { get; set; } = null!;

    /// <summary>Grupo al que aplica la opcionalidad del turno en vacaciones.</summary>
    [ForeignKey(nameof(GrupoId))]
    public Grupo? Grupo { get; set; }

    /// <summary>
    /// Día de la semana en que el slot puede omitirse (ej. <c>"Lunes"</c>, <c>"Viernes"</c>).
    /// </summary>
    [Required]
    [Column("dia")]
    public string Dia { get; set; } = null!;

    /// <summary>Identificador del <see cref="TipoTurno"/> que puede omitirse cuando hay vacaciones.</summary>
    [Required]
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = null!;

    /// <summary>Tipo de turno que puede omitirse en período de vacaciones.</summary>
    [ForeignKey(nameof(TipoTurnoId))]
    public TipoTurno? TipoTurno { get; set; }
}
