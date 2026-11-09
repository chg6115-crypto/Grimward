using Grimward.Domain.Config;
using UnityEngine;
namespace Grimward.Presentation.Config
{
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "Grimward/Balance Config")]
    public sealed class BalanceConfigAsset : ScriptableObject
    {
        [SerializeField] private BalanceConfig values = new BalanceConfig();
        public BalanceConfig ToDomain()
        {
            var result = values.Copy();
            result.Validate();
            return result;
        }
    }
}
