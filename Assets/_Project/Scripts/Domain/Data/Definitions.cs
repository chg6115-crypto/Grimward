using System;

namespace Grimward.Domain.Data
{
    // 정의 데이터. 필드 이름 = JSON 키. Unity JsonUtility와 테스트 코드 양쪽에서 채운다.
    // 문자열 종류값(kind, target 등)은 GameDatabase가 검증한다.

    [Serializable]
    public sealed class GameDataFile
    {
        public BalanceConfig balance;
        public ClassDefinition[] classes;
        public SkillDefinition[] skills;
        public MonsterDefinition[] monsters;
        public EncounterDefinition[] encounters;
        public ItemDefinition[] items;
        public NameDefinition[] monsterTypes;
        public InjuryDefinition[] injuries;
        public MapDefinition[] maps;
        public InvestigationDefinition[] investigations;
    }

    /// <summary>조사 물건 (엑셀 23_조사물건). 결과 셋 중 하나: 보급품(supplyItem)을 쓰면 좋음 확정.</summary>
    [Serializable]
    public sealed class InvestigationDefinition
    {
        public string id;
        public string name;
        public string situation;
        public InvestigationResultDefinition good;
        public InvestigationResultDefinition normal;
        public InvestigationResultDefinition bad;
        /// <summary>이 보급품을 쓰면 좋음 확정 (예: 수상한 상자 — 열쇠). 비어 있으면 없음.</summary>
        public string supplyItem;
    }

    [Serializable]
    public sealed class InvestigationResultDefinition
    {
        public double chance;
        public string text;
        public ItemAmountDefinition[] rewards;
    }

    [Serializable]
    public sealed class ItemAmountDefinition
    {
        public string item;
        public int amount;
    }

    /// <summary>
    /// 탐험 맵 하나 (기획안 5장). 육각 좌표 노드 + 길. 길로 연결된 노드로만 이동한다.
    /// 튜토리얼처럼 고정 맵은 이벤트까지 여기에 정의하고, 얕은 숲은 구조만 정의한 뒤 이벤트를 배치한다(M2-4).
    /// </summary>
    [Serializable]
    public sealed class MapDefinition
    {
        public string id;
        public string name;
        /// <summary>탐험 구역 이름 (지도 창 제목: 얕은 숲 등). 비면 name을 쓴다.</summary>
        public string region;
        /// <summary>육각 격자 반경 (1depth 2 · 2depth 3).</summary>
        public int radius;
        public string startNode;
        public MapNodeDefinition[] nodes;
        public MapEdgeDefinition[] edges;
        /// <summary>true면 이 맵에서는 기습이 없다 (튜토리얼). 노드의 고정 기습(ambush)은 예외.</summary>
        public bool noAmbush;
        /// <summary>밤 랜덤 전투 편성 (가중치 로스터 편성 id). 비면 이 맵에는 밤 랜덤 전투가 없다 (튜토리얼).</summary>
        public string nightEncounter;
        /// <summary>true면 지각 판정을 하지 않고 forceDetect 노드만 탐지한다 (튜토리얼: 탐지 = 기습을 한 번만 보여 준다).</summary>
        public bool fixedDetection;
        /// <summary>이 맵의 기본 규칙 메모 (튜토리얼: 진형 붕괴 없음 등). 표시용.</summary>
        public string notes;
    }

    [Serializable]
    public sealed class MapNodeDefinition
    {
        public string id;
        public string name;
        public int q;
        public int r;
        /// <summary>노드 이벤트 (한 노드에 최대 2개 겹침: 전투+보물, 전투+채집 등). 비어 있으면 빈 노드.</summary>
        public NodeEventDefinition[] events;
        /// <summary>true면 처음 발견할 때 지각 판정 없이 항상 탐지 (튜토리얼: 기습 알려 주기).</summary>
        public bool forceDetect;
        /// <summary>
        /// 고정 기습: "party"(파티가 기습) · "enemy"(기습당함) · 비면 규칙대로.
        /// 맵의 noAmbush보다 우선한다 (첫 방문 전투에서만).
        /// </summary>
        public string ambush;
    }

