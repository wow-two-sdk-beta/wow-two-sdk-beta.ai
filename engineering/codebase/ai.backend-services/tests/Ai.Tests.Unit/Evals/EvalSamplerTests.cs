using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Ai.Tests.Unit.Evals;

public sealed class EvalSamplerTests
{
    private static readonly IReadOnlyList<EvalItemModel> Items = [.. Enumerable.Range(1, 8).Select(index => new EvalItemModel
    {
        Id = Guid.Parse($"0192f0aa-0000-7000-8000-{index:D12}"),
        Set = "set",
        Input = new Dictionary<string, string?> { ["title"] = $"post {index}" },
        Expected = "{}",
        Tags = index % 2 == 0 ? ["lang:ru", "slice:positive"] : ["lang:en"],
    })];

    [Fact]
    public void Select_ShouldKeepTheSameItems_ForTheSameSeed_InTheSetsOrder()
    {
        var sample = Sample(count: 3, seed: 11);

        var first = Value(EvalSampler.Select("set", Items, sample));
        var shuffled = Value(EvalSampler.Select("set", [.. Items.Reverse()], sample));
        var other = Value(EvalSampler.Select("set", Items, Sample(count: 3, seed: 12)));

        first.Should().HaveCount(3);
        shuffled.Select(item => item.Id).Should().BeEquivalentTo(first.Select(item => item.Id), "each item ranks on its own");
        first.Select(item => item.Id).Should().BeInAscendingOrder("the set's order is kept");
        other.Select(item => item.Id).Should().NotEqual(first.Select(item => item.Id));
        EvalSampler.Rank(11, Items[0].Id).Should().Be(EvalSampler.Rank(11, Items[0].Id));
    }

    [Fact]
    public void Select_ShouldFilterByIdsThenTags_AndRefuseWhatKeepsNothing()
    {
        Value(EvalSampler.Select("set", Items, Sample(tags: ["lang:ru", "slice:positive"]))).Should().HaveCount(4);
        Value(EvalSampler.Select("set", Items, Sample(ids: [Items[0].Id, Items[1].Id], tags: ["lang:ru"]))).Should().ContainSingle().Which.Id.Should().Be(Items[1].Id);
        Value(EvalSampler.Select("set", Items, Sample(count: 100))).Should().HaveCount(8);
        Value(EvalSampler.Select("set", [], Sample())).Should().BeEmpty("a whole empty set is no error");

        EvalSampler.Select("set", Items, Sample(tags: ["lang:uz"])).Should().BeOfType<Result<IReadOnlyList<EvalItemModel>>.Failure>();
        EvalSampler.Select("set", Items, Sample(ids: [Guid.NewGuid()])).Should().BeOfType<Result<IReadOnlyList<EvalItemModel>>.Failure>();
        EvalSampler.Normalize(0, null, null, null).Should().BeOfType<Result<EvalSampleModel>.Failure>();
        Value(EvalSampler.Normalize(null, null, [" lang:ru ", "lang:ru", ""], [Guid.Empty, Items[0].Id, Items[0].Id]))
            .Should().BeEquivalentTo(new EvalSampleModel { Tags = ["lang:ru"], ItemIds = [Items[0].Id] });
    }

    [Fact]
    public void InputHash_ShouldIgnoreKeyOrder_AndSeeEveryValue()
    {
        var one = ModuleEvalsBroker.InputHash(new Dictionary<string, string?> { ["a"] = "1", ["b"] = "Привет" });
        var two = ModuleEvalsBroker.InputHash(new Dictionary<string, string?> { ["b"] = "Привет", ["a"] = "1" });
        var three = ModuleEvalsBroker.InputHash(new Dictionary<string, string?> { ["a"] = "1", ["b"] = null });

        two.Should().Be(one).And.MatchRegex("^[0-9a-f]{64}$");
        three.Should().NotBe(one);
    }

    [Fact]
    public void QueueErrors_ShouldBeTrimmed_AndNameTheOrphanThreshold()
    {
        EvalRunQueue.Trim(null).Should().BeNull();
        EvalRunQueue.Trim("  ").Should().BeNull();
        EvalRunQueue.Trim(new string('x', 3000))!.Length.Should().Be(2000);
        EvalRunQueue.OrphanedError(TimeSpan.FromMinutes(2)).Should().StartWith("Orphaned").And.Contain("120 seconds");
    }

    private static EvalSampleModel Sample(int? count = null, int? seed = null, IReadOnlyList<string>? tags = null, IReadOnlyList<Guid>? ids = null) =>
        Value(EvalSampler.Normalize(count, seed, tags, ids));

    private static T Value<T>(Result<T> result) where T : notnull => result.Should().BeOfType<Result<T>.Success>().Subject.Value;
}
