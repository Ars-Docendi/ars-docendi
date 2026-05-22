using MediatR;

namespace Modules.Designaciones.Contracts.Events;

public sealed record DesignacionAprobada(Guid DesignacionId, DateTimeOffset AprobadaEn) : INotification;
