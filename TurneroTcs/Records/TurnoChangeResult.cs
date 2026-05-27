namespace TurneroTcs.Records;

/// <summary>
/// Representa el resultado de una operación de cambio de turno (movimiento o intercambio).
/// </summary>
/// <param name="Succeeded">Indica si el cambio de turno se completó exitosamente.</param>
/// <param name="Error">Mensaje de error cuando la operación falla; <see langword="null"/> si fue exitosa.</param>
public sealed record TurnoChangeResult(bool Succeeded, string? Error)
{
    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    /// <returns>Un <see cref="TurnoChangeResult"/> que indica éxito.</returns>
    public static TurnoChangeResult Ok()
    {
        return new TurnoChangeResult(true, null);
    }

    /// <summary>
    /// Crea un resultado fallido con el mensaje de error especificado.
    /// </summary>
    /// <param name="error">Descripción del error ocurrido.</param>
    /// <returns>Un <see cref="TurnoChangeResult"/> que indica fallo.</returns>
    public static TurnoChangeResult Fail(string error)
    {
        return new TurnoChangeResult(false, error);
    }
}
