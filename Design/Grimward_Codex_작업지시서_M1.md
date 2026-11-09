# Grimward — Codex 작업 지시서 (M1)

> 작성일: 2026-11-09
> 대상: Codex (코드 작성 담당)
> 이번 작업 범위: **M1 — 고정 맵 + 이동 + 전장의 안개 + 시간 + 자동 귀환 경로**
> M2 이후는 참고용이며 **이번에 구현하지 않는다.**

---

## 0. 함께 받는 자료

| 자료 | 사용 범위 |
|---|---|
| `Grimward_통합기획.md` | **「최신 결정 및 구현 순서」와 Part I만** 규칙으로 사용한다. Part II는 과거 논의 기록이므로 **읽지 않는다.** |
| `Grimward_게임설계.xlsx` | 수치 참고: `10_밸런스상수`, `18_맵_이벤트`, `06_탐험_시간`. M1에 필요한 값은 이 문서 4장에 모아 두었다. |

규칙이 이 지시서와 통합기획서 사이에서 다르면 **통합기획서(최신 결정 → Part I)** 를 따르고, 차이를 보고서에 적는다.

---

## 1. 프로젝트와 작업 환경

- Unity **6000.6.0f1**, **Universal 2D 템플릿**. 프로젝트 `C:\UnityProject\Grimward`는 이미 생성되어 있고 git 저장소가 초기화되어 있다.
- 기획 문서는 프로젝트 루트의 `Design/` 폴더에 있다 (Assets 밖이라 Unity가 임포트하지 않음).
- 템플릿에 **Input System 패키지**가 들어 있다. uGUI의 EventSystem은 `InputSystemUIInputModule`을 사용한다.
- 템플릿 기본 파일(`Assets/Welcome`, `Assets/Scenes/SampleScene.unity`, `Assets/Settings`)은 지우지 않는다. 새 씬은 `Assets/_Project/Scenes/Main.unity`에 만든다.
- 네임스페이스 루트는 `Grimward` (예: `Grimward.Domain.Map`).
- Codex는 `Assets/` 아래만 작성·수정한다. `ProjectSettings/`, `Packages/manifest.json`은 건드리지 않는다.
  - 필요한 패키지가 있으면 직접 추가하지 말고 보고서에 적는다. (기본 포함: Unity Test Framework, uGUI, TextMeshPro)
- 언어: C#. 주석과 UI 텍스트는 한국어 가능, 식별자(클래스·변수명)는 영어.
- 프레임: 시작 시 `Application.targetFrameRate = 60` (프로젝트 기본 규칙).

---

## 2. 구조 규칙 (SOLID를 이 프로젝트에 적용한 형태)

### 2-1. 폴더와 어셈블리

```
Assets/
  _Project/
    Scripts/
      Domain/            Grimward.Domain.asmdef   (noEngineReferences: true)
        Core/            GameTime, IRandomSource, SeededRandom ...
        Map/             HexCoord, NodeData, EdgeData, MapData, IMapProvider, FixedMapProvider, MapValidator ...
        Exploration/     ExplorationState, ITravelTimeCalculator, TravelTimeCalculator, FogOfWar, ReturnPathFinder ...
        Config/          BalanceConfig (순수 C# 데이터 클래스)
      Presentation/      Grimward.Presentation.asmdef  (Domain 참조)
        Bootstrap/       GameBootstrap (구성 루트, MonoBehaviour)
        Config/          BalanceConfigAsset (ScriptableObject → BalanceConfig 변환)
        UI/              ExplorationScreen, MapView, LogView ...
    Data/                BalanceConfig.asset
    Scenes/              Main.unity
  Tests/
    EditMode/            Grimward.Domain.Tests.asmdef  (Domain 참조, EditMode 전용)
```

- **Domain 어셈블리는 UnityEngine을 참조하지 않는다** (`noEngineReferences: true`). 게임 규칙은 전부 여기 있다.
- **Presentation은 화면 표시와 입력만** 담당한다. 규칙 계산을 하지 않고 Domain을 호출한다.
- 의존 방향: Presentation → Domain. 반대 방향 참조 금지.

