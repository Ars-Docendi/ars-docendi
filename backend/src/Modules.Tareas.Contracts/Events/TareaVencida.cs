using MediatR;

namespace Modules.Tareas.Contracts.Events;

public sealed record TareaVencida(Guid TareaId, DateTimeOffset VencimientoEn) : INotification;
