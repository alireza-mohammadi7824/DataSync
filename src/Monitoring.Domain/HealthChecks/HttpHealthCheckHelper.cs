using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Monitoring.HealthChecks;

internal static class HttpHealthCheckHelper
{
    private const int ContentSnippetLimit = 64 * 1024;

    public static HttpMethod ResolveMethod(string? method, HttpMethod defaultMethod)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return defaultMethod;
        }

        var upper = method.Trim().ToUpperInvariant();

        return upper switch
        {
            "GET" => HttpMethod.Get,
            "HEAD" => HttpMethod.Head,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "OPTIONS" => HttpMethod.Options,
            "TRACE" => HttpMethod.Trace,
            _ => new HttpMethod(upper)
        };
    }

    public static bool IsExpectedStatus(HttpStatusCode statusCode, int[]? expected, int defaultMin, int defaultMax)
    {
        var code = (int)statusCode;

        if (expected is { Length: > 0 })
        {
            return expected.Contains(code);
        }

        return code >= defaultMin && code <= defaultMax;
    }

    public static async Task<string> ReadContentSnippetAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return string.Empty;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = ArrayPool<byte>.Shared.Rent(ContentSnippetLimit);

        try
        {
            var totalRead = 0;

            while (totalRead < ContentSnippetLimit)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(totalRead, ContentSnippetLimit - totalRead), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (totalRead == 0)
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(buffer, 0, totalRead);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
