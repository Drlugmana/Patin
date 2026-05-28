namespace TurneroTcs.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Define la demanda semanal de personal para un <see cref="Grupo"/> en un día y tipo de turno.
/// Extiende la configuración base con opciones de flexibilidad como turnos auxiliares,
/// cobertura solo con personal secundario y restricción de persona única por semana.
/// </summary>
[Table("planificacion")]
public class Planificacion
{
    /// <summary>Identificador único de la planificación.</summary>
    [Key]
    [Required]
    [Column("planificacion_id")]
    public string PlanificacionId { get; set; } = null!;

    /// <summary>Identificador del <see cref="Grupo"/> al que corresponde esta planificación.</summary>
    [Required]
    [Column("grupo_id")]
    public string GrupoId { get; set; } = null!;

    /// <summary>Grupo al que pertenece esta planificación.</summary>
    [ForeignKey(nameof(GrupoId))]
    public Grupo? Grupo { get; set; }

    /// <summary>
    /// Día de la semana al que aplica (ej. <c>"Lunes"</c>, <c>"Sabado"</c>).
    /// </summary>
    [Required]
    [Column("dia")]
    public string Dia { get; set; } = null!;

    /// <summary>Identificador del <see cref="TipoTurno"/> requerido.</summary>
    [Required]
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = null!;

    /// <summary>Tipo de turno requerido en este slot de planificación.</summary>
    [ForeignKey(nameof(TipoTurnoId))]
    public TipoTurno? TipoTurno { get; set; }

    /// <summary>Número de personas requeridas para este día y turno.</summary>
    [Required]
    [Column("numero_personas")]
    public int NumeroPersonas { get; set; }

    /// <summary>Número mínimo de personas requerido para este día y turno.</summary>
    [Required]
    [Column("min_personas")]
    public int NumeroPersonasMinimo { get; set; }

    /// <summary>
    /// Indica si esta planificación corresponde a un turno auxiliar.
    /// Los turnos auxiliares son cubiertos por personal de apoyo externo al grupo principal.
    /// </summary>
    [Required]
    [Column("is_auxiliar")]
    public bool IsAuxiliar { get; set; }

    /// <summary>
    /// Cuando es <see langword="true"/>, este slot solo puede ser cubierto por personal
    /// de grupos secundarios, nunca por miembros primarios del grupo.
    /// </summary>
    [Required]
    [Column("usa_solo_secundarios")]
    public bool UsaSoloSecundarios { get; set; }

    /// <summary>
    /// Identificador del <see cref="Grupo"/> del que provienen los candidatos secundarios.
    /// <see langword="null"/> cuando <see cref="UsaSoloSecundarios"/> es <see langword="false"/>.
    /// </summary>
    [Column("grupo_fuente_secundarios_id")]
    public string? GrupoFuenteSecundariosId { get; set; }

    /// <summary>Grupo fuente de personal secundario para cubrir este slot.</summary>
    [ForeignKey(nameof(GrupoFuenteSecundariosId))]
    public Grupo? GrupoFuenteSecundarios { get; set; }

    /// <summary>
    /// Cuando es <see langword="true"/>, el algoritmo de rotación asigna a la misma persona
    /// durante toda la semana en este slot, garantizando continuidad operacional.
    /// </summary>
    [Required]
    [Column("usar_persona_unica_por_semana")]
    public bool UsarPersonaUnicaPorSemana { get; set; }
}
