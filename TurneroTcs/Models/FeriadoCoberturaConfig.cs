using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Configuración de cobertura requerida para un equipo durante los días feriados.
/// Define cuántas personas de un grupo específico deben estar presentes
/// en un tipo de turno determinado cuando se declara un feriado.
/// </summary>
[Table("feriado_cobertura_config")]
public class FeriadoCoberturaConfig
{
    /// <summary>Identificador único de la configuración de cobertura en feriado.</summary>
    [Key]
    [Required]
    [Column("feriado_cobertura_config_id", TypeName = "varchar(12)")]
    public string FeriadoCoberturaConfigId { get; set; } = null!;

    /// <summary>Identificador del <see cref="Equipo"/> al que aplica esta configuración.</summary>
    [Required]
    [Column("equipo_id")]
    public string EquipoId { get; set; } = null!;

    /// <summary>Equipo al que aplica esta configuración de cobertura.</summary>
    [ForeignKey(nameof(EquipoId))]
    public Equipo? Equipo { get; set; }

    /// <summary>Identificador del <see cref="Grupo"/> cuya cobertura se configura.</summary>
    [Required]
    [Column("grupo_id")]
    public string GrupoId { get; set; } = null!;

    /// <summary>Grupo cuya cobertura se configura para el feriado.</summary>
    [ForeignKey(nameof(GrupoId))]
    public Grupo? Grupo { get; set; }

    /// <summary>Identificador del <see cref="TipoTurno"/> al que aplica la cobertura.</summary>
    [Required]
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = null!;

    /// <summary>Tipo de turno para el que se define la cobertura mínima en feriado.</summary>
    [ForeignKey(nameof(TipoTurnoId))]
    public TipoTurno? TipoTurno { get; set; }

    /// <summary>
    /// Número de personas que deben estar visibles/asignadas a este turno durante el feriado.
    /// </summary>
    [Required]
    [Column("cantidad_visible")]
    public int CantidadVisible { get; set; }
}
