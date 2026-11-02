using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Npgsql;

namespace Modules.Designaciones.Repositories;

internal sealed partial class RepositorioDesignaciones
{
    public async Task<T> EjecutarCoordinadoAsync<T>(Guid personaId,
        Func<CancellationToken, Task<T>> operacion, CancellationToken ct)
    {
        T resultado = default!;
        async Task Ejecutar(CancellationToken token)
        {
            // Identity antes del agregado de vigencias, compatible con administración.
            await ValidarDestinosAsync([], [personaId], token);
            await BloquearAsync(personaId, null, token, rechazarObsoletas: true);
            resultado = await operacion(token);
        }
        if (System.Transactions.Transaction.Current is not null)
        {
            // Mantener abierta la conexión enlistada hasta finalizar la mutación local.
            await db.Database.OpenConnectionAsync(ct);
            try { await Ejecutar(ct); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
        else await new UnidadDeTrabajo(db).EjecutarEnTransaccionAsync(Ejecutar, ct);
        return resultado;
    }

    public async Task BloquearAsync(Guid personaId, Guid? materiaId, CancellationToken ct,
        bool rechazarObsoletas = false)
    {
        if (db.Database.CurrentTransaction is null && System.Transactions.Transaction.Current is null)
            throw new InvalidOperationException("El guard de vigencias requiere la transacción de la mutación.");
        // El reemplazo administrativo abarca todas las materias, incluso altas aún inexistentes.
        // Persona primero; el circuito además identifica explícitamente el par canónico.
        var persona = $"designaciones:persona:{personaId:D}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({persona}, 0))", ct);
        if (materiaId is { } materia)
        {
            var par = $"designaciones:par:{personaId:D}:{materia:D}";
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({par}, 0))", ct);
        }
        db.ChangeTracker.DetectChanges();
        var observadas = db.ChangeTracker.Entries<Designacion>()
            .Where(e => e.Entity.PersonaId == personaId).ToArray();
        if (rechazarObsoletas && observadas.Length > 0)
        {
            var ids = observadas.Select(e => e.Entity.Id).ToArray();
            var versiones = await db.Designaciones.AsNoTracking().Where(d => ids.Contains(d.Id))
                .Select(d => new { d.Id, d.Version }).ToDictionaryAsync(d => d.Id, d => d.Version, ct);
            if (observadas.Any(e => !versiones.TryGetValue(e.Entity.Id, out var version)
                || version != e.Property(d => d.Version).OriginalValue))
                throw new DbUpdateConcurrencyException("La vigencia observada por administración cambió; vuelva a consultar.");
        }
        foreach (var entrada in observadas)
        {
            if (entrada.State != EntityState.Unchanged)
                throw new DbUpdateConcurrencyException("No se puede revalidar una vigencia ya modificada fuera del guard.");
            entrada.State = EntityState.Detached;
        }
    }

    public async Task ValidarDestinosAsync(IReadOnlyCollection<Guid> usuarios,
        IReadOnlyCollection<Guid> personas, CancellationToken ct)
    {
        // Las rutas administrativas editan la cuenta antes de persona/vigencias.
        // Tomar usuarios antes que personas evita invertir ese orden al actuar sobre sí mismo.
        if (usuarios.Count > 0 && personas.Count > 0)
        {
            await ValidarDestinosAsync(usuarios, [], ct);
            await ValidarDestinosAsync([], personas, ct);
            return;
        }
        if (db.Database.CurrentTransaction is { } transaccion)
        {
            await ProteccionDestinosIdentity.ValidarAsync(db.Database.GetDbConnection(),
                transaccion.GetDbTransaction(), usuarios, personas, ct);
            return;
        }
        if (System.Transactions.Transaction.Current is null)
            throw new InvalidOperationException("El destino debe validarse dentro de la transacción consumidora.");
        // TransactionScope no expone el DbTransaction local de Npgsql: invocar el mismo
        // protocolo SQL en la conexión enlistada, sin abrir otra ni escribir Identity.
        try
        {
            var idsUsuarios = usuarios.Distinct().Order().ToArray();
            var idsPersonas = personas.Distinct().Order().ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT identity.validar_destinos_logicos({idsUsuarios}, {idsPersonas})", ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation
            && ex.ConstraintName == "identity_destino_no_elegible")
        {
            throw new ExcepcionAplicacion(TipoErrorAplicacion.Conflicto,
                "identity-destination-conflict", "El destino de la referencia no existe o dejó de estar activo.");
        }
    }
}
