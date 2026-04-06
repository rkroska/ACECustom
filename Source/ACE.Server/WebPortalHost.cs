using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using ACE.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System;

namespace ACE.Server.Web
{
    public static class WebPortalHost
    {
        private static IHost? _host;
        private static bool _started;
        private static readonly System.Threading.SemaphoreSlim _hostLock = new System.Threading.SemaphoreSlim(1, 1);
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(WebPortalHost));

        public static string Secret { get; } = GenerateDynamicSecret();

        private static string GenerateDynamicSecret()
        {
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        public static async Task Start(string[] args)
        {
            var envStr = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isDevelopment = string.Equals(envStr, "Development", StringComparison.OrdinalIgnoreCase);
            var isDebuggerAttached = System.Diagnostics.Debugger.IsAttached;

            await _hostLock.WaitAsync();
            try
            {
                if (_started)
                {
                    log.Warn("[WEB PORTAL] Start() called while server is already running. Ignoring.");
                    return;
                }

                var secret = Secret;


                // Environment-Aware Resolution:
                // Development fallback: Crawl for Source tree dist (Vite build)
                // Production fallback: Use binary-local 'wwwroot'

                string? distPath = null;
                if (isDevelopment || isDebuggerAttached)
                {
                    distPath = FindSourceDist();
                }

                // If not in dev, or crawler failed, look for binary-local wwwroot
                if (string.IsNullOrEmpty(distPath))
                {
                    distPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                }

                // Fail Fast: If the assets are missing, the Web Portal cannot function.
                if (!Directory.Exists(distPath))
                {
                    log.Fatal($"[WEB PORTAL] Assets NOT found at: {distPath}. Start aborted.");
                    log.Fatal($"[WEB PORTAL] Please ensure you have run 'npm run build' in the ClientApp directory and that the 'wwwroot' folder is present.");
                    throw new DirectoryNotFoundException($"Web Portal assets missing at {distPath}");
                }

                // Entrypoint Check (Non-Fatal): Verify if the SPA is present
                // We intentionally avoid a fatal error, as the primary purpose of the server is to serve the game client.
                if (!File.Exists(Path.Combine(distPath, "index.html")))
                {
                    log.Warn($"[WEB PORTAL] Asset directory exists, but 'index.html' was NOT found at: {distPath}. Web Portal UI requests will return a 404 (Not Found).");
                }

                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    Args = args,
                    WebRootPath = distPath
                });

                if (isDevelopment || isDebuggerAttached)
                {
                    builder.Logging.SetMinimumLevel(LogLevel.Debug);
                }

                log.Info($"Web Portal using asset path: {distPath}");

                // Add services to the container.
                builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
                builder.Logging.AddFilter("System", LogLevel.Warning);
                builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

                // JWT Authentication Setup
                var key = Encoding.ASCII.GetBytes(secret);

                builder.Services.AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(x =>
                {
                    x.MapInboundClaims = false;
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = true;
                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };

                    // Cookie Support: Extract JWT from cookie if present
                    x.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            if (context.Request.Cookies.TryGetValue("ilt_auth_token", out var token))
                                context.Token = token;
                            return Task.CompletedTask;
                        }
                    };
                });

                builder.Services.AddAuthorization();

                // IMPORTANT: We must tell MVC to look for controllers in THIS assembly
                builder.Services.AddControllers()
                    .AddApplicationPart(typeof(WebPortalHost).Assembly);

                builder.Services.AddSignalR();

                // Build the app
                var app = builder.Build();

                // Configure the HTTP request pipeline.
                
                // Environment-Aware resolution of static assets
                if (isDevelopment)
                {
                    app.UseDeveloperExceptionPage();
                }

                // Serve static files (including the React app)
                app.UseDefaultFiles();
                app.UseStaticFiles();

                app.UseRouting();

                // Enable support for reverse proxies (NGINX) by preserving client IP and protocol
                // This must be placed before UseAuthentication/UseAuthorization
                var forwardedHeadersOptions = new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                };
                forwardedHeadersOptions.KnownProxies.Clear();
                forwardedHeadersOptions.KnownNetworks.Clear();
                app.UseForwardedHeaders(forwardedHeadersOptions);

                app.UseAuthentication();
                app.UseAuthorization();

                app.MapControllers();

                // Diagnostic health check
                app.MapGet("/api/health", () => "OK");

                // Handle React routing - fallback to index.html for non-API routes
                app.MapWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"), builder =>
                {
                    builder.UseStaticFiles();
                    builder.UseRouting();
                    builder.UseEndpoints(endpoints =>
                    {
                        endpoints.MapFallbackToFile("index.html");
                    });
                });

                // Secure Binding:
                // Development: "*" (Accessible globally for debugging)
                // Production: "localhost" (Restricted to loopback; assumes a TLS-terminating reverse proxy)
                var host = isDevelopment ? "*" : "localhost";
                var port = isDevelopment ? 5000 : 5001;
                var url = $"http://{host}:{port}";

                app.Urls.Clear();
                app.Urls.Add(url);
                await app.StartAsync();

                _host = app;
                _started = true;
                log.Info($"Web Portal listening at {url}");

                if (!isDevelopment)
                {
                    log.Warn("SECURE CONFIGURATION: Portal is bound to loopback only. A reverse proxy (e.g., NGINX, IIS) is required to terminate SSL/TLS and expose this service.");
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed to start Web Portal", ex);

                if (_host != null)
                {
                    if (_host is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync();
                    else
                        _host.Dispose();
                        
                    _host = null;
                }

                _started = false;
                throw; // Rethrow so the caller (Program.cs) can realize the failure
            }
            finally
            {
                _hostLock.Release();
            }
        }

        private static string? FindSourceDist()
        {
            // Crawl up to find the project root (Source/ACE.WebPortal/ClientApp)
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                // Path 1: Source tree is a child of the current directory (Development)
                // Looking for Source/ACE.WebPortal/ClientApp
                var projectRoot = Path.Combine(dir.FullName, "ACE.WebPortal", "ClientApp");
                if (Directory.Exists(projectRoot))
                    return Path.Combine(projectRoot, "dist");

                // Path 2: Source tree is in a peer directory 'Server/Source' (Production Build Target)
                // Looking for Server/Source/ACE.WebPortal/ClientApp
                var peerSourceRoot = Path.Combine(dir.FullName, "Server", "Source", "ACE.WebPortal", "ClientApp");
                if (Directory.Exists(peerSourceRoot))
                    return Path.Combine(peerSourceRoot, "dist");

                // Also check if we are already in the ACE.WebPortal parent folder
                if (dir.Name == "ACE.WebPortal")
                    return Path.Combine(dir.FullName, "ClientApp", "dist");

                dir = dir.Parent;
            }
            return null;
        }

        public static async Task StopAsync()
        {
            await _hostLock.WaitAsync();
            try
            {
                if (!_started || _host == null) return;

                log.Info("[WEB PORTAL] Stopping Host...");
                await _host.StopAsync();
                _host.Dispose();
                _host = null;
                _started = false;
            }
            finally
            {
                _hostLock.Release();
            }
        }
    }
}
