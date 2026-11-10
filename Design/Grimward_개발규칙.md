# Grimward — 개발 규칙 (Codex 공통)

> 작성일: 2026-11-09 · M1·M2 작업지시서의 구조 규칙을 옮겨 정리한 **상시 규칙**이다.
> 마일스톤별 범위·수치·테스트는 각 작업지시서(현재 `Grimward_Codex_작업지시서_M3.md`)에 둔다.
> 게임 규칙은 `Grimward_통합기획.md`(「플레이테스트 결정」 → 「최신 결정」 → Part I 순).

---

## 1. 문서 우선순위

| 순위 | 문서 | 용도 |
|---|---|---|
| 1 | `Grimward_통합기획.md` 「플레이테스트 결정」 | 가장 최근 확정 규칙 |
| 2 | `Grimward_통합기획.md` 「최신 결정」·Part I | v0.1 구현 기준 |
| 3 | 현재 작업지시서 | 이번 범위, 기본값, 테스트, 완료 조건 |
| 4 | 이 문서 | 코드 구조와 작업 방식 |
| 참고 | `Grimward_게임설계.xlsx` | 수치 (`10_밸런스상수`, `22_기술` 등) |

- Part II(과거 논의 기록)는 규칙으로 쓰지 않는다.
- 문서끼리 다르면 위 순위를 따르고, 차이를 보고서에 적는다.
- **[미정]** 항목은 임의로 확정하지 않는다. 기본값이 필요하면 `BalanceConfig`에 옵션으로 두고 보고서에 적는다.

---

## 2. 프로젝트와 작업 환경

- Unity **6000.6.0f1**, Universal 2D 템플릿, C#. 프로젝트 `C:\UnityProject\Grimward` (git).
- 기획 문서는 루트의 `Design/` (Assets 밖이라 임포트되지 않음). **Codex는 `Design/`을 수정하지 않는다.**
- Codex는 `Assets/` 아래만 작성·수정한다. `ProjectSettings/`, `Packages/manifest.json`은 건드리지 않는다. 필요한 패키지는 보고서에 요청한다.
- 템플릿 기본 파일(`Assets/Welcome`, `Assets/Scenes/SampleScene.unity`, `Assets/Settings`)은 지우지 않는다. 게임 씬은 `Assets/_Project/Scenes/Main.unity`.
- Input System 패키지 사용: uGUI EventSystem은 `InputSystemUIInputModule`.
- 시작 시 `Application.targetFrameRate = 60`.
- 네임스페이스 루트 `Grimward`. 식별자는 영어, 주석·UI 텍스트는 한국어 가능.

---

## 3. 폴더와 어셈블리

```
Assets/
  _Project/
    Scripts/
      Domain/            Grimward.Domain.asmdef   (noEngineReferences: true)
        Core/            GameTime, IRandomSource, SeededRandom
        Config/          BalanceConfig (순수 C# 데이터), DamageRounding
        Map/             HexCoord, NodeData, EdgeData, MapData, IMapProvider, 맵 제공자, MapValidator
        Exploration/     ExplorationState, ITravelTimeCalculator, TravelTimeCalculator, FogOfWar, ReturnPathFinder ...
        Characters/      CharacterStats, Adventurer, StatFormulas
        Combat/          CombatFormulas, Combatant, BattleState, TurnOrder, BattleResult, EnemyFactory, BattleFactory, (기술·배치)
      Presentation/      Grimward.Presentation.asmdef  (Domain 참조)
        Bootstrap/       GameBootstrap (구성 루트, MonoBehaviour)
        Config/          BalanceConfigAsset (ScriptableObject → BalanceConfig)
        Map/             HexMapTheme, HexMapView, HexTileView (육각 타일 지도)
        UI/              ExplorationScreen, BattleView, LogView, UiFactory
    Editor/              Grimward.Editor.asmdef (HexTileSetup 메뉴, 검증 도구)
    Art/Hex/             임시 육각 타일 스프라이트
    Prefabs/             HexTile.prefab
    Data/                BalanceConfig.asset, HexMapTheme.asset
    Scenes/              Main.unity
  Tests/
    EditMode/            Grimward.Domain.Tests.asmdef (Domain 참조, EditMode 전용)
```

- **Domain은 UnityEngine을 참조하지 않는다.** 게임 규칙은 전부 Domain에 있다.
- **Presentation은 표시와 입력만** 담당한다. 규칙 계산을 하지 않고 Domain을 호출한다.
- 의존 방향: Presentation → Domain. 반대 참조 금지.
- 전투 계산(명중·피해·턴 순서·기술)은 **Combat 폴더에만** 둔다. `ExplorationState`는 전투 시작·종료 연결만 하고 커지지 않게 한다.

---

## 4. SOLID 적용 방식

| 원칙 | 이 프로젝트에서의 규칙 |
|---|---|
| 단일 책임 | 클래스 하나는 한 가지 규칙만. 이동 시간, 안개, 귀환 경로, 조우 판정을 한 클래스에 섞지 않는다 |
| 개방-폐쇄 | 아래 인터페이스 3곳만 확장 지점으로 연다 |
| 리스코프 치환 | 모든 `IMapProvider` 구현은 같은 규칙을 지키는 `MapData`를 반환한다 → `MapValidator`로 검증 |
| 인터페이스 분리 | 인터페이스는 메서드 1~3개 |
| 의존성 역전 | Domain 클래스는 생성자로 의존성을 받는다. 조립은 `GameBootstrap` 한 곳에서만 |

