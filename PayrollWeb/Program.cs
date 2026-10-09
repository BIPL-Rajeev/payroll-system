using PayrollWeb.Components;
using PayrollWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthorization();

// Auth: scoped AuthState per circuit and a handler that attaches
// "Bearer {token}" to every API request made with the typed client.
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<AuthTokenHandler>();

// Payroll API client (base URL from appsettings.json), wired with the
// bearer-token handler so all requests carry the JWT once logged in.
string apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";
builder.Services.AddTransient(sp =>
{
    var bearer = sp.GetRequiredService<AuthTokenHandler>();
    bearer.InnerHandler = new HttpClientHandler();
    return new HttpClient(bearer) { BaseAddress = new Uri(apiBaseUrl) };
});
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ToastService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
