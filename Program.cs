using System.Text;
using JaeZoo.Server.Data;
using JaeZoo.Server.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ======== DB ========
var cs = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(cs))
{
    // по умолчанию SQLite локально
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlite("Data Source=jaezoo.db"));
}
else
{
    // если задана строка подключения (например, Postgres на Render)
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseNpgsql(cs));
}

// ======== Controllers / SignalR / Swagger ========
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ======== CORS (разреши свой клиент) ========
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("client", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true); // укажи точные Origins в проде
    });
});

// ======== JWT ========
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SET_A_STRONG_SECRET_KEY_32+_CHARS";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "JaeZoo";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "JaeZooClient";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        // для SignalR: токен из query ?access_token=
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ======== Migrate DB on start ========
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine("DB migrate error: " + ex.Message);
    }
}

var enableSwagger = app.Environment.IsDevelopment() ||
                    string.Equals(app.Configuration["EnableSwagger"], "true", StringComparison.OrdinalIgnoreCase);

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseCors("client");
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.MapGet("/", () => Results.Ok(new
{
    service = "JaeZoo.Server",
    status = "ok",
    timeUtc = DateTime.UtcNow
}));

app.Run();
