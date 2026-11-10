using System.Collections.Generic;
using Grimward.Domain.Core;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>한 전투가 공유하는 상태. BattleState가 만들고 규칙 클래스들이 읽고 쓴다.</summary>
    public sealed class BattleContext
    {
        public BalanceConfig Config { get; }
        public IRandomSource Random { get; }
        public Formation Formation { get; }
        public CombatantFactory Factory { get; }
        public bool IsNight { get; }
        public int Round { get; internal set; }
        public Combatant CurrentActor { get; internal set; }
        public List<BattleEvent> Log { get; } = new List<BattleEvent>();
        /// <summary>처치한 몬스터 정의 ID (의뢰 집계용).</summary>
        public List<string> KilledMonsterIds { get; } = new List<string>();

        public BattleContext(BalanceConfig config, IRandomSource random, Formation formation,
            CombatantFactory factory, bool isNight)
        {
            Config = config; Random = random; Formation = formation; Factory = factory; IsNight = isNight;
        }

        public void Emit(BattleEvent e) => Log.Add(e);

        public static BattleSide Opposite(BattleSide side) => side == BattleSide.Party ? BattleSide.Enemy : BattleSide.Party;
    }
}
