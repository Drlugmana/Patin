namespace TurneroTcs.Records;

/// <summary>
/// Representa el resultado de guardar un conjunto de turnos en estado de vista previa.
/// Informa cuántos turnos fueron creados y cuántos fueron omitidos por duplicación u otro criterio.
/// </summary>
/// <param name="Succeeded">Indica si la operación se completó exitosamente.</param>
/// <param name="CreatedCount">Número de turnos efectivamente creados.</param>
/// <param name="SkippedCount">Número de turnos omitidos (por ejemplo, duplicados).</param>
/// <param name="Error">Mensaje de error cuando la operación falla; <see langword="null"/> si fue exitosa.</param>
public sealed record TurnoPreviewSaveResult(bool Succeeded, int CreatedCount, int SkippedCount, string? Error)
{
    /// <summary>
    /// Crea un resultado exitoso con los contadores de turnos creados y omitidos.
    /// </summary>
    /// <param name="createdCount">Número de turnos creados.</param>
    /// <param name="skippedCount">Número de turnos omitidos.</param>
    /// <returns>Un <see cref="TurnoPreviewSaveResult"/> que indica éxito.</returns>
    public static TurnoPreviewSaveResult Ok(int createdCount, int skippedCount)
    {
        return new TurnoPreviewSaveResult(true, createdCount, skippedCount, null);
    }

    /// <summary>
    /// Crea un resultado fallido con el mensaje de error especificado.
    /// </summary>
    /// <param name="error">Descripción del error ocurrido.</param>
    /// <returns>Un <see cref="TurnoPreviewSaveResult"/> que indica fallo con contadores en cero.</returns>
    public static TurnoPreviewSaveResult Fail(string error)
    {
        return new TurnoPreviewSaveResult(false, 0, 0, error);
    }
}
