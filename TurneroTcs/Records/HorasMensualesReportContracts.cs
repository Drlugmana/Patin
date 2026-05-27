namespace TurneroTcs.Records;

public record HorasMensualesReportRequest(
    string? FromDate,
    string? ToDate,
    string? FromMonth,
    string? ToMonth,
    string? EquipoId,
    string? GrupoId,
    string? PersonaId,
    string? SortField,
    string? SortDirection,
    int Page = 1,
    int PageSize = 15);

public record RecargosMensualesReportRequest(
    string? FromDate,
    string? ToDate,
    string? FromMonth,
    string? ToMonth,
    string? EquipoId,
    string? GrupoId,
    string? PersonaId,
    string? SortField,
    string? SortDirection,
    decimal? ValorHoraBase,
    decimal? IncrementoNocturno,
    decimal? IncrementoFeriado,
    decimal? IncrementoFinSemana,
    int Page = 1,
    int PageSize = 15)
    : HorasMensualesReportRequest(
        FromDate,
        ToDate,
        FromMonth,
        ToMonth,
        EquipoId,
        GrupoId,
        PersonaId,
        SortField,
        SortDirection,
        Page,
        PageSize);

public record HorasMensualesActorContext(
    string? UserId,
    bool IsAdmin,
    bool IsLider,
    bool IsUsuario);

public record HorasMensualesPdfDocument(byte[] Content, string FileName);

public record HorasMensualesExcelDocument(byte[] Content, string FileName);
