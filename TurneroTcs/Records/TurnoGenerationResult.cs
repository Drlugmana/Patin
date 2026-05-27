namespace TurneroTcs.Records;

/// <summary>
/// Representa el resultado de una operación de generación automática de turnos.
/// Incluye el estado de la operación y la cantidad de turnos creados.
/// </summary>
/// <param name="Succeeded">Indica si la generación de turnos se completó exitosamente.</param>
/// <param name="Error">Mensaje de error cuando la operación falla; <see langword="null"/> si fue exitosa.</param>
/// <param name="CreatedCount">Número de turnos creados durante la operación.</param>
public record TurnoGenerationResult(bool Succeeded, string? Error, int CreatedCount)
{
    /// <summary>
    /// Crea un resultado exitoso con la cantidad de turnos generados.
    /// </summary>
    /// <param name="count">Número de turnos creados.</param>
    /// <returns>Un <see cref="TurnoGenerationResult"/> que indica éxito.</returns>
    public static TurnoGenerationResult Ok(int count) => new(true, null, count);

    /// <summary>
    /// Crea un resultado fallido con el mensaje de error especificado.
    /// </summary>
    /// <param name="error">Descripción del error ocurrido.</param>
    /// <returns>Un <see cref="TurnoGenerationResult"/> que indica fallo con cero turnos creados.</returns>
    public static TurnoGenerationResult Fail(string error) => new(false, error, 0);
}
