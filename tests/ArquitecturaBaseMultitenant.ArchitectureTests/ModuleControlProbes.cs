using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Control.Services;

#pragma warning disable CA1812
internal sealed class InvalidHelperGuard(IUnitOfWork unitOfWork)
{
    public IUnitOfWork UnitOfWork { get; } = unitOfWork;
}
internal sealed class ControlPhoneObserver : IPhoneLinkObserver
{
    public Task VerifiedAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct) => Task.CompletedTask;
    public Task RemovedAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct) => Task.CompletedTask;
    public Task AccountDeletedAsync(Guid userId, CancellationToken ct) => Task.CompletedTask;
}
#pragma warning restore CA1812
