using Microsoft.Extensions.Options;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddOptions<RaceDayApiOptions>()
    .Bind(builder.Configuration.GetSection(RaceDayApiOptions.SectionName))
    .Validate(options => options.TryGetBaseAddress(out _), "RaceDayApi:BaseUrl must be an absolute HTTP or HTTPS URL without credentials, a query, or a fragment.")
    .ValidateOnStart();

builder.Services.AddHttpClient<RaceDayApiClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<RaceDayApiOptions>>().Value;
    client.BaseAddress = options.GetBaseAddress();
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // API session cookies are attached to individual requests by the client service.
    // A shared handler must never keep or reuse a user's cookie automatically.
    UseCookies = false,
    AllowAutoRedirect = false
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
