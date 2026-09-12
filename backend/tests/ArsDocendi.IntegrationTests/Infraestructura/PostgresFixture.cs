using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Modules.Asistente;
using Modules.Designaciones.Infrastructure;
using Modules.Portal.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: Xunit.AssemblyFixture(typeof(ArsDocendi.IntegrationTests.Infraestructura.PostgresFixture))]

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Base de prueba aislada, con los roles del asistente ya creados sobre ella.
/// </summary>
public sealed record BaseDePrueba(
    string Cadena,
    string RolSoloLectura,
    string RolSoloLecturaPii,
    string Password);

public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Password de los roles del asistente en el contenedor efímero de test.</summary>
    internal const string PasswordDeRol = "asistente-de-prueba";

    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("ars_docendi_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public ValueTask InitializeAsync() => new(_contenedor.StartAsync());

    public ValueTask DisposeAsync() => new(_contenedor.DisposeAsync().AsTask());

    /// <summary>
    /// Crea una base migrada y, sobre ella, los dos roles de solo lectura del
    /// asistente con sus privilegios ya aplicados.
    /// </summary>
    /// <remarks>
    /// Los roles se crean acá y no en <c>provision-db.sh</c> porque ese script no
    /// corre en los tests: la base de prueba nace de las migraciones EF. Llevan
    /// sufijo único porque los roles son objetos de CLUSTER y varias clases de
    /// test comparten el contenedor.
    ///
    /// Se les dan los mismos atributos que en producción —en particular
    /// <c>NOBYPASSRLS</c>— para que lo que se prueba acá sea lo que se despliega.
    /// </remarks>
    /// <summary>
    /// La base plantilla: migrada una sola vez por corrida y nunca usada por un
    /// test.
    /// </summary>
    /// <remarks>
    /// Las 45 clases de test creaban su base y corrían las tres migraciones cada
    /// una. Las migraciones son idénticas siempre —no dependen del test— así que
    /// se corren una vez y el resto de las bases se clonan con
    /// <c>CREATE DATABASE … TEMPLATE</c>, que PostgreSQL resuelve copiando
    /// archivos.
    ///
    /// <b>La plantilla NO lleva roles ni privilegios</b>, y eso no es una omisión.
    /// Los roles son objetos de CLUSTER y los GRANT los nombran: clonar una base
    /// con GRANT adentro dejaría el clon concediéndole a los roles de la
    /// plantilla, no a los de la clase. Se crean después del clon, sobre él, con
    /// el mismo código que usa el migrador del módulo.
    ///
    /// Ese orden resuelve además la trampa del <c>search_path</c>: vive en
    /// <c>pg_db_role_setting</c>, cuya clave es el OID de la base, y <b>no se
    /// clona</b>. Como los roles se crean sobre el clon ya existente, el
    /// <c>ALTER ROLE … IN DATABASE</c> apunta al OID correcto. Si algún día la
    /// plantilla llevara roles, esto pasaría a ser un falso verde silencioso en
    /// todos los tests que sostienen el invariante #14; hay un test que lo vigila.
    /// </remarks>
    private string? _plantilla;

    private readonly SemaphoreSlim _candadoDeLaPlantilla = new(1, 1);

    private async Task<string> PlantillaAsync()
    {
        if (_plantilla is not null)
        {
            return _plantilla;
        }

        await _candadoDeLaPlantilla.WaitAsync();
        try
        {
            if (_plantilla is not null)
            {
                return _plantilla;
            }

            var nombre = $"plantilla_{Guid.NewGuid():N}";
            await CrearBaseVaciaAsync(nombre);
            await MigrarAsync(CadenaDe(nombre));

            // Sin esto, la primera clonación falla: PostgreSQL no admite
            // CREATE DATABASE … TEMPLATE mientras alguien esté conectado a la
            // plantilla, y EF deja conexiones vivas después de migrar.
            NpgsqlConnection.ClearAllPools();

            _plantilla = nombre;
            return nombre;
        }
        finally
        {
            _candadoDeLaPlantilla.Release();
        }
    }

    private async Task CrearBaseVaciaAsync(string nombre, string? plantilla = null)
    {
        await using var conexion = new NpgsqlConnection(_contenedor.GetConnectionString());
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(
            plantilla is null
                ? $"CREATE DATABASE \"{nombre}\""
                : $"CREATE DATABASE \"{nombre}\" TEMPLATE \"{plantilla}\"",
            conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private string CadenaDe(string nombre) =>
        new NpgsqlConnectionStringBuilder(_contenedor.GetConnectionString())
        {
            Database = nombre,
            Pooling = false,
        }.ConnectionString;

    /// <summary>Corre las tres migraciones, en el orden del Host.</summary>
    /// <remarks>
    /// EL ORDEN ES EL DEL HOST, y no es indiferente. <c>Program</c> compone
    /// identity → designaciones → aulas → portal → tareas → asistente, y la RLS de
    /// portal invoca una función que referencia <c>designaciones.designaciones</c>:
    /// con portal primero, el CREATE FUNCTION falla con «relation does not exist».
    ///
    /// Estuvo al revés y no rompía nada, porque hasta que portal no dependió de
    /// designaciones las dos secuencias eran equivalentes. Un fixture que migra en
    /// un orden que producción no usa prueba otro sistema, y sólo se nota el día
    /// que el orden empieza a importar.
    /// </remarks>
    private static async Task MigrarAsync(string cadena, string? migracionDesignaciones = null)
    {
        await using (var identity = CrearIdentity(cadena))
        {
            await identity.Database.MigrateAsync();
        }

        await using (var designaciones = CrearDesignaciones(cadena))
        {
            if (migracionDesignaciones is null)
            {
                await designaciones.Database.MigrateAsync();
            }
            else
            {
                await designaciones.GetService<IMigrator>().MigrateAsync(migracionDesignaciones);
            }
        }

        await using (var portal = CrearPortal(cadena))
        {
            await portal.Database.MigrateAsync();
        }
    }

    /// <param name="migracionDesignaciones">
    /// Hasta qué migración de designaciones migrar. En <c>null</c> —lo normal— va
    /// hasta la última y la base se CLONA de la plantilla.
    /// </param>
    /// <remarks>
    /// <b>Pedir una migración concreta desactiva la plantilla</b>, y no es una
    /// omisión: un clon ya viene con el historial entero aplicado, así que no hay
    /// forma de que quede a mitad de camino. Ese caso migra desde una base vacía y
    /// paga el precio completo — por eso lo usa un solo test, el que verifica cómo
    /// se comportaba el esquema ANTES de una migración.
    /// </remarks>
    public async Task<BaseDePrueba> CrearBaseMigradaAsync(
        string prefijo, string? migracionDesignaciones = null)
    {
        var identificador = Guid.NewGuid();
        var nombre = $"{prefijo}_{identificador:N}";
        var sufijoRol = identificador.ToString("N")[..8];
        var rolSoloLectura = $"asistente_ro_t{sufijoRol}";
        var rolSoloLecturaPii = $"asistente_ro_pii_t{sufijoRol}";

        if (migracionDesignaciones is null)
        {
            await CrearBaseVaciaAsync(nombre, await PlantillaAsync());
        }
        else
        {
            await CrearBaseVaciaAsync(nombre);
            await MigrarAsync(CadenaDe(nombre), migracionDesignaciones);
        }

        var cadena = CadenaDe(nombre);

        await CrearRolesDelAsistenteAsync(nombre, rolSoloLectura, rolSoloLecturaPii);

        // Los GRANT se aplican con el MISMO código que el migrador del módulo, no
        // con una copia del script: si acá se probara una copia, la prueba diría
        // que la copia funciona.
        //
        // NO se aplican sobre un esquema a medio migrar: los privilegios del
        // asistente nombran tablas y columnas del esquema COMPLETO, así que sobre
        // un estado histórico fallan con «relation does not exist». Quien pide una
        // migración parcial está probando la migración, no los privilegios.
        if (migracionDesignaciones is null)
        {
            await using var conexion = new NpgsqlConnection(cadena);
            await conexion.OpenAsync();
            await PrivilegiosAsistente.AplicarAsync(
                conexion, rolSoloLectura, rolSoloLecturaPii, CancellationToken.None);

            await RegistrosAsistente.AplicarAsync(
                conexion, rolSoloLectura, rolSoloLecturaPii, CancellationToken.None);
        }

        return new BaseDePrueba(cadena, rolSoloLectura, rolSoloLecturaPii, PasswordDeRol);
    }

    public async Task EliminarBaseAsync(BaseDePrueba baseDePrueba)
    {
        var nombre = new NpgsqlConnectionStringBuilder(baseDePrueba.Cadena).Database
            ?? throw new InvalidOperationException("La cadena no contiene una base de datos.");

        await using var conexion = new NpgsqlConnection(_contenedor.GetConnectionString());
        await conexion.OpenAsync();
        await EjecutarAsync(conexion, $"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)");

        // Los roles sobreviven al DROP DATABASE: son de cluster. Sin esta baja, cada
        // clase de test dejaría dos roles colgados en el contenedor compartido.
        foreach (var rol in new[] { baseDePrueba.RolSoloLectura, baseDePrueba.RolSoloLecturaPii })
        {
            await EjecutarAsync(conexion, $"DROP ROLE IF EXISTS \"{rol}\"");
        }
    }

    private async Task CrearRolesDelAsistenteAsync(string baseDeDatos, params string[] roles)
    {
        await using var conexion = new NpgsqlConnection(_contenedor.GetConnectionString());
        await conexion.OpenAsync();
        foreach (var rol in roles)
        {
            await EjecutarAsync(conexion,
                $"""
                CREATE ROLE "{rol}" WITH LOGIN PASSWORD '{PasswordDeRol}'
                    NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT
                """);

            // Igual que en producción: nombres calificados obligatorios.
            await EjecutarAsync(conexion,
                $"ALTER ROLE \"{rol}\" IN DATABASE \"{baseDeDatos}\" SET search_path = ''");
        }
    }

    private static async Task EjecutarAsync(NpgsqlConnection conexion, string sql)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    public static IdentityDbContext CrearIdentity(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(cadena, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .Options;
        return new IdentityDbContext(opciones);
    }

    public static DesignacionesDbContext CrearDesignaciones(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<DesignacionesDbContext>()
            .UseNpgsql(cadena)
            .Options;
        return new DesignacionesDbContext(opciones);
    }

    public static PortalDbContext CrearPortal(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql(cadena)
            .Options;
        return new PortalDbContext(opciones);
    }
}

/// <summary>
/// Base de toda clase de test que necesita una base de datos propia.
/// </summary>
/// <remarks>
/// <b>El <c>[Trait]</c> está acá y no en cada clase</b>, y esa es la decisión.
/// xUnit hereda los traits de la clase base, así que marcar la base marca a las
/// 45 derivadas sin que ninguna tenga que acordarse: <b>heredar es la marca</b>, y
/// no puede desincronizarse. Una clase que necesite base y no herede de acá es un
/// hallazgo, no algo a parchear pegándole la etiqueta a mano.
///
/// Sirve para el ciclo corto local:
/// <c>dotnet test backend/ArsDocendi.slnx --filter 'carril!=base'</c> corre los
/// casos puros —los que no levantan Docker— en menos de un segundo. El filtro va
/// escrito con <c>!=</c> a propósito, que es <b>fail-safe</b>: si el trait
/// desapareciera, la corrida incluiría todo. Con <c>carril=base</c> un typo
/// correría cero tests en verde, que es la forma peor.
///
/// <b>El gate del PR sigue siendo la suite completa</b>: este filtro es una
/// comodidad del ciclo de trabajo, no un recorte de lo que se verifica.
/// </remarks>
[Trait("carril", "base")]
public abstract class ClasePostgresAislada(PostgresFixture postgres, string prefijo) : IAsyncLifetime
{
    private BaseDePrueba? _base;

    /// <summary>
    /// La fixture, para los tests que crean bases EXTRA por su cuenta.
    /// </summary>
    /// <remarks>
    /// Lo usan los que necesitan más de una base a la vez —comparar dos, o migrar
    /// hasta un punto concreto del historial—, que es un caso distinto del de la
    /// base propia que esta clase provisiona sola.
    /// </remarks>
    protected PostgresFixture Postgres => postgres;

    protected string Cadena => _base?.Cadena ?? string.Empty;

    /// <summary>Rol de solo lectura del asistente, sin datos personales.</summary>
    protected string RolSoloLectura => _base?.RolSoloLectura ?? string.Empty;

    /// <summary>Rol de solo lectura del asistente, con datos personales.</summary>
    protected string RolSoloLecturaPii => _base?.RolSoloLecturaPii ?? string.Empty;

    public async ValueTask InitializeAsync()
    {
        _base = await postgres.CrearBaseMigradaAsync(prefijo);
    }

    public async ValueTask DisposeAsync()
    {
        _apertura?.Dispose();
        _apertura = null;

        if (_base is not null)
        {
            await postgres.EliminarBaseAsync(_base);
            _base = null;
        }
    }

    /// <summary>
    /// Techo de cada comando de test, en segundos.
    /// </summary>
    /// <remarks>
    /// Está acá y no como parámetro porque ningún llamador quiere otro valor: lo
    /// que hacía falta era que dejara de ser un detalle que cada copia recordaba o
    /// se olvidaba. Tres de las copias que esto reemplaza no lo ponían.
    /// </remarks>
    private const int TimeoutDeComandoSegundos = 60;

    /// <summary>Arma un comando con sus parámetros y el techo de la clase.</summary>
    protected static NpgsqlCommand Preparar(
        NpgsqlConnection conexion, string sql, params (string Nombre, object Valor)[] parametros)
    {
        var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = TimeoutDeComandoSegundos };

        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        return comando;
    }

    /// <summary>Ejecuta SQL sin resultado sobre la base de la clase.</summary>
    protected async Task EjecutarAsync(
        string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = Preparar(conexion, sql, parametros);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Lee el primer valor de la primera fila.</summary>
    protected async Task<T> EscalarAsync<T>(
        string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = Preparar(conexion, sql, parametros);

        return (T)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>
    /// El seed sintético, leído una sola vez por corrida.
    /// </summary>
    /// <remarks>
    /// El <c>Lazy</c> no recibe token a propósito: el <c>Task</c> es compartido por
    /// todas las clases de test, y atarlo al token del primero lo cancelaría para
    /// los que vengan después.
    /// </remarks>
    private static readonly Lazy<Task<string>> Semilla = new(() => File.ReadAllTextAsync(
        Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql")));

    /// <summary>Aplica el seed sintético sobre la base de la clase.</summary>
    protected Task SembrarAsync() => SembrarAsync(TestContext.Current.CancellationToken);

    /// <inheritdoc cref="SembrarAsync()"/>
    /// <remarks>
    /// El <c>CommandTimeout</c> alto no es defensivo: el seed son cientos de líneas
    /// de SQL en un solo comando y con el default de Npgsql llega justo.
    /// </remarks>
    protected Task SembrarAsync(CancellationToken ct) => SembrarAsync(Cadena, ct);

    /// <summary>
    /// Aplica el seed sobre una base CUALQUIERA, no la de esta clase.
    /// </summary>
    /// <remarks>
    /// Existe para los tests que crean bases extra —comparar dos, verificar que
    /// reconstruir una no toca a la vecina—. Comparte el archivo cacheado con la
    /// sobrecarga de arriba: es el mismo seed, leído una sola vez por corrida.
    /// </remarks>
    protected static async Task SembrarAsync(string cadena, CancellationToken ct)
    {
        var sql = await Semilla.Value;

        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    protected async Task<NpgsqlConnection> AbrirConexionAsync()
    {
        var conexion = new NpgsqlConnection(Cadena);
        await conexion.OpenAsync();
        return conexion;
    }

    /// <summary>
    /// Abre una conexión autenticada como uno de los dos roles del asistente. Es
    /// la única forma honesta de probar un límite que impone el motor: consultarlo
    /// desde el dueño de la base no prueba nada.
    /// </summary>
    /// <summary>
    /// Las dos cadenas de solo lectura de la base de prueba, con los tipos
    /// envoltorio que usa el módulo.
    /// </summary>
    /// <remarks>
    /// Existen para que las piezas del carril se prueben con las mismas cadenas
    /// tipadas que reciben en producción: armar un <c>string</c> suelto acá
    /// probaría un camino que el módulo no tiene.
    /// </remarks>
    protected (CadenaSoloLectura Basica, CadenaSoloLecturaPii ConDatosPersonales) CadenasDeLectura()
    {
        var actual = _base ?? throw new InvalidOperationException("La base de prueba no está inicializada.");

        return (new CadenaSoloLectura(CadenaDeRol(actual, actual.RolSoloLectura)),
                new CadenaSoloLecturaPii(CadenaDeRol(actual, actual.RolSoloLecturaPii)));
    }

    private AperturaDeLectura? _apertura;

    /// <summary>
    /// La apertura de lectura del módulo, sobre las dos cadenas de la base de
    /// prueba.
    /// </summary>
    /// <remarks>
    /// Una por clase de test y no una por uso: cada <c>AperturaDeLectura</c> lleva
    /// dos <c>NpgsqlDataSource</c>, y construir una por consumidor dejaría fuentes
    /// sin disponer. Se libera en <c>DisposeAsync</c>.
    /// </remarks>
    /// <remarks>
    /// <c>private protected</c> y no <c>protected</c>: esta clase es pública y
    /// <c>AperturaDeLectura</c> es <c>internal</c> del módulo. Es la misma razón
    /// por la que <c>ClasificadorDeSensibilidad()</c> devuelve la interfaz.
    /// </remarks>
    private protected AperturaDeLectura Apertura
    {
        get
        {
            if (_apertura is null)
            {
                var (basica, conDatosPersonales) = CadenasDeLectura();
                _apertura = new AperturaDeLectura(
                    basica, conDatosPersonales, Options.Create(new OpcionesAsistente()));
            }

            return _apertura;
        }
    }

    /// <summary>
    /// El clasificador de sensibilidad resuelto contra la base de prueba.
    /// </summary>
    /// <remarks>
    /// Se construye con el manifiesto real del repositorio y no con uno de
    /// juguete: si el manifiesto nombrara una columna que ya no existe, los tests
    /// del carril tienen que enterarse.
    /// </remarks>
    /// <remarks>
    /// Devuelve la interfaz y no el tipo concreto porque esta clase es pública y el
    /// catálogo es <c>internal</c> del módulo. El test del caché, que necesita el
    /// contador de lecturas, lo construye por su cuenta.
    /// </remarks>
    protected IClasificadorDeSensibilidad ClasificadorDeSensibilidad() =>
        new CatalogoDeSensibilidad(Apertura, ManifiestoDeSensibilidad.Cargar());

    private static string CadenaDeRol(BaseDePrueba baseDePrueba, string rol) =>
        new NpgsqlConnectionStringBuilder(baseDePrueba.Cadena)
        {
            Username = rol,
            Password = baseDePrueba.Password,
            Pooling = false,
        }.ConnectionString;

    protected async Task<NpgsqlConnection> AbrirConexionComoAsistenteAsync(bool conDatosPersonales)
    {
        var actual = _base ?? throw new InvalidOperationException("La base de prueba no está inicializada.");
        var cadena = new NpgsqlConnectionStringBuilder(actual.Cadena)
        {
            Username = conDatosPersonales ? actual.RolSoloLecturaPii : actual.RolSoloLectura,
            Password = actual.Password,
            Pooling = false,
        }.ConnectionString;

        var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync();
        return conexion;
    }
}
