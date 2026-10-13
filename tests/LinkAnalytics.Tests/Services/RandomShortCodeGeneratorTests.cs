using LinkAnalytics.Api.Services;

namespace LinkAnalytics.Tests.Services;

public class RandomShortCodeGeneratorTests
{
    private readonly RandomShortCodeGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsBase62CodeWithExpectedLength()
    {
        var code = _generator.Generate();

        Assert.Matches($"^[0-9A-Za-z]{{{RandomShortCodeGenerator.CodeLength}}}$", code);
    }

    [Fact]
    public void Generate_ProducesDifferentCodes()
    {
        var codes = Enumerable.Range(0, 1_000).Select(_ => _generator.Generate()).ToHashSet();

        Assert.Equal(1_000, codes.Count);
    }
}
