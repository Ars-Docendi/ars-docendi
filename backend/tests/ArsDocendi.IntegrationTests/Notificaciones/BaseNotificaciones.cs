using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Notificaciones;

/// <summary>Row of <c>notificaciones.notificaciones</c> as the tests read it straight from the database.</summary>
public sealed record FilaNotificacion(
    Guid Id, Guid Destinatario, string? RolDestino, string TipoEvento, string TipoEntidad, Guid EntidadId,
    string Titulo, string Mensaje, string Contexto, DateTimeOffset? LeidaEn, DateTimeOffset CreadaEn);

/// <summary>
/// Shared plumbing of the notification tests: synthetic seed, host with development auth and
/// direct SQL access to the notifications table.
/// </summary>
public abstract class BaseNotificaciones(PostgresFixture postgres, string prefijo)
    : ClasePostgresAislada(postgres, prefijo)
{
    protected static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    protected static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    protected static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    protected static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    protected static readonly Guid Decanato = Guid.Parse("a0000000-0000-4000-8000-000000000005");
    protected static readonly Guid Administrativo = Guid.Parse("a0000000-0000-4000-8000-000000000006");
    protected static readonly Guid SysAdmin = Guid.Parse("a0000000-0000-4000-8000-000000000007");
    protected static readonly Guid MateriaDelDocente = Guid.Parse("70000000-0000-4000-8000-000000000101");

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected async Task SembrarAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), Ct);
        await EjecutarAsync(sql, 60);
    }

    protected WebApplicationFactory<Program> CrearHost(Action<IServiceCollection>? servicios = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");
            if (servicios is not null) builder.ConfigureTestServices(servicios);
        });

    protected static HttpClient ClienteDe(WebApplicationFactory<Program> host, Guid usuario, string rol)
    {
        var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
        return cliente;
    }

    protected async Task EjecutarAsync(string sql, int tiempoLimite = 30, params (string Nombre, object? Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = tiempoLimite };
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        await comando.ExecuteNonQueryAsync(Ct);
    }

    protected async Task<long> EscalarAsync(string sql, params (string Nombre, object? Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        return (long)(await comando.ExecuteScalarAsync(Ct))!;
    }

    protected async Task<Guid> InsertarAsync(
        Guid destinatario, string? rolDestino = null, DateTimeOffset? creadaEn = null, bool leida = false,
        string titulo = "Titulo", string tipoEntidad = "TAREA", Guid? entidadId = null)
    {
        var id = Guid.NewGuid();
        await EjecutarAsync("""
            INSERT INTO notificaciones.notificaciones
                (id, destinatario_usuario_id, rol_destino, tipo_evento, tipo_entidad, entidad_id, titulo, mensaje, creada_en, leida_en)
            VALUES (@id, @d, @r, 'TAREA_ASIGNADA', @te, @e, @t, 'Mensaje', @c, CASE WHEN @l THEN @c ELSE NULL END)
            """, 30,
            ("id", id), ("d", destinatario), ("r", rolDestino), ("te", tipoEntidad),
            ("e", entidadId ?? Guid.NewGuid()), ("t", titulo), ("c", creadaEn ?? DateTimeOffset.UtcNow), ("l", leida));
        return id;
    }

    protected async Task<IReadOnlyList<FilaNotificacion>> NotificacionesAsync(Guid? destinatario = null)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            SELECT id, destinatario_usuario_id, rol_destino, tipo_evento, tipo_entidad, entidad_id,
                   titulo, mensaje, contexto::text, leida_en, creada_en
            FROM notificaciones.notificaciones
            WHERE @d::uuid IS NULL OR destinatario_usuario_id = @d
            ORDER BY creada_en, id
            """, conexion);
        comando.Parameters.AddWithValue("d", (object?)destinatario ?? DBNull.Value);
        await using var lector = await comando.ExecuteReaderAsync(Ct);
        var filas = new List<FilaNotificacion>();
        while (await lector.ReadAsync(Ct))
        {
            filas.Add(new FilaNotificacion(
                lector.GetGuid(0), lector.GetGuid(1), lector.IsDBNull(2) ? null : lector.GetString(2),
                lector.GetString(3), lector.GetString(4), lector.GetGuid(5), lector.GetString(6),
                lector.GetString(7), lector.GetString(8),
                lector.IsDBNull(9) ? null : lector.GetFieldValue<DateTimeOffset>(9),
                lector.GetFieldValue<DateTimeOffset>(10)));
        }
        return filas;
    }

    protected static System.Text.Json.JsonElement ContextoDe(FilaNotificacion fila) =>
        System.Text.Json.JsonDocument.Parse(fila.Contexto).RootElement.Clone();

    protected static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<T>(Ct))!;

    private static string RaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "AGENTS.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("The repository root was not found.");
    }
}
