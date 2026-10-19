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

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task Actor_selectivo_y_metadata_en_un_lote_independiente_del_numero_de_documentos(int documentos)
    {
        var ct = TestContext.Current.CancellationToken;
        var persona = new Persona { Id = Guid.NewGuid(), Nombre = "Nombre", Apellido = "Apellido", Documento = "Sintético" };
        var cuenta = new Usuario { Id = Guid.NewGuid(), PersonaId = persona.Id, Persona = persona, Upn = "prueba@example.invalid", NombreParaMostrar = "Prueba", Activo = true };
        persona.Usuario = cuenta;
        var llamadasIdentity = new List<string>(); var llamadasStorage = new List<string>();
        var identity = SondaContrato.Crear<IConsultasIdentity>((nombre, args) =>
        {
            llamadasIdentity.Add(nombre);
            return nombre == nameof(IConsultasIdentity.ListarUsuariosAsync)
                ? Task.FromResult<IReadOnlyList<Usuario>>([cuenta]) : Task.FromResult<Usuario?>(cuenta);
        });
        var metadata = new Dictionary<Guid, ArchivoDto>();
        var storage = SondaContrato.Crear<IArchivosPrueba>((nombre, args) =>
        {
            llamadasStorage.Add(nombre);
            return nombre == nameof(IConsultasArchivos.ObtenerPorIdsAsync)
                ? Task.FromResult<IReadOnlyDictionary<Guid, ArchivoDto>>(metadata)
                : Task.FromResult<ArchivoDto?>(metadata[(Guid)args![0]!]);
        });
        await using var conexion = await AbrirConexionAsync();
        await using var crearPerfil = new NpgsqlCommand("INSERT INTO portal.perfiles(id,persona_id) VALUES (@id,@id)", conexion);
        crearPerfil.Parameters.AddWithValue("id", persona.Id); await crearPerfil.ExecuteNonQueryAsync(ct);
        for (var n = 0; n < documentos; n++)
        {
            var archivo = Guid.NewGuid(); var proyecto = Guid.NewGuid();
            metadata[archivo] = new(archivo, "documento_proyecto", "archivo.pdf", "application/pdf", "application/pdf", 123, null, "disponible", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            await using var insertar = new NpgsqlCommand("""
                INSERT INTO portal.proyectos(id,perfil_id,nombre,rol,descripcion,desde) VALUES (@id,@perfil,'Proyecto','Rol','Descripción','2020-01-01');
                INSERT INTO portal.proyecto_documentos(proyecto_id,nombre,archivo_id) VALUES (@id,'archivo.pdf',@archivo);
                """, conexion);
            insertar.Parameters.AddWithValue("id", proyecto); insertar.Parameters.AddWithValue("perfil", persona.Id); insertar.Parameters.AddWithValue("archivo", archivo);
            await insertar.ExecuteNonQueryAsync(ct);
        }
        await using var db = PostgresFixture.CrearPortal(Cadena);
        var servicio = new ServicioPortal(new RepositorioPortal(db), identity, new ActorPortal(cuenta.Id), storage);
        var perfil = await servicio.ObtenerPropioAsync(ct);
        Assert.Equal(documentos, perfil.Proyectos.Count);
        Assert.All(perfil.Proyectos, p => Assert.Equal(123, p.Documento!.TamanoBytes));
        Assert.Equal(new[] { nameof(IConsultasIdentity.ObtenerUsuarioAsync) }, llamadasIdentity);
        Assert.Equal(new[] { nameof(IConsultasArchivos.ObtenerPorIdsAsync) }, llamadasStorage);
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
