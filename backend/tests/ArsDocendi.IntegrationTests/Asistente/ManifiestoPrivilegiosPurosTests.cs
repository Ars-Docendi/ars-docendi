namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Lo que se puede afirmar del manifiesto de privilegios <b>sin</b> una base de datos.
/// </summary>
/// <remarks>
/// Son las aserciones sobre el texto del manifiesto y sobre el comparador, que es
/// una función pura: no necesitan que exista ninguna tabla. Estaban en
/// <see cref="ManifiestoPrivilegiosTests"/>, que hereda de
/// <c>ClasePostgresAislada</c>, así que cada una provisionaba una base entera
/// —crear, migrar tres schemas, crear dos roles, aplicar privilegios y borrar—
/// para leer un JSON del repositorio.
///
/// Las que sí necesitan la base se quedan allá, y son las que importan: comparar
/// el manifiesto contra los privilegios que la migración aplicó de verdad. El
/// corte es ese y no un criterio de tamaño.
/// </remarks>
public sealed class ManifiestoPrivilegiosPurosTests
{
    private const string RolBasico = "asistente_ro";
    private const string RolPii = "asistente_ro_pii";

    // --------------------------------------- el manifiesto dice lo que promete

    [Fact]
    public void La_cache_de_idempotencia_esta_denegada_con_motivo()
    {
        var manifiesto = Manifiesto.Cargar();

        var entrada = Assert.Single(
            manifiesto.Tablas, t => t is { Schema: "designaciones", Tabla: "idempotencia_comandos" });

        Assert.Equal("denegada-explicita", entrada.Estado);
        Assert.False(string.IsNullOrWhiteSpace(entrada.Motivo));
        Assert.Empty(entrada.Declarados());
    }

    [Fact]
    public void El_schema_de_auditoria_esta_denegado_con_motivo()
    {
        var manifiesto = Manifiesto.Cargar();

        var audit = Assert.Single(manifiesto.Schemas, s => s.Nombre == "audit");

        Assert.Equal("denegado", audit.Estado);
        Assert.False(string.IsNullOrWhiteSpace(audit.Motivo));
        Assert.DoesNotContain("audit", manifiesto.SchemasExpuestos);
    }

    [Fact]
    public void Toda_denegacion_explicita_lleva_motivo_escrito()
    {
        var manifiesto = Manifiesto.Cargar();

        var sinMotivo = manifiesto.Tablas
            .Where(t => !t.EsConcedida && string.IsNullOrWhiteSpace(t.Motivo))
            .Select(t => t.Cualificado)
            .Concat(manifiesto.Tablas
                .SelectMany(t => t.ColumnasDenegadas
                    .Where(c => string.IsNullOrWhiteSpace(c.Motivo))
                    .Select(c => $"{t.Cualificado}.{c.Columna}")))
            .ToList();

        Assert.True(sinMotivo.Count == 0,
            "Denegaciones sin motivo escrito: " + string.Join(", ", sinMotivo));
    }

    [Fact]
    public void Las_columnas_personales_solo_las_lee_el_rol_con_acceso_a_datos_personales()
    {
        var manifiesto = Manifiesto.Cargar();
        string[] personales = ["documento", "cuil", "fecha_nacimiento", "telefono"];

        var personas = Assert.Single(
            manifiesto.Tablas, t => t is { Schema: "identity", Tabla: "personas" });

        foreach (var columna in personales)
        {
            Assert.DoesNotContain(columna, personas.ColumnasConcedidas[RolBasico]);
            Assert.Contains(columna, personas.ColumnasConcedidas[RolPii]);
        }
    }

    // ------------------------------------------------- direcciones 1 y 2, sobre el comparador

    [Fact]
    public void Un_privilegio_concedido_fuera_del_manifiesto_hace_fallar_la_comparacion()
    {
        var manifiesto = Manifiesto.Cargar();
        var efectivos = manifiesto.Tablas
            .SelectMany(t => t.Declarados())
            .Select(d => new PrivilegioEfectivo(d.Schema, d.Tabla, d.Columna, d.Rol))
            .Append(new PrivilegioEfectivo("identity", "personas", "documento", RolBasico))
            .ToList();

        var desviaciones = ComparadorManifiesto.Comparar(manifiesto, efectivos, []);

        var detectada = Assert.Single(desviaciones, d => d.Tipo == TipoDesviacion.PrivilegioNoDeclarado);
        Assert.Equal("identity.personas.documento", detectada.Objeto);
        Assert.Contains(RolBasico, detectada.Detalle, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_privilegio_declarado_que_desaparecio_hace_fallar_la_comparacion()
    {
        var manifiesto = Manifiesto.Cargar();
        var efectivos = manifiesto.Tablas
            .SelectMany(t => t.Declarados())
            .Where(d => !(d.Schema == "designaciones" && d.Tabla == "designaciones" && d.Columna == "horas"))
            .Select(d => new PrivilegioEfectivo(d.Schema, d.Tabla, d.Columna, d.Rol))
            .ToList();

        var desviaciones = ComparadorManifiesto.Comparar(manifiesto, efectivos, []);

        var faltantes = desviaciones
            .Where(d => d.Tipo == TipoDesviacion.PrivilegioDeclaradoInexistente)
            .ToList();
        Assert.All(faltantes, d => Assert.Equal("designaciones.designaciones.horas", d.Objeto));
        Assert.NotEmpty(faltantes);
    }

    [Fact]
    public void Un_manifiesto_que_coincide_con_la_base_no_produce_desviaciones()
    {
        var manifiesto = Manifiesto.Cargar();
        var efectivos = manifiesto.Tablas
            .SelectMany(t => t.Declarados())
            .Select(d => new PrivilegioEfectivo(d.Schema, d.Tabla, d.Columna, d.Rol))
            .ToList();
        var columnas = manifiesto.Tablas
            .Where(t => t.EsConcedida)
            .SelectMany(t => t.ColumnasClasificadas.Select(c => new ColumnaReal(t.Schema, t.Tabla, c)))
            .ToList();

        var desviaciones = ComparadorManifiesto.Comparar(manifiesto, efectivos, columnas);

        Assert.True(desviaciones.Count == 0, ComparadorManifiesto.Describir(desviaciones));
    }
}
