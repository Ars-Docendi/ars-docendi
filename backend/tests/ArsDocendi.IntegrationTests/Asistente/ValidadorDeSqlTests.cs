using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el validador de la SQL generada (RNF-08, RNF-23).
/// </summary>
/// <remarks>
/// Todo acá corre en memoria: el validador es una función pura sobre texto. Los
/// tests que verifican que la <b>otra</b> capa —el motor— también rechaza están
/// en <see cref="RlsAlcanceTests"/> y en <see cref="EjecucionAcotadaTests"/>.
///
/// El ataque de los tres primeros tests está verificado sobre una base real en el
/// prototipo previo: fijándose otro actor con <c>"set_config"</c> entre comillas
/// dobles, una consulta que debía devolver 26 filas devolvía 138.
/// </remarks>
public sealed class ValidadorDeSqlTests
{
    // ------------------------------------------- identificadores entrecomillados

    [Fact]
    public void Rechaza_la_escritura_del_actor_entre_comillas_dobles()
    {
        var veredicto = ValidadorDeSql.Validar(
            """SELECT "set_config"('app.asistente_user_id','x',true)""");

        Assert.False(veredicto.EsValida);
    }

    [Fact]
    public void Rechaza_la_variante_con_subconsulta_escalar()
    {
        var veredicto = ValidadorDeSql.Validar(
            """
            SELECT p.id,
                   (SELECT "set_config"('app.asistente_user_id','00000000-0000-0000-0000-000000000001',true))
              FROM identity.personas p
            """);

        Assert.False(veredicto.EsValida);
    }

    [Fact]
    public void Rechaza_la_variante_con_join_lateral()
    {
        var veredicto = ValidadorDeSql.Validar(
            """
            SELECT p.id
              FROM identity.personas p
              CROSS JOIN LATERAL (
                  SELECT "set_config"('app.asistente_user_id','00000000-0000-0000-0000-000000000001',true)
              ) AS colado
            """);

        Assert.False(veredicto.EsValida);
    }

    [Fact]
    public void Rechaza_la_lectura_del_ajuste_entre_comillas_dobles()
    {
        var veredicto = ValidadorDeSql.Validar(
            """SELECT "current_setting"('app.asistente_user_id')""");

        Assert.False(veredicto.EsValida);
    }

    [Theory]
    [InlineData("""SELECT "SET_CONFIG"('a','b',true)""")]
    [InlineData("""SELECT "Set_Config"('a','b',true)""")]
    [InlineData("""SELECT "set_config"   ('a','b',true)""")]
    [InlineData("SELECT \"set_config\"\n\n('a','b',true)")]
    [InlineData("""SELECT "set_config"/* nada que ver */('a','b',true)""")]
    public void Ni_las_mayusculas_ni_el_espacio_intercalado_evaden(string sql)
    {
        // El chequeo no exige que el paréntesis siga al nombre: alcanza con que el
        // nombre aparezca. Por eso ni el espacio, ni el salto de línea, ni un
        // comentario intercalado abren una vía.
        Assert.False(ValidadorDeSql.Validar(sql).EsValida);
    }