    /// <summary>
    /// 노드 이벤트. kind:
    /// battle — encounter 편성 전투 / gather — item을 amount개 얻음 / investigate — 조사 물건(objectId) /
    /// treasure — 보물 / boss — 보스 전투(encounter).
    /// </summary>
    [Serializable]
    public sealed class NodeEventDefinition
    {
        public string kind;
        public string encounter;
        public string item;
        public int amount;
        public string objectId;
        /// <summary>조사 결과 고정 ("good" 등, 튜토리얼). 비어 있으면 굴림.</summary>
        public string fixedResult;
    }

    [Serializable]
    public sealed class MapEdgeDefinition
    {
        public string a;
        public string b;
    }

    /// <summary>부상 한 단계(경상/중상)의 효과.</summary>
    [Serializable]
    public sealed class InjuryStageDefinition
    {
        public string name;
        /// <summary>전투 내내 붙는 능력치 효과 (stat, duration 무시).</summary>
        public EffectDefinition[] effects;
        /// <summary>출혈: 전투마다 처음 bleedRounds 라운드 동안 차례 시작 시 피해.</summary>
        public int bleedPerTurn;
        public int bleedRounds;
    }

    /// <summary>부상 (경상 → 중상). 출정이 끝나도 남는다.</summary>
    [Serializable]
    public sealed class InjuryDefinition
    {
        public string id;
        public string name;
        public InjuryStageDefinition minor;
        public InjuryStageDefinition major;
        /// <summary>현장 치료(응급처치·치유의 빛·붕대)로 경상 제거 / 중상 → 경상.</summary>
        public bool fieldTreatable;
        /// <summary>하룻밤 자택 휴식으로 낫는다 (뇌진탕).</summary>
        public bool healedByRest;
        public string description;
    }

    /// <summary>기술에 붙은 부상 태그.</summary>
    [Serializable]
    public sealed class SkillInjuryDefinition
    {
        public string injuryId;
        /// <summary>명중 시 경상 확률 (치명타면 100%).</summary>
        public double chance;
    }

    /// <summary>id → 표시 이름 (몬스터 타입 등).</summary>
    [Serializable]
    public sealed class NameDefinition
    {
        public string id;
        public string name;
    }

    [Serializable]
    public sealed class ClassDefinition
    {
        public string id;
        public string name;
        public string role;
        public int strength;
        public int dexterity;
        public int vitality;
        public int intelligence;
        public int weaponMin;
        public int weaponMax;
        /// <summary>"strength" 또는 "intelligence". 공격력 보너스에 쓰는 능력치.</summary>
        public string attackStat;
        public double armor;
        public double formationResist;
        /// <summary>v0.1 고정 장착 기술 4개.</summary>
        public string[] skills;
    }

    [Serializable]
    public sealed class MonsterDefinition
    {
        public string id;
        public string name;
        public int hp;
        public int attackMin;
        public int attackMax;
        public int accuracy;
        public int dodge;
        public int speed;
        public double armor;
        public double crit;
        public double formationResist;
        public string[] types;
        /// <summary>차지하는 칸 수. 0이나 비어 있으면 1. (버그베어 2, 이후 4칸 몬스터도 가능)</summary>
        public int size;
        /// <summary>한 차례에 하는 행동 수. 0이나 비어 있으면 1. (버그베어 2: 느리지만 차례가 오면 두 번)</summary>
        public int actionsPerTurn;
        /// <summary>능력치 편차 배율. 0이면 1(기본), 보스는 0.5처럼 작게.</summary>
        public double varianceScale;
        public string[] skills;
        /// <summary>항상 켜져 있는 능력 (보스 등).</summary>
        public PassiveDefinition[] passives;
        /// <summary>의뢰 집계용 분류 (예: "goblin").</summary>
        public string family;
        /// <summary>상태이상 저항 (일반 10% · 정예 20% · 보스 40%).</summary>
        public ResistDefinition resists;
        /// <summary>마법 저항: 마법 피해를 방어도 대신 이 비율로 줄인다. 기본 0.</summary>
        public double magicResist;
        /// <summary>면역 상태이상 (DataKeys.Resist 값).</summary>
        public string[] immunities;
    }

