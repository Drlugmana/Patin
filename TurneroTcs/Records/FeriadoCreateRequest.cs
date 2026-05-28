namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para registrar un nuevo feriado en el sistema.
/// </summary>
/// <param name="NombreFeriado">Nombre descriptivo del feriado.</param>
/// <param name="InicioFeriado">Fecha de inicio del periodo feriado.</param>
/// <param name="FinFeriado">Fecha de fin del periodo feriado (inclusive).</param>
public record FeriadoCreateRequest(
    string NombreFeriado,
    DateOnly InicioFeriado,
    DateOnly FinFeriado
);
