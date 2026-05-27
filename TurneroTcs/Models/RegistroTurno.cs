using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Asignación concreta de una persona a un tipo de turno en una fecha específica.
/// Es el registro operativo diario generado a partir de la planificación.
/// Indica además si el día es feriado y si el empleado laboró o no por esa razón.
/// </summary>
[Table("registro_turno")]
public class RegistroTurno
{
    /// <summary>Identificador único del registro de turno.</summary>
    [Key]
    [Required]
    [Column("turno_id")]
    public string TurnoId { get; set; } = null!;

    /// <summary>Identificador de la <see cref="Persona"/> asignada a este turno.</summary>
    [Required]
    [Column("persona_id")]
    public string PersonaId { get; set; } = string.Empty;

    /// <summary>Persona asignada a este turno.</summary>
    [ForeignKey(nameof(PersonaId))]
    public Persona? Persona { get; set; }

    /// <summary>Identificador del <see cref="TipoTurno"/> que corresponde a este registro.</summary>
    [Required]
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = string.Empty;

    /// <summary>Tipo de turno (horario) asignado.</summary>
    [ForeignKey(nameof(TipoTurnoId))]
    public TipoTurno? TipoTurno { get; set; }

    /// <summary>
    /// Identificador del <see cref="Grupo"/> al que corresponde este turno.
    /// Puede ser <see langword="null"/> para turnos auxiliares compartidos entre grupos.
    /// </summary>
    [Column("grupo_id")]
    public string? GrupoId { get; set; }

    /// <summary>Grupo de trabajo al que pertenece este turno.</summary>
    [ForeignKey(nameof(GrupoId))]
    public Grupo? Grupo { get; set; }

    /// <summary>Fecha en que se ejecuta el turno.</summary>
    [Required]
    [Column("fecha_turno", TypeName = "date")]
    public DateOnly FechaTurno { get; set; }

    /// <summary>
    /// Indica si la fecha del turno corresponde a un día feriado.
    /// Permite calcular recargos salariales sin consultar la tabla de feriados.
    /// </summary>
    [Required]
    [Column("es_feriado")]
    public bool EsFeriado { get; set; }

    /// <summary>
    /// Indica si la persona no laboró este turno por ser día feriado.
    /// Un empleado puede tener <see cref="EsFeriado"/> = <see langword="true"/>
    /// pero <see cref="NoLaboradoPorFeriado"/> = <see langword="false"/> si fue asignado como cobertura de feriado.
    /// </summary>
    [Required]
    [Column("no_laborado_por_feriado")]
    public bool NoLaboradoPorFeriado { get; set; }

    /// <summary>
    /// Indica si este es un turno extra que no forma parte de la rotación base.
    /// Los turnos extra se generan para cubrir necesidades operacionales puntuales.
    /// </summary>
    [Required]
    [Column("es_turno_extra")]
    public bool EsTurnoExtra { get; set; }
}
