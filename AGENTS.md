# AGENTS.md — Grimward

AI 코딩 에이전트(Codex 등)가 이 저장소에서 작업할 때 가장 먼저 읽는 안내서다.

## 프로젝트

- **Grimward**: 「다키스트 던전 라이트」. 마을에서 의뢰를 받고 숲으로 출정해 4칸 진형 턴제 전투로 탐험하다가, 언제 돌아설지 판단하는 RPG. 매주 악마 습격을 막는다.
- 플랫폼: 모바일 가로 + PC (최대 FHD).
- 엔진: **Unity 6000.6.0f1**, Universal 2D(URP), Input System, uGUI + TextMeshPro, C#.
- 현재 단계: **v0.1 (얕은 숲 1구역 루프)**. 2026-11-10 재시작 — 이전 코드는 git 태그 `archive/pre-restart`.

## 먼저 읽을 문서 (`Design/`)

| 순위 | 문서 | 용도 |
|---|---|---|
| 1 | `Design/Grimward_기획안.md` | **게임 규칙의 기준** |
| 2 | `Design/Grimward_게임설계.xlsx` | 수치·목록 (기술, 몬스터, 조사 물건, 맵 좌표, 특성, 밸런스 상수) |
| 3 | `Design/Grimward_개발계획.md` | 버전·마일스톤, **현재 마일스톤의 범위와 완료 조건** |
| 4 | `Design/Grimward_개발규칙.md` | **상시 개발 규칙**: 폴더·어셈블리, 데이터 주도, SOLID, 금지 사항, 화면 이름 규칙, 테스트, 보고서 양식 |
| 참고 | `Design/Grimward_통합기획.md` | 논의 기록. 규칙으로 쓰지 않는다 |

- 문서끼리 다르면 위 순위를 따르고, 차이를 작업 보고서에 적는다.
- 마일스톤별 작업지시서가 있으면(`Design/Grimward_작업지시서_M?.md`) 그 범위를 우선한다.

## 작업 규칙 요약 (자세한 내용은 개발규칙)

- **작업은 승인 후에 시작한다.** 요청을 받으면 먼저 무엇을 어떻게 할지(범위·바꿀 파일·정할 것)를 제안하고,
  사용자가 승인한 뒤에 파일을 만들거나 수정한다. 질문에는 설명만 하고 파일을 바꾸지 않는다.
- 진행 중에 범위 밖 문제나 새 결정 사항이 생기면 멈추고 다시 묻는다.
- `Assets/` 아래만 작성·수정. `ProjectSettings/`, `Packages/`, `Design/`은 수정하지 않는다.
- 템플릿 기본 파일(`Assets/Welcome`, `Assets/Scenes/SampleScene.unity`, `Assets/Settings`)은 지우지 않는다.
- 코드는 `Assets/_Project/`, 테스트는 `Assets/Tests/EditMode/`.
- 어셈블리: `Grimward.Domain`(UnityEngine 참조 금지, 게임 규칙 전부) ← `Grimward.Presentation`(화면·입력) / `Grimward.Editor` / `Grimward.Domain.Tests`.
- 직업·기술·몬스터·조사 물건·특성·의뢰·보급품·맵은 데이터. 코드에 이름·수치를 쓰지 않는다.
- `UnityEngine.Random` 금지, 모든 게임 난수는 `IRandomSource` 하나에서.
- 인터페이스는 `IRandomSource`, `IMapProvider`, `ITravelTimeCalculator`, `ISaveStore`만.
- 게임 상태 static 싱글턴 금지. 조립은 `GameBootstrap` 한 곳.
- UI 오브젝트 이름은 `Panel_`, `Btn_`, `Txt_` 접두어. EventSystem은 `InputSystemUIInputModule`.
- **[미정]** 항목은 임의로 확정하지 않는다. 기본값은 데이터에 두고 보고서에 적는다.
- 식별자는 영어, 주석·UI 텍스트는 한국어 가능. 네임스페이스 루트 `Grimward`.

## 작업이 끝나면

- EditMode 테스트 전부 통과, 콘솔 오류 없음.
- `Design/Grimward_개발규칙.md` 12장 양식대로 보고서를 **응답 본문에** 작성.
- 커밋은 마일스톤 단위, 제목은 영어. 마일스톤 완료 시 태그 `v0.1-m?`.
