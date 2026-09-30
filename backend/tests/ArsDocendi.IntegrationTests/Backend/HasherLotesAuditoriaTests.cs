using ArsDocendi.Shared.Auditing;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class HasherLotesAuditoriaTests
{
    [Fact]
    public void Hash_es_estable_ante_orden_de_propiedades_y_de_entrada()
    {
        var nonce = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var eventoUno = Evento(1, "{\"b\":2,\"a\":1}");
        var eventoDos = Evento(2, "{\"nombre\":\"á\",\"activo\":true}");
        var eventoUnoReordenado = Evento(1, "{\"a\":1,\"b\":2}") with { ChangedColumns = ["nombre", "activo"] };
        var eventoDosReordenado = Evento(2, "{\"activo\":true,\"nombre\":\"á\"}") with { ChangedColumns = ["nombre", "activo"] };

        var hashOriginal = HasherLotesAuditoria.CalcularHash(
            "prod", 1, 2, nonce, [eventoUno, eventoDos]);
        var hashReordenado = HasherLotesAuditoria.CalcularHash(
            "prod", 1, 2, nonce, [eventoDosReordenado, eventoUnoReordenado]);

        Assert.Equal(hashOriginal, hashReordenado);
    }

    [Fact]
    public void Hash_cambia_si_cambia_evento_nonce_o_ambiente()
    {
        var evento = Evento(1, "{\"valor\":1}");
        var nonce = Guid.NewGuid();
        var hash = HasherLotesAuditoria.CalcularHash("prod", 1, 1, nonce, [evento]);
        var otroNonce = HasherLotesAuditoria.CalcularHash("prod", 1, 1, Guid.NewGuid(), [evento]);
        var otroAmbiente = HasherLotesAuditoria.CalcularHash("staging", 1, 1, nonce, [evento]);
        var eventoAlterado = HasherLotesAuditoria.CalcularHash(
            "prod", 1, 1, nonce, [Evento(1, "{\"valor\":2}")]);
        var previo = Enumerable.Repeat((byte)7, 32).ToArray();
        var hashConPrevio = HasherLotesAuditoria.CalcularHash("prod", 1, 1, nonce, [evento], previo);

        Assert.NotEqual(hash, otroNonce);
        Assert.NotEqual(hash, otroAmbiente);
        Assert.NotEqual(hash, eventoAlterado);
        Assert.NotEqual(hash, hashConPrevio);
    }

    [Fact]
    public void Hash_normaliza_numeros_decimales_y_rechaza_claves_json_duplicadas()
    {
        var nonce = Guid.NewGuid();
        var uno = HasherLotesAuditoria.CalcularHash("prod", 1, 1, nonce, [Evento(1, "{\"n\":1.0}")]);
        var entero = HasherLotesAuditoria.CalcularHash("prod", 1, 1, nonce, [Evento(1, "{\"n\":1}")]);

        Assert.Equal(entero, uno);
        Assert.Throws<InvalidDataException>(() => HasherLotesAuditoria.CalcularHash(
            "prod", 1, 1, nonce, [Evento(1, "{\"n\":1,\"n\":2}")]));
    }

    [Fact]
    public void Hash_rechaza_huecos_o_secuencias_fuera_del_rango()
    {
        Assert.Throws<InvalidDataException>(() => HasherLotesAuditoria.CalcularHash(
            "prod", 1, 3, Guid.NewGuid(), [Evento(1, "{}"), Evento(3, "{}")]));
        Assert.Throws<InvalidDataException>(() => HasherLotesAuditoria.CalcularHash(
            "prod", 1, 1, Guid.NewGuid(), [Evento(2, "{}")]));
    }

    [Theory]
    [InlineData("100000000000000000000000000000000000000000000001", "100000000000000000000000000000000000000000000002")]
    [InlineData("0.000000000000000000000000000000000000001", "0")]
    public void Hash_no_redondea_numeros_json_distintos(string izquierda, string derecha)
    {
        var nonce = Guid.NewGuid();
        Assert.NotEqual(
            HasherLotesAuditoria.CalcularHash("test", 1, 1, nonce, [Evento(1, "{\"n\":" + izquierda + "}")]),
            HasherLotesAuditoria.CalcularHash("test", 1, 1, nonce, [Evento(1, "{\"n\":" + derecha + "}")]));
    }

    [Fact]
    public void Vector_v1_coincide_con_referencia_independiente_python_sha256()
    {
        var hash = HasherLotesAuditoria.CalcularHash("prod", 1, 1,
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"), [Evento(1, "{\"n\":1}")]);
        Assert.Equal("b2dd714b6959f401ac36d77b2568daa02b592ea75243a68cf2ee8cf07c5750c4",
            Convert.ToHexString(hash).ToLowerInvariant());
    }

    private static EventoAuditoriaSellable Evento(long secuencia, string newRow) =>
        new(100 + secuencia, secuencia, "identity", "roles", $"id-{secuencia}", "INSERT",
            null, newRow, ["activo", "nombre"], null,
            DateTimeOffset.Parse("2026-09-28T12:00:00Z"), null, null);
}