### 2-2. 원칙을 적용하는 방식

| 원칙 | 이 프로젝트에서의 규칙 |
|---|---|
| 단일 책임 | 클래스 하나는 한 가지 규칙만. 예: 이동 시간 계산, 안개 공개, 귀환 경로 계산을 한 클래스에 섞지 않는다 |
| 개방-폐쇄 | 아래 2-3의 "확장 지점"만 인터페이스로 열어 둔다 |
| 리스코프 치환 | `IMapProvider` 구현(고정 맵/생성 맵)은 같은 규칙을 지키는 `MapData`를 반환해야 한다 → `MapValidator`로 검증 |
| 인터페이스 분리 | 인터페이스는 작게. 메서드 1~3개 |
| 의존성 역전 | Domain 클래스는 구체 클래스 대신 인터페이스를 생성자로 받는다. 조립은 `GameBootstrap` 한 곳에서만 |

### 2-3. 인터페이스는 이 세 곳(+난수)에만

| 인터페이스 | 이유 | M1 구현 |
|---|---|---|
| `IRandomSource` | 시드 지정, 테스트 재현 | `SeededRandom` (`System.Random` 래핑, 시드 생성자) |
| `IMapProvider` | 1단계 고정 맵 → 2단계 생성 맵을 같은 자리에 교체 | `FixedMapProvider` |
| `ITravelTimeCalculator` | v0.2에 지형별 이동속도를 붙일 자리 | `TravelTimeCalculator` (처음 60분 / 재방문 30분) |

- 위 목록 외에는 **구현이 하나뿐인 인터페이스를 만들지 않는다.** (과설계 방지)

### 2-4. 금지 사항

- 규칙 코드 안에 숫자를 직접 쓰지 않는다. 모든 수치는 `BalanceConfig`에서 읽는다.
- `UnityEngine.Random` 사용 금지. 모든 난수는 `IRandomSource` 하나에서.
- 게임 상태를 담는 static 싱글턴 금지.
- Domain에서 `MonoBehaviour`, `ScriptableObject`, `Debug.Log` 사용 금지. 로그가 필요하면 이벤트나 반환값으로 Presentation에 넘긴다.
- 통합기획서의 **[미정]** 항목을 임의로 확정하지 않는다. 기본값이 필요하면 `BalanceConfig`에 두고 보고서에 적는다.

---

## 3. 핵심 데이터 (통합기획 Part I 8장 기준)

- **HexCoord:** 축 좌표(q, r). 이웃 6방향, 원점으로부터의 거리, 반경 N 범위 열거.
- **NodeData:** `Id`, `Coord`, `EventType`(Start / Battle / Treasure / Empty), `IsDiscovered`, `IsVisited`, `IsEventCompleted`, 연결된 경로 ID 목록.
  - 발견·방문과 이벤트 완료는 **별도 값**으로 관리한다.
- **EdgeData:** `Id`, 양 끝 노드 ID, `IsDiscovered`, `IsTraversed`. 이동 시간은 저장하지 않고 `ITravelTimeCalculator`가 계산한다.
- **MapData:** 반경, 노드 목록, 경로 목록, 시작 노드 ID. 고정 맵과 생성 맵이 **같은 타입**을 반환한다.
- **GameTime:** `Day`(1부터), `MinuteOfDay`(0~1439). 분 더하기, 날짜 넘김, 낮/밤 판정, 표시 문자열(예: "1일차 06:00").

---

## 4. M1 구현 범위

### 4-1. 고정 맵 (`FixedMapProvider`)

