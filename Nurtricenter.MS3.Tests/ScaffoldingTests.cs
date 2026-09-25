using FluentAssertions;
using Nurtricenter.MS3.Api.Endpoints.Contracts;
using Nurtricenter.MS3.Application.Handlers;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Tests;

public sealed class ScaffoldingTests
{
    [Fact]
    public void Test_project_references_the_three_layers_of_the_microservice()
    {
        typeof(Contract).Assembly.GetName().Name.Should().Be("Nurtricenter.MS3.Core");
        typeof(CreateContractCommandHandler).Assembly.GetName().Name.Should().Be("Nurtricenter.MS3.Application");
        typeof(CreateContractEndpoint).Assembly.GetName().Name.Should().Be("Nurtricenter.MS3.Api");
    }
}
