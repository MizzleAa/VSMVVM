# Canvas Drawing Tools 계획

## Tool 모드 시스템

`CanvasToolMode` enum으로 현재 도구 상태 관리. 툴바 ToggleButton 그룹으로 전환.

```
Select | Rectangle | Ellipse | Line | Pen | Polygon (Click) | Polygon (Draw) | Image
```

---

## 그리기 도구

### 1. 사각형 (Rectangle)
- 마우스 드래그 → 사각형 생성
- 드래그 중 반투명 미리보기
- Shift 홀드 → 정사각형

### 2. 원 (Ellipse)
- 마우스 드래그 → 타원 생성
- Shift 홀드 → 정원 (1:1)

### 3. 선 (Line)
- 마우스 드래그 → 직선 생성
- Shift 홀드 → 수평/수직/45도 스냅

### 4. 펜 (Pen / Freehand)
- 마우스 드래그 → 자유 곡선 (Polyline)
- 스트로크 두께/색상 설정

### 5. 폴리곤 — 클릭 모드 (Polygon Click)
- 클릭으로 꼭짓점 순서대로 추가
- 더블클릭 또는 시작점 클릭 → 폴리곤 닫기 완성
- 그리는 중 경로 미리보기 (점선)
- ESC → 취소

### 6. 폴리곤 — 마우스 그리기 모드 (Polygon Draw)
- 마우스 드래그로 자유 형태 폴리곤 그리기
- 마우스 놓으면 자동으로 시작점과 연결하여 닫기
- 내부적으로 포인트를 일정 간격으로 샘플링하여 Polygon 생성

### 7. 이미지 추가 (Image)
- 파일 다이얼로그로 이미지 선택 (PNG/JPG/BMP)
- 캔버스에 Image 요소 배치
- 기존 Adorner로 리사이즈/이동

---

## 공통 UI

- 스트로크 색상/두께 설정
- Fill 색상 설정
- 도구별 커서 변경 (십자, 펜 등)
- Undo/Redo (향후)

---

## 구현 위치

| 영역 | 파일 |
|------|------|
| Tool enum/인터페이스 | `VSMVVM.WPF/Controls/` |
| 그리기 로직 | `VSMVVM.WPF/Controls/` (LayeredCanvas 확장 or Tool 클래스) |
| ViewModel | `VSMVVM.WPF.Sample/ViewModels/CanvasViewModel.cs` |
| UI (툴바/바인딩) | `VSMVVM.WPF.Sample/Views/CanvasView.xaml` |
| Behavior | `VSMVVM.WPF.Sample/Behaviors/CanvasViewBehavior.cs` |
