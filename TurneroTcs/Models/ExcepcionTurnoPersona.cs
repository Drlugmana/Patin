using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Define una excepción temporal que impide que una persona sea asignada
/// a un tipo de turno específico durante un rango de fechas.
/// Se usa para casos como calamidades médicas, embarazo, o restricciones temporales.
/// </summary>
[Table("excepcion_turno_persona")]
public class ExcepcionTurnoPersona
{
    /// <summary>Identificador único de la excepción.</summary>
    [Key]
    [Column("excepcion_turno_persona_id")]
    public string ExcepcionTurnoPersonaId { get; set; } = null!;

    /// <summary>Identificador de la persona afectada por la excepción.</summary>
    [Required]
    [Column("persona_id")]
    public string PersonaId { get; set; } = null!;

    /// <summary>Persona a la que aplica esta excepción.</summary>
    [ForeignKey(nameof(PersonaId))]
    public Persona? Persona { get; set; }

    /// <summary>Identificador del tipo de turno que no puede ser cubierto.</summary>
    [Required]
    [Column("tipo_turno_id")]
    public string TipoTurnoId { get; set; } = null!;

    /// <summary>Tipo de turno exceptuado.</summary>
    [ForeignKey(nameof(TipoTurnoId))]
    public TipoTurno? TipoTurno { get; set; }

    /// <summary>Motivo de la excepción (ej. calamidad médica, embarazo, cirugía).</summary>
    [Required]
    [Column("motivo_excepcion", TypeName = "varchar(250)")]
    public string MotivoExcepcion { get; set; } = null!;

    /// <summary>Fecha de inicio a partir de la cual la excepción es válida.</summary>
    [Required]
    [Column("fecha_inicio")]
    public DateOnly FechaInicio { get; set; }

    /// <summary>Fecha de fin hasta la cual la excepción es válida (inclusive).</summary>
    [Required]
    [Column("fecha_fin")]
    public DateOnly FechaFin { get; set; }

    /// <summary>Lista de dias de la semana en formato CSV de enteros (0=Lunes..6=Domingo) que indica
    /// en que dias aplica la excepcion. Valor vacio = todos los dias.</summary>
    [Column("dias_semana", TypeName = "varchar(50)")]
    public string DiasSemana { get; set; } = string.Empty;

    [NotMapped]
    public IReadOnlySet<DayOfWeek> Dias
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DiasSemana)) return new HashSet<DayOfWeek>();
            try
            {
                var parts = DiasSemana.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var set = new HashSet<DayOfWeek>();
                foreach (var p in parts)
                {
                    if (int.TryParse(p, out var v))
                    {
                        // map storage 0..6 to DayOfWeek (DayOfWeek enum starts Sunday=0)
                        // We store 0=Lunes..6=Domingo, so convert: stored 0 -> DayOfWeek.Monday (1)
                        var dow = p == "" ? (DayOfWeek)0 : (DayOfWeek)((v + 1) % 7);
                        set.Add(dow);
                    }
                }

                return set;
            }
            catch
            {
                return new HashSet<DayOfWeek>();
            }
        }
    }

    /// <summary>Fecha de creación del registro de excepción.</summary>
    [Column("fecha_creacion")]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>Identificador del usuario que creó la excepción.</summary>
    [Column("creado_por", TypeName = "varchar(250)")]
    public string? CreadoPor { get; set; }

    /// <summary>Fecha de última actualización del registro.</summary>
    [Column("fecha_ultima_actualizacion")]
    public DateTime? FechaUltimaActualizacion { get; set; }

    /// <summary>Identificador del usuario que actualizó por última vez la excepción.</summary>
    [Column("actualizado_por", TypeName = "varchar(250)")]
    public string? ActualizadoPor { get; set; }
}