    [Serializable]
    public sealed class StatWeightDefinition
    {
        public int strength;
        public int dexterity;
        public int intelligence;
        public int Sum => strength + dexterity + intelligence;
    }

    [Serializable]
    public sealed class ResistDefinition
    {
        public double stun;
        public double poison;
        public double bleed;
        public double burn;
        public double debuff;
    }

    /// <summary>
    /// 패시브. kind:
    /// allyAura — 보유자가 살아 있는 동안 같은 편 다른 전투원 모두에게 effects(stat) 적용.
    /// guardBackline — 보유자가 ranks 칸에 있는 동안 같은 편 protectedRanks 칸은 진형 붕괴(위치 이동)에 걸리지 않음.
    /// </summary>
    [Serializable]
    public sealed class PassiveDefinition
    {
        public string id;
        public string name;
        public string kind;
        public string description;
        public EffectDefinition[] effects;
        public int[] ranks;
        public int[] protectedRanks;
    }

    [Serializable]
    public sealed class SkillLevel
    {
        public int accuracy;
        public double damage;
        public double crit;
    }

    [Serializable]
    public sealed class EffectDefinition
    {
        /// <summary>"stat" | "poison"</summary>
        public string kind;
        /// <summary>stat 종류: damagePct · accuracy · crit · dodge · speed · damageTaken</summary>
        public string stat;
        public double value;
        public int duration;
        /// <summary>"target" | "self" | "allAllies"</summary>
        public string target;
        /// <summary>이 타입의 적을 상대할 때 효과 2배 (예: 축복 vs unholy).</summary>
        public string doubledVsType;
        /// <summary>poison·bleed·burn: 차례 시작 시 피해.</summary>
        public int damagePerTurn;
        /// <summary>적에게 거는 효과의 기본 확률. 실제 = chance − 대상 저항. 0이면 100%.</summary>
        public double chance;
        /// <summary>레벨이 levelStep만큼 오를 때마다 더하는 값 (stat 효과). 0이면 레벨과 무관.</summary>
        public double perLevel;
        /// <summary>perLevel을 더하는 레벨 간격. 1 = 매 레벨, 2 = Lv3·5·7… (0은 1로 본다).</summary>
        public int levelStep;

        /// <summary>시전자 레벨에서의 수치: value + perLevel × ⌊(레벨 − 1) ÷ levelStep⌋.</summary>
        public double ValueAt(int level)
        {
            if (perLevel == 0) return value;
            int step = levelStep > 0 ? levelStep : 1;
            return value + perLevel * ((Math.Max(1, level) - 1) / step);
        }
    }

    [Serializable]
    public sealed class HealDefinition
    {
        public int min;
        public int max;
        /// <summary>0이면 미사용. 회복량에 지능 ÷ 값을 더한다.</summary>
        public int intelligenceDivisor;
    }

    [Serializable]
    public sealed class FixedDamageDefinition
    {
        public int min;
        public int max;
        /// <summary>피해에 지능 × 값을 더한다.</summary>
        public int intelligenceMultiplier;
    }

    [Serializable]
    public sealed class DisplacementDefinition
    {
        /// <summary>양수 = 뒤로 밀기, 음수 = 앞으로 당기기 (칸 수).</summary>
        public int ranks;
        /// <summary>기본 확률. 실제 = chance − 대상 저항.</summary>
        public double chance;
    }

    [Serializable]
    public sealed class SummonDefinition
    {
        public string monsterId;
        public int count;
        /// <summary>시전자 혼자 남았을 때 소환 수.</summary>
        public int countWhenAlone;
        /// <summary>참이면 앞칸에 소환하고 기존 아군은 스스로 뒤로 물러난다 (진형 붕괴가 아니므로 저항·거구의 벽과 무관).</summary>
        public bool inFront;
        /// <summary>같은 몬스터가 이 수만큼 살아 있으면 소환하지 않는다 (0 = 제한 없음). 소환 수도 빈 자리만큼만.</summary>
        public int maxAlive;
    }

