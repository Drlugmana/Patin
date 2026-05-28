namespace TurneroTcs.Services.RotacionV2.Domain;

/// <summary>
/// Representa a un empleado que participa en el proceso de generación de rotación de turnos.
/// Contiene su identificador único, número de orden y los grupos a los que pertenece.
/// </summary>
public sealed record Empleado
{
    /// <summary>Identificador único del empleado dentro del sistema.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// Número de orden del empleado dentro del problema de rotación.
    /// Se utiliza para nombrar variables internas del motor de optimización.
    /// </summary>
    public required int Numero { get; init; }

    /// <summary>Nombre completo del empleado, utilizado en diagnósticos y resultados.</summary>
    public required string Nombre { get; init; }

    /// <summary>Identificador del grupo al que pertenece el empleado de forma principal.</summary>
    public required string GrupoPrimarioId { get; init; }

    /// <summary>
    /// Conjunto de identificadores de grupos secundarios a los que el empleado puede ser asignado como apoyo.
    /// La comparación de identificadores es insensible a mayúsculas.
    /// </summary>
    public HashSet<string> GruposSecundariosIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
