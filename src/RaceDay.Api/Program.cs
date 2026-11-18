using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using RaceDay.Api.Authentication;
using RaceDay.Api.Services;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "RaceDay API", Version = "v1" });
    options.AddSecurityDefinition("SessionCookie", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.ParameterLocation.Cookie,
        Name = ".RaceDay.Session",
        Description = "Log in through POST /api/auth/login. The API sets this server-side session cookie."
    });
    options.OperationFilter<SessionCookieOperationFilter>();
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

var connectionString = builder.Configuration.GetConnectionString("RaceDayDatabase")
    ?? throw new InvalidOperationException("Connection string 'RaceDayDatabase' is missing.");
builder.Services.AddDbContext<RaceDayDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddOptions<BlobStorageOptions>()
    .Bind(builder.Configuration.GetSection(BlobStorageOptions.SectionName));
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BlobStorageOptions>>().Value;
    options.Validate();
    return new BlobServiceClient(options.ConnectionString);
});
builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!builder.Configuration.GetValue<bool>("Docker:DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}
app.UseSession();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health/ready", async (RaceDayDbContext dbContext, CancellationToken cancellationToken) =>
{
    try
    {
        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();
app.Run();

public partial class Program;