**인터페이스는 이 세 곳(+난수)에만 둔다**

| 인터페이스 | 이유 |
|---|---|
| `IRandomSource` | 시드 지정, 테스트 재현. `NextDouble()`, `NextInt(min, max)` (양끝 포함) |
| `IMapProvider` | 고정 맵 → 구조 선택형 맵 → 생성 맵을 같은 자리에 교체 |
| `ITravelTimeCalculator` | v0.2에 지형별 이동속도를 붙일 자리 |

- 위 목록 외에는 **구현이 하나뿐인 인터페이스를 만들지 않는다.** 상태 없는 공식은 static 순수 함수로 둬도 된다.

---

## 5. 금지 사항

- 규칙 코드에 숫자를 직접 쓰지 않는다. 모든 수치는 `BalanceConfig`에서 읽는다. 새 필드를 추가할 때 기존 `BalanceConfig.asset` 값이 사라지지 않게 한다.
- `UnityEngine.Random` 금지. 모든 난수는 `IRandomSource` 하나에서. 화면 장식용 난수(타일 장식 등)는 게임 난수를 소비하지 않도록 좌표 해시로 만든다.
- 게임 상태를 담는 static 싱글턴 금지.
- Domain에서 `MonoBehaviour`, `ScriptableObject`, `Debug.Log` 금지. 로그는 이벤트나 반환값으로 Presentation에 넘긴다.
- Domain에 "시간 경과에 따른 연출"을 넣지 않는다. 연출(아이콘 이동 등)은 Presentation이 Domain의 단계 API를 호출하며 만든다.

---

## 6. 핵심 데이터

- **HexCoord:** 축 좌표(q, r), pointy-top. 이웃 6방향, 거리, 반경 N 범위 열거.
- **NodeData:** `Id`, `Coord`, `EventType`(Start / Battle / Treasure / Empty), `IsDiscovered`, `IsVisited`, `IsEventCompleted`, 연결 경로 ID. 발견·방문과 완료는 별도 값.
- **EdgeData:** `Id`, 양 끝 노드 ID, `IsDiscovered`, `IsTraversed`. 이동 시간은 저장하지 않고 `ITravelTimeCalculator`가 계산. 경로에는 이벤트가 없다(플레이테스트 P7).
- **MapData:** 반경, 노드, 경로, 시작 노드 ID. 모든 맵 제공자가 같은 타입을 반환.
- **GameTime:** `Day`(1부터), `MinuteOfDay`(0~1439). 분 더하기, 날짜 넘김, 낮(06:00~19:59)/밤 판정, "N일차 HH:MM" 표시.

---

## 7. 화면 규칙

- UI는 코드 생성(`UiFactory`) 방식을 유지한다. 프리팹 UI 전환은 그래픽 단계에서 따로 한다.
- 지도는 육각 타일 프리팹(`HexTile`)을 월드에 배치하는 `HexMapView`가 그린다. 테마·프리팹 연결은 `HexMapTheme.asset`(메뉴 `Grimward/Art/4`).
- **오브젝트 이름 규칙 (AltTester 자동화용):** `Btn_`, `Txt_`, `Panel_` 접두어. 예: `Btn_AutoReturn`, `Btn_Node_03`, `Btn_Skill_0`, `Btn_Target_01`, `Txt_Time`, `Panel_Battle`. UI가 아닌 오브젝트(카메라·부트스트랩·EventSystem)에는 접두어를 쓰지 않는다.
- 지도 노드 클릭은 `HexTileView`(PolygonCollider2D + Physics2DRaycaster)로 받는다. 자동 검증 스크립트도 이 경로를 사용한다.

---

## 8. 시드와 재현성

- `GameBootstrap`에서 시드로 `SeededRandom`을 만든다 (Inspector, 기본 12345). 모든 판정(맵 구조 선택·이벤트 배치·전투·조우·드랍)이 이 객체에서 나온다.
- 같은 버전·시드·행동 순서 → 같은 결과. 결정성 테스트를 마일스톤마다 유지한다.
- 테스트는 고정 값을 돌려주는 가짜 난수를 주입한다.

---

## 9. 테스트

- EditMode, Domain만. 기존 테스트를 유지하고 마일스톤마다 추가한다. 규칙이 바뀌어 기존 테스트가 틀려지면 **고친 이유를 보고서에 적는다.**
- 완료 조건 공통: 모든 EditMode 테스트 통과, Domain에 UnityEngine 참조 없음.

---

## 10. 커밋

- 마일스톤 단위로 커밋한다. 제목은 영어, 본문은 한국어 가능.
- 그래픽·도구 작업처럼 마일스톤과 무관한 변경은 따로 커밋한다.

---

## 11. 보고서 양식 (작업 후 응답 본문에 작성)

1. 추가·변경·삭제한 파일 목록과 역할 (한 줄씩)
2. 실행 방법과 확인 순서 (씬, Inspector 설정, 메뉴)
3. 테스트 결과 (전체 개수, 통과 여부, 수정한 기존 테스트와 이유)
4. [미정] 항목에 적용한 기본값과 위치
5. 임의로 정한 것과 그 이유
6. 기획서와 다르게 구현한 곳, 모호했던 규칙
7. 다음 마일스톤을 위해 열어 둔 확장 지점
