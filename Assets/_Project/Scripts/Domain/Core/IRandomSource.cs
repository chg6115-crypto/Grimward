namespace Grimward.Domain.Core
{
    /// <summary>모든 게임 난수의 단일 출처. 테스트에서는 고정 값을 돌려주는 구현으로 바꾼다.</summary>
    public interface IRandomSource
    {
        /// <summary>[0, 1) 범위 실수.</summary>
        double NextDouble();

        /// <summary>[minInclusive, maxInclusive] 범위 정수 (양끝 포함).</summary>
        int NextInt(int minInclusive, int maxInclusive);
    }
}