    [Fact]
    public void Acepta_un_alias_entrecomillado_legitimo()
    {
        var veredicto = ValidadorDeSql.Validar(
            """SELECT count(*) AS "cantidad" FROM identity.carreras""");

        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    [Fact]
    public void Acepta_un_alias_entrecomillado_que_coincide_con_palabra_clave()
    {
        // Un identificador NO es una palabra clave. Chequear los entrecomillados
        // contra palabras clave rompería consultas correctas sin cerrar ningún
        // agujero: el agujero eran las FUNCIONES.
        var veredicto = ValidadorDeSql.Validar(
            """SELECT c.name AS "update", c.code AS "grant" FROM identity.carreras c""");

        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    // ------------------------------------------------------------------- reloj

    [Theory]
    [InlineData("now")]
    [InlineData("current_date")]
    [InlineData("current_timestamp")]
    [InlineData("localtime")]
    [InlineData("localtimestamp")]
    [InlineData("statement_timestamp")]
    [InlineData("clock_timestamp")]
    [InlineData("transaction_timestamp")]
    public void Rechaza_cada_una_de_las_ocho_funciones_de_reloj(string funcion)
    {
        var veredicto = ValidadorDeSql.Validar(
            $"SELECT * FROM designaciones.pedidos WHERE created_at < {funcion}()");

        Assert.False(veredicto.EsValida);
    }

    [Theory]
    [InlineData("current_date")]
    [InlineData("current_timestamp")]
    [InlineData("localtime")]
    [InlineData("localtimestamp")]
    public void Rechaza_las_formas_sin_parentesis(string funcion)
    {
        // PostgreSQL admite estas cuatro sin paréntesis. Un chequeo que exigiera
        // el paréntesis para considerarlas función las dejaría pasar todas.
        var veredicto = ValidadorDeSql.Validar(
            $"SELECT * FROM designaciones.pedidos WHERE created_at < {funcion}");

        Assert.False(veredicto.EsValida);
    }

    [Fact]
    public void Rechaza_el_reloj_entrecomillado()
    {
        Assert.False(ValidadorDeSql.Validar(
            """SELECT "now"() AS ahora FROM identity.carreras""").EsValida);
    }

    [Fact]
    public void Acepta_una_fecha_literal()
    {
        // La fecha del turno entra por parámetro, así que prohibir el reloj no
        // rompe ningún caso legítimo.
        var veredicto = ValidadorDeSql.Validar(
            """
            SELECT id FROM designaciones.designaciones
             WHERE vigente_hasta < DATE '2026-03-01'
            """);

        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    // -------------------------------------------------- mutación y sentencias

    [Theory]
    [InlineData("INSERT INTO identity.carreras (code, name) VALUES ('x','y')")]
    [InlineData("UPDATE identity.carreras SET name = 'x'")]
    [InlineData("DELETE FROM identity.carreras")]
    [InlineData("DROP TABLE identity.carreras")]
    [InlineData("CREATE TABLE colada (id int)")]
    [InlineData("ALTER TABLE identity.carreras ADD COLUMN colada int")]
    [InlineData("GRANT SELECT ON identity.carreras TO PUBLIC")]
    [InlineData("TRUNCATE identity.carreras")]
    public void Rechaza_la_mutacion_y_la_definicion_de_datos(string sql)
    {
        Assert.False(ValidadorDeSql.Validar(sql).EsValida);
    }

    [Fact]
    public void Rechaza_una_mutacion_escondida_detras_de_un_select()
    {
        // Empieza con SELECT, así que la lista blanca del comienzo no alcanza:
        // la palabra clave prohibida aparece más adelante.
        Assert.False(ValidadorDeSql.Validar(
            "SELECT * FROM identity.carreras; DELETE FROM identity.carreras").EsValida);
    }

    [Fact]
    public void Rechaza_dos_sentencias()
    {
        Assert.False(ValidadorDeSql.Validar(
            "SELECT 1 FROM identity.carreras; SELECT 2 FROM identity.carreras").EsValida);
    }

    [Fact]
    public void Acepta_el_punto_y_coma_final()
    {
        var veredicto = ValidadorDeSql.Validar("SELECT code FROM identity.carreras;");
        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    [Fact]
    public void Acepta_un_punto_y_coma_dentro_de_un_literal()
    {
        var veredicto = ValidadorDeSql.Validar(
            "SELECT code FROM identity.carreras WHERE name = 'uno; dos'");

        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    [Fact]
    public void Rechaza_lo_que_no_empieza_con_select_ni_with()
    {
        // Lista blanca del comienzo: las listas de prohibiciones se quedan
        // cortas, y acá alcanza con dos entradas.
        Assert.False(ValidadorDeSql.Validar("EXPLAIN SELECT 1").EsValida);
        Assert.False(ValidadorDeSql.Validar("VALUES (1)").EsValida);
    }

    [Fact]
    public void Acepta_una_consulta_que_empieza_con_with()
    {
        var veredicto = ValidadorDeSql.Validar(
            """
            WITH activas AS (SELECT id, name FROM identity.carreras WHERE is_active)
            SELECT name FROM activas ORDER BY name
            """);

        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    // ---------------------------------------------- comentarios y literales

    [Fact]
    public void Una_palabra_prohibida_dentro_de_un_comentario_no_rechaza()
    {
        var veredicto = ValidadorDeSql.Validar(
            """
            -- este DELETE es solo un comentario
            SELECT code FROM identity.carreras /* y este DROP también */
            """);

        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    [Fact]
    public void Una_palabra_prohibida_dentro_de_un_literal_no_rechaza()
    {
        var veredicto = ValidadorDeSql.Validar(
            "SELECT code FROM identity.carreras WHERE name = 'DROP TABLE'");

        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    [Fact]
    public void Los_comentarios_de_bloque_anidados_no_dejan_codigo_afuera()
    {
        // PostgreSQL anida los comentarios de bloque. Un tokenizador que buscara
        // el primer cierre daría por terminado el comentario en el cierre interno
        // y leería el resto como código.
        var veredicto = ValidadorDeSql.Validar(
            "SELECT /* uno /* dos */ tres */ code FROM identity.carreras");

        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    [Fact]
    public void Un_literal_de_signo_pesos_no_esconde_una_funcion_prohibida()
    {
        // Adentro de $$...$$ el contenido es texto, no código: no se ejecuta. Lo
        // que importa es que el tokenizador no pierda la sincronización y siga
        // leyendo bien lo que viene después.
        var veredicto = ValidadorDeSql.Validar(
            "SELECT $marca$set_config$marca$ AS texto, \"set_config\"('a','b',true)");

        Assert.False(veredicto.EsValida);
    }

    [Fact]
    public void Un_parametro_posicional_no_se_confunde_con_un_delimitador()
    {
        // $1 es un parámetro, no la apertura de un literal. Confundirlos haría que
        // el tokenizador se tragara el resto de la consulta buscando un cierre.
        var veredicto = ValidadorDeSql.Validar(
            "SELECT code FROM identity.carreras WHERE id = $1");

        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    [Theory]
    [InlineData("SELECT 'sin cerrar FROM identity.carreras")]
    [InlineData("SELECT \"sin cerrar FROM identity.carreras")]
    [InlineData("SELECT code /* sin cerrar FROM identity.carreras")]
    [InlineData("SELECT $tag$ sin cerrar FROM identity.carreras")]
    public void Una_construccion_sin_cerrar_se_rechaza(string sql)
    {
        // Falla cerrada: si el tokenizador no sabe dónde termina un literal,
        // tampoco sabe qué parte del texto es código.
        Assert.False(ValidadorDeSql.Validar(sql).EsValida);
    }

    [Fact]
    public void Una_consulta_vacia_se_rechaza()
    {
        Assert.False(ValidadorDeSql.Validar("   ").EsValida);
    }

    // ------------------------------------------------------- caso legítimo

    [Fact]
    public void Acepta_una_consulta_de_cobertura_de_catedra()
    {
        // El caso de uso más importante del proyecto tiene que pasar.
        var veredicto = ValidadorDeSql.Validar(
            """
            SELECT p.apellido, p.nombre, c.nombre AS cargo, d.horas
              FROM designaciones.designaciones d
              JOIN identity.personas p ON p.id = d.persona_id
              JOIN identity.materias m ON m.id = d.materia_id
              JOIN designaciones.cargos c ON c.id = d.cargo_id
             WHERE m.name = 'Algoritmos y Estructuras de Datos'
               AND d.vigente_hasta IS NULL
             ORDER BY c.orden, p.apellido
            """);

        Assert.True(veredicto.EsValida, veredicto.Motivo);
    }

    [Fact]
    public void El_motivo_del_rechazo_identifica_la_construccion()
    {
        var veredicto = ValidadorDeSql.Validar(
            """SELECT "set_config"('a','b',true)""");

        // El motivo va al registro de diagnóstico, no a la respuesta del usuario:
        // nombra construcciones de SQL.
        Assert.False(veredicto.EsValida);
        Assert.NotNull(veredicto.Motivo);
        Assert.Contains("set_config", veredicto.Motivo, StringComparison.Ordinal);
    }
}
