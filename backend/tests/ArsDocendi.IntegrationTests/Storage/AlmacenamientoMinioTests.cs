using System.Net;
using System.Security.Cryptography;
using System.Text;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Storage;
using ArsDocendi.Storage.Api;
using ArsDocendi.Storage.Contracts;
using ArsDocendi.Storage.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Modules.Portal.Application;
using Modules.Portal.Contracts.Dtos;
using Modules.Portal.Repositories;
using Npgsql;
using Testcontainers.Minio;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed class AlmacenamientoMinioTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string AccessKey = "test-access-key";
    private const string SecretKey = "test-secret-key-123456";
    private const string Bucket = "arsdocendi-test";
    private const string StagingBucket = "arsdocendi-staging-test";
    private static readonly Guid Propietario = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid OtroPropietario = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    private readonly MinioContainer minio = new MinioBuilder("quay.io/minio/minio:latest")
        .WithUsername(AccessKey)
        .WithPassword(SecretKey)
        .Build();
    private string cadena = string.Empty;
    private IMinioClient? cliente;

    public async ValueTask InitializeAsync()
    {
        await minio.StartAsync();
        cadena = await postgres.CrearBaseMigradaAsync("storage_minio");
        var endpoint = new Uri(minio.GetConnectionString());
        var nuevoCliente = new MinioClient();
        nuevoCliente.WithEndpoint(endpoint.Host, endpoint.Port);
        nuevoCliente.WithCredentials(AccessKey, SecretKey);
        nuevoCliente.Build();
        cliente = nuevoCliente;
        await cliente!.MakeBucketAsync(new MakeBucketArgs().WithBucket(Bucket));
        await cliente.MakeBucketAsync(new MakeBucketArgs().WithBucket(StagingBucket));
    }

    public async ValueTask DisposeAsync()
    {
        cliente?.Dispose();
        if (cadena.Length > 0) await postgres.EliminarBaseAsync(cadena);
        await minio.DisposeAsync();
    }

    [Fact]
    public async Task Schema_conserva_referencias_de_archivo_y_metadata_legacy()
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(TestContext.Current.CancellationToken);
        const string sql = """
            SELECT table_schema, table_name, column_name
            FROM information_schema.columns
            WHERE (table_schema, table_name, column_name) IN (
                ('designaciones', 'pedido_adjuntos', 'archivo_id'),
                ('designaciones', 'pedido_adjuntos', 'uri'),
                ('portal', 'cvs', 'archivo_id'),
                ('portal', 'cvs', 'uri'),
                ('portal', 'proyecto_documentos', 'archivo_id'),
                ('portal', 'proyecto_documentos', 'uri'))
            """;
        await using var comando = new NpgsqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var columnas = new HashSet<string>(StringComparer.Ordinal);
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
            columnas.Add($"{lector.GetString(0)}.{lector.GetString(1)}.{lector.GetString(2)}");

        Assert.Equal(6, columnas.Count);
    }

    [Fact]
    public async Task Carga_confirma_hash_descarga_y_reintento_idempotente()
    {
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db);
        var contenido = Encoding.UTF8.GetBytes("%PDF-1.7\narchivo de prueba\n");
        var hash = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

        var sesion = await servicio.IniciarCargaAsync(
            new IniciarCargaArchivoDto(PropositosArchivo.Cv, "cv.pdf", "application/pdf", contenido.Length),
            Propietario,
            TestContext.Current.CancellationToken);
        using var http = new HttpClient();
        using var cuerpo = new ByteArrayContent(contenido);
        using var respuesta = await http.PutAsync(sesion.UrlSubida, cuerpo, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var confirmado = await servicio.ConfirmarCargaAsync(
            new ConfirmarCargaArchivoDto(sesion.ArchivoId, hash, contenido.Length),
            Propietario,
            TestContext.Current.CancellationToken);
        Assert.Equal(EstadosArchivo.Disponible, confirmado.Estado);
        Assert.Equal(hash, confirmado.Sha256);

        var asociado = await servicio.RequerirDisponibleDePropietarioAsync(
            sesion.ArchivoId, PropositosArchivo.Cv, Propietario, TestContext.Current.CancellationToken);
        Assert.Equal(confirmado.Id, asociado.Id);
        await Assert.ThrowsAsync<ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion>(() => servicio.RequerirDisponibleDePropietarioAsync(
            sesion.ArchivoId, PropositosArchivo.DocumentoProyecto, Propietario, TestContext.Current.CancellationToken));

        var repetido = await servicio.ConfirmarCargaAsync(
            new ConfirmarCargaArchivoDto(sesion.ArchivoId, hash, contenido.Length),
            Propietario,
            TestContext.Current.CancellationToken);
        Assert.Equal(confirmado.Id, repetido.Id);
        Assert.Equal(confirmado.Sha256, repetido.Sha256);

        var descarga = await servicio.AbrirDescargaAsync(sesion.ArchivoId, TestContext.Current.CancellationToken);
        Assert.NotNull(descarga);
        using var lector = new StreamReader(descarga!.Contenido);
        Assert.Equal(Encoding.UTF8.GetString(contenido), await lector.ReadToEndAsync(TestContext.Current.CancellationToken));
        await descarga.Contenido.DisposeAsync();
    }

    [Fact]
    public async Task Rechaza_hash_incorrecto_y_no_permite_asociacion_de_otro_propietario()
    {
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db);
        var contenido = Encoding.UTF8.GetBytes("%PDF-1.7\nsegundo archivo\n");
        var sesion = await servicio.IniciarCargaAsync(
            new IniciarCargaArchivoDto(PropositosArchivo.DocumentoProyecto, "proyecto.pdf", "application/pdf", contenido.Length),
            Propietario,
            TestContext.Current.CancellationToken);
        using var http = new HttpClient();
        using var cuerpo = new ByteArrayContent(contenido);
        using var respuesta = await http.PutAsync(sesion.UrlSubida, cuerpo, TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion>(() => servicio.ConfirmarCargaAsync(
            new ConfirmarCargaArchivoDto(sesion.ArchivoId, "00", contenido.Length), Propietario, TestContext.Current.CancellationToken));
        Assert.Equal("archivo-hash-mismatch", error.Codigo);

        var estado = await servicio.ObtenerAsync(sesion.ArchivoId, TestContext.Current.CancellationToken);
        Assert.Equal(EstadosArchivo.Rechazado, estado!.Estado);
        await Assert.ThrowsAsync<ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion>(() => servicio.RequerirDisponibleDePropietarioAsync(
            sesion.ArchivoId, PropositosArchivo.DocumentoProyecto, OtroPropietario, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rechaza_contenido_invalido_y_lo_deja_no_descargable()
    {
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db);
        var contenido = Encoding.UTF8.GetBytes("esto no es un PDF");
        var sesion = await servicio.IniciarCargaAsync(
            new IniciarCargaArchivoDto(PropositosArchivo.Cv, "cv.pdf", "application/pdf", contenido.Length),
            Propietario,
            TestContext.Current.CancellationToken);
        using var http = new HttpClient();
        using var cuerpo = new ByteArrayContent(contenido);
        using var respuesta = await http.PutAsync(sesion.UrlSubida, cuerpo, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion>(() => servicio.ConfirmarCargaAsync(
            new ConfirmarCargaArchivoDto(sesion.ArchivoId), Propietario, TestContext.Current.CancellationToken));
        var archivo = await servicio.ObtenerAsync(sesion.ArchivoId, TestContext.Current.CancellationToken);
        Assert.Equal(EstadosArchivo.Rechazado, archivo!.Estado);
        Assert.Null(await servicio.AbrirDescargaAsync(sesion.ArchivoId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Antivirus_infectado_rechaza_y_no_publica_el_archivo()
    {
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db, new AntivirusInfectado());
        var contenido = Encoding.UTF8.GetBytes("%PDF-1.7\narchivo infectado\n");
        var sesion = await servicio.IniciarCargaAsync(
            new IniciarCargaArchivoDto(PropositosArchivo.DocumentoProyecto, "proyecto.pdf", "application/pdf", contenido.Length),
            Propietario,
            TestContext.Current.CancellationToken);
        using var http = new HttpClient();
        using var cuerpo = new ByteArrayContent(contenido);
        using var respuesta = await http.PutAsync(sesion.UrlSubida, cuerpo, TestContext.Current.CancellationToken);

        var archivo = await servicio.ConfirmarCargaAsync(
            new ConfirmarCargaArchivoDto(sesion.ArchivoId), Propietario, TestContext.Current.CancellationToken);
        Assert.Equal(EstadosArchivo.Rechazado, archivo.Estado);
        Assert.Null(await servicio.AbrirDescargaAsync(sesion.ArchivoId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Objeto_ausente_y_sesion_expirada_no_se_publican()
    {
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db);
        var sesionAusente = await servicio.IniciarCargaAsync(
            new IniciarCargaArchivoDto(PropositosArchivo.Cv, "ausente.pdf", "application/pdf", 12),
            Propietario,
            TestContext.Current.CancellationToken);

        var ausente = await Assert.ThrowsAsync<ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion>(() => servicio.ConfirmarCargaAsync(
            new ConfirmarCargaArchivoDto(sesionAusente.ArchivoId), Propietario, TestContext.Current.CancellationToken));
        Assert.Equal("archivo-object-missing", ausente.Codigo);
        Assert.Equal(EstadosArchivo.Pendiente,
            (await servicio.ObtenerAsync(sesionAusente.ArchivoId, TestContext.Current.CancellationToken))!.Estado);

        var sesionExpirada = await servicio.IniciarCargaAsync(
            new IniciarCargaArchivoDto(PropositosArchivo.Cv, "expirado.pdf", "application/pdf", 12),
            Propietario,
            TestContext.Current.CancellationToken);
        var entidad = await db.Archivos.SingleAsync(x => x.Id == sesionExpirada.ArchivoId, TestContext.Current.CancellationToken);
        entidad.ExpiraEn = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expirado = await Assert.ThrowsAsync<ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion>(() => servicio.ConfirmarCargaAsync(
            new ConfirmarCargaArchivoDto(sesionExpirada.ArchivoId), Propietario, TestContext.Current.CancellationToken));
        Assert.Equal("archivo-upload-expired", expirado.Codigo);
    }

    [Fact]
    public async Task Imagen_valida_se_confirma_y_eliminacion_es_idempotente()
    {
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db);
        var contenido = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var sesion = await servicio.IniciarCargaAsync(
            new IniciarCargaArchivoDto(PropositosArchivo.DniFrente, "dni.png", "image/png", contenido.Length),
            Propietario,
            TestContext.Current.CancellationToken);
        using var http = new HttpClient();
        using var cuerpo = new ByteArrayContent(contenido);
        using var respuesta = await http.PutAsync(sesion.UrlSubida, cuerpo, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var archivo = await servicio.ConfirmarCargaAsync(
            new ConfirmarCargaArchivoDto(sesion.ArchivoId), Propietario, TestContext.Current.CancellationToken);
        Assert.Equal(EstadosArchivo.Disponible, archivo.Estado);

        await servicio.EliminarAsync(sesion.ArchivoId, Propietario, TestContext.Current.CancellationToken);
        await servicio.EliminarAsync(sesion.ArchivoId, Propietario, TestContext.Current.CancellationToken);
        Assert.Equal(EstadosArchivo.Eliminado,
            (await servicio.ObtenerAsync(sesion.ArchivoId, TestContext.Current.CancellationToken))!.Estado);
        Assert.Null(await servicio.AbrirDescargaAsync(sesion.ArchivoId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Descarga_http_solo_expone_archivo_al_propietario()
    {
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db);
        var archivoId = await SubirPdfAsync(servicio, "privado.pdf");
        var propietario = new ArchivosController(servicio, new UsuarioActual(Propietario));
        var otroActor = new ArchivosController(servicio, new UsuarioActual(OtroPropietario));

        var metadata = await propietario.Obtener(archivoId, TestContext.Current.CancellationToken);
        var metadataResponse = Assert.IsType<OkObjectResult>(metadata.Result);
        var metadataDto = Assert.IsType<ArchivoDto>(metadataResponse.Value);
        var serializado = System.Text.Json.JsonSerializer.Serialize(metadataDto);
        Assert.DoesNotContain("bucket", serializado, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clave", serializado, StringComparison.OrdinalIgnoreCase);

        var metadataAjena = await otroActor.Obtener(archivoId, TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(metadataAjena.Result);
        Assert.IsType<NotFoundResult>(await otroActor.Descargar(archivoId, TestContext.Current.CancellationToken));

        var descarga = Assert.IsType<FileStreamResult>(
            await propietario.Descargar(archivoId, TestContext.Current.CancellationToken));
        await descarga.FileStream.DisposeAsync();
    }

    [Fact]
    public async Task Buckets_de_ambientes_no_comparten_objetos()
    {
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicioStaging = CrearServicio(db, bucket: StagingBucket, ambiente: "staging");
        var contenido = Encoding.UTF8.GetBytes("%PDF-1.7\nstaging\n");
        var sesion = await servicioStaging.IniciarCargaAsync(
            new IniciarCargaArchivoDto(PropositosArchivo.DocumentoProyecto, "staging.pdf", "application/pdf", contenido.Length),
            Propietario,
            TestContext.Current.CancellationToken);
        using var http = new HttpClient();
        using var cuerpo = new ByteArrayContent(contenido);
        using var respuesta = await http.PutAsync(sesion.UrlSubida, cuerpo, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        await servicioStaging.ConfirmarCargaAsync(
            new ConfirmarCargaArchivoDto(sesion.ArchivoId), Propietario, TestContext.Current.CancellationToken);

        var proveedor = new ProveedorMinio(cliente!);
        var clave = $"archivos/{sesion.ArchivoId:N}";
        Assert.NotNull(await proveedor.ObtenerAsync(StagingBucket, clave, TestContext.Current.CancellationToken));
        Assert.Null(await proveedor.ObtenerAsync(Bucket, clave, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Portal_asocia_cv_y_reemplaza_el_objeto_anterior()
    {
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        await using var portalDb = PostgresFixture.CrearPortal(cadena);
        var almacenamiento = CrearServicio(db);
        var identidad = new IdentityDePrueba();
        var portal = new ServicioPortal(
            new RepositorioPortal(portalDb), identidad, new UsuarioActual(Propietario), almacenamiento);

        var primero = await SubirPdfAsync(almacenamiento, "cv-primero.pdf");
        var cv = await portal.GuardarCvAsync(new GuardarCvDto(primero), TestContext.Current.CancellationToken);
        Assert.Equal(primero, cv.ArchivoId);

        var segundo = await SubirPdfAsync(almacenamiento, "cv-segundo.pdf");
        cv = await portal.GuardarCvAsync(new GuardarCvDto(segundo), TestContext.Current.CancellationToken);
        Assert.Equal(segundo, cv.ArchivoId);

        var persistido = await portalDb.Cvs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(segundo, persistido.ArchivoId);
        Assert.Null(persistido.Uri);
        Assert.Equal(EstadosArchivo.Eliminado,
            (await almacenamiento.ObtenerAsync(primero, TestContext.Current.CancellationToken))!.Estado);
        Assert.Null(await almacenamiento.AbrirDescargaAsync(primero, TestContext.Current.CancellationToken));

        var documentoPrimero = await SubirPdfAsync(
            almacenamiento, "proyecto-primero.pdf", PropositosArchivo.DocumentoProyecto);
        var proyecto = await portal.CrearAsync(
            new GuardarProyectoDto("Proyecto", "Investigadora", "Documento asociado", new DateOnly(2024, 1, 1), null, null, documentoPrimero), TestContext.Current.CancellationToken);
        Assert.Equal(documentoPrimero, proyecto.Documento?.ArchivoId);

        var documentoSegundo = await SubirPdfAsync(
            almacenamiento, "proyecto-segundo.pdf", PropositosArchivo.DocumentoProyecto);
        proyecto = await portal.EditarAsync(
            proyecto.Id,
            new GuardarProyectoDto("Proyecto actualizado", "Directora", "Documento reemplazado", new DateOnly(2024, 1, 1), null, null, documentoSegundo), TestContext.Current.CancellationToken);
        Assert.Equal(documentoSegundo, proyecto.Documento?.ArchivoId);
        Assert.Equal(EstadosArchivo.Eliminado,
            (await almacenamiento.ObtenerAsync(documentoPrimero, TestContext.Current.CancellationToken))!.Estado);

        var descarga = await portal.DescargarDocumentoAsync(proyecto.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(descarga);
        await descarga!.Contenido.DisposeAsync();
        await portal.EliminarAsync<Modules.Portal.Domain.Proyecto>(proyecto.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EstadosArchivo.Eliminado,
            (await almacenamiento.ObtenerAsync(documentoSegundo, TestContext.Current.CancellationToken))!.Estado);
    }

    private async Task<Guid> SubirPdfAsync(
        ServicioAlmacenamientoArchivos servicio,
        string nombre,
        string proposito = PropositosArchivo.Cv)
    {
        var contenido = Encoding.UTF8.GetBytes($"%PDF-1.7\n{nombre}\n");
        var sesion = await servicio.IniciarCargaAsync(
            new IniciarCargaArchivoDto(proposito, nombre, "application/pdf", contenido.Length),
            Propietario,
            TestContext.Current.CancellationToken);
        using var http = new HttpClient();
        using var cuerpo = new ByteArrayContent(contenido);
        using var respuesta = await http.PutAsync(sesion.UrlSubida, cuerpo, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var archivo = await servicio.ConfirmarCargaAsync(
            new ConfirmarCargaArchivoDto(sesion.ArchivoId), Propietario, TestContext.Current.CancellationToken);
        return archivo.Id;
    }

    [Fact]
    public async Task Limpia_pendiente_y_huerfano_sin_afectar_archivo_asociado()
    {
        await using var db = PostgresFixture.CrearAlmacenamiento(cadena);
        var servicio = CrearServicio(db);
        var sesion = await servicio.IniciarCargaAsync(
            new IniciarCargaArchivoDto(PropositosArchivo.DniFrente, "dni.png", "image/png", 8),
            Propietario,
            TestContext.Current.CancellationToken);
        var asociado = await SubirPdfAsync(servicio, "asociado.pdf");
        const string claveHuerfana = "archivos/huerfano-de-prueba";
        var huérfano = Encoding.UTF8.GetBytes("objeto huérfano");
        await cliente!.PutObjectAsync(new PutObjectArgs()
            .WithBucket(Bucket)
            .WithObject(claveHuerfana)
            .WithStreamData(new MemoryStream(huérfano))
            .WithObjectSize(huérfano.Length)
            .WithContentType("application/octet-stream"), TestContext.Current.CancellationToken);

        var eliminados = await servicio.LimpiarAsync(DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        Assert.Equal(2, eliminados);
        var archivo = await servicio.ObtenerAsync(sesion.ArchivoId, TestContext.Current.CancellationToken);
        Assert.Equal(EstadosArchivo.Eliminado, archivo!.Estado);
        Assert.Null(await new ProveedorMinio(cliente!).ObtenerAsync(
            Bucket, claveHuerfana, TestContext.Current.CancellationToken));
        var descarga = await servicio.AbrirDescargaAsync(asociado, TestContext.Current.CancellationToken);
        Assert.NotNull(descarga);
        await descarga!.Contenido.DisposeAsync();
    }

    private ServicioAlmacenamientoArchivos CrearServicio(
        AlmacenamientoDbContext db,
        IAntivirusArchivos? antivirus = null,
        string? bucket = null,
        string? ambiente = null)
    {
        var opciones = Options.Create(new AlmacenamientoOptions
        {
            Endpoint = new Uri(minio.GetConnectionString()).Authority,
            AccessKey = AccessKey,
            SecretKey = SecretKey,
            Bucket = bucket ?? Bucket,
            Ambiente = ambiente ?? "test",
            ExpiracionCargaSegundos = 60,
            TamanoMaximoPdf = 1024 * 1024,
            TamanoMaximoImagen = 1024 * 1024,
        });
        return new ServicioAlmacenamientoArchivos(
            db,
            new ProveedorMinio(cliente!),
            antivirus ?? new AntivirusLimpio(),
            opciones,
            NullLogger<ServicioAlmacenamientoArchivos>.Instance);
    }

    private sealed class AntivirusLimpio : IAntivirusArchivos
    {
        public Task<ResultadoAntivirus> AnalizarAsync(Stream contenido, CancellationToken ct) =>
            Task.FromResult(new ResultadoAntivirus(true, true, null));
    }

    private sealed class AntivirusInfectado : IAntivirusArchivos
    {
        public Task<ResultadoAntivirus> AnalizarAsync(Stream contenido, CancellationToken ct) =>
            Task.FromResult(new ResultadoAntivirus(false, false, "infected-test"));
    }

    private sealed class UsuarioActual(Guid id) : ICurrentUser
    {
        public string? UserId => id.ToString();
        public string? Email => "docente@test.invalid";
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
    }

    private sealed class IdentityDePrueba : IConsultasIdentity
    {
        private readonly Persona persona = new()
        {
            Id = Propietario,
            Documento = "50000001",
            Nombre = "Docente",
            Apellido = "Prueba",
            CreadoEn = DateTimeOffset.UtcNow,
        };

        private readonly Usuario usuario;

        public IdentityDePrueba()
        {
            usuario = new Usuario
            {
                Id = Propietario,
                AzureOid = Guid.NewGuid(),
                Upn = "docente@test.invalid",
                NombreParaMostrar = "Docente Prueba",
                Activo = true,
                PersonaId = Propietario,
                Persona = persona,
                CreadoEn = DateTimeOffset.UtcNow,
            };
            persona.Usuario = usuario;
        }

        public Task<Persona?> ObtenerPersonaAsync(Guid personaId, CancellationToken ct) =>
            Task.FromResult(personaId == Propietario ? persona : null);

        public Task<bool> TieneRolEnMateriaAsync(Guid usuarioId, string codigoRol, Guid materiaId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<bool> TieneRolEnCarreraAsync(Guid usuarioId, string codigoRol, Guid carreraId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<bool> TieneRolGlobalAsync(Guid usuarioId, string codigoRol, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<string>> ObtenerCodigosDeRolesDeSistemaAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> ObtenerCodigosDePermisosAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<Guid>> ObtenerMateriasDeRolAsync(Guid usuarioId, string codigoRol, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<Guid>> ObtenerCarrerasDeRolAsync(Guid usuarioId, string codigoRol, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<Guid?> ObtenerCarreraDeMateriaAsync(Guid materiaId, CancellationToken ct) =>
            Task.FromResult<Guid?>(null);

        public Task<IReadOnlyList<Materia>> ListarMateriasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Materia>>([]);

        public Task<IReadOnlyList<Persona>> ListarPersonasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Persona>>([persona]);

        public Task<IReadOnlyList<Usuario>> ListarUsuariosAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Usuario>>([usuario]);
    }
}