| 항목 | 값 |
|---|---|
| 반경 | 2 (19칸) |
| 활성 노드 | 11개 |
| 구성 | 시작 1 · 전투 6 · 보물 2 · 빈 노드 2 |
| 시작 노드 | 맵 가장자리(원점 거리 2) |
| 연결 | 인접한 활성 노드 사이에만 경로. 모든 활성 노드가 시작점에서 도달 가능 |
| 순환 경로 | 1~2개 |
| 최대 경로 거리 | 시작점에서 가장 먼 활성 노드까지 5칸 이내 |

- 배치와 연결은 Codex가 위 조건을 만족하게 직접 정한다. 코드 안의 표(배열)로 정의해도 된다.
- **`MapValidator`** 를 만들어 위 조건을 검사한다. 2단계 생성기도 같은 검증기를 쓴다.

### 4-2. 이동

- 현재 노드에서 **경로로 연결된 노드**로만 이동한다.
- 이동 시간: 경로를 처음 지나면 60분, 이미 지나간 경로면 30분 (`IsTraversed` 기준, **경로 단위** 판정).
- 이동이 끝나면 경로 `IsTraversed = true`, 도착 노드 `IsVisited = true`.
- 도착 노드 처리 (M1):
  - 빈 노드·시작 노드 → 이벤트 완료.
  - 전투·보물 → **방문(미완료)으로 두고** 로그에 "전투 발생 — M2에서 구현", "보물 발견 — M3에서 구현" 출력. 떠날 수 있다.

### 4-3. 경로 이벤트 판정 (발생 여부만)

- 이동 1회당 1회 판정. 확률은 낮 처음 25% / 낮 재방문 5% / 밤 처음 40% / 밤 재방문 25%.
- 낮/밤은 **출발 시각** 기준으로 판정한다. (보고서에 명시)
- M1은 발생 여부를 로그로만 출력한다 ("매복 발생 — M2에서 구현"). 이동은 그대로 완료한다.

### 4-4. 전장의 안개

- 노드를 방문하면 그 노드와 **경로로 연결된 노드와 그 경로**가 발견된다.
- 발견된 노드의 이벤트 종류는 `?`로 표시, 방문하면 공개.
- 미발견 노드·경로는 화면에 표시하지 않는다.
- 게임 시작 시 시작 노드는 방문·완료 상태이고, 연결된 노드가 발견된 상태다.

### 4-5. 시간

- 시작: 1일차 06:00.
- 낮 06:00~19:59, 밤 20:00~05:59. 24:00이 지나면 다음 일차.
- 화면 상단에 "N일차 HH:MM (낮/밤)" 표시.

### 4-6. 자동 귀환 경로

- **완료한 노드만** 지나는 최단 경로(칸 수 기준 BFS)를 현재 위치에서 시작 노드까지 계산한다.
  - 미완료 보물 노드 통과 예외는 **[미정]** → `BalanceConfig`에 옵션(`AllowIncompleteSafeNodesOnReturn`, 기본값 false)으로 두고 보고서에 적는다.
- 경로를 따라 실제로 이동한다 (각 구간은 재방문 30분, 경로 이벤트 판정 포함 — M1은 로그만).
- 시작 노드에 도착하면 로그 "마을로 귀환 — M4에서 구현".
- 시작 노드에 있을 때는 **수동 귀환** 버튼이 보인다 (M1은 로그만).

### 4-7. 화면 (최소)

- 한 화면: 시각 표시, 지도(발견된 노드를 육각 좌표 위치에 버튼으로 표시, 현재 위치 강조), 이동 가능한 노드 버튼, 자동 귀환 버튼, 로그창.
- 그래픽 작업 없음. 기본 uGUI + TextMeshPro.
- **오브젝트 이름 규칙** (나중에 AltTester 자동화용): `Btn_`, `Txt_`, `Panel_` 접두어. 예: `Btn_AutoReturn`, `Btn_Node_03`, `Txt_Time`, `Panel_Log`.

### 4-8. 시드

- `GameBootstrap`에서 시드를 정해 `SeededRandom`을 만든다 (Inspector 입력, 기본값 12345).
- M1에서 같은 시드 재현 검증은 필수가 아니지만, 모든 난수가 이 객체 하나에서 나오도록 만든다.

