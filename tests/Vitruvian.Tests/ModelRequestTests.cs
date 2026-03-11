using VitruvianAbstractions;
using VitruvianAbstractions.Interfaces;
using Xunit;

namespace VitruvianTests;

public sealed class ModelRequestTests
{
    [Fact]
    public void Complexity_DefaultsToNull()
    {
        var request = new ModelRequest { Prompt = "test" };
        Assert.Null(request.Complexity);
    }

    [Fact]
    public void Complexity_CanBeSet()
    {
        var request = new ModelRequest { Prompt = "test", Complexity = Complexity.Medium };
        Assert.Equal(Complexity.Medium, request.Complexity);
    }
}
