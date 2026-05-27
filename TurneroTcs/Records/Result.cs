namespace TurneroTcs.Records;

/// <summary>
/// Representa el resultado de una operación sin valor de retorno.
/// Encapsula el estado de éxito o fracaso junto con un mensaje de error opcional.
/// </summary>
/// <param name="Succeeded">Indica si la operación se completó exitosamente.</param>
/// <param name="Error">Mensaje de error cuando la operación falla; <see langword="null"/> si fue exitosa.</param>
public record Result(bool Succeeded, string? Error)
{
    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    /// <returns>Un <see cref="Result"/> que indica éxito.</returns>
    public static Result Ok() => new(true, null);

    /// <summary>
    /// Crea un resultado fallido con el mensaje de error especificado.
    /// </summary>
    /// <param name="error">Descripción del error ocurrido.</param>
    /// <returns>Un <see cref="Result"/> que indica fallo.</returns>
    public static Result Fail(string error) => new(false, error);
}