---

## 5. M1에 필요한 수치 (`BalanceConfig` 기본값)

| 이름 | 값 |
|---|---|
| FirstTraversalMinutes | 60 |
| RevisitTraversalMinutes | 30 |
| DayStartMinute | 360 (06:00) |
| NightStartMinute | 1200 (20:00) |
| GameStartDay / GameStartMinute | 1 / 360 |
| PathEventChanceDayFirst / DayRevisit | 0.25 / 0.05 |
| PathEventChanceNightFirst / NightRevisit | 0.40 / 0.25 |
| MapRadius (1depth) | 2 |
| ActiveNodeCount | 11 |
| MaxPathDistance | 5 |
| AllowIncompleteSafeNodesOnReturn | false ([미정]) |
| DefaultSeed | 12345 |

---

## 6. 테스트 (EditMode, Domain만)

최소한 아래를 테스트로 작성하고 통과시킨다.

1. HexCoord: 이웃 6개, 거리 계산, 반경 2 범위가 19칸.
2. MapValidator: 고정 맵이 4-1의 모든 조건을 만족 (활성 11, 구성, 시작 노드 가장자리, 전체 연결, 순환 1~2, 최대 거리 ≤ 5).
3. TravelTimeCalculator: 처음 60분, 재방문 30분.
4. 이동: 경로 `IsTraversed`·노드 `IsVisited` 갱신, 연결되지 않은 노드로는 이동 불가.
5. FogOfWar: 방문 시 연결된 노드·경로만 발견.
6. GameTime: 19:59 → 낮, 20:00 → 밤, 05:59 → 밤, 06:00 → 낮, 23:30 + 60분 → 다음 일차 00:30.
7. 경로 이벤트 확률 선택: 낮/밤 × 처음/재방문 4구간이 올바른 확률을 고른다 (난수는 테스트용 고정 값 주입).
8. ReturnPathFinder: 완료한 노드만 지나는 최단 경로, 미완료 노드는 우회, 경로가 없으면 없음을 반환.

---

## 7. 완료 조건 (M1)

- Unity에서 `Main` 씬을 실행하면 고정 맵에서 노드를 이동할 수 있다.
- 이동할 때마다 시간이 흐르고, 낮/밤이 바뀌고, 날짜가 넘어간다.
- 안개가 규칙대로 걷힌다.
- 자동 귀환이 완료한 노드만 지나 시작 노드로 간다.
- 6장의 테스트가 모두 통과한다.
- Domain 어셈블리에 UnityEngine 참조가 없다.

---

## 8. 보고서 (작업 후 함께 제출)

1. 만든 파일 목록과 각 파일의 역할 (한 줄씩)
2. 실행 방법 (씬, Inspector 설정)
3. 테스트 결과
4. 고정 맵 배치 (노드 좌표·종류·연결 표)
5. 임의로 정한 것과 그 이유 (예: 낮/밤 판정 기준 시각)
6. 통합기획서와 다르게 구현한 곳, 또는 규칙이 모호했던 곳
7. M2를 위해 미리 열어 둔 확장 지점

---

## 9. 이후 마일스톤 (참고만, 이번에 구현하지 않음)

| 단계 | 내용 |
|---|---|
| M2 | 전투: 속도+1d8 턴, 공격·방어·도주, 선형 명중률, 밤 보정, 치명 ×1.5, 방어도·소수점 버림, 고블린, 매복 전투 |
| M3 | 가방 8칸·스택·버리기, 회복약(전투 중 행동 1회), 드랍, 보물 |
| M4 | 마을(길드·출정 준비·정산·자택 휴식), 고블린 토벌 의뢰(일차 기한), 경험치·레벨업, 사망 처리 |
| 맵 2단계 | 고정 시드 절차적 생성 (`IMapProvider` 교체, `MapValidator` 재사용) |
| 맵 3단계 | 출정마다 다른 시드 |
