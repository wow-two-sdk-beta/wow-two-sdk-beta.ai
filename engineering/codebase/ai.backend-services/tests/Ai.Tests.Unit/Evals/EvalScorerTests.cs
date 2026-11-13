using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;

namespace Ai.Tests.Unit.Evals;

public sealed class EvalScorerTests
{
    private const string Expected = """{"relevance":"close","kind":"runway","year":2026,"online":false}""";

    [Fact]
    public void Score_ShouldBeCorrect_WhenEveryExpectedFieldMatches_IgnoringCase()
    {
        var score = EvalScorer.Score(Expected, """{"relevance":"Close","kind":"RUNWAY","year":2026,"online":false,"extra":"x"}""", [], []);

        score.Correct.Should().BeTrue();
        score.Misses.Should().BeEmpty();
        score.Answer.Should().Be("""{"relevance":"Close","kind":"RUNWAY","year":2026,"online":false}""", "only the compared fields are kept");
    }

    [Fact]
    public void Score_ShouldListEachMiss_AsFieldExpectedAnswer()
    {
        var score = EvalScorer.Score(Expected, """{"relevance":"none","kind":"runway","year":"2026"}""", [], []);

        score.Correct.Should().BeFalse();
        score.Misses.Should().Equal("relevance:close>none", "online:false>null");
        score.Answer.Should().Contain("\"online\":null");
    }

    [Fact]
    public void Score_ShouldCompareOnlyTheScoreFields_WhenTheSetNamesThem()
    {
        var score = EvalScorer.Score(Expected, """{"relevance":"close","kind":"market"}""", ["relevance", "missing-in-expected"], []);

        score.Correct.Should().BeTrue();
        score.Answer.Should().Be("""{"relevance":"close"}""");
    }

    [Fact]
    public void Score_ShouldBeSevere_WhenAMissIsASevereMiss()
    {
        var severe = EvalScorer.Score(Expected, """{"relevance":"none","kind":"runway"}""", ["relevance"], ["relevance:CLOSE>none"]);
        var mild = EvalScorer.Score(Expected, """{"relevance":"far","kind":"runway"}""", ["relevance"], ["relevance:close>none"]);

        severe.Severe.Should().BeTrue();
        mild.Severe.Should().BeFalse();
        mild.Correct.Should().BeFalse();
    }

    [Fact]
    public void Score_ShouldMissEveryField_WhenTheAnswerIsNotAnObject()
    {
        var score = EvalScorer.Score("""{"relevance":"close"}""", "close", [], []);

        score.Misses.Should().Equal("relevance:close>null");
        score.Answer.Should().Be("""{"relevance":null}""");
    }
}
