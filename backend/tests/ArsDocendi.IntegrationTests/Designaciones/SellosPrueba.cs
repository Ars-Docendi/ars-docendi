using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services.Integridad;

namespace ArsDocendi.IntegrationTests.Designaciones;

/// <summary>Arma el sellado como lo registra el módulo, con la clave de desarrollo o una explícita.</summary>
internal static class SellosPrueba
{
    public static ClavesSellado Claves(IntegridadOptions? opciones = null, string ambiente = "Development") =>
        new(Options.Create(opciones ?? new IntegridadOptions()), new HostingEnvironment { EnvironmentName = ambiente });

    public static SelladorPedidos Sellador(DesignacionesDbContext db, ClavesSellado? claves = null) =>
        new(new RepositorioSellos(db), claves ?? Claves(), new UnidadDeTrabajo(db), NullLogger<SelladorPedidos>.Instance);

    /// <summary>
    /// Para un fixture que llevó por EF un pedido a una etapa del circuito: registra el envío
    /// que lo habría dejado ahí y lo sella, como si hubiera llegado por la aplicación.
    /// </summary>
    public static async Task SellarEtapaForzadaAsync(DesignacionesDbContext db, Pedido pedido, CancellationToken ct)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var evento = new PedidoHistorial
        {
            Id = Guid.NewGuid(),
            PedidoId = pedido.Id,
            Accion = AccionesHistorial.Enviar,
            RolId = Guid.Parse("a1000000-0000-4000-8000-000000000002"),
            ActorId = Guid.Parse("a0000000-0000-4000-8000-000000000002"),
            Etapa = pedido.Estado,
            CreadoEn = CanonicoSellos.AMicrosegundos(DateTimeOffset.UtcNow),
        };
        db.PedidoHistorial.Add(evento);
        await Sellador(db).SellarAsync(pedido, evento, ct);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }

    public static VerificadorIntegridadPedidos Verificador(DesignacionesDbContext db, ClavesSellado? claves = null) =>
        new(new RepositorioSellos(db), claves ?? Claves());
}
