namespace ArsDocendi.Shared.Persistencia;

/// <summary>
/// Contrato transversal que cada módulo implementa para aplicar sus migraciones
/// de base de datos. El composition root (el Host) resuelve todas las
/// implementaciones registradas y las ejecuta en el arranque one-shot
/// <c>--migrate</c>, sin conocer los <c>DbContext</c> internos de cada módulo
/// (respeta la frontera cross-module: solo se comparte esta interfaz).
/// </summary>
public interface IMigradorModulo
{
    /// <summary>
    /// Aplica las migraciones pendientes del módulo. Debe ser idempotente:
    /// re-ejecutar sobre una base ya migrada no produce cambios ni error.
    /// </summary>
    Task MigrarAsync(CancellationToken ct);
}
