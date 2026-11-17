using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Ai.Tests.E2E;

/// <summary>The HTTP API under <c>api/ai/</c> on the real host: token and scope refusals, a queued comparison from
/// <c>202</c> through the worker to a report as JSON, CSV and Markdown, data scopes between tenants, and the provider
/// catalog.</summary>
/// <param name="fixture">The shared host, over its own PostgreSQL, with scripted providers and a test token issuer.</param>
[Collection(AiApiCollection.Name)]
public sealed class AiApiTests(AiApiFixture fixture)
{
    private const string Haiku = "anthropic/claude-haiku-5-5";
    private const string AllScopes = "ai.read ai.run ai.prompts-manage ai.evals-manage";

    [Fact]
    public async Task Routes_ShouldAnswer401_WithoutAValidToken()
    {
        using var anonymous = fixture.Host.CreateClient();
        using var forged = Client("not-a-jwt");
        using var otherAudience = Client(AiApiFixture.Token(AllScopes, audience: "https://files.example.test/"));

        (await anonymous.GetAsync("/api/ai/usecases")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/ai/runs", new { useCase = "x" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await forged.GetAsync("/api/ai/evals/runs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await otherAudience.GetAsync("/api/ai/providers")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a token for another service is refused");
        (await anonymous.GetAsync("/api/system/status")).StatusCode.Should().Be(HttpStatusCode.OK, "status stays open");
    }

    [Fact]
    public async Task Routes_ShouldAnswer403_WithoutTheRoutesScope()
    {
        using var reader = Client(AiApiFixture.Token("ai.read"));
        using var runner = Client(AiApiFixture.Token("ai.run"));

        (await reader.GetAsync("/api/ai/usecases")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await reader.PutAsJsonAsync("/api/ai/usecases/forbidden-case", new { promptMode = "rewrite" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.PostAsJsonAsync("/api/ai/runs", new { useCase = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.PostAsJsonAsync("/api/ai/evals/sets/x/runs", new { models = new[] { Haiku } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.GetAsync("/api/ai/evals/sets/x/items")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "item inputs need ai.evals-manage");
        (await reader.GetAsync("/api/ai/evals/sets/x/export")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await runner.GetAsync("/api/ai/inferences")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Queue_ShouldAnswer202_AndTheWorkerShouldRunIt_ToAReportInEveryFormat()
    {
        using var admin = Client(AiApiFixture.Token(AllScopes));
        var (useCase, set) = await SetUpAsync(admin, "queue", items: 3);

        using var queued = await admin.PostAsJsonAsync($"/api/ai/evals/sets/{set}/runs",
            new { models = new[] { Haiku, "openai/gpt-6-luna" }, count = 2, seed = 5, note = "e2e", requestedBy = "e2e" });

        queued.StatusCode.Should().Be(HttpStatusCode.Accepted, await queued.Content.ReadAsStringAsync());
        var batch = await DataAsync(queued);
        var batchId = batch.GetProperty("id").GetString()!;
        queued.Headers.Location!.ToString().Should().Be($"/api/ai/evals/runs?batch={batchId}");
        batch.GetProperty("itemIds").GetArrayLength().Should().Be(2);
        var runIds = batch.GetProperty("runs").EnumerateArray().Select(run => run.GetProperty("id").GetString()!).ToList();
        runIds.Should().HaveCount(2);
        batch.GetProperty("runs").EnumerateArray().Should().OnlyContain(run => run.GetProperty("status").GetString() == "queued");

        foreach (var runId in runIds)
            await WaitForAsync(admin, runId, "succeeded");

        var listed = await DataAsync(await admin.GetAsync($"/api/ai/evals/runs?batch={batchId}"));
        listed.EnumerateArray().Should().HaveCount(2).And.OnlyContain(run => run.GetProperty("itemsDone").GetInt32() == 2);

        var json = await DataAsync(await admin.GetAsync($"/api/ai/evals/sets/{set}/report?runs={string.Join(',', runIds)}&answers=true&format=json"));
        json.GetProperty("useCase").GetString().Should().Be(useCase);
        json.GetProperty("runs").EnumerateArray().Should().HaveCount(2).And.OnlyContain(run =>
            run.GetProperty("items").GetInt32() == 2 && run.GetProperty("accuracy").GetDouble() == 1 && run.GetProperty("spentMicroUsd").GetInt64() > 0);
        var promptHash = json.GetProperty("runs")[0].GetProperty("run").GetProperty("promptHash").GetString();
        json.GetProperty("items").EnumerateArray().Should().HaveCount(4).And.OnlyContain(item => item.GetProperty("promptHash").GetString() == promptHash);

        using var csv = await admin.GetAsync($"/api/ai/evals/sets/{set}/report?runs={string.Join(',', runIds)}&format=csv");
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var lines = (await csv.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(5);
        lines[0].Should().StartWith("run_id,status,provider,model,prompt_version,prompt_hash");
        lines.Skip(1).Should().OnlyContain(line => line.Contains(",succeeded,", StringComparison.Ordinal) && line.Contains(promptHash!, StringComparison.Ordinal));

        var markdown = await (await admin.GetAsync($"/api/ai/evals/sets/{set}/report?format=md")).Content.ReadAsStringAsync();
        markdown.Should().Contain($"### Eval `{set}`").And.Contain(promptHash!);
        (await admin.GetAsync($"/api/ai/evals/sets/{set}/report?format=pdf")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_ShouldEndAQueuedRun_AndRatingsShouldShowInTheReport()
    {
        using var admin = Client(AiApiFixture.Token(AllScopes));
        var (_, set) = await SetUpAsync(admin, "rate", items: 1);
        var now = await DataAsync(await admin.PostAsJsonAsync("/api/ai/evals/runs", new { set, model = Haiku }));
        now.GetProperty("status").GetString().Should().Be("succeeded");
        var runId = now.GetProperty("id").GetString()!;
        var itemId = (await DataAsync(await admin.GetAsync($"/api/ai/evals/sets/{set}/items")))[0].GetProperty("id").GetString();

        var rated = await DataAsync(await admin.PostAsJsonAsync($"/api/ai/evals/runs/{runId}/ratings", new { itemId, rating = 5, comment = "right" }));
        var canceled = await DataAsync(await admin.PostAsync($"/api/ai/evals/runs/{runId}/cancel", content: null));

        rated.GetProperty("rating").GetInt32().Should().Be(5);
        canceled.GetProperty("status").GetString().Should().Be("succeeded", "an ended run stays as it is");
        (await admin.GetAsync($"/api/ai/evals/runs/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var problem = await admin.PostAsJsonAsync($"/api/ai/evals/runs/{runId}/ratings", new { itemId, rating = 9 });
        problem.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await problem.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be("Validation");
    }

    [Fact]
    public async Task Scopes_ShouldKeepTenantsApart_OnEveryReadAndWrite()
    {
        using var admin = Client(AiApiFixture.Token(AllScopes, isOperator: true));
        using var tenantA = Client(AiApiFixture.Token(AllScopes, tenant: "a"));
        using var tenantB = Client(AiApiFixture.Token(AllScopes, tenant: "b"));
        var (useCase, _) = await SetUpAsync(admin, "scopes", items: 0);
        var setA = "scope-a-" + Guid.NewGuid().ToString("N")[..8];

        var created = await DataAsync(await tenantA.PutAsJsonAsync($"/api/ai/evals/sets/{setA}", new { useCase, scoreFields = new[] { "relevance" } }));
        await DataAsync(await tenantA.PostAsJsonAsync($"/api/ai/evals/sets/{setA}/items", new { input = new { title = "a's post" }, expected = """{"relevance":"close"}""" }));
        var runA = await DataAsync(await tenantA.PostAsJsonAsync("/api/ai/runs", new { useCase, variables = new { title = "a's call" }, subject = "post:a" }));
        var runB = await DataAsync(await tenantB.PostAsJsonAsync("/api/ai/runs", new { useCase, variables = new { title = "b's call" }, subject = "post:b" }));

        created.GetProperty("scope").GetString().Should().Be("tenant:a", "a write naming no scope lands in the caller's");
        runA.GetProperty("attempts")[0].GetProperty("scope").GetString().Should().Be("tenant:a");
        runB.GetProperty("attempts")[0].GetProperty("scope").GetString().Should().Be("tenant:b");

        (await DataAsync(await tenantB.GetAsync("/api/ai/evals/sets"))).EnumerateArray().Should().NotContain(set => set.GetProperty("key").GetString() == setA);
        (await tenantB.GetAsync($"/api/ai/evals/sets/{setA}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await tenantB.GetAsync($"/api/ai/evals/sets/{setA}/report")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await tenantB.PostAsJsonAsync($"/api/ai/evals/sets/{setA}/runs", new { models = new[] { Haiku } })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await tenantB.PutAsJsonAsync($"/api/ai/evals/sets/{setA}", new { useCase })).StatusCode.Should().Be(HttpStatusCode.Conflict, "a set keeps its scope");
        (await tenantB.PostAsJsonAsync("/api/ai/runs", new { useCase, variables = new { title = "x" }, scope = "tenant:a" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tenantB.GetAsync("/api/ai/inferences?scope=tenant%3Aa")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var seenByB = await DataAsync(await tenantB.GetAsync($"/api/ai/inferences?useCase={useCase}"));
        seenByB.EnumerateArray().Should().ContainSingle().Which.GetProperty("subject").GetString().Should().Be("post:b");
        var totalsB = await DataAsync(await tenantB.GetAsync($"/api/ai/inferences/totals?from=2000-01-01&to=2100-01-01"));
        totalsB.EnumerateArray().Where(total => total.GetProperty("useCase").GetString() == useCase).Should().ContainSingle()
            .Which.GetProperty("attempts").GetInt64().Should().Be(1);
        var seenByAdmin = await DataAsync(await admin.GetAsync($"/api/ai/inferences?useCase={useCase}"));
        seenByAdmin.EnumerateArray().Select(inference => inference.GetProperty("subject").GetString()).Should().BeEquivalentTo("post:a", "post:b");
        (await DataAsync(await admin.GetAsync($"/api/ai/inferences?useCase={useCase}&scope=tenant%3Ab"))).EnumerateArray().Should().ContainSingle();
        (await DataAsync(await tenantB.PostAsJsonAsync("/api/ai/inferences/forget", new { subject = "post:a" }))).GetInt32().Should().Be(0, "b cannot purge a's calls");
    }

    [Fact]
    public async Task Providers_ShouldListTheCatalog_WithPricedModels_AndNoKey()
    {
        using var reader = Client(AiApiFixture.Token("ai.read"));

        using var response = await reader.GetAsync("/api/ai/providers?live=true");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, text);
        using var body = JsonDocument.Parse(text);
        var anthropic = body.RootElement.GetProperty("data").EnumerateArray().Single(provider => provider.GetProperty("name").GetString() == "anthropic");
        anthropic.GetProperty("pricedModels").EnumerateArray().Select(model => model.GetString()).Should().Contain("claude-haiku-5-5");
        anthropic.GetProperty("configured").GetBoolean().Should().BeFalse();
        text.Should().NotContainEquivalentOf("apiKey");
    }

    /// <summary>Creates a rewrite use case with one prompt and a set with <paramref name="items"/> items, over HTTP.</summary>
    private static async Task<(string UseCase, string Set)> SetUpAsync(HttpClient admin, string name, int items)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var useCase = $"{name}-{suffix}";
        var set = $"{name}-set-{suffix}";
        var schema = """{"type":"object","required":["relevance"],"properties":{"relevance":{"type":"string"}}}""";
        await DataAsync(await admin.PutAsJsonAsync($"/api/ai/usecases/{useCase}", new { promptMode = "rewrite", outputSchema = schema, route = new[] { Haiku } }));
        await DataAsync(await admin.PostAsJsonAsync($"/api/ai/usecases/{useCase}/prompts",
            new { systemTemplate = "You rate posts.", userTemplate = "Post: {{title}}", parameters = new { maxOutputTokens = 64 } }));
        await DataAsync(await admin.PutAsJsonAsync($"/api/ai/evals/sets/{set}", new { useCase, scoreFields = new[] { "relevance" } }));
        for (var index = 0; index < items; index++)
        {
            await DataAsync(await admin.PostAsJsonAsync($"/api/ai/evals/sets/{set}/items",
                new { input = new JsonObject { ["title"] = $"post {index}" }, expected = """{"relevance":"close"}""", tags = new[] { "lang:en" } }));
        }

        return (useCase, set);
    }

    private static async Task WaitForAsync(HttpClient client, string runId, string status)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var run = await DataAsync(await client.GetAsync($"/api/ai/evals/runs/{runId}"));
            if (run.GetProperty("status").GetString() == status)
                return;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Run {runId} reads {run}.");
            await Task.Delay(100);
        }
    }

    private HttpClient Client(string token)
    {
        var client = fixture.Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            response.IsSuccessStatusCode.Should().BeTrue($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} answered {(int)response.StatusCode}: {text}");
            using var document = JsonDocument.Parse(text);
            return document.RootElement.GetProperty("data").Clone();
        }
    }
}
