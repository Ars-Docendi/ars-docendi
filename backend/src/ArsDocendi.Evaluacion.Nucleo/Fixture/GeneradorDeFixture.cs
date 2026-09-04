using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ArsDocendi.Evaluacion.Nucleo.Fixture;

/// <summary>
/// Genera el fixture sintético de evaluación, determinista y sin reloj.
/// </summary>
/// <remarks>
/// <b>Determinista de verdad</b>: correrlo dos veces produce el mismo texto byte
/// a byte, en cualquier proceso y en cualquier máquina. Los identificadores se
/// derivan del índice, no de <c>gen_random_uuid()</c>, y las fechas de la ancla
/// fija, no del día.
///
/// Un dataset cuyo resultado esperado cambia con el calendario mide qué día lo
/// corriste. Por eso «lo actual» se expresa con banderas del dominio —el período
/// con <see cref="PeriodoActivo"/>, la designación con vigencia abierta— y no
/// comparando fechas contra el reloj.
///
/// <b>Las colisiones son parte del contrato</b>, no un accidente. El detector de
/// ambigüedad —épica posterior— dispara con nombres de materia repetidos entre
/// carreras y con apellidos compartidos. Si el fixture no los reprodujera, los
/// ítems de diálogo que lo prueban darían verde sin medir nada.
/// </remarks>
public sealed class GeneradorDeFixture
{
    /// <summary>Fecha ancla. Todo lo demás se calcula relativo a ésta.</summary>
    public static readonly DateOnly Ancla = new(2026, 3, 2);

    /// <summary>Cuántas carreras genera.</summary>
    public const int Carreras = 3;

    /// <summary>Índice del período que queda activo.</summary>
    private const int PeriodoActivo = 1;

    private const int Periodos = 3;

    private readonly int _personas;

    /// <summary>
    /// Nombres de materia que se repiten entre carreras, con en cuántas aparecen.
    /// </summary>
    /// <remarks>
    /// Las cardinalidades están declaradas acá y verificadas por test. Confiar en
    /// que el generador «probablemente» produzca colisiones es exactamente cómo un
    /// dataset termina no midiendo lo que dice medir.
    /// </remarks>
    public static readonly IReadOnlyList<(string Nombre, int Carreras)> MateriasCompartidas =
    [
        ("Análisis Matemático", 3),
        ("Algoritmos y Estructuras de Datos", 2),
        ("Inglés Técnico", 2),
    ];

    /// <summary>Apellidos compartidos, con cuántas personas los llevan.</summary>
    /// <remarks>
    /// Lista y no diccionario: la enumeración de un diccionario no está garantizada
    /// por contrato, y el fixture tiene que salir idéntico byte a byte.
    /// </remarks>
    public static readonly IReadOnlyList<(string Apellido, int Personas)> ApellidosCompartidos =
    [
        ("Gómez", 3),
        ("Fernández", 2),
        ("Rodríguez", 2),
        ("Suárez", 2),
    ];

    private static readonly string[] NombresDeCarrera =
    [
        "Ingeniería en Informática",
        "Ingeniería Industrial",
        "Ingeniería Electrónica",
    ];

    private static readonly string[] CodigosDeCarrera = ["INF", "IND", "ELE"];

    /// <summary>Materias propias de cada carrera, además de las compartidas.</summary>
    private static readonly string[][] MateriasPropias =
    [
        ["Paradigmas de Programación", "Bases de Datos", "Sistemas Operativos"],
        ["Investigación Operativa", "Gestión de la Calidad"],
        ["Circuitos Eléctricos", "Señales y Sistemas"],
    ];

    private static readonly string[] NombresDePila =
    [
        "Ana", "Bruno", "Carla", "Diego", "Elena", "Federico", "Gabriela",
        "Hugo", "Irene", "Javier", "Karina", "Lucas", "Marina", "Nicolás",
        "Olivia", "Pablo", "Rocío", "Santiago", "Tamara", "Valentín",
    ];

