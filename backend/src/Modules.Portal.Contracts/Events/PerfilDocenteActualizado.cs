using MediatR;

namespace Modules.Portal.Contracts.Events;

public sealed record PerfilDocenteActualizado(Guid DocenteId, DateTimeOffset ActualizadoEn) : INotification;
