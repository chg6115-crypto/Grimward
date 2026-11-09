namespace Grimward.Domain.Core
{
    public interface IRandomSource
    {
        double NextDouble();
        int NextInt(int minInclusive, int maxInclusive);
    }
}
