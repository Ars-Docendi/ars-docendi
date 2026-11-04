using System.Reflection;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Storage.Contracts;
using Modules.Portal.Contracts.Dtos;
using Modules.Portal.Application;
using Modules.Portal.Repositories;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

public sealed class ConsultasSelectivasTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "portal_selectivo")
{
    [Fact]
    public async Task Reemplazar_cv_desvincula_y_evalua_retencion_sin_delete_incondicional()
    {
        var ct = TestContext.Current.CancellationToken;
        var persona = new Persona { Id = Guid.NewGuid(), Nombre = "Nombre", Apellido = "Apellido", Documento = "Sintético" };
        var uid = Guid.NewGuid(); var anterior = Guid.NewGuid(); var nuevo = Guid.NewGuid();
        var identity = SondaContrato.Crear<IConsultasIdentity>((n, a) => Task.FromResult<Usuario?>(new Usuario
        { Id = uid, Upn = "prueba@example.invalid", NombreParaMostrar = "Prueba", Persona = persona, Activo = true }));
        var retenciones = new List<Guid>(); var protegidos = new List<Guid>();
        var archivo = new ArchivoDto(nuevo, "cv", "nuevo.pdf", "application/pdf", "application/pdf", 42, null, "disponible", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var storage = SondaContrato.Crear<IArchivosRetencionPrueba>((n, args) => n switch
        {
            nameof(IAlmacenamientoArchivos.RequerirDisponibleDePropietarioAsync) => Task.FromResult(archivo),
            nameof(IAlmacenamientoArchivos.EliminarAsync) => throw new InvalidOperationException("El CV anterior sigue en un pedido: DELETE incondicional no es retención."),
            nameof(IProteccionArchivos.EjecutarAsync) => Proteger(args!),
            nameof(IRetencionArchivos.EliminarSiSinReferenciasAsync) => Retener((Guid)args![0]!),
            _ => throw new InvalidOperationException(n)
        });
        Task<bool> Retener(Guid id) { retenciones.Add(id); return Task.FromResult(false); }
        Task<CvDto> Proteger(object?[] args)
        {
            protegidos.AddRange((IReadOnlyCollection<Guid>)args[0]!);
            return ((Func<CancellationToken, Task<CvDto>>)args[1]!)(ct);
        }
        await using var db = PostgresFixture.CrearPortal(Cadena);
        var perfil = new Modules.Portal.Domain.Perfil { Id = Guid.NewGuid(), PersonaId = persona.Id };
        var cv = new Modules.Portal.Domain.Cv { Id = Guid.NewGuid(), PerfilId = perfil.Id, Nombre = "anterior.pdf", ArchivoId = anterior };
        db.Perfiles.Add(perfil); db.Cvs.Add(cv); await db.SaveChangesAsync(ct);
        var servicio = new ServicioPortal(new RepositorioPortal(db), identity, new ActorPortal(uid), storage);
        var observada = cv.Version;
        var result = await servicio.GuardarCvAsync(new(nuevo) { Version = observada }, ct);
        Assert.Equal(nuevo, result.ArchivoId);
        var conflicto = await Assert.ThrowsAsync<ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion>(() =>
            servicio.GuardarCvAsync(new(anterior) { Version = observada }, ct));
        Assert.Equal("concurrency-conflict", conflicto.Codigo);
        Assert.Equal(nuevo, cv.ArchivoId);
        Assert.Equal(new[] { anterior }, retenciones);
        Assert.Contains(anterior, protegidos); Assert.Contains(nuevo, protegidos);
    }

}

public interface IArchivosRetencionPrueba : IArchivosPrueba, IProteccionArchivos, IRetencionArchivos;
public interface IArchivosPrueba : IAlmacenamientoArchivos, IConsultasArchivos;
public class SondaContrato : DispatchProxy
{
    public Func<string, object?[]?, object?> Manejar { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Manejar(targetMethod!.Name, args);
    public static T Crear<T>(Func<string, object?[]?, object?> manejar) where T : class
    {
        var proxy = Create<T, SondaContrato>(); ((SondaContrato)(object)proxy).Manejar = manejar; return proxy;
    }
}
internal sealed class ActorPortal(Guid id) : ICurrentUser
{
    public string? UserId => id.ToString(); public string? Email => null; public IReadOnlyList<string> Roles => ["docente"]; public bool IsAuthenticated => true;
}
