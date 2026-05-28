using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Define un bloque de planificación auxiliar compartida a nivel de equipo.
/// Establece un tipo de turno auxiliar con un límite diario de personas que puede
/// ser cubierto por miembros de los grupos autorizados dentro del rango de días indicado.
/// </summary>
[Table("planificacion_auxiliar_equipo")]
public class PlanificacionAuxiliarEquipo
{
    /// <summary>Identificador único de la planificación auxiliar de equipo.</summary>
    [Key]
    [Required]
    [Column("planificacion_auxiliar_equipo_id")]
    public string PlanificacionAuxiliarEquipoId { get; set; } = null!;

    /// <summary>Identificador del <see cref="Equipo"/> al que pertenece esta planificación auxiliar.</summary>
    [Required]
    [Column("equipo_id")]
    public string EquipoId { get; set; } = null!;

    /// <summary>Equipo al que pertenece esta planificación auxiliar.</summary>
    [ForeignKey(nameof(EquipoId))]
    public Equipo? Equipo { get; set; }

    /// <summary>Identificador del <see cref="TipoTurno"/> auxiliar que se planifica.</summary>
    [Required]
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = null!;

    /// <summary>Tipo de turno auxiliar que se planifica.</summary>
    [ForeignKey(nameof(TipoTurnoId))]
    public TipoTurno? TipoTurno { get; set; }

    /// <summary>Primer día de la semana en que aplica este auxiliar (ej. <c>"Lunes"</c>).</summary>
    [Required]
    [Column("desde_dia")]
    public string DesdeDia { get; set; } = null!;

    /// <summary>Último día de la semana en que aplica este auxiliar (ej. <c>"Domingo"</c>).</summary>
    [Required]
    [Column("hasta_dia")]
    public string HastaDia { get; set; } = null!;

    /// <summary>Número máximo de personas que pueden asignarse a este turno auxiliar por día.</summary>
    [Required]
    [Column("max_por_dia")]
    public int MaxPorDia { get; set; }

    /// <summary>Grupos cuyos miembros están autorizados para cubrir este turno auxiliar.</summary>
    public ICollection<PlanificacionAuxiliarEquipoGrupo> GruposPermitidos { get; set; } = new List<PlanificacionAuxiliarEquipoGrupo>();
}