    /// <summary>
    /// Apellidos que lleva una sola persona. Tienen que alcanzar para que la lista
    /// completa no cicle: ver la nota de <see cref="ApellidosEnOrden"/>.
    /// </summary>
    private static readonly string[] ApellidosPropios =
    [
        "Acosta", "Benítez", "Cabrera", "Duarte", "Escobar", "Ferreyra",
        "Giménez", "Herrera", "Ibarra", "Juárez", "Ledesma", "Maidana",
        "Núñez", "Ocampo", "Paredes", "Quiroga", "Ramírez", "Sosa",
        "Tejada", "Urrutia", "Vega", "Zárate",
    ];

    private static readonly string[] Dedicaciones =
    [
        "Categoría 1", "Categoría 3", "Categoría 5",
    ];

    private static readonly string[] CodigosDeCargo =
    [
        "titular", "asociado", "adjunto", "jtp", "ayudante1", "ayudante2",
    ];

    /// <summary>
    /// Crea el generador.
    /// </summary>
    /// <param name="personas">
    /// Cuántas personas generar. Es parámetro para poder demostrar en un test que
    /// cambiarlo NO corre los valores de las secciones siguientes.
    /// </param>
    public GeneradorDeFixture(int personas = 24)
    {
        if (personas < 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(personas), personas,
                "El fixture necesita al menos doce personas para alojar los apellidos compartidos.");
        }

        // Sin este tope, la asignación de apellidos ciclaría y los compartidos
        // aparecerían MÁS veces de las declaradas. Las cardinalidades son un
        // contrato verificado por test: romperlas en silencio haría que el test
        // fallara sin explicar por qué, o peor, que pasara midiendo otra cosa.
        var disponibles = ApellidosEnOrden().Count;
        if (personas > disponibles)
        {
            throw new ArgumentOutOfRangeException(
                nameof(personas), personas,
                $"Solo hay {disponibles} apellidos y ciclarlos rompería las cardinalidades "
                + "de colisión declaradas. Agregá apellidos propios antes de subir este número.");
        }

