# Grimward — 개발 규칙

> 작성일: 2026-11-10 (재시작판) · 사람과 AI 코딩 에이전트(Codex 등)가 함께 따르는 상시 규칙
> 무엇을 만들지는 `Grimward_확정기획.md`, 수치·목록은 `Grimward_게임설계.xlsx`, 지금 할 일은 `Grimward_개발계획.md`의 현재 마일스톤을 본다.

---

## 1. 문서 우선순위

| 순위 | 문서 | 용도 |
|---|---|---|
| 1 | `Design/Grimward_확정기획.md` | 게임 규칙의 기준 |
| 2 | `Design/Grimward_게임설계.xlsx` | 수치·목록(기술, 몬스터, 조사 물건, 맵 좌표, 특성 등) |
| 3 | `Design/Grimward_개발계획.md` | 마일스톤별 범위·완료 조건 |
| 4 | 이 문서 | 코드 구조와 작업 방식 |
| 참고 | `Design/Grimward_통합기획.md` | 논의 기록. 규칙으로 쓰지 않는다 |

- 문서끼리 다르면 위 순위를 따르고, 차이를 작업 보고서에 적는다.
- **[임시]** 수치는 그대로 쓰되 데이터로 빼서 바꾸기 쉽게 한다. **[미정]** 항목은 임의로 확정하지 않고 기본값을 데이터에 두고 보고서에 적는다.

---

## 2. 프로젝트와 재시작

- Unity **6000.6.0f1**, Universal 2D(URP), Input System, uGUI + TextMeshPro, C#. 프로젝트 `C:\UnityProject\Grimward` (git).
- **2026-11-10 재시작**: 이전 코드(M1~M2)는 git 태그 `archive/pre-restart`로 보관하고 `Assets/_Project`를 새로 구성한다. 이전 코드는 필요할 때 git에서 꺼내 새 규칙에 맞게 다듬어 쓴다.
  - 다시 쓸 만한 것: HexCoord, GameTime, SeededRandom, MapData·NodeData·EdgeData, MapValidator, 육각 타일 프리팹·HexMapView, 임시 타일 아트
  - 새로 쓰는 것: 전투 전체, 탐험 상태, 설정 데이터, 테스트
- 템플릿 기본 파일(`Assets/Welcome`, `Assets/Scenes/SampleScene.unity`, `Assets/Settings`)은 지우지 않는다.
- AI 에이전트는 `Assets/` 아래만 작성·수정한다. `ProjectSettings/`, `Packages/`, `Design/`은 수정하지 않는다(필요하면 보고서에 요청).
- 식별자는 영어, 주석과 UI 텍스트는 한국어 가능. 네임스페이스 루트 `Grimward`.
- 시작 시 `Application.targetFrameRate = 60`.

---

## 3. 폴더와 어셈블리

```
Assets/_Project/
  Scripts/
    Domain/                Grimward.Domain.asmdef (noEngineReferences: true)
      Core/                GameTime, IRandomSource, SeededRandom, 공용 유틸
      Data/                정의 데이터(직업·기술·몬스터·조사 물건·특성·의뢰·보급품)의 순수 C# 형태, BalanceConfig
      Units/               Adventurer, StatFormulas, Traits, Roster
      Combat/              BattleState, Formation, TurnOrder, SkillResolver, CombatFormulas, StatusEffects, EnemyAI
      Map/                 HexCoord, MapData, NodeData, EdgeData, MapLayouts, EventPlacer, MapValidator
      Expedition/          ExpeditionState, FogOfWar, TravelTime, ReturnPathFinder, CurioResolver, Inventory
      Town/                GuildState, QuestBoard, Recruitment, Shop, Settlement, Rest
      Raid/                RaidSchedule, DefenseBattle
      Save/                SaveData, ISaveStore, SaveService
    Presentation/          Grimward.Presentation.asmdef (Domain 참조)
      Bootstrap/           GameBootstrap (구성 루트)
      Data/                gamedata.json 로드 → GameDatabase
      Town/ Expedition/ Battle/ Common/   화면·패널·입력
    Editor/                Grimward.Editor.asmdef (씬 생성·데이터 검증 메뉴)
  Data/Resources/GameData/ gamedata.json
  Prefabs/  Art/  Scenes/
Assets/Tests/EditMode/     Grimward.Domain.Tests.asmdef
```

