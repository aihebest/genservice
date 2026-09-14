using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GenService.API.Services;

/// <summary>
/// Authenticates server-to-server calls from the Logistics Platform with a
/// shared secret in the <c>X-Integration-Key</c> header.
///
/// Deliberately NOT a user JWT: the Logistics API calls us as a machine, with no
/// signed-in person behind it, and we do not want to mint role-bearing tokens for
/// another system. The secret lives in Azure App Service configuration
/// (Integration:InboundKey) and never in the repository.
/// </summary>
public sealed class RequireIntegrationKeyAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Integration-Key";
    public const string ConfigKey  = "Integration:InboundKey";

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var cfg = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                                                 .CreateLogger("IntegrationKeyAuth");

        var expected = cfg[ConfigKey];
        if (string.IsNullOrWhiteSpace(expected))
        {
            // Fail closed. An unconfigured secret must never mean "let everyone in".
            log.LogError("Integration endpoint called but {ConfigKey} is not configured — rejecting.", ConfigKey);
            ctx.Result = new ObjectResult(new { message = "Integration is not configured on this server." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return;
        }

        if (!ctx.HttpContext.Request.Headers.TryGetValue(HeaderName, out var supplied) ||
            !FixedTimeEquals(supplied.ToString(), expected))
        {
            log.LogWarning("Rejected integration call to {Path} from {Ip} — bad or missing {Header}.",
                ctx.HttpContext.Request.Path, ctx.HttpContext.Connection.RemoteIpAddress, HeaderName);
            ctx.Result = new UnauthorizedObjectResult(new { message = "Invalid integration key." });
            return;
        }

        await next();
    }

    /// <summary>Constant-time compare so the secret can't be recovered by timing the response.</summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
