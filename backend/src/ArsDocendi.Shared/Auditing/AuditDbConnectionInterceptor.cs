using System.Data.Common;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArsDocendi.Shared.Auditing;

// Propagates the current request's user (and correlation id) into PostgreSQL session GUCs
// every time EF Core acquires a pooled connection. The audit.log_change trigger in schema
// `audit` reads these via current_setting('app.current_user_id', true) to stamp each row
// it writes to audit.change_log.
//
// We always SET on open — even when there's no authenticated user — to avoid bleeding a
// previous request's identity onto a connection reused from the Npgsql pool.
public sealed class AuditDbConnectionInterceptor(
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetSessionContextAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetSessionContextAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task SetSessionContextAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var userId = ResolveUserId();
        var requestId = httpContextAccessor.HttpContext?.TraceIdentifier ?? string.Empty;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_user_id', @user_id, false), " +
                          "       set_config('app.request_id',    @request_id, false)";

        var userParam = cmd.CreateParameter();
        userParam.ParameterName = "@user_id";
        userParam.Value = userId ?? string.Empty;
        cmd.Parameters.Add(userParam);

        var requestParam = cmd.CreateParameter();
        requestParam.ParameterName = "@request_id";
        requestParam.Value = requestId;
        cmd.Parameters.Add(requestParam);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // Only forward the user id when it parses as a UUID — that's what audit.change_log.changed_by
    // expects. Anything else (Azure OID claim that hasn't been mapped yet, anonymous request) →
    // empty string, which the trigger converts to NULL via NULLIF.
    private string? ResolveUserId()
    {
        if (!currentUser.IsAuthenticated)
        {
            return null;
        }

        return Guid.TryParse(currentUser.UserId, out var uuid) ? uuid.ToString() : null;
    }
}
