# AGENTS.md — Grimward

AI 코딩 에이전트(Codex 등)가 이 저장소에서 작업할 때 가장 먼저 읽는 안내서다.

## 프로젝트

- **Grimward**: 마을에서 의뢰를 받고, 어두워지는 숲을 탐험하다가 언제 돌아설지 판단하는 턴제 탐험 RPG (모바일 가로 화면 목표).
- 엔진: **Unity 6000.6.0f1**, Universal 2D 템플릿, C#.
- 현재 단계: **v0.1 — 최소 시스템 구축 및 연결 검증.** 텍스트·버튼 UI + 임시 육각 타일 지도.

## 먼저 읽을 문서 (`Design/`)

| 문서 | 용도 |
|---|---|
| `Design/Grimward_Codex_작업지시서_M3.md` | **현재 작업 지시서.** 이번 범위·기본값·테스트·완료 조건 |
| `Design/Grimward_개발규칙.md` | **상시 개발 규칙.** 폴더·어셈블리 구조, SOLID 적용, 금지 사항, 데이터 정의, 화면 이름 규칙, 보고서 양식 |
| `Design/Grimward_통합기획.md` | 게임 규칙. **「플레이테스트 결정」 → 「최신 결정」 → Part I** 순으로 기준. Part II는 과거 논의 기록이므로 규칙으로 쓰지 않는다 |
| `Design/Grimward_게임설계.xlsx` | 수치 참고 (`10_밸런스상수`, `22_기술` 등. 주황 칸 = 플레이테스트 변경) |

규칙이 서로 다르면 **통합기획서(플레이테스트 결정 → 최신 결정 → Part I)** 를 따르고, 차이를 작업 보고서에 적는다.
M1·M2 지시서는 구현 완료 후 삭제했다. 필요하면 git 기록을 본다.

## 작업 규칙 요약

- `Assets/` 아래만 작성·수정한다. `ProjectSettings/`, `Packages/`, `Design/`은 수정하지 않는다 (필요하면 보고서에 요청).
- 템플릿 기본 파일(`Assets/Welcome`, `Assets/Scenes/SampleScene.unity`, `Assets/Settings`)은 지우지 않는다.
- 코드는 `Assets/_Project/` 아래, 테스트는 `Assets/Tests/EditMode/` 아래.
- 어셈블리: `Grimward.Domain`(UnityEngine 참조 금지, 게임 규칙 전부) → `Grimward.Presentation`(화면·입력) / `Grimward.Domain.Tests`(EditMode).
- 네임스페이스 루트: `Grimward`.
- 수치는 코드에 직접 쓰지 않고 `BalanceConfig`에서 읽는다. `UnityEngine.Random` 금지, 모든 난수는 `IRandomSource` 하나에서.
- 인터페이스는 개발규칙에 정한 곳(난수·맵 제공·이동 시간 계산)에만. 구현이 하나뿐인 인터페이스를 늘리지 않는다.
- 게임 상태를 담는 static 싱글턴 금지. 조립은 `GameBootstrap` 한 곳에서.
- UI 오브젝트 이름은 `Btn_`, `Txt_`, `Panel_` 접두어 (나중에 AltTester 자동화용).
- Input System 패키지 사용 중: EventSystem은 `InputSystemUIInputModule`.
- 시작 시 `Application.targetFrameRate = 60`.
- 통합기획서의 **[미정]** 항목은 임의로 확정하지 않는다. 기본값이 필요하면 `BalanceConfig`에 두고 보고서에 적는다.
- 식별자는 영어, 주석과 UI 텍스트는 한국어 가능.

## 작업이 끝나면

- EditMode 테스트를 모두 통과시킨다.
- `Design/Grimward_개발규칙.md` 11장 양식대로 보고서를 **응답 본문에** 작성한다.
- 커밋은 마일스톤 단위로, 제목은 영어로 쓴다 (본문은 한국어 가능).
