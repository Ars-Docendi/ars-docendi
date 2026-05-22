using MediatR;

namespace Modules.Aulas.Contracts.Events;

public sealed record ReservaAulaConfirmada(Guid ReservaId, string AulaCodigo, DateTimeOffset Fecha) : INotification;
