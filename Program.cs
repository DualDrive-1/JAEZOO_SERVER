using JaeZoo.Server.Data;
using JaeZoo.Server.Hubs;
using JaeZoo.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---------- DB ----------
var conn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=jaezoo.db";
var isPg = conn.Contains("Host=", StringComparison.OrdinalIgnoreCase)
           || conn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
           || conn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

if (isPg) builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));
else builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(conn));

// ---------- JWT ----------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt");
var key = jwt.GetValue<string>("Key") ?? "fallback_key_change_me";
var issuer = jwt.GetValue<string>("Issuer") ?? "JaeZoo";
var audience = jwt.GetValue<string>("Audience") ?? "JaeZooClient";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };

        // ВАЖНО: разрешаем токен из query (?access_token=...) для SignalR-хаба
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();

// ---------- MVC + SignalR + Swagger ----------
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "JaeZoo API", Version = "v1" });
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введите JWT: Bearer {token}"
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, Array.Empty<string>() } });
});

var app = builder.Build();

// ---------- БД миграции на старте ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ---------- Проксирование/HTTPS/Swagger ----------
var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

// Для контейнера HTTPS-редирект не обязателен
// app.UseHttpsRedirection();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ---------- Маршруты ----------
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
