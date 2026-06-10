using SelfEvolving.AssetManagement.Web.Client.Pages;
using SelfEvolving.AssetManagement.Web.Components;
using SelfEvolving.AssetManagement.Web.Configuration;
using SelfEvolving.AssetManagement.Web.Models;
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
builder.Services.AddSingleton<AssetInventoryService>();
builder.Services.AddSingleton<OpaGuidancePolicyService>();
builder.Services.AddSingleton<AssetOwnershipService>();
builder.Services.AddSingleton<FeedbackIngestionService>();

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

app.MapGet("/api/assets", (AssetInventoryService service) => Results.Ok(service.GetAll()));

app.MapGet("/api/assets/{id:int}", (int id, AssetInventoryService service) =>
{
    var asset = service.GetById(id);
    return asset is null ? Results.NotFound() : Results.Ok(asset);
});

app.MapPost("/api/assets", (CreateAssetRequest request, AssetInventoryService service, OpaGuidancePolicyService policyService) =>
{
    var policyDecision = policyService.EvaluateAssetCreate(request);
    if (!policyDecision.Allowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        var created = service.Create(request);
        return Results.Created($"/api/assets/{created.Id}", created);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapGet("/api/assets/{id:int}/assignments", (int id, AssetInventoryService inventoryService, AssetOwnershipService ownershipService) =>
{
    if (inventoryService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(ownershipService.GetAssignments(id));
});

app.MapPost("/api/assets/{id:int}/assignments", (int id, CreateAssetAssignmentRequest request, AssetInventoryService inventoryService, AssetOwnershipService ownershipService) =>
{
    if (inventoryService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    try
    {
        var created = ownershipService.Assign(id, request);
        return Results.Created($"/api/assets/{id}/assignments/{created.Id}", created);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/feedback", (FeedbackIngestionService service) => Results.Ok(service.GetAll()));

app.MapPost("/api/feedback", (CreateFeedbackRequest request, FeedbackIngestionService service) =>
{
    try
    {
        var created = service.Create(request);
        return Results.Created($"/api/feedback/{created.Id}", created);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SelfEvolving.AssetManagement.Web.Client._Imports).Assembly);

app.Run();

public partial class Program;
