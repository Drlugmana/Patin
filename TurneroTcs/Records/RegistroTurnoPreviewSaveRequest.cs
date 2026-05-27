namespace TurneroTcs.Records;

/// <summary>
/// Solicitud para confirmar y persistir un conjunto de turnos que se encontraban en estado de vista previa.
/// Contiene la lista de ítems de preview que deben convertirse en registros definitivos.
/// </summary>
/// <param name="Items">Colección de turnos en preview que se desean guardar.</param>
public sealed record RegistroTurnoPreviewSaveRequest(IReadOnlyList<RegistroTurnoPreviewItem> Items);
