using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Configuración del número de personas requeridas en un <see cref="Grupo"/>
/// para un <see cref="TipoTurno"/> y día de la semana específicos.
/// Define la demanda de cobertura base que el algoritmo de planificación debe satisfacer.
/// </summary>
[Table("grupo_turno_config")]
public class GrupoTurnoConfig
{
    /// <summary>Identificador único de la configuración grupo–turno.</summary>
    [Key]
    [Required]
    [Column("grupo_turno_config_id")]
    public string GrupoTurnoConfigId { get; set; } = null!;

    /// <summary>Identificador del <see cref="Grupo"/> al que aplica esta configuración.</summary>
    [Required]
    [Column("grupo_id")]
    public string GrupoId { get; set; } = string.Empty;

    /// <summary>Grupo al que aplica esta configuración.</summary>
    [ForeignKey(nameof(GrupoId))]
    public Grupo? Grupo { get; set; }

    /// <summary>Identificador del <see cref="TipoTurno"/> configurado.</summary>
    [Required]
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = string.Empty;

    /// <summary>Tipo de turno para el que se define la cobertura.</summary>
    [ForeignKey(nameof(TipoTurnoId))]
    public TipoTurno? TipoTurno { get; set; }

    /// <summary>
    /// Día de la semana al que aplica esta configuración (ej. <c>"Lunes"</c>, <c>"Sabado"</c>).
    /// Máximo 10 caracteres.
    /// </summary>
    [Required]
    [Column("dia", TypeName = "varchar(10)")]
    public string Dia { get; set; } = string.Empty;

    /// <summary>
    /// Número de personas requeridas para este grupo, turno y día.
    /// Valor entre 1 y 99.
    /// </summary>
    [Required]
    [Range(1, 99)]
    [Column("numero_personas")]
    public int NumeroPersonas { get; set; }
}
