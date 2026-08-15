using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Application;
using Nurtricenter.MS3.Infrastructure;
using Nurtricenter.MS3.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFastEndpoints();


builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "Nurtricenter MS3 - Contracting & Production";
        s.Version = "v1";
        s.Description = "Microservice for contract management, billing, and daily production/packaging operations.";
    };
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);

    app.UseSwaggerGen();
    app.UseSwaggerUi();
}

app.UseDefaultExceptionHandler();
app.UseHttpsRedirection();
app.UseFastEndpoints();

app.Run();
