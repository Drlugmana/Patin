using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Plantilla maestra (blueprint) para la generación de turnos según el algoritmo de patrones.
/// Define las reglas base de un slot de turno: el día, el tipo de turno, las etiquetas
/// de clasificación y si puede ser cubierto por personal de grupos secundarios.
/// Se usa como punto de partida antes de resolver la rotación concreta.
/// </summary>
[Table("planificacion_blueprint")]
public class PlanificacionBlueprint
{
    /// <summary>Identificador único del blueprint de planificación.</summary>
    [Key]
    [Column("planificacion_blueprint_id", TypeName = "varchar(12)")]
    public string PlanificacionBlueprintId { get; set; } = null!;

    /// <summary>
    /// Identificador del <see cref="Grupo"/> al que aplica este blueprint.
    /// <see langword="null"/> indica que el blueprint es transversal a todos los grupos del equipo.
    /// </summary>
    [Column("grupo_id")]
    public string? GrupoId { get; set; }

    /// <summary>
    /// Día de la semana al que aplica (ej. <c>"Lunes"</c>, <c>"Sabado"</c>).
    /// Máximo 12 caracteres.
    /// </summary>
    [Required]
    [Column("dia", TypeName = "varchar(12)")]
    public string Dia { get; set; } = string.Empty;

    /// <summary>Identificador del <see cref="TipoTurno"/> que define el horario del slot.</summary>
    [Required]
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = null!;

    /// <summary>
    /// Etiquetas de clasificación del slot, separadas por coma.
    /// Se usan para filtrar y agrupar slots en la vista de planificación.
    /// Máximo 250 caracteres.
    /// </summary>
    [Required]
    [Column("etiquetas", TypeName = "varchar(250)")]
    public string Etiquetas { get; set; } = string.Empty;

    /// <summary>
    /// Mínimo de personas que deben estar asignadas a este turno.
    /// El valor por defecto es <c>0</c> (sin mínimo obligatorio).
    /// </summary>
    [Column("min_personas_turno")]
    public int MinPersonasTurno { get; set; } = 0;

    /// <summary>
    /// Cuando es <see langword="true"/>, este slot admite candidatos de grupos secundarios
    /// además del grupo primario.
    /// </summary>
    [Column("usa_grupos_secundarios")]
    public bool UsaGruposSecundarios { get; set; } = false;

    /// <summary>Grupo al que aplica este blueprint.</summary>
    public Grupo? Grupo { get; set; }

    /// <summary>Tipo de turno que define el horario del slot.</summary>
    public TipoTurno? TipoTurno { get; set; }
}
