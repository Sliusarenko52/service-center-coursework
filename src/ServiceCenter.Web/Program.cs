using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ServiceCenter.Web.Data;
using ServiceCenter.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);
var defaultConn = $"Data Source={Path.Combine(dataDir, "service_center.db")}";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? defaultConn;

var dbFactory = new SqliteConnectionFactory(connectionString);
DatabaseInitializer.Initialize(dbFactory);

builder.Services.AddSingleton(dbFactory);
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RequestService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "sc_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        if (ex is AppException appEx)
        {
            context.Response.StatusCode = appEx.StatusCode;
            var payload = new Dictionary<string, object?>
            {
                ["type"] = $"https://httpstatuses.com/{appEx.StatusCode}",
                ["title"] = appEx.ErrorCode,
                ["status"] = appEx.StatusCode,
                ["detail"] = appEx.Message,
                ["code"] = appEx.ErrorCode
            };
            if (appEx.FieldErrors is not null)
                payload["fieldErrors"] = appEx.FieldErrors;
            if (appEx is ConflictAppException conflictEx && conflictEx.CurrentVersion.HasValue)
                payload["currentVersion"] = conflictEx.CurrentVersion.Value;

            await context.Response.WriteAsJsonAsync(payload);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "INTERNAL_ERROR",
            Detail = "Внутрішня помилка сервера. Зверніться до адміністратора."
        });
    });
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", (SqliteConnectionFactory factory) =>
{
    try
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1;";
        cmd.ExecuteScalar();
        return Results.Ok(new { status = "healthy", database = "connected", timestamp = DateTime.UtcNow });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllers();

app.Run();

public partial class Program { }
