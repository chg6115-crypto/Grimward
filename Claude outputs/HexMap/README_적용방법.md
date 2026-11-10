# 육각 지도 적용 방법

## 1. 파일 옮기기 (Assets/_Project 아래 같은 경로로)

| Claude outputs/HexMap 안의 파일 | 옮길 위치 | 구분 |
|---|---|---|
| `Scripts/Presentation/Map/HexMapTheme.cs` | `Assets/_Project/Scripts/Presentation/Map/` | 새 파일 |
| `Scripts/Presentation/Map/HexTileView.cs` | `Assets/_Project/Scripts/Presentation/Map/` | 새 파일 |
| `Scripts/Presentation/Map/HexMapView.cs` | `Assets/_Project/Scripts/Presentation/Map/` | 새 파일 |
| `Scripts/Presentation/UI/ExplorationScreen.cs` | `Assets/_Project/Scripts/Presentation/UI/` | 교체 |
| `Scripts/Presentation/Bootstrap/GameBootstrap.cs` | `Assets/_Project/Scripts/Presentation/Bootstrap/` | 교체 |
| `Editor/HexTileSetup.cs` | `Assets/_Project/Editor/` | 교체 (메뉴 4 추가) |

그리고 **`Assets/_Project/Scripts/Presentation/UI/MapView.cs`(와 .meta)를 삭제**한다. 예전 버튼식 지도라 더 이상 쓰지 않는다.

## 2. Unity에서 실행

1. 컴파일이 끝나면 메뉴 **Grimward → Art → 4. Main 씬에 육각 지도 연결**
   - `Assets/_Project/Data/HexMapTheme.asset` 생성 (타일 프리팹·마커·길·나무 자동 연결)
   - Main 씬의 카메라를 `Main Camera`로 정리하고 모든 레이어를 그리게 설정
   - GameBootstrap에 테마와 카메라 연결 후 씬 저장
2. Main 씬 Play → 왼쪽에 육각 지도, 오른쪽에 탐험 기록이 보이면 성공
3. 길로 이어진 칸을 클릭해 이동

## 3. 알아둘 것

- 색·숲 고리 수·나무 비율·노드 번호 표시는 `HexMapTheme.asset` Inspector에서 조정한다.
- `Editor/M2SmokeValidation.cs`(Codex가 만든 명령줄 검증)는 예전 버튼 지도(`Btn_Node_XX`의 Button)를 누르도록 되어 있어 이 변경 후에는 실패한다. 다음 Codex 작업 때 타일 클릭 방식으로 고치게 한다.
- EditMode 테스트(Domain)는 영향 없다.