        _personas = personas;
    }

    /// <summary>Genera el SQL del fixture.</summary>
    public string Generar()
    {
        var sql = new StringBuilder();

        Encabezado(sql);
        EscribirCarreras(sql);
        EscribirMaterias(sql);
        EscribirPersonas(sql);
        EscribirUsuarios(sql);
        EscribirPeriodos(sql);
        EscribirDesignaciones(sql);
        EscribirPedidos(sql);

        return sql.ToString();
    }

    /// <summary>Huella estable del fixture, para el sellado de reportes.</summary>
    public string Huella() =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Generar())));

    // ------------------------------------------------------------- secciones

    private static void Encabezado(StringBuilder sql) => sql.Append(
        $"""
        -- Fixture sintético de evaluación. GENERADO: no editar a mano.
        --
        -- Determinista: los identificadores se derivan del índice y las fechas de
        -- la fecha ancla {Ancla:yyyy-MM-dd}. Correr el generador dos veces produce
        -- este mismo archivo, byte a byte.
        --
        -- Ningún dato personal es real. «Lo actual» se expresa con banderas del
        -- dominio —periodos.activo, designaciones.vigente_hasta IS NULL— y nunca
        -- comparando contra el reloj.

        """);

    private static void EscribirCarreras(StringBuilder sql)
    {
        sql.Append("\nINSERT INTO identity.carreras (id, code, name, is_active) VALUES\n");

        var filas = Enumerable.Range(0, Carreras).Select(indice =>
            $"    ('{IdDeCarrera(indice)}', '{CodigosDeCarrera[indice]}', "
            + $"'{Escapar(NombresDeCarrera[indice])}', TRUE)");

        sql.Append(string.Join(",\n", filas));
        sql.Append("\nON CONFLICT (id) DO NOTHING;\n");
    }

    private static void EscribirMaterias(StringBuilder sql)
    {
        sql.Append("\nINSERT INTO identity.materias (id, code, name, carrera_id, is_active) VALUES\n");

        var filas = new List<string>();
        var indice = 0;

        foreach (var (carrera, propias) in MateriasPropias.Index())
        {
            foreach (var nombre in propias)
            {
                filas.Add(FilaDeMateria(indice++, nombre, carrera));
            }
        }

        // Las compartidas van después y de forma explícita: es la colisión que el
        // detector de ambigüedad necesita, y tiene que ser legible en el archivo.
        foreach (var (nombre, cuantasCarreras) in MateriasCompartidas)
        {
            for (var carrera = 0; carrera < cuantasCarreras; carrera++)
            {
                filas.Add(FilaDeMateria(indice++, nombre, carrera));
            }
        }

        sql.Append(string.Join(",\n", filas));
        sql.Append("\nON CONFLICT (id) DO NOTHING;\n");
    }

    private static string FilaDeMateria(int indice, string nombre, int carrera) =>
        $"    ('{IdDeMateria(indice)}', 'M{indice:D3}', '{Escapar(nombre)}', "
        + $"'{IdDeCarrera(carrera)}', TRUE)";

    private void EscribirPersonas(StringBuilder sql)
    {
        sql.Append(
            "\nINSERT INTO identity.personas "
            + "(id, documento, cuil, legajo, nombre, apellido, fecha_nacimiento, telefono) VALUES\n");

        var azar = FuenteDe("personas");
        var apellidos = ApellidosEnOrden();
        var filas = new List<string>();

        for (var indice = 0; indice < _personas; indice++)
        {
            var nombre = NombresDePila[azar.Next(NombresDePila.Length)];
            var apellido = apellidos[indice];
            var documento = 20_000_000 + (indice * 137);

            filas.Add(
                $"    ('{IdDePersona(indice)}', '{documento}', '20-{documento}-4', "
                + $"'L{indice:D4}', '{Escapar(nombre)}', '{Escapar(apellido)}', "
                + $"DATE '{Ancla.AddYears(-30 - (indice % 25)):yyyy-MM-dd}', "
                + $"'11-4000-{indice:D4}')");
        }

        sql.Append(string.Join(",\n", filas));
        sql.Append("\nON CONFLICT (id) DO NOTHING;\n");
    }

    /// <summary>
    /// Los apellidos, con los compartidos repetidos tantas veces como declaran.
    /// </summary>
    /// <remarks>
    /// Se arma sin tocar ninguna fuente de aleatoriedad: las cardinalidades de
    /// colisión son un contrato, no un sorteo.
    /// </remarks>
    private static IReadOnlyList<string> ApellidosEnOrden()
    {
        var apellidos = new List<string>();

        foreach (var (apellido, cuantas) in ApellidosCompartidos)
        {
            apellidos.AddRange(Enumerable.Repeat(apellido, cuantas));
        }

        apellidos.AddRange(ApellidosPropios);
        return apellidos;
    }

    private void EscribirUsuarios(StringBuilder sql)
    {
        sql.Append(
            "\nINSERT INTO identity.users (id, azure_oid, upn, display_name, is_active, persona_id) VALUES\n");

        var filas = Enumerable.Range(0, _personas).Select(indice =>
            $"    ('{IdDeUsuario(indice)}', '{IdDeAzure(indice)}', "
            + $"'usuario{indice:D3}@evaluacion.invalido', 'Usuario {indice:D3}', TRUE, "
            + $"'{IdDePersona(indice)}')");

        sql.Append(string.Join(",\n", filas));
        sql.Append("\nON CONFLICT (id) DO NOTHING;\n");

        EscribirAsignaciones(sql);
    }

    /// <summary>
    /// Asigna roles: uno global, uno por carrera y uno por materia.
    /// </summary>
    /// <remarks>
    /// Los tres ámbitos tienen que existir porque la métrica de abstención vive
    /// justamente en la diferencia: un actor acotado que recibe cero filas no está
    /// en el mismo caso que uno global que recibe cero filas.
    /// </remarks>
    private static void EscribirAsignaciones(StringBuilder sql)
    {
        sql.Append(
            "\nINSERT INTO identity.user_roles (id, user_id, role_id, materia_id, carrera_id) VALUES\n");

        string[] filas =
        [
            // Secretaría Académica: alcance global.
            $"    ('{IdDeAsignacion(0)}', '{IdDeUsuario(0)}', "
            + "'a1000000-0000-4000-8000-000000000004', NULL, NULL)",

            // Coordinador de la carrera 0.
            $"    ('{IdDeAsignacion(1)}', '{IdDeUsuario(1)}', "
            + $"'a1000000-0000-4000-8000-000000000003', NULL, '{IdDeCarrera(0)}')",

            // Jefe de cátedra de la materia 0, que pertenece a la carrera 0.
            $"    ('{IdDeAsignacion(2)}', '{IdDeUsuario(2)}', "
            + $"'a1000000-0000-4000-8000-000000000002', '{IdDeMateria(0)}', '{IdDeCarrera(0)}')",

            // Docente sin permisos de designaciones: el caso en que RLS devuelve
            // cero filas por falta de permiso y no por falta de datos.
            $"    ('{IdDeAsignacion(3)}', '{IdDeUsuario(3)}', "
            + $"'a1000000-0000-4000-8000-000000000001', '{IdDeMateria(0)}', '{IdDeCarrera(0)}')",
        ];

        sql.Append(string.Join(",\n", filas));
        sql.Append("\nON CONFLICT (id) DO NOTHING;\n");
    }

    private static void EscribirPeriodos(StringBuilder sql)
    {
        sql.Append(
            "\nINSERT INTO designaciones.periodos "
            + "(id, nombre, carga_desde, carga_hasta, impacto_desde, impacto_hasta, activo) VALUES\n");

        var filas = Enumerable.Range(0, Periodos).Select(indice =>
        {
            var desplazamiento = (indice - PeriodoActivo) * 180;
            var cargaDesde = Ancla.AddDays(desplazamiento - 30);

            return
                $"    ('{IdDePeriodo(indice)}', '{Ancla.Year + (indice - PeriodoActivo)} — Cuatrimestre 1', "
                + $"DATE '{cargaDesde:yyyy-MM-dd}', DATE '{cargaDesde.AddDays(45):yyyy-MM-dd}', "
                + $"DATE '{Ancla.AddDays(desplazamiento):yyyy-MM-dd}', "
                + $"DATE '{Ancla.AddDays(desplazamiento + 150):yyyy-MM-dd}', "
                + (indice == PeriodoActivo ? "TRUE)" : "FALSE)");
        });

        sql.Append(string.Join(",\n", filas));
        sql.Append("\nON CONFLICT (id) DO NOTHING;\n");
    }

    private void EscribirDesignaciones(StringBuilder sql)
    {
        sql.Append(
            "\nINSERT INTO designaciones.designaciones "
            + "(id, persona_id, materia_id, cargo_id, dedicacion, horas, vigente_desde, vigente_hasta) VALUES\n");

        var azar = FuenteDe("designaciones");
        var materias = TotalDeMaterias();
        var filas = new List<string>();

        for (var indice = 0; indice < _personas; indice++)
        {
            var cargo = indice % CodigosDeCargo.Length;
            var materia = indice % materias;

            // Una de cada cinco está cerrada: sin designaciones históricas, la
            // diferencia entre «vigente» e «hubo» no se puede medir.
            var cerrada = indice % 5 == 4;
            var vigenteHasta = cerrada
                ? $"DATE '{Ancla.AddDays(-60):yyyy-MM-dd}'"
                : "NULL";

            filas.Add(
                $"    ('{IdDeDesignacion(indice)}', '{IdDePersona(indice)}', '{IdDeMateria(materia)}', "
                + $"'{IdDeCargo(cargo)}', '{Dedicaciones[azar.Next(Dedicaciones.Length)]}', "
                + $"{4 + azar.Next(8)}, DATE '{Ancla.AddDays(-365):yyyy-MM-dd}', {vigenteHasta})");
        }

        sql.Append(string.Join(",\n", filas));
        sql.Append("\nON CONFLICT (id) DO NOTHING;\n");
    }

    private void EscribirPedidos(StringBuilder sql)
    {
        sql.Append(
            "\nINSERT INTO designaciones.pedidos "
            + "(id, numero, periodo_id, persona_id, materia_id, novedad, estado, prioritario, "
            + "cargo_solicitado_id, dedicacion_solicitada, horas) VALUES\n");

        string[] novedades = ["Alta", "Baja", "Cambio de cargo o dedicación", "Sin novedad"];
        string[] estados =
        [
            "borrador", "en_revision_coordinador", "en_revision_secretaria",
            "en_revision_decanato", "en_lote", "rechazado",
        ];

        var azar = FuenteDe("pedidos");
        var materias = TotalDeMaterias();
        var filas = new List<string>();
        var cuantos = _personas / 2;

        for (var indice = 0; indice < cuantos; indice++)
        {
            var novedad = novedades[indice % novedades.Length];
            var esBaja = novedad == "Baja";

            filas.Add(
                $"    ('{IdDePedido(indice)}', '{Ancla.Year}-{indice:D6}', "
                + $"'{IdDePeriodo(PeriodoActivo)}', '{IdDePersona(indice)}', "
                + $"'{IdDeMateria(indice % materias)}', '{Escapar(novedad)}', "
                + $"'{estados[indice % estados.Length]}', {(indice % 7 == 0 ? "TRUE" : "FALSE")}, "
                + (esBaja ? "NULL, NULL, NULL)" :
                    $"'{IdDeCargo(indice % CodigosDeCargo.Length)}', "
                    + $"'{Dedicaciones[azar.Next(Dedicaciones.Length)]}', {4 + azar.Next(8)})"));
        }

        sql.Append(string.Join(",\n", filas));
        sql.Append("\nON CONFLICT (id) DO NOTHING;\n");
    }

    // ------------------------------------------------------------------ apoyo

    /// <summary>
    /// Fuente de aleatoriedad propia de cada sección.
    /// </summary>
    /// <remarks>
    /// <b>Ésta es la trampa que evita.</b> Con una sola fuente compartida, cambiar
    /// cuántas veces se la llama en una sección corre todos los valores de las
    /// siguientes. Los apellidos compartidos son el ancla de varios ítems de
    /// diálogo, así que ese acoplamiento rompería ítems que nadie tocó.
    ///
    /// La semilla se deriva del nombre de la sección con un hash estable, no con
    /// <c>GetHashCode()</c>, que en .NET está aleatorizado por proceso.
    /// </remarks>
    private static Random FuenteDe(string seccion)
    {
        var resumen = SHA256.HashData(Encoding.UTF8.GetBytes(seccion));
        return new Random(BitConverter.ToInt32(resumen, 0));
    }

    private static int TotalDeMaterias() =>
        MateriasPropias.Sum(propias => propias.Length)
        + MateriasCompartidas.Sum(compartida => compartida.Carreras);

    private static string Escapar(string valor) => valor.Replace("'", "''", StringComparison.Ordinal);

    private static string Identificador(char prefijo, int indice) =>
        string.Create(CultureInfo.InvariantCulture, $"{prefijo}0000000-0000-4000-8000-{indice:D12}");

    /// <summary>Identificador de carrera, derivado del índice.</summary>
    public static string IdDeCarrera(int indice) => Identificador('c', indice);

    /// <summary>Identificador de materia, derivado del índice.</summary>
    public static string IdDeMateria(int indice) => Identificador('7', indice);

    /// <summary>Identificador de persona, derivado del índice.</summary>
    public static string IdDePersona(int indice) => Identificador('d', indice);

    /// <summary>Identificador de cuenta, derivado del índice.</summary>
    public static string IdDeUsuario(int indice) => Identificador('a', indice);

    private static string IdDeAzure(int indice) => Identificador('9', indice);

    private static string IdDeAsignacion(int indice) => Identificador('e', indice);

    /// <summary>Identificador de período, derivado del índice.</summary>
    public static string IdDePeriodo(int indice) => Identificador('b', indice);

    private static string IdDeDesignacion(int indice) => Identificador('f', indice);

    private static string IdDePedido(int indice) => Identificador('8', indice);

    /// <summary>
    /// Identificador del cargo. Los cargos ya vienen sembrados por la migración de
    /// <c>designaciones</c>, así que el fixture usa sus identificadores fijos en
    /// lugar de crear otros.
    /// </summary>
    private static string IdDeCargo(int indice) =>
        $"c3000000-0000-4000-8000-{indice + 1:D12}";
}
