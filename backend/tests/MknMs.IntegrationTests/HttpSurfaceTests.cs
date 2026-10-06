using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Exercises every process endpoint over real HTTP, in-process.
/// </summary>
/// <remarks>
/// These tests exist to close the coverage gap that allowed a routing
/// defect to survive: the service-level integration tests call services
/// directly and never trigger ASP.NET Core's endpoint-building path, so
/// a malformed route delegate is invisible to them. This class builds
/// the full host via WebApplicationFactory and issues one HTTP request
/// per process endpoint.
///
/// The assertion is deliberately narrow: the request must not produce
/// an HTTP 500 caused by endpoint-construction failure. A 200 with a
/// service-level Failure payload, or a 422, or a 4xx, are all fine — the
/// point is that the routing pipeline and dependency injection accepted
/// the request.
///
/// Reference: this class was added after the runtime validation that
/// found the missing [FromBody] annotations on ConfigurationEndpoints.
/// </remarks>
public class HttpSurfaceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HttpSurfaceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:MknDb",
                "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev");
        });
    }

    private static async Task AssertNoRoutingFailureAsync(HttpResponseMessage response, string endpoint)
    {
        // A 500 with the endpoint-construction message is the specific
        // defect these tests guard against. Any other status is
        // acceptable — the endpoint was reached.
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain(
                "Body was inferred but the method does not allow inferred body parameters",
                $"endpoint {endpoint} must not fail at routing construction");
        }
    }

    [Fact]
    public async Task Root_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRoles_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/config/1.0/roles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Materialize_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/11.0/materialize",
            new { triggerType = "Scheduled" });

        await AssertNoRoutingFailureAsync(response, "/api/processes/11.0/materialize");
    }

    [Fact]
    public async Task GenerateAssignment_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/5.0/generate",
            new { fromDate = "2026-10-06", toDate = "2026-10-31" });

        await AssertNoRoutingFailureAsync(response, "/api/processes/5.0/generate");
    }

    [Fact]
    public async Task ManageConfirmationRespond_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/7.0/respond",
            new { assignmentId = 1, operation = "Confirm" });

        await AssertNoRoutingFailureAsync(response, "/api/processes/7.0/respond");
    }

    [Fact]
    public async Task ManageConfirmationSweep_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/7.0/sweep",
            new { });

        await AssertNoRoutingFailureAsync(response, "/api/processes/7.0/sweep");
    }

    [Fact]
    public async Task EvaluateFillStatus_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/10.0/evaluate",
            new { fromDate = "2026-10-06", toDate = "2026-10-31" });

        await AssertNoRoutingFailureAsync(response, "/api/processes/10.0/evaluate");
    }

    [Fact]
    public async Task RecordAttendance_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/6.0/record",
            new { memberId = 1, occurrenceId = 1 });

        await AssertNoRoutingFailureAsync(response, "/api/processes/6.0/record");
    }

    [Fact]
    public async Task CreateManualAssignment_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/12.0/manual",
            new { memberId = 1, dutyId = 1, occurrenceId = 1, actingAdminId = 1 });

        await AssertNoRoutingFailureAsync(response, "/api/processes/12.0/manual");
    }

    [Fact]
    public async Task IssueIdentifier_WithRouteAndBody_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/config/3.0/members/1/identifiers",
            new
            {
                type = "Card",
                number = "HTTP-TEST-001",
                assignedDate = "2026-10-06",
                authorizedByAdminId = 1,
            });

        await AssertNoRoutingFailureAsync(response, "/api/config/3.0/members/1/identifiers");
    }

    [Fact]
    public async Task GrantEligibility_WithRouteAndBody_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/config/4.0/members/1/eligibility",
            new { dutyId = 1, grantedDate = "2026-10-06", grantedByAdminId = 1 });

        await AssertNoRoutingFailureAsync(response, "/api/config/4.0/members/1/eligibility");
    }

    [Fact]
    public async Task RevokeEligibility_WithRouteAndBody_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.DeleteAsync("/api/config/4.0/eligibility/1");

        await AssertNoRoutingFailureAsync(response, "/api/config/4.0/eligibility/1");
    }
}
