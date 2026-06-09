using SelfEvolving.AssetManagement.Web.Client.Pages;
using SelfEvolving.AssetManagement.Web.Components;
using SelfEvolving.AssetManagement.Web.Configuration;
using SelfEvolving.AssetManagement.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services
    .AddOptions<SystemArchitectureOptions>()
    .Bind(builder.Configuration.GetSection(SystemArchitectureOptions.SectionName));

builder.Services.AddSingleton<ArchitectureSpecificationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/api/system/blueprint", (ArchitectureSpecificationService service) => Results.Ok(service.GetBlueprint()));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SelfEvolving.AssetManagement.Web.Client._Imports).Assembly);

app.Run();

public partial class Program;
