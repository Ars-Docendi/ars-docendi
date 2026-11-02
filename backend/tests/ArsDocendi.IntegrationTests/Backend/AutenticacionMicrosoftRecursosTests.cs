using System.Net;
using ArsDocendi.Host.Autenticacion;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed partial class AutenticacionMicrosoftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_con_token_libera_la_solicitud_despues_del_envio_aunque_falle(bool falla)
    {
        using var contenido = new ContenidoRastreable();
        using var transporte = new TransporteRastreable(contenido, falla);
        using var cliente = new HttpClient(transporte) { BaseAddress = new Uri("https://localhost") };
        var ct = TestContext.Current.CancellationToken;

        if (falla)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                PostConTokenAsync(cliente, "/api/auth/logout", "token-pruebas", ct));
        }
        else
        {
            using var respuesta = await PostConTokenAsync(cliente, "/api/auth/logout", "token-pruebas", ct);
            Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        }

        Assert.True(contenido.Liberado);
    }

    private sealed class ContenidoRastreable() : StringContent(string.Empty)
    {
        public bool Liberado { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Liberado = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TransporteRastreable(ContenidoRastreable contenido, bool falla) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Content = contenido;
            await Task.Yield();
            Assert.False(contenido.Liberado);
            Assert.Equal("token-pruebas", Assert.Single(request.Headers.GetValues(AntifalsificacionSesion.HeaderToken)));
            if (falla) throw new HttpRequestException("Fallo de transporte de prueba.");
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
