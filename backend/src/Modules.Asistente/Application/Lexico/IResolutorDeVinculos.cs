namespace Modules.Asistente.Application;

/// <summary>
/// A qué apunta un vínculo: qué clase de cosa es y con qué identificador se abre.
/// </summary>
/// <param name="Tipo">
/// El nombre estable del recurso —<c>pedido-designacion</c>, por ejemplo—. Lo elige
/// quien resuelve, no el asistente.
/// </param>
/// <param name="Id">El identificador con el que la interfaz lo abre.</param>
/// <remarks>
/// <b>No hay ruta acá, y es deliberado.</b> A dónde va el usuario es una decisión
/// del cliente: hoy una ruta, mañana otra, y el backend no tiene por qué enterarse.
/// El servidor dice QUÉ; el cliente resuelve DÓNDE, y un tipo que no reconoce no lo
/// pinta.
/// </remarks>
public sealed record DestinoDelVinculo(string Tipo, string Id);

/// <summary>
/// Un vínculo ya ubicado en el resultado: qué celda lo ofrece y a qué apunta.
/// </summary>
/// <param name="Fila">Índice de la fila, en el orden en que viajan las filas.</param>
/// <param name="Columna">Índice de la columna dentro de esa fila.</param>
public sealed record VinculoDelResultado(int Fila, int Columna, string Tipo, string Id);

/// <summary>
/// Decide, para textos que aparecieron en un resultado, cuáles identifican algo que
/// el actor <b>puede abrir</b> en la aplicación.
/// </summary>
/// <remarks>
/// <b>Es un puerto, y el asistente no lo implementa.</b> Quién puede abrir un
/// trámite lo sabe el módulo de designaciones, quién puede abrir un perfil lo sabe
/// portal, y así. La composición vive en el composition root, que es el único
/// proyecto que ve a todos los módulos.
///
/// <b>Por qué no alcanza con que la fila haya llegado.</b> Lo tentador es razonar
/// que si la RLS dejó pasar la fila entonces el actor puede ver el recurso. Son dos
/// reglas distintas, escritas dos veces, y divergen: el ámbito departamental es una
/// lista fija de códigos de rol en el módulo y <c>identity.roles.scope</c> en las
/// funciones del asistente; los roles del módulo salen del token y están filtrados
/// por el rol seleccionado en la sesión, y los del asistente son las asignaciones
/// vigentes leídas en vivo; el permiso es un claim de un lado y la matriz en vivo
/// del otro. Un vínculo construido sobre la regla equivocada es un botón que
/// responde 403 al apretarlo, que es justo lo que el invariante #7 prohíbe.
///
/// La dirección segura del error es <b>no ofrecer</b>: se muestra el dato igual.
/// </remarks>
public interface IResolutorDeVinculos
{
    /// <summary>
    /// De los candidatos dados, los que resultaron ser algo abrible por el actor.
    /// </summary>
    /// <param name="candidatos">
    /// Textos que aparecieron como valor completo de alguna celda. Vienen sin
    /// filtrar por dominio: el asistente no sabe qué forma tiene el identificador de
    /// cada módulo, así que manda todo lo que parece un identificador y cada
    /// implementación descarta lo que no le corresponde.
    /// </param>
    /// <returns>
    /// Un mapa del candidato a su destino. Los que no resolvieron sencillamente no
    /// están: <b>no</b> se distingue «no existe» de «existe y no lo alcanzás», que
    /// sería un canal de inferencia sobre lo que el actor no puede ver.
    /// </returns>
    Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
        IReadOnlyCollection<string> candidatos, CancellationToken ct);
}

/// <summary>
/// La implementación con que arranca el módulo: no resuelve nada.
/// </summary>
/// <remarks>
/// El módulo del asistente se registra solo y sin conocer a ningún otro, así que
/// necesita algo que responder cuando nadie compuso el adaptador. Devolver vacío es
/// la única respuesta honesta: el turno contesta igual y no ofrece vínculos.
/// </remarks>
internal sealed class SinVinculos : IResolutorDeVinculos
{
    private static readonly IReadOnlyDictionary<string, DestinoDelVinculo> Nada =
        new Dictionary<string, DestinoDelVinculo>(StringComparer.Ordinal);

    public Task<IReadOnlyDictionary<string, DestinoDelVinculo>> ResolverAsync(
        IReadOnlyCollection<string> candidatos, CancellationToken ct) =>
        Task.FromResult(Nada);
}