- **Domain은 UnityEngine을 참조하지 않는다.** 게임 규칙은 전부 Domain에 있다.
- **Presentation은 표시와 입력만.** 규칙 계산을 하지 않고 Domain을 호출한다.
- 의존 방향: Presentation → Domain. 반대 금지.
- 폴더 구조는 마일스톤마다 필요한 만큼만 만든다(빈 폴더를 미리 만들지 않는다).

---

## 4. 데이터 주도

- **직업, 기술, 몬스터, 조사 물건, 특성, 의뢰, 보급품, 맵 구조는 데이터다.** 코드에 이름·수치를 쓰지 않는다.
- 원본은 엑셀(`Grimward_게임설계.xlsx`). Unity에서는 `Assets/_Project/Data/Resources/GameData/gamedata.json` 하나로 두고, Presentation(`GameDataLoader`)이 `JsonUtility`로 읽어 Domain 정의(`GameDatabase`)로 넘긴다. 정의 클래스는 Domain의 `[Serializable]` 순수 C# 클래스이며 필드 이름 = JSON 키.
- 엑셀 → JSON 내보내기는 지금은 스크립트(`Claude outputs/M0_M1/Tools/gen_gamedata.py`)로 하고, 이후 에디터 도구로 옮긴다. JSON을 고친 뒤에는 메뉴 `Grimward/게임 데이터 검증`을 돌린다.
- 기술은 「효과 조합」으로 정의한다: 피해·다중 대상·버프·디버프·중독·회복·위치 이동·진형 붕괴·방어도 무시 등. 새 기술이 새 코드를 요구하지 않게 한다. v0.1은 20개 기술이 쓰는 효과만 구현한다.
- 상태 효과(버프·디버프·중독)는 하나의 구조: 전투원에게 붙는 상태 + 남은 라운드 + 수치 보정.
- [임시] 수치(가격·보상·확률)는 `BalanceConfig` 또는 데이터 에셋에 둔다.

---

## 5. SOLID 적용

| 원칙 | 규칙 |
|---|---|
| 단일 책임 | 클래스 하나는 규칙 하나. 예: 행동 순서, 명중 계산, 진형 재정렬, 안개, 귀환 경로를 섞지 않는다 |
| 개방-폐쇄 | 새 기술·몬스터·조사 물건은 데이터 추가로 끝나야 한다 |
| 리스코프 | 모든 맵 제공자는 같은 규칙을 지키는 MapData를 반환 → MapValidator로 검증 |
| 인터페이스 분리 | 인터페이스는 메서드 1~3개 |
| 의존성 역전 | Domain 클래스는 생성자로 의존성을 받는다. 조립은 GameBootstrap 한 곳 |

**인터페이스는 이 네 곳에만**

| 인터페이스 | 이유 |
|---|---|
| `IRandomSource` | 시드 난수, 테스트 재현 (`NextDouble`, `NextInt(min, max)` 양끝 포함) |
| `IMapProvider` | 튜토리얼 고정 맵 / 얕은 숲 구조 3종 / 이후 생성 맵 교체 |
| `ITravelTimeCalculator` | 이후 지형별 이동 시간 |
| `ISaveStore` | 로컬 파일 저장 / 테스트용 메모리 저장 |

- 위 목록 외에 구현이 하나뿐인 인터페이스를 만들지 않는다. 상태 없는 공식은 static 순수 함수로 둬도 된다.

---

## 6. 금지 사항

- 규칙 코드에 숫자·이름을 직접 쓰지 않는다.
- `UnityEngine.Random` 금지. 모든 게임 난수는 `IRandomSource` 하나에서. 화면 장식용 난수는 좌표 해시 등으로 만들어 게임 난수를 소비하지 않는다.
- 게임 상태를 담는 static 싱글턴 금지.
- Domain에서 `MonoBehaviour`, `ScriptableObject`, `Debug.Log` 금지. 로그·이벤트는 반환값이나 C# 이벤트로 넘긴다.
- Domain에 시간 경과 연출을 넣지 않는다. 연출(아이콘 이동, 공격 모션)은 Presentation이 Domain의 단계 API를 호출하며 만든다.

