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
builder.Services.AddSingleton<PolicyDecisionAuditService>();
builder.Services.AddSingleton<AssetOwnershipService>();
builder.Services.AddSingleton<FeedbackIngestionService>();
builder.Services.AddSingleton<EvolutionOrchestrationService>();
builder.Services.AddSingleton<EvolutionApprovalService>();
builder.Services.AddSingleton<EvolutionLifecycleService>();

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
app.MapGet("/api/policy/decisions", (PolicyDecisionAuditService service) => Results.Ok(service.GetAll()));

app.MapGet("/api/assets/{id:int}", (int id, AssetInventoryService service) =>
{
    var asset = service.GetById(id);
    return asset is null ? Results.NotFound() : Results.Ok(asset);
});

app.MapPost("/api/assets", (CreateAssetRequest request, AssetInventoryService service, OpaGuidancePolicyService policyService, PolicyDecisionAuditService policyAuditService) =>
{
    var policyDecision = policyService.EvaluateAssetCreate(request);
    policyAuditService.RecordAssetCreate(request, policyDecision);
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

app.MapGet("/api/feedback/{id:int}", (int id, FeedbackIngestionService service) =>
{
    var feedback = service.GetById(id);
    return feedback is null ? Results.NotFound() : Results.Ok(feedback);
});

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

app.MapGet("/api/evolution/candidates", (EvolutionOrchestrationService service) => Results.Ok(service.GetAll()));

app.MapGet("/api/evolution/candidates/{id:int}", (int id, EvolutionOrchestrationService service) =>
{
    var candidate = service.GetById(id);
    return candidate is null ? Results.NotFound() : Results.Ok(candidate);
});

app.MapGet("/api/evolution/candidates/{id:int}/telemetry", (int id, EvolutionOrchestrationService service) =>
{
    if (service.GetById(id) is null)
    {
        return Results.NotFound();
    }

    var telemetry = service.GetTelemetry(id);
    return telemetry is null ? Results.NotFound() : Results.Ok(telemetry);
});

app.MapGet("/api/evolution/candidates/{id:int}/fitness", (int id, EvolutionOrchestrationService service) =>
{
    if (service.GetById(id) is null)
    {
        return Results.NotFound();
    }

    var fitness = service.GetFitnessEvaluation(id);
    return fitness is null ? Results.NotFound() : Results.Ok(fitness);
});

app.MapPost("/api/evolution/candidates/{id:int}/fitness", (int id, CreateEvolutionFitnessEvaluationRequest request, EvolutionOrchestrationService service) =>
{
    if (service.GetById(id) is null)
    {
        return Results.NotFound();
    }

    try
    {
        var recorded = service.SetFitnessEvaluation(id, request);
        return Results.Created($"/api/evolution/candidates/{id}/fitness", recorded);
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

app.MapPost("/api/evolution/candidates/from-feedback/{feedbackId:int}", (int feedbackId, FeedbackIngestionService feedbackService, EvolutionOrchestrationService evolutionService) =>
{
    var feedback = feedbackService.GetById(feedbackId);
    if (feedback is null)
    {
        return Results.NotFound();
    }

    try
    {
        var created = evolutionService.CreateFromFeedback(feedback);
        return Results.Created($"/api/evolution/candidates/{created.Id}", created);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapGet("/api/evolution/candidates/{id:int}/approvals", (int id, EvolutionOrchestrationService evolutionService, EvolutionApprovalService approvalService) =>
{
    if (evolutionService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(approvalService.GetApprovals(id));
});

app.MapGet("/api/evolution/candidates/{id:int}/events", (int id, EvolutionOrchestrationService evolutionService, EvolutionLifecycleService lifecycleService) =>
{
    if (evolutionService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(lifecycleService.GetByCandidateId(id));
});

app.MapPost("/api/evolution/candidates/{id:int}/approvals", (int id, CreateEvolutionApprovalRequest request, EvolutionOrchestrationService evolutionService, EvolutionApprovalService approvalService, EvolutionLifecycleService lifecycleService) =>
{
    if (evolutionService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    try
    {
        if (string.Equals(request.Decision?.Trim(), "Approve", StringComparison.OrdinalIgnoreCase) &&
            !evolutionService.MeetsMinimumFitnessGate(id))
        {
            throw new InvalidOperationException($"Candidate '{id}' does not meet the minimum fitness score gate.");
        }

        var created = approvalService.CreateApproval(id, request);
        var status = created.Decision == "Approve" ? "Approved" : "Rejected";
        evolutionService.UpdateStatus(id, status);
        lifecycleService.Record(id, status, created.ReviewerId, created.Notes);
        return Results.Created($"/api/evolution/candidates/{id}/approvals/{created.Id}", created);
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

app.MapPost("/api/evolution/candidates/{id:int}/activate", (int id, EvolutionOrchestrationService evolutionService, EvolutionLifecycleService lifecycleService) =>
{
    if (evolutionService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    try
    {
        var activated = evolutionService.Activate(id);
        lifecycleService.Record(id, "Activated", "system", null);
        return Results.Ok(activated);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapPost("/api/evolution/candidates/{id:int}/rollout/promote", (int id, EvolutionOrchestrationService evolutionService, EvolutionLifecycleService lifecycleService) =>
{
    if (evolutionService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    try
    {
        var promoted = evolutionService.PromoteRollout(id);
        lifecycleService.Record(id, $"PromotedTo{promoted.RolloutStage}", "system", null);
        return Results.Ok(promoted);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapPost("/api/evolution/candidates/{id:int}/rollback", (int id, EvolutionOrchestrationService evolutionService, EvolutionLifecycleService lifecycleService) =>
{
    if (evolutionService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    try
    {
        var rolledBack = evolutionService.Rollback(id);
        lifecycleService.Record(id, "RolledBack", "system", null);
        return Results.Ok(rolledBack);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapPost("/api/evolution/candidates/{id:int}/regression-signal", (int id, string? reason, EvolutionOrchestrationService evolutionService, EvolutionLifecycleService lifecycleService) =>
{
    if (evolutionService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    try
    {
        var rolledBack = evolutionService.AutoRollbackOnRegression(id);
        lifecycleService.Record(id, "AutoRolledBack", "system", reason);
        return Results.Ok(rolledBack);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapPost("/api/evolution/candidates/{id:int}/release", (int id, EvolutionOrchestrationService evolutionService, EvolutionLifecycleService lifecycleService) =>
{
    if (evolutionService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    try
    {
        var released = evolutionService.Release(id);
        lifecycleService.Record(id, "Released", "system", null);
        return Results.Ok(released);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapPost("/api/evolution/candidates/{id:int}/retire", (int id, EvolutionOrchestrationService evolutionService, EvolutionLifecycleService lifecycleService) =>
{
    if (evolutionService.GetById(id) is null)
    {
        return Results.NotFound();
    }

    try
    {
        var retired = evolutionService.Retire(id);
        lifecycleService.Record(id, "Retired", "system", null);
        return Results.Ok(retired);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SelfEvolving.AssetManagement.Web.Client._Imports).Assembly);

app.Run();

public partial class Program;
