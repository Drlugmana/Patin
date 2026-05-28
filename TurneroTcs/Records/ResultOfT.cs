namespace TurneroTcs.Records;

/// <summary>
/// Representa el resultado de una operación que puede devolver un valor de tipo <typeparamref name="T"/>.
/// Encapsula el estado de éxito o fracaso, un mensaje de error opcional y el valor resultante.
/// </summary>
/// <typeparam name="T">Tipo del valor devuelto cuando la operación es exitosa.</typeparam>
/// <param name="Succeeded">Indica si la operación se completó exitosamente.</param>
/// <param name="Error">Mensaje de error cuando la operación falla; <see langword="null"/> si fue exitosa.</param>
/// <param name="Value">Valor resultante de la operación; <see langword="default"/> si falló.</param>
public record Result<T>(bool Succeeded, string? Error, T? Value)
{
    /// <summary>
    /// Crea un resultado exitoso con el valor especificado.
    /// </summary>
    /// <param name="value">Valor devuelto por la operación.</param>
    /// <returns>Un <see cref="Result{T}"/> que indica éxito con el valor provisto.</returns>
    public static Result<T> Ok(T value) => new(true, null, value);

    /// <summary>
    /// Crea un resultado fallido con el mensaje de error especificado.
    /// </summary>
    /// <param name="error">Descripción del error ocurrido.</param>
    /// <returns>Un <see cref="Result{T}"/> que indica fallo con valor <see langword="default"/>.</returns>
    public static Result<T> Fail(string error) => new(false, error, default);
}
