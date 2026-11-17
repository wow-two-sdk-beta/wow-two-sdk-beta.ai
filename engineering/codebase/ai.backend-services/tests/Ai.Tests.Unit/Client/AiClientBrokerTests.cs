using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Ai;
using WoW.Two.Sdk.Beta.Be.Ai.Client;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Inferences;
using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Ai.Tests.Unit.Client;

/// <summary>The client's brokers over a stub handler: every broker registered on one named client, requests on the API's
/// routes, and the service's problems read back as the contract's errors.</summary>
public sealed class AiClientBrokerTests
{
    [Fact]
    public void AddAiClient_ShouldRegisterEveryBroker_InPlaceOfTheModules()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAiRunner>(new NoRunner());
        services.AddAiClient(options => options.BaseAddress = new Uri("https://ai.example.test/"));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAiRunner>().Should().NotBeOfType<NoRunner>("the client replaces a Form 1 registration");
        provider.GetServices<IAiRunner>().Should().ContainSingle();
        provider.GetRequiredService<IPromptsBroker>().Should().NotBeNull();
        provider.GetRequiredService<IModelPricesBroker>().Should().NotBeNull();
        provider.GetRequiredService<IInferencesBroker>().Should().NotBeNull();
        provider.GetRequiredService<IEvalsBroker>().Should().NotBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"code":"NotFound","detail":"Eval set 'x' does not exist."}""", AppErrorType.NotFound, "Eval set 'x' does not exist.")]
    [InlineData(HttpStatusCode.Conflict, """{"code":"Conflict","detail":"taken"}""", AppErrorType.Conflict, "taken")]
    [InlineData(HttpStatusCode.BadRequest, """{"title":"bad"}""", AppErrorType.Validation, "bad")]
    [InlineData(HttpStatusCode.Unauthorized, "", AppErrorType.Unauthorized, "The Ai service answered 401.")]
    [InlineData(HttpStatusCode.Forbidden, "", AppErrorType.Forbidden, "The Ai service answered 403.")]
    [InlineData(HttpStatusCode.BadGateway, "<html/>", AppErrorType.ExternalUnavailable, "The Ai service answered 502.")]
    public async Task Brokers_ShouldReadTheServicesProblem_AsTheContractsError(HttpStatusCode status, string body, AppErrorType type, string message)
    {
        var (provider, _) = Client(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/problem+json") });
        using var scope = provider;

        var result = await provider.GetRequiredService<IEvalsBroker>().GetSetAsync("x");

        var error = result.Should().BeOfType<Result<EvalSetModel>.Failure>().Subject.Error;
        error.Type.Should().Be(type);
        error.Message.Should().Be(message);
    }

    [Fact]
    public async Task Brokers_ShouldCallTheApisRoutes_WithScopesAndEscapedKeys()
    {
        var (provider, requests) = Client(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"data":[]}""", Encoding.UTF8, "application/json") });
        using var scope = provider;
        var inferences = provider.GetRequiredService<IInferencesBroker>();
        var evals = provider.GetRequiredService<IEvalsBroker>();

        (await inferences.ListAsync(new InferenceQuery { UseCase = "relevance", Scopes = ["tenant:a", AiConstants.Unscoped], Limit = 5 })).Should().BeOfType<Result<IReadOnlyList<InferenceModel>>.Success>();
        (await inferences.ListAsync(new InferenceQuery { Scopes = [] })).Should().BeOfType<Result<IReadOnlyList<InferenceModel>>.Success>();
        (await evals.ListRunsAsync(new EvalRunQuery { Status = EvalRunStatus.Running, Limit = 3 })).Should().BeOfType<Result<IReadOnlyList<EvalRunModel>>.Success>();
        (await evals.ListRunsAsync("a set")).Should().BeOfType<Result<IReadOnlyList<EvalRunModel>>.Success>();
        (await evals.GetSetAsync(" ")).Should().BeOfType<Result<EvalSetModel>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Validation);

        requests.Select(request => request.RequestUri!.PathAndQuery).Should().Equal(
            "/root/api/ai/inferences?useCase=relevance&limit=5&scope=tenant%3Aa&scope=",
            "/root/api/ai/evals/runs?status=running&limit=3",
            "/root/api/ai/evals/sets/a%20set/runs");
    }

    [Fact]
    public async Task Brokers_ShouldAnswerUnavailable_WhenTheServiceIsUnreachable()
    {
        var (provider, _) = Client(_ => throw new HttpRequestException("connection refused"));
        using var scope = provider;

        var result = await provider.GetRequiredService<IPromptsBroker>().ListUseCasesAsync();

        result.Should().BeOfType<Result<IReadOnlyList<UseCaseModel>>.Failure>().Which.Error.Type.Should().Be(AppErrorType.ExternalUnavailable);
    }

    private static (ServiceProvider Provider, List<HttpRequestMessage> Requests) Client(Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var requests = new List<HttpRequestMessage>();
        var services = new ServiceCollection();
        services.AddAiClient(options => options.BaseAddress = new Uri("https://ai.example.test/root"))
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(request =>
            {
                requests.Add(request);
                return answer(request);
            }));
        return (services.BuildServiceProvider(), requests);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }

    private sealed class NoRunner : IAiRunner
    {
        public Task<Result<AiRunModel>> RunAsync(AiRunRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
