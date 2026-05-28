namespace TurneroTcs.Records;

/// <summary>
/// Solicitud de actualización parcial (PATCH) para modificar uno o más atributos de una persona.
/// Solo los campos con valor distinto de <see langword="null"/> serán actualizados.
/// </summary>
/// <param name="Nombre">Nuevo primer nombre; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="Apellido">Nuevo primer apellido; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="SegundoNombre">Nuevo segundo nombre; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="SegundoApellido">Nuevo segundo apellido; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="Ultimatix">Nuevo identificador de Ultimatix del empleado; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="EquipoId">Nuevo equipo al que se reasigna la persona; <see langword="null"/> para conservar el valor actual.</param>
/// <param name="GrupoIds">
/// Nueva colección de grupos principales de la persona.
/// <see langword="null"/> para conservar los grupos actuales.
/// </param>
/// <param name="GrupoIdsSecundarios">
/// Nueva colección de grupos secundarios de la persona.
/// <see langword="null"/> para conservar los grupos secundarios actuales.
/// </param>
/// <param name="ColorUsuario">Nuevo color de identificación visual del usuario en la interfaz; <see langword="null"/> para conservar el valor actual.</param>
public sealed record PersonaPatchRequest(
    string? Nombre,
    string? Apellido,
    string? SegundoNombre,
    string? SegundoApellido,
    string? Ultimatix,
    string? EquipoId,
    IReadOnlyCollection<string>? GrupoIds,
    IReadOnlyCollection<string>? GrupoIdsSecundarios,
    string? ColorUsuario);
