namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Representa un grupo de trabajo al que pertenecen los empleados dentro del problema de rotación.
/// Los grupos delimitan qué slots puede cubrir cada empleado según su asignación primaria o secundaria.
/// </summary>
public sealed record GrupoTrabajo
{
    /// <summary>Identificador único del grupo dentro del problema de rotación.</summary>
    public required string Id { get; init; }

    /// <summary>Nombre descriptivo del grupo, utilizado en diagnósticos y reportes.</summary>
    public string Nombre { get; init; } = string.Empty;
}
