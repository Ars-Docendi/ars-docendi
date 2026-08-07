using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Shared.Identity;

/// <summary>
/// Aplica las migraciones de los schemas <c>identity</c> y <c>audit</c>.
/// <para>
/// ORDEN IMPORTANTE: este migrador debe correr ANTES que el de cualquier módulo.
/// Las tablas de negocio terminan su DDL llamando a <c>audit.attach(...)</c>, que
/// no existe hasta que este migrador crea el schema <c>audit</c>; y
/// <c>designaciones</c> declara FKs contra <c>identity.personas</c> y
/// <c>identity.materias</c>. Se garantiza registrando <c>AddArsDocendiShared()</c>
/// primero en el Host: el contenedor devuelve las implementaciones de
/// <see cref="IMigradorModulo"/> en orden de registración.
/// </para>
/// </summary>
internal sealed class MigradorIdentity(IdentityDbContext db) : IMigradorModulo
{
    public Task MigrarAsync(CancellationToken ct) => db.Database.MigrateAsync(ct);
}
