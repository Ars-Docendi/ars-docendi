namespace Modules.Asistente.Contracts;

/// <summary>
/// El modo mantenimiento del asistente, tal como lo puede ver un permiso que no
/// es <c>asistente.consultar</c> (design.md D7 de sistema-seccion-unificada):
/// sin razón ni actor, sólo si está activo.
/// </summary>
public sealed record EstadoDeMantenimientoPublico(bool Activo);

/// <summary>
/// Lectura pública del estado de mantenimiento del asistente, para que
/// <c>GET /api/administracion/sistema/estado</c> lo muestre a
/// <c>sistema.estado.ver</c> sin depender de <c>asistente.consultar</c>
/// (design.md D7 de sistema-seccion-unificada, ARS-155).
/// </summary>
public interface IConsultaDeMantenimiento
{
    Task<EstadoDeMantenimientoPublico> ConsultarAsync(CancellationToken ct);
}
