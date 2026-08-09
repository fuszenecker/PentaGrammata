using PentaGrammata.Configuration;
using PentaGrammata.Players;

namespace PentaGrammata.Tests.Players;

[TestClass]
public sealed class NoiseGeneratorFactoryTests
{
    private readonly NoiseGeneratorFactory _factory = new();

    [TestMethod]
    public void Create_None_ReturnsNull()
    {
        Assert.IsNull(_factory.Create(NoiseType.None));
    }

    [TestMethod]
    [DataRow(NoiseType.Gaussian)]
    [DataRow(NoiseType.Uniform)]
    [DataRow(NoiseType.Pink)]
    public void Create_KnownType_ReturnsGeneratorProducingFiniteSamples(NoiseType type)
    {
        var generator = _factory.Create(type);

        Assert.IsNotNull(generator);

        for (int i = 0; i < 1000; i++)
        {
            double sample = generator.Next();
            Assert.IsFalse(double.IsNaN(sample) || double.IsInfinity(sample), "noise sample must be finite");
        }
    }
}
