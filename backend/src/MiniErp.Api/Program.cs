using MiniErp.App.Modules.Platform;
using MiniErp.Contracts.Modules.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IPlatformAdministrationModule>(_ => PlatformModuleRegistration.Create());

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/v1/module-registration", (IPlatformAdministrationModule platformModule) =>
    Results.Ok(new
    {
        module = platformModule.Descriptor.Key,
        name = platformModule.Descriptor.Name,
        boundary = platformModule.Descriptor.Boundary,
        registered = platformModule.RegistrationEvidence.IsRegistered
    }));

app.Run();
