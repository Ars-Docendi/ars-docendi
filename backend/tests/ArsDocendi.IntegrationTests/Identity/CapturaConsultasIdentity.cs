using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArsDocendi.IntegrationTests.Identity;

internal sealed class CapturaConsultasIdentity : DbCommandInterceptor
{
    public List<string> Comandos { get; } = [];
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Comandos.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
