using System.Security.Cryptography;
using System.Text;

namespace MOMBotPro.API.Middleware;

public class ZoomWebhookVerificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;

    public ZoomWebhookVerificationMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next   = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var req = context.Request;

        // Only intercept POST /api/zoom/webhook
        if (!req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            !req.Path.Equals("/api/zoom/webhook", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Enable buffering so the controller can also read the body after us
        req.EnableBuffering();

        string rawBody;
        using (var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true))
            rawBody = await reader.ReadToEndAsync();

        // Reset so the controller's model-binding reads from the beginning
        req.Body.Position = 0;

        var timestamp = req.Headers["x-zm-request-timestamp"].FirstOrDefault() ?? string.Empty;
        var signature  = req.Headers["x-zm-signature"].FirstOrDefault()         ?? string.Empty;
        var secret     = _config["Zoom:WebhookSecret"]                           ?? string.Empty;

        // message = "v0:<timestamp>:<rawBody>"
        var message      = $"v0:{timestamp}:{rawBody}";
        var secretBytes  = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var hashBytes    = HMACSHA256.HashData(secretBytes, messageBytes);

        // expected = "v0=" + lower-hex(HMAC)
        var expectedSignature = "v0=" + Convert.ToHexString(hashBytes).ToLowerInvariant();

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var actualBytes   = Encoding.UTF8.GetBytes(signature);

        // Constant-time comparison (FixedTimeEquals returns false if lengths differ)
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }
}