---

## 7. 핵심 규칙 구현 메모 (확정기획 요약)

- **시간**: GameTime(일차, 분). 낮 06:00~19:59. 휴식 = 다음 날 06:00. 하루 1회 출정 플래그.
- **전투 순서**: 라운드마다 속도 + 1d8. 동률은 기본 속도, 그다음 무작위.
- **행동**: 기술 / 이동 / 넘기기 / 도주(행동 소모), 아이템(턴 소모 없음, 턴당 1개).
- **진형**: 양쪽 4칸, 1번부터 채움. 사망 시 자동 당김. 위치 이동은 목적지 전투원과 교환. 재정렬은 행동이 끝난 뒤 한 번.
- **광역 대상**: 공격 시작 시 위치로 고정, 중간에 죽은 대상은 건너뜀.
- **상태 효과**: 걸린 즉시 적용, 받은 전투원의 차례 끝에 −1, 전투 종료 시 해제. 중독은 차례 시작 시 피해.
- **피해**: decimal로 계산 후 소수점 버림(부동소수 오차로 한 단계 더 버려지지 않게).
- **탐험**: 이동 중 사건 없음. 노드 도착 시 이벤트 처리, 밤 + 완료 노드 도착 시 랜덤 전투 판정. 자동 귀환은 한 칸씩 진행하는 API.
- **저장**: 로컬 JSON(`Application.persistentDataPath`). 마을 행동마다 자동 저장. 출정 시작 직전 상태를 저장해 두고, 던전에서 강제 종료하면 그 상태로 복원. 앱 일시정지(백그라운드)는 종료로 보지 않는다.

---

## 8. 화면 규칙

- uGUI + TextMeshPro. 마을은 Scene 하나에 Panel 전환. Scene 구성은 `Title` · `Town` · `Expedition`(지도 + 전투 패널) 기준으로 시작.
- Canvas Scaler: Scale With Screen Size, 1920×1080, Match 0.5에서 시작. 16:9 · 19.5:9 · 20:9 확인. 안전 영역(노치) 대응.
- 터치 영역은 넉넉하게(약 48dp 이상). 마우스 호버에만 의존하는 기능 금지.
- **이름 규칙** (AltTester 자동화용): `Panel_`, `Btn_`, `Txt_` 접두어. 예: `Panel_TownMain`, `Btn_Expedition`, `Txt_RaidCountdown`. UI가 아닌 오브젝트에는 쓰지 않는다.
- 반복 팝업(확인, 캐릭터 상세, 의뢰 상세)은 공통 프리팹.
- 아트는 임시 이미지. 스타일 통일은 나중에.

---

## 9. 시드와 재현성

- GameBootstrap에서 시드로 SeededRandom 생성(Inspector, 기본 12345). 맵 구조 선택·이벤트 배치·전투·조우·조사 결과·드랍·고용 후보 모두 이 객체에서.
- 같은 버전·시드·행동 순서 → 같은 결과. 결정성 테스트를 유지한다.
- 저장 데이터에 난수 상태(또는 시드와 호출 수)를 함께 저장한다.

---

## 10. 테스트

- EditMode, Domain 대상. 마일스톤마다 완료 조건의 규칙을 테스트로 남긴다.
- 테스트는 고정 값을 돌려주는 가짜 난수와 메모리 저장소를 쓴다.
- 공통 완료 조건: EditMode 테스트 전부 통과, Domain에 UnityEngine 참조 없음, 콘솔 오류 없음.

---

## 11. 커밋

- 마일스톤 단위로 커밋하고 태그(`v0.1-m1` 등)를 단다. 제목은 영어, 본문은 한국어 가능.
- 아트·도구 작업은 기능 커밋과 분리한다.

---

## 12. 보고서 양식 (작업 후 응답 본문에)

1. 추가·변경·삭제한 파일과 역할
2. 실행·확인 방법 (씬, 메뉴, Inspector)
3. 테스트 결과 (개수, 통과 여부, 바꾼 테스트와 이유)
4. [미정]·[임시] 항목에 쓴 기본값과 위치
5. 임의로 정한 것과 이유
6. 기획과 다르게 구현한 곳, 모호했던 규칙
7. 다음 마일스톤을 위해 열어 둔 확장 지점
