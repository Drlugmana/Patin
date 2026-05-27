using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurneroTcs.Models;

/// <summary>
/// Representa un dia o rango de dias festivos reconocidos por la empresa.
/// </summary>
[Table("feriado")]
public class Feriado
{
    /// <summary>Identificador unico del feriado.</summary>
    [Key]
    [Required]
    [Column("feriado_id", TypeName = "varchar(12)")]
    public string FeriadoId { get; set; } = null!;

    /// <summary>Nombre descriptivo del feriado. Maximo 120 caracteres.</summary>
    [Required]
    [Column("nombre_feriado", TypeName = "varchar(120)")]
    public string NombreFeriado { get; set; } = string.Empty;

    /// <summary>Primer dia del periodo de feriado (inclusive).</summary>
    [Required]
    [Column("inicio_feriado")]
    public DateOnly InicioFeriado { get; set; }

    /// <summary>Ultimo dia del periodo de feriado (inclusive).</summary>
    [Required]
    [Column("fin_feriado")]
    public DateOnly FinFeriado { get; set; }
}
