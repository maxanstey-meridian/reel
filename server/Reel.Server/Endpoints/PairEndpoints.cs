namespace Reel.Server.Endpoints;

using Reel.Server.Infrastructure.Configuration;

public static class PairEndpoints
{
    public static WebApplication MapPairEndpoints(this WebApplication app)
    {
        app.MapGet(
            "/pair",
            (HttpContext context, ReelConfig config) =>
            {
                var key = context.Request.Query["key"].FirstOrDefault() ?? "";
                var serverUrl = $"http://{context.Request.Host}";
                var webLink = config.WebUrl is not null
                    ? $"{config.WebUrl}/connect?url={Uri.EscapeDataString(serverUrl)}&key={Uri.EscapeDataString(key)}"
                    : null;

                var html = $$"""
                    <!DOCTYPE html>
                    <html lang="en">
                    <head>
                        <meta charset="utf-8">
                        <meta name="viewport" content="width=device-width, initial-scale=1">
                        <title>Reel — Pair</title>
                        <style>
                            * { margin: 0; padding: 0; box-sizing: border-box; }
                            body { font-family: system-ui, sans-serif; background: #0a0a0a; color: #e5e5e5;
                                   display: flex; align-items: center; justify-content: center; min-height: 100dvh; padding: 1rem; }
                            .card { text-align: center; max-width: 24rem; }
                            h1 { font-size: 1.5rem; font-weight: 600; margin-bottom: .5rem; }
                            .status { margin: 1.5rem 0; font-size: .875rem; color: #a3a3a3; }
                            .status.ok { color: #22c55e; }
                            .status.fail { color: #ef4444; }
                            a { color: #3b82f6; text-decoration: none; }
                            a:hover { text-decoration: underline; }
                            .spinner { display: inline-block; width: 1rem; height: 1rem; border: 2px solid #333;
                                       border-top-color: #e5e5e5; border-radius: 50%; animation: spin .6s linear infinite; }
                            @keyframes spin { to { transform: rotate(360deg); } }
                        </style>
                    </head>
                    <body>
                        <div class="card">
                            <h1>Reel</h1>
                            <div id="msg" class="status"><span class="spinner"></span> Connecting…</div>
                            <div id="link" style="display:none"></div>
                        </div>
                        <script>
                            (async () => {
                                const key = '{{key.Replace("'", "\\'")}}';
                                const msg = document.getElementById('msg');
                                const link = document.getElementById('link');
                                try {
                                    const res = await fetch('/api/connect', {
                                        method: 'POST',
                                        headers: { 'Content-Type': 'application/json' },
                                        body: JSON.stringify({ key }),
                                    });
                                    if (res.ok) {
                                        msg.className = 'status ok';
                                        msg.textContent = 'Connected to Reel';
                                        {{(webLink is not null
                                            ? $"link.innerHTML = '<a href=\"{webLink}\">Open Reel →</a>'; link.style.display = 'block';"
                                            : "")}}
                                    } else {
                                        msg.className = 'status fail';
                                        msg.textContent = 'Invalid key';
                                    }
                                } catch {
                                    msg.className = 'status fail';
                                    msg.textContent = 'Connection failed';
                                }
                            })();
                        </script>
                    </body>
                    </html>
                    """;

                return Results.Content(html, "text/html");
            }
        );

        return app;
    }
}
