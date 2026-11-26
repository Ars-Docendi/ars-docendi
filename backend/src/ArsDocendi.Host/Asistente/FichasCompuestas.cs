using Modules.Asistente.Application;

namespace ArsDocendi.Host.Asistente;

/// <summary>Reparte el pedido de ficha entre los proveedores: el primero que la tiene, la da.</summary>
public sealed class FichasCompuestas(IReadOnlyList<IProveedorDeFichas> proveedores) : IProveedorDeFichas
{
    public async Task<FichaDelElemento?> ObtenerAsync(string tipo, string id, CancellationToken ct)
    {
        foreach (var proveedor in proveedores)
        {
            if (await proveedor.ObtenerAsync(tipo, id, ct) is { } ficha)
            {
                return ficha;
            }
        }

        return null;
    }
}
