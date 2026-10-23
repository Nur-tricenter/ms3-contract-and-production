using Nurtricenter.MS3.Infrastructure;
using Nurtricenter.MS3.Infrastructure.Messaging;
using Nurtricenter.MS3.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddContractingRabbitMq(builder.Configuration);
builder.Services.AddOutboxRelay();
builder.Services.AddCatalogConsumers();
builder.Services.AddHostedService<OutboxRelayService>();

var host = builder.Build();
host.Run();