    [Serializable]
    public sealed class TypeBonusDefinition
    {
        public string type;
        public double damage;
    }

    [Serializable]
    public sealed class SkillDefinition
    {
        public string id;
        public string name;
        /// <summary>표시용 종류 (근접 · 원거리 · 회복 · 버프 · 소환 …).</summary>
        public string kind;
        /// <summary>사용 가능한 자기 칸 (1~4).</summary>
        public int[] useRanks;
        /// <summary>"enemy" | "ally" | "self" | "allAllies" | "allySlot"(소환)</summary>
        public string target;
        /// <summary>target = enemy일 때 노릴 수 있는 적 칸 (1~4).</summary>
        public int[] targetRanks;
        /// <summary>true면 targetRanks의 적 전원에게 각각 판정.</summary>
        public bool multi;
        public bool damaging;
        public bool ignoreArmor;
        /// <summary>명중 시 가방에서 아이템 1개를 훔친다 (소매치기 낚아채기). 한 마리당 1회.</summary>
        public bool steal;
        /// <summary>"physical"(비어 있으면) | "magical" — 마법 피해는 마법 저항으로 줄어든다.</summary>
        public string damageType;
        /// <summary>능력치 가중치(합 3). 피해·회복 보너스 = 내림((근력×a + 민첩×b + 지능×c) ÷ statWeightDivisor).</summary>
        public StatWeightDefinition weights;
        /// <summary>피해 편차 (정밀 0.1 · 보통 0.25 · 거침 0.5). 0이면 공격력 범위를 그대로 굴린다(몬스터 기본).
        /// 범위 = 중앙값 × (1 ± 편차), 중앙값 = 무기 평균 + 능력치 보너스. 지각 안정성이 하한을 끌어올린다.</summary>
        public double variance;
        /// <summary>레벨 1~5. 비어 있으면 0 보정.</summary>
        public SkillLevel[] levels;
        public FixedDamageDefinition fixedDamage;
        public TypeBonusDefinition[] typeBonuses;
        public EffectDefinition[] effects;
        public HealDefinition heal;
        public DisplacementDefinition displacement;
        public SummonDefinition summon;
        public bool curePoison;
        /// <summary>사용 후 이 라운드 수 동안 사용 불가.</summary>
        public int cooldown;
        /// <summary>적 AI가 이 강화 기술을 쓸 확률. 0이면 balance.aiBuffChance.</summary>
        public double aiUseChance;
        /// <summary>부상 태그 (없으면 null 또는 injuryId 빈 값).</summary>
        public SkillInjuryDefinition injury;
        /// <summary>현장 치료: 대상의 fieldTreatable 부상을 치료 (경상 제거 / 중상 → 경상).</summary>
        public bool treatInjury;
        public string description;
    }

    [Serializable]
    public sealed class EncounterSlotDefinition
    {
        public string[] candidates;
        /// <summary>1이면 항상, 0.5면 50%로 등장.</summary>
        public double chance;
    }

    [Serializable]
    public sealed class EncounterDefinition
    {
        public string id;
        public string name;
        public EncounterSlotDefinition[] slots;
        /// <summary>비어 있지 않으면 이 중 하나의 편성 ID를 무작위로 골라 그것을 쓴다 (예: 얕은 숲 보스 2종, 일반 방 로스터).</summary>
        public string[] variants;
        /// <summary>variants 가중치 (같은 길이). 비어 있으면 균등.</summary>
        public int[] variantWeights;
    }

    [Serializable]
    public sealed class ItemDefinition
    {
        public string id;
        public string name;
        public int heal;
        public bool curePoison;
        /// <summary>현장 치료 (붕대).</summary>
        public bool treatInjury;
        /// <summary>출혈 상태 해제 (붕대).</summary>
        public bool cureBleed;
        public int stack;
        public int price;
        /// <summary>횃불: 전투 밖에서 쓰면 켜지는 시간(분). 남은 시간에 더해진다(최대 torchMaxMinutes).</summary>
        public int torchMinutes;
    }
}
