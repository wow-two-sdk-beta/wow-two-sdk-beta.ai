using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Ai;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting;
using WoW.Two.Sdk.Beta.Be.Ai.Hosting.Access;

namespace Ai.Tests.Unit.Hosting;

public sealed class AiScopeGrantTests
{
    [Fact]
    public void Unscoped_ShouldSeeAndWriteTheUnscopedBucketOnly()
    {
        var grant = AiScopeGrant.Unscoped;

        grant.Allows(null).Should().BeTrue();
        grant.Allows(" ").Should().BeTrue();
        grant.Allows("tenant:a").Should().BeFalse();
        grant.TryWrite(null, out var scope).Should().BeTrue();
        scope.Should().Be(AiConstants.Unscoped);
        grant.TryWrite("tenant:a", out _).Should().BeFalse();
        grant.TryRead(null, out var filter).Should().BeTrue();
        filter.Should().Equal(AiConstants.Unscoped);
    }

    [Fact]
    public void Of_ShouldWriteToTheFirstScope_AndReadOnlyTheGrantedOnes()
    {
        var grant = AiScopeGrant.Of("tenant:a", "tenant:b");

        grant.TryWrite(null, out var scope).Should().BeTrue();
        scope.Should().Be("tenant:a");
        grant.TryWrite(" tenant:b ", out scope).Should().BeTrue();
        scope.Should().Be("tenant:b");
        grant.TryWrite(AiConstants.Unscoped, out _).Should().BeTrue("a blank scope names none, which takes the default");
        grant.TryRead(["tenant:b"], out var filter).Should().BeTrue();
        filter.Should().Equal("tenant:b");
        grant.TryRead(["tenant:c"], out _).Should().BeFalse();
        grant.TryRead([AiConstants.Unscoped], out _).Should().BeFalse("the unscoped bucket is not granted");
        grant.TryRead(null, out filter).Should().BeTrue();
        filter.Should().Equal("tenant:a", "tenant:b");
    }

    [Fact]
    public void Every_ShouldReadEveryScope_WithNoFilter()
    {
        AiScopeGrant.Every.TryRead(null, out var filter).Should().BeTrue();
        filter.Should().BeNull();
        AiScopeGrant.Every.TryRead(["tenant:z", ""], out filter).Should().BeTrue();
        filter.Should().Equal("tenant:z", "");
        AiScopeGrant.Every.TryWrite(null, out var scope).Should().BeTrue();
        scope.Should().Be(AiConstants.Unscoped);
        AiScopeGrant.None.TryWrite(null, out _).Should().BeFalse();
        AiScopeGrant.None.TryRead(null, out filter).Should().BeTrue();
        filter.Should().BeEmpty();
    }

    [Theory]
    [InlineData("scope", "ai.read ai.run", "ai.run", true)]
    [InlineData("scp", "ai.evals-manage", "ai.evals-manage", true)]
    [InlineData("scope", "ai.read", "ai.run", false)]
    [InlineData("roles", "ai.run", "ai.run", false)]
    [InlineData("scope", "ai.reader", "ai.read", false)]
    public void HasScope_ShouldReadTheScopeClaims_WordByWord(string type, string value, string scope, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(type, value)], "test"));

        AiScopeAuthorizationHandler.HasScope(user, scope).Should().Be(expected);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void AddAiApi_ShouldLiftTheScopes_InDevelopmentOnly(string environment, bool starts)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IHostEnvironment>(new Environment(environment));
        services.AddAiApi(options => options.AllowAnonymousInDevelopment = true);
        using var provider = services.BuildServiceProvider();

        var read = () => provider.GetRequiredService<IOptions<AiApiOptions>>().Value;

        if (starts)
            read.Should().NotThrow();
        else
            read.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("Development");
    }

    [Fact]
    public void AddAiApi_ShouldKeepTheHostsScopeAccess()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAiScopeAccess, CustomAccess>();
        services.AddAiApi();

        services.Where(service => service.ServiceType == typeof(IAiScopeAccess)).Should().ContainSingle()
            .Which.ImplementationType.Should().Be<CustomAccess>();
    }

    private sealed class CustomAccess : IAiScopeAccess
    {
        public ValueTask<AiScopeGrant> GrantAsync(Microsoft.AspNetCore.Http.HttpContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(AiScopeGrant.Every);
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "ai-tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
