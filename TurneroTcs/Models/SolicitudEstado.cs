namespace TurneroTcs.Models;

/// <summary>
/// Define los posibles estados del ciclo de vida de una <see cref="Solicitud"/>.
/// El flujo normal es: <see cref="Pendiente"/> → <see cref="AprobadoLider"/> → <see cref="AprobadoFinal"/>.
/// </summary>
public enum SolicitudEstado
{
    /// <summary>La solicitud fue creada y está esperando la primera aprobación del líder.</summary>
    Pendiente,

    /// <summary>El líder directo aprobó la solicitud; pendiente de aprobación final.</summary>
    AprobadoLider,

    /// <summary>La solicitud fue aprobada en todos los niveles y está vigente.</summary>
    AprobadoFinal,

    /// <summary>La solicitud fue rechazada por alguno de los aprobadores.</summary>
    Rechazado,

    /// <summary>La solicitud fue anulada por el solicitante antes de su aprobación final.</summary>
    Cancelado
}
