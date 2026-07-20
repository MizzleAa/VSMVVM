# VSMVVM

[![CI](https://github.com/MizzleAa/VSMVVM/actions/workflows/ci.yml/badge.svg)](https://github.com/MizzleAa/VSMVVM/actions/workflows/ci.yml)
[![Publish](https://github.com/MizzleAa/VSMVVM/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/MizzleAa/VSMVVM/actions/workflows/publish-nuget.yml)
[![Tests](https://img.shields.io/badge/tests-768%20passing-brightgreen)](#testing)
[![Targets](https://img.shields.io/badge/targets-net8.0%20%7C%20net9.0-blue)](#testing)
[![NuGet](https://img.shields.io/nuget/v/VSMVVM.Core?label=NuGet)](https://www.nuget.org/packages/VSMVVM.Core/)

Lightweight, modular MVVM framework for **WPF** with built-in Source Generator, DI Container, Tailwind-inspired Design System, and Blueprint-style workflow scheduler.

## Architecture

| Package | Target | Description |
|---------|--------|-------------|
| **VSMVVM.Core** | .NET Standard 2.0 | MVVM base, DI, Source Generator, Messenger, Guard |
| **VSMVVM.WPF** | .NET 8 / .NET 9 (WPF) | Host, Services, Controls, Behaviors, SVG |
| **VSMVVM.WPF.Design** | .NET 8 / .NET 9 (WPF) | Tailwind-inspired design tokens, themed controls |
| **VSMVVM.Core.Scheduler** | .NET Standard 2.0 | Blueprint-style workflow engine — graph model, pins (`IPinTypeRegistry` 확장 포인트, 사용자 타입/`List<>`/`Dictionary<,>`/배열 generic 자동 인스턴스화, StableName 라운드트립), **다형성 핀 시스템** (`Phase K~L` — `PinDescriptor.TypeParameterName`, `IPolymorphicNode`, `INodeInstancePropertyHost`), 빌트인 노드: 흐름 (Start/End/Sequence/Branch/Delay/Toggle/**Repeat**/**Fork**/**Join**), 진단 (Log 가변 인자, Assert/Guard/RangeAssert), IO (Output/Input), 데이터 소스 (다형성 Constant, Random Int/Double/Bool), **Variables** (다형성 GetVariable/SetVariable + GraphVariable), **컬렉션** (다형성 `List<T>`: Add/Get/Count/Clear/Contains/RemoveAt + `Dictionary<K,V>`: Set/Get/ContainsKey/Remove/Keys/Values/Count), execution engine (**`InvalidateDataCache` — 루프 iteration 사이 pull-cache 리셋**, **`ISchedulerService.SetBreakpoint(id, bool)` — 실행 중 브레이크포인트 명시 동기화**), Source Generator, `[MethodNode]` 메서드 / **`[ParameterNode]` 정적 필드** / **`ValueTuple` 반환 자동 다중 출력 핀** → 자동 노드 등록 (`CustomNodeFactory`), **ProfilingStats**, **ExecutionRun + IExecutionHistoryStore**, **ISchedulerLogSink** (Trace/Debug/Info/Warn/Error), **시스템 가드** (MaxNodes/Timeout/MemoryBudget), `ICompilationService` 추상화 |
| **VSMVVM.Core.Scheduler.Scripting** | .NET 8 / .NET 9 | Roslyn-backed `ICompilationService` — 사용자 C# 코드를 collectible ALC로 동적 컴파일 (선택적) |
| **VSMVVM.WPF.Scheduler** | .NET 8 / .NET 9 (WPF) | NodeGraphCanvas + NodeView/PinView/ConnectionView + ViewModel 6종 (Node/Pin/Connection/Graph + **SchedulerLogVM/ExecutionHistoryVM**) + Pin 타입별 색상 컨버터 + **NodeInspectorPanel** / **SchedulerLogPanel** / **ExecutionHistoryPanel (GanttChartView)** / **CollectionDetailWindow** (컬렉션 값 브레드크럼 드릴다운 + LineChart/ConfusionMatrix/Heatmap 통합 뷰) + `NodeViewModel.OpenSourceRequested` (인스펙터 "Open Code"). `VSMVVM.WPF.Design` 토큰 기반 테마 |
| **VSMVVM.WPF.Scheduler.Editor** | .NET 8 / .NET 9 (WPF) | AvalonEdit 호스팅 `CodeNodeEditor` UserControl + 디자인 토큰 신택스 하이라이팅(Dark/Light XSHD) + `Theme`/`SourceCode`/`Diagnostics` DP. 사용자 코드 노드 편집 UI |

기존 MVVM 의존성: `VSMVVM.WPF.Design` → `VSMVVM.WPF` → `VSMVVM.Core` (NuGet 이 자동 해결).
Scheduler 의존성: `VSMVVM.Core.Scheduler.Scripting` → `VSMVVM.Core.Scheduler` → `VSMVVM.Core`. UI: `VSMVVM.WPF.Scheduler` → `VSMVVM.WPF` + `VSMVVM.WPF.Design` + `VSMVVM.Core.Scheduler`. 코드 에디터: `VSMVVM.WPF.Scheduler.Editor` → `VSMVVM.WPF.Scheduler` + `AvalonEdit`.

---

## Installation

공개 NuGet.org 에서 설치합니다. 모든 패키지는 [nuget.org/packages/VSMVVM.Core](https://www.nuget.org/packages/VSMVVM.Core/) 등으로 게시됩니다.

```bash
dotnet add package VSMVVM.WPF
dotnet add package VSMVVM.WPF.Design                # 선택 — Tailwind-inspired 다크/라이트 테마
dotnet add package VSMVVM.Core.Scheduler            # 선택 — Blueprint-style workflow 엔진
dotnet add package VSMVVM.Core.Scheduler.Scripting  # 선택 — Roslyn 동적 컴파일
dotnet add package VSMVVM.WPF.Scheduler             # 선택 — NodeGraph UI 컨트롤
dotnet add package VSMVVM.WPF.Scheduler.Editor      # 선택 — AvalonEdit 코드 에디터
```

`VSMVVM.WPF` 가 `VSMVVM.Core` 를 자동으로 가져옵니다. 다른 패키지도 각자의 의존성이 자동 해결됩니다.

---

## Quick Start

```csharp
// Program.cs
using VSMVVM.WPF.Host;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VSMVVMHost
            .CreateHost<Bootstrapper, App>(args, "MyApp")
            .UseSplash<SplashWindow>()
            .Build()
            .RunApp<MainWindow>();
    }
}
```

```csharp
// Bootstrapper.cs
public class Bootstrapper : AppBootstrapper
{
    protected override void RegisterServices(IServiceCollection sc)
    {
        sc.AddSingleton<IDialogService, DialogService>();
        sc.AddSingleton<IDispatcherService, WPFDispatcherService>();
    }

    protected override void ViewModelMapping(IViewModelMapper mapper)
    {
        mapper.Register<MainView, MainViewModel>();
    }

    protected override void RegionMapping(IRegionManager rm)
    {
        rm.Mapping<HomeView>("MainRegion");
    }

    protected override void OnStartUp(IServiceContainer container)
    {
        var splash = container.GetService<ISplashService>();
        splash?.Report("Loading...", 0.5);
    }

    protected override void RegisterModules() { }
}
```

---

## VSMVVM.Core

### Source Generator

코드 생성기가 `partial class` 의 boilerplate 를 자동 생성합니다.

#### `[Property]`

```csharp
public partial class MyViewModel : ViewModelBase
{
    [Property]
    private string _name;
    // → public string Name { get; set; }  (with OnPropertyChanged)
}
```

#### `[PropertyChangedFor]`

```csharp
[Property]
[PropertyChangedFor(nameof(FullName))]
private string _firstName;
// → Name setter에서 OnPropertyChanged(nameof(FullName)) 자동 호출
```

#### `[NotifyCanExecuteChangedFor]`

```csharp
[Property]
[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
[NotifyCanExecuteChangedFor(nameof(ResetCommand))]
private string _name;
// → Name setter에서 SaveCommand?.RaiseCanExecuteChanged() 자동 호출
```

#### `[RelayCommand]` / `[AsyncRelayCommand]`

```csharp
[RelayCommand]
private void Save() => /* ... */;
// → public RelayCommand SaveCommand { get; }

[RelayCommand(CanExecute = nameof(CanSave))]
private void Save() => /* ... */;
private bool CanSave() => !string.IsNullOrEmpty(Name);

[AsyncRelayCommand(CanExecute = nameof(CanLoad))]
private async Task LoadData()
{
    IsBusy = true;
    await Task.Delay(1000);
    IsBusy = false;
}
private bool CanLoad() => !IsBusy;
```

#### `[Log]`

`[RelayCommand]` / `[AsyncRelayCommand]` 메서드 또는 `[Property]` 필드에 함께 적용하면, Source Generator 가 생성된 코드 안에 `ILoggerService` 호출을 자동 삽입합니다 — "어떤 버튼이 눌렸는가 / 어떤 항목이 선택되었는가" 를 별도 코드 없이 기록할 때 사용.

```csharp
// 1) Command 메서드 — Command 실행 람다에 진입 로그 삽입.
[RelayCommand]
[Log]
private void Save() => /* ... */;
// → "[Command] MyNs.MyVm.Save" 를 Info 레벨로 자동 로깅

[RelayCommand]
[Log(Level = LogLevel.Debug)]
private void ToggleAside() => /* ... */;
// → Debug 레벨

[AsyncRelayCommand]
[Log(Level = LogLevel.Warn)]
private async Task SubmitOrder() => /* ... */;
// → 비동기 Command 도 동일

// 2) 파라미터가 있는 Command — CommandParameter 가 메시지에 보간되어 어떤 항목인지 식별 가능.
[RelayCommand]
[Log]
private void NavigateToPage(object naviObject) => /* ... */;
// → "[Command] MyNs.MyVm.NavigateToPage(Dashboard)" (naviObject.ToString() 보간)

// 3) [Property] 필드 — 생성된 setter 안에 값 변경 시 로깅 삽입. EqualityComparer 가드 후이므로 실제 변경 시에만 기록.
[Property]
[Log]
private ActionItem _selectedAction;
// → SelectedAction = 새 값 일 때 "[Property] MyNs.MyVm.SelectedAction = Import" 로깅
```

메시지 포맷:

- 파라미터 없는 Command: `[Command] {Namespace}.{ClassName}.{MethodName}`
- 파라미터 있는 Command: `[Command] {Namespace}.{ClassName}.{MethodName}({param})` — `param.ToString()` 결과. `null` 도 안전 (빈 문자열로 표시).
- Property: `[Property] {Namespace}.{ClassName}.{PropertyName} = {value}` — `value.ToString()` 결과.

동작 원리:

- 메서드/필드 본문은 변경하지 않음. 생성된 `XxxCommand` 람다 또는 `XxxProperty` setter 에만 로깅 호출이 삽입됨.
- 직접 메서드를 호출하거나 필드를 직접 대입(`_selectedAction = items[0]`)하면 로그가 남지 않음 — 오직 Command 경로(`ICommand.Execute`) 또는 Property setter 경로로 변경할 때만 기록.
- `ServiceLocator.GetServiceProvider()` 를 통해 `ILoggerService` 를 해석. ServiceLocator 가 미초기화여도 try/catch 가드로 명령/setter 실행은 정상 진행.
- `[Property]` setter 는 `EqualityComparer<T>.Default.Equals` (또는 ObservableValidator 의 `SetProperty`) 가드 안에서 로깅 — 같은 값으로 다시 set 해도 로그는 남지 않음 (의도된 변경만 기록).

### DI Container

Singleton / Transient / Scoped 라이프사이클 지원. 생성자 주입 기반.

```csharp
// 등록
sc.AddSingleton<IMyService, MyService>();
sc.AddTransient<ILogger, FileLogger>();
sc.AddSingleton<ICache>(new MemoryCache());
sc.AddSingleton<IDb, SqlDb>(c => new SqlDb(c.GetService<IConfig>()));

// 해석
var service = container.GetService<IMyService>();
```

### Services (Core)

Core 가 제공하는 핵심 서비스. 인터페이스만 정의된 항목 (WPF 에서 구현) 은 VSMVVM.WPF 섹션 참조.

| 서비스 | 인터페이스 | 핵심 메서드 | 용도 |
|---|---|---|---|
| **Messenger** | `IMessenger` | `Register`, `Send`, `Unregister`, `Cleanup` | WeakReference 기반 Pub/Sub, 토큰 채널 분리 |
| **LocalizeService** | `ILocalizeService` | `SetResourceManager`, `ChangeLocale`, `GetString`, `Subscribe` | 다국어 ResourceManager 기반 로컬라이제이션 |
| **RegionManager** | `IRegionManager` | `Mapping<TView>`, `Navigate`, `GoBack` / `GoForward`, `GetCurrentViewDisplayName` | View 네비게이션 + Back/Forward 히스토리 |
| **ServiceContainer** | `IServiceContainer` | `GetService<T>`, `GetService(Type)`, `ContainsService` | DI 해석 (resolve) |
| **ServiceCollection** | `IServiceCollection` | `AddSingleton` / `AddTransient` / `AddScoped`, `CreateContainer` | DI 등록 |

인터페이스만 Core 에 정의되고 구현이 WPF 에 있는 항목:
`IDispatcherService`, `IDialogService`, `IWindowService`, `IBuildInfoService`, `ISplashService`, `IVersionControlService` → **VSMVVM.WPF / Services (WPF)** 참조.

`ILoggerService` 는 사용자 정의 구현 (NLog/Serilog 등을 어댑터로 등록).

### Messenger

타입 기반 Pub/Sub 메시징. WeakReference 기반이라 수신자가 GC 되면 자동 해제.

```csharp
// 구독
messenger.Register<MyMessage>(this, msg => Handle(msg));

// 토큰 채널 분리
messenger.Register<MyMessage>(this, "channelA", msg => HandleA(msg));

// 발행
messenger.Send(new MyMessage { Data = "Hello" });
messenger.Send(new MyMessage(), "channelA");

// 해제
messenger.Unregister<MyMessage>(this);
messenger.UnregisterAll(this);
```

### ObservableValidator

DataAnnotation 기반 유효성 검증.

```csharp
public partial class FormViewModel : ObservableValidator
{
    [Property]
    [Required(ErrorMessage = "Name is required.")]
    [MinLength(2)]
    private string _name;

    [RelayCommand]
    private void Submit()
    {
        ValidateAllProperties();
        if (!HasErrors) { /* save */ }
    }
}
```

### StateStore (Redux-style)

전역 상태 관리. WeakReference 기반 구독.

```csharp
public class AppStateStore : StateStoreBase<AppState>
{
    public AppStateStore() : base(new AppState()) { }

    public void IncrementCounter()
    {
        var next = new AppState { Counter = State.Counter + 1 };
        UpdateState(next);  // → 모든 구독자에 자동 통지
    }
}

// 구독
store.Subscribe(state => Counter = state.Counter);
```

### Guard

Fail-fast 방어적 검증 유틸리티.

```csharp
Guard.IsNotNull(param, nameof(param));
Guard.IsNotNullOrEmpty(name, nameof(name));
Guard.IsInRange(age, 1, 150, nameof(age));
Guard.IsOfType<IService>(obj, nameof(obj));
Guard.IsNotEmpty(list, nameof(list));
Guard.IsTrue(condition, nameof(condition));
```

### ViewModelBase

`INotifyPropertyChanged` / `INotifyPropertyChanging` / `ICleanup` 구현.

```csharp
public partial class MyViewModel : ViewModelBase
{
    // SetProperty, OnPropertyChanged, OnPropertyChanging 제공
    // partial 메서드: On{Property}Changing(value), On{Property}Changed(oldValue, newValue)
}
```

### BatchObservableCollection

대량 추가/제거 시 단일 `CollectionChanged` 이벤트 발생.

### Helper Interfaces

| 인터페이스 | 용도 |
|---|---|
| `ICleanup` | ViewModel 정리 (`Cleanup()`) |
| `IAsyncInitializable` | 비동기 초기화 (`InitializeAsync()`) |
| `IServiceInitializable` | 서비스 초기화 (`Initialize(IServiceContainer)`) |
| `IModule` | 모듈 시스템 (`RegisterServices`, `ViewModelMapping`, `RegionMapping`) |
| `INavigateAware` | 네비게이션 인식 (`OnNavigatedTo`, `OnNavigatedFrom`) |
| `IRegion` | Region 컨트롤 인터페이스 (`Activate`, `Deactivate`) |
| `IViewModelMapper` | View-ViewModel 매핑 (`Register<TView, TViewModel>`) |
| `IAsyncRelayCommand` | 비동기 명령 (`ExecuteAsync`, `CanExecute`) |

---

## VSMVVM.WPF

### Host (Fluent Builder)

```csharp
VSMVVMHost
    .CreateHost<Bootstrapper, App>(args, "MyApp")
    .UseSplash<SplashWindow>()         // 별도 STA 스레드 스플래시
    .Build()                           // Bootstrapper 라이프사이클 실행
    .ShutdownMode(ShutdownMode.OnMainWindowClose)
    .Popup<LoginWindow>(dialog: true)  // 모달 팝업
    .RunApp<MainWindow>();             // Application.Run()
```

### Services (WPF)

Core 인터페이스의 구현체 + WPF 고유 서비스. DI 컨테이너에 등록해 사용.

| Service | Interface | Description |
|---------|-----------|-------------|
| **WPFDispatcherService** | `IDispatcherService` | UI 스레드 디스패칭 (`Invoke`, `InvokeAsync`, `Yield` Background priority) |
| **DialogService** | `IDialogService` | XAML 디자인 시스템 기반 모달 다이얼로그 (OK / OK·Cancel / Yes·No), 파일/폴더 다이얼로그 |
| **WindowService** | `IWindowService` | Window 상속 View 직접 표시. `DialogParameter` / `DialogResultData` 반사 주입 |
| **SplashService** | `ISplashService` | 별도 STA 스레드 스플래시 (`Report(msg, progress)` + 자동 Close) |
| **BuildInfoService** | `IBuildInfoService` | `AppVersion`, `BuildTime`, `FrameworkVersion`, `OSArchitecture` |
| **VersionControlService** | `IVersionControlService` | Git 정보 (`BranchName`, `CommitHash`, `CommitDate`, `IsDirty`) |
| **WindowPlacementService** | `IWindowPlacementService` | 창 위치/크기 JSON 저장/복원 |
| **ShortcutService** | `IShortcutService` | 글로벌/스코프 키보드 단축키 (`RegisterGlobal`, `RegisterScoped`) |
| **ZoomService** | `IZoomService` | 줌/팬 좌표 변환 (Canvas 인터랙션) |
| **UndoRedoService** | `IUndoRedoService` | Undo/Redo 스택 (용량 제한 50, `CanUndo` / `CanRedo`) |
| **WpfFileDialogService** | `IFileDialogService` | 경량 파일 열기/저장 다이얼로그 |

#### 등록 예

```csharp
protected override void RegisterServices(IServiceCollection sc)
{
    sc.AddSingleton<IDispatcherService, WPFDispatcherService>();
    sc.AddSingleton<IDialogService, DialogService>();
    sc.AddSingleton<ISplashService, SplashService>();
    sc.AddSingleton<IBuildInfoService, BuildInfoService>();
    sc.AddSingleton<IUndoRedoService, UndoRedoService>();
    sc.AddSingleton<IShortcutService, ShortcutService>();
    // ...
}
```

### Controls

| Control | Description |
|---------|-------------|
| **WPFRegion** | ContentControl 기반 Region (Navigation + `INavigateAware` + Back/Forward History) |
| **ImageCanvas** | 줌/팬 캔버스, 자식 선택/리사이즈/드래그 지원 |
| **LayeredCanvas** | Z-Order 레이어 캔버스 |
| **CanvasSelectionAdorner** | 8방향 리사이즈 핸들 Adorner |

### Behaviors (Interaction)

XAML 에서 이벤트→커맨드 바인딩.

```xml
<Button xmlns:i="clr-namespace:VSMVVM.WPF.MarkupExtensions;assembly=VSMVVM.WPF">
    <i:Interaction.Triggers>
        <i:EventTrigger EventName="MouseEnter">
            <i:InvokeCommandAction Command="{Binding HoverCommand}"/>
        </i:EventTrigger>
    </i:Interaction.Triggers>
</Button>
```

### MarkupExtensions

| Extension | Description |
|-----------|-------------|
| **ViewModelLocator** | `AutoWireViewModel="True"` — View-ViewModel 자동 바인딩 |
| **LocalizeExtension** | `{me:Localize Key=UI_TITLE}` — 다국어 바인딩 |
| **BindingProxy** | DataContext 프록시 (DataGrid 등 비시각 트리 바인딩) |
| **EqualityConverter** | 두 바인딩 값 비교 MultiValueConverter |

### Media

| Feature | Description |
|---------|-------------|
| **SvgImageConverter** | SVG → DrawingImage 변환 |
| **SvgImageExtension** | `{me:SvgImage Source=/Assets/icon.svg}` XAML 마크업 |

### GlobalExceptionHandler

```csharp
GlobalExceptionHandler.Initialize(app);
// → DispatcherUnhandledException + TaskScheduler.UnobservedTaskException 자동 처리
```

---

## VSMVVM.WPF.Design

Tailwind CSS 에서 영감받은 유틸리티 기반 WPF 디자인 시스템.

### 적용

```xml
<!-- App.xaml -->
<ResourceDictionary Source="/VSMVVM.WPF.Design;component/Index.xaml"/>
```

### Tokens

| Category | Examples |
|----------|---------|
| **Spacing** | `P1`~`P8`, `M1`~`M8`, `Mb2`, `Px2Py1`, `Gap2`~`Gap4` |
| **Typography** | `TextXs`~`Text4xl`, `FontSans`, `FontBold`, `FontSemibold` |
| **Sizing** | `W12`~`W96`, `H1`~`H12`, `MinW20`~`MinW64` |
| **Effects** | `RoundedSm`/`Md`/`Lg`/`Full`, `ShadowSm`/`Md`/`Lg`, `Border`, `Opacity50` |

### Colors (Dark / Light)

Zinc + Blue 팔레트. `ThemeDark.xaml` / `ThemeLight.xaml` 런타임 전환.

| Token | Description |
|-------|-------------|
| `BgPrimary` / `BgSecondary` / `BgTertiary` | 배경 |
| `TextPrimary` / `TextSecondary` / `TextMuted` | 텍스트 |
| `AccentPrimary` / `AccentHover` | 강조색 (Blue) |
| `BorderDefault` / `BorderHover` / `BorderFocus` | 테두리 |
| `Success` / `Warning` / `Error` / `Info` | 상태 색상 |

### Styled Controls (15)

모든 기본 컨트롤에 디자인 토큰이 적용됩니다:

`Button` · `TextBox` · `PasswordBox` · `CheckBox` · `ComboBox` · `ListBox` · `ListView` · `DataGrid` · `TabControl` · `Expander` · `ScrollViewer` · `ProgressBar` · `ContextMenu` · `Window` · `Dialog`

### Components

| Component | Description |
|-----------|-------------|
| **DateTimePicker** | 날짜/시간 선택기 |
| **InfoPopup** | 정보 팝업/툴팁 컴포넌트 |
| **JsonEditor** | JSON 구문 강조 편집기 |
| **LoadingOverlay** | 로딩 오버레이 (`IsLoading` 바인딩) |

### Window Chrome

커스텀 타이틀바 + 최소화/최대화/닫기 버튼. `WindowChrome` + `WindowButtonsBehavior` 기반.
`WindowChrome.CustomButtons` attached property 로 AppBar 에 커스텀 버튼(Back/Forward 등) 배치 가능.

### Navigation History (Back / Forward)

`IRegionManager` 가 네비게이션 히스토리 스택을 자동 관리합니다.

```csharp
// 이전/다음 페이지 이동
regionManager.GoBack("MainRegion");
regionManager.GoForward("MainRegion");

// 상태 확인
bool canBack = regionManager.CanGoBack("MainRegion");
bool canForward = regionManager.CanGoForward("MainRegion");

// 현재 View 표시 이름 자동 생성 (PascalCase → 공백 분리)
// "DefaultDesignView" → "Default Design"
string displayName = regionManager.GetCurrentViewDisplayName("MainRegion");
```

### Logging

`ILoggerService` 인터페이스로 로깅 추상화. Trace/Debug/Info/Warn/Error/Fatal 6단계 레벨 지원.

```csharp
// DI 등록
sc.AddSingleton<ILoggerService, MyLoggerService>();

// 사용
logger.Info("App started");
logger.Error("Failed", exception);
```

#### Command / Property 자동 로깅 (`[Log]` 어트리뷰트)

`[RelayCommand]` / `[AsyncRelayCommand]` 메서드 또는 `[Property]` 필드에 `[Log]` 를 함께 붙이면 Source Generator 가 Command 람다 / Property setter 에 로깅 호출을 삽입합니다. "어떤 버튼이 눌렸는가 / 어떤 항목이 선택되었는가" 를 명시 호출 없이 추적할 때 사용 — 자세한 사용법은 위의 [`[Log]`](#log) 섹션 참조.

- 기본 레벨: `Info` (`Level = LogLevel.Debug` 등으로 변경 가능)
- 메시지 포맷:
  - `[Command] {Ns}.{Cls}.{Method}` (파라미터 없음)
  - `[Command] {Ns}.{Cls}.{Method}({param})` (CommandParameter 보간)
  - `[Property] {Ns}.{Cls}.{Property} = {value}` (setter 호출)
- 사전 조건: `ServiceLocator.SetServiceProvider(...)` 가 호출되어 `ILoggerService` 해석 가능해야 함. 미초기화여도 try/catch 가드로 명령 실행 / setter 는 정상 동작.

```csharp
[RelayCommand]
[Log]
private void NavigateToPage(object naviObject) { /* ... */ }
// 사용자 클릭 시 NLog 출력 예시:
// {"level":"INFO","message":"[Command] DeepInsight.Aside.ViewModels.AsideViewModel.NavigateToPage(Dashboard)", ...}

[Property]
[Log]
private ActionItem _selectedAction;
// 사이드바 메뉴 항목 클릭 시:
// {"level":"INFO","message":"[Property] DeepInsight.TrainingManagement.ViewModels.TrainingActionsViewModel.SelectedAction = Train Start", ...}
```

---

## VSMVVM.Core.Scheduler (Workflow Automation Engine)

Unreal Engine Blueprint에서 영감을 받은 **노드 기반 워크플로우 실행 엔진**. 사용자가 노드를 배치/연결해 실행 흐름을 정의하면 런타임 엔진이 이를 push exec / pull data 모델로 실행한다.

> **구성**: 헤드리스 엔진 (`VSMVVM.Core.Scheduler`) + Roslyn 동적 컴파일 (`VSMVVM.Core.Scheduler.Scripting`, 선택) + WPF UI 컨트롤 (`VSMVVM.WPF.Scheduler`) + AvalonEdit 코드 에디터 (`VSMVVM.WPF.Scheduler.Editor`).

### 설치

```bash
# 그래프 모델 + 실행 엔진만 (Roslyn 의존성 없음)
dotnet add package VSMVVM.Core.Scheduler

# 사용자 코드 노드(C# 동적 컴파일)까지 활성화
dotnet add package VSMVVM.Core.Scheduler.Scripting
```

### 핵심 개념

| 개념 | 설명 |
| --- | --- |
| **NodeGraph** | 노드 + 연결(NodeConnection) + 레이아웃 컨테이너. `Changed` 이벤트로 에디터 동기화. |
| **Pin (Exec / Data)** | Blueprint의 흰 핀(제어 흐름) + 타입별 색상 데이터 핀. `PinKind.Exec` / `PinKind.Data`. |
| **PinCompatibility** | 방향 반대 + 동일 Kind + 타입 호환성 검증. |
| **ConnectionRules (N:M)** | exec/data-output은 1:N 무제한 · data-input은 1:1(기존 연결 자동 disconnect). |
| **INode / NodeBase** | `ExecuteAsync(ctx) -> ExecutionFlow` 반환. `[Node]` 속성 + Source Generator가 핀 디스크립터 자동 emit. |
| **ExecutionContext** | 그래프 실행 컨텍스트. `GetInput<T>(node, pinId)` / `SetOutput<T>(...)` + DataCache (1 tick 내 풀 결과 캐시). |
| **SchedulerService** | 메인 실행 루프. push exec / pull data, breakpoint 게이트, MaxNodesExecuted / PerNodeTimeoutMs / GraphTimeout 가드. |
| **ICompilationService** | 사용자 C# 코드 → 어셈블리 동적 컴파일 추상화. Core.Scheduler에는 인터페이스만, 구현은 `Scripting` 패키지. |

### Built-in 노드

| TypeId | 카테고리 | 역할 |
|---|---|---|
| `Core.Start` | Flow | 그래프 진입점. exec-out "Then". |
| `Core.End` | Flow | 흐름 종단. exec-in "In". |
| `Core.Sequence` | Flow | 순차 분기 (Then0/Then1/Then2 순서 보존). |
| `Core.Branch` | Flow | `bool Condition` → True/False 분기. |
| `Core.Delay` | Flow | `double Seconds` → 비동기 대기 후 Then. CancellationToken 존중. |
| `Core.Log` | Diagnostics | `Format` (string.Format 템플릿) + 동적 `Arg0..ArgN` (인스펙터 +/− 로 가변) → `ILoggerService.Info` + `ISchedulerLogSink`. |
| `Core.Output` | IO | `ctx.Outputs[Key]=Value` → `ExecutionResult.Outputs` 노출. |
| `Core.Input` | IO | `ctx.Inputs[Key]` 에서 외부 입력 값을 출력 핀으로 노출 (Default fallback). 그래프를 함수처럼 호출할 때 사용. |
| `Core.Assert` | Validation | false 면 `AssertionFailedException`. |
| `Core.Guard` | Validation | 비-throw 분기 (Pass/Fail exec). |
| `Core.RangeAssert` | Validation | Value < Min 또는 > Max → throw. |
| `Core.Constant` | Math | **다형성** (`Phase L`). 인스턴스의 `ItemType` 속성으로 출력 타입 결정. 인스펙터의 NODE PROPERTIES 섹션에서 변경. |
| `Core.Random.Int` / `Core.Random.Double` / `Core.Random.Bool` | Math | 시드 가능한 난수. |
| `Core.Toggle` | Flow | bool 상태 토글. |
| `Core.Variable.Get` / `Core.Variable.Set` | Variables | **다형성** (`Phase K`). 인스턴스의 `ItemType` + `VariableName` (그래프 변수 lookup). |
| `Core.List.Add` / `Get` / `Count` / `Clear` / `Contains` / `RemoveAt` | Collections | **다형성** `List<T>` 조작. 인스턴스의 `ItemType` 으로 T 결정. |

### Attribute API (사용자 노드 정의)

```csharp
using VSMVVM.Core.Scheduler.Attributes;
using VSMVVM.Core.Scheduler.Nodes;

[Node("MyApp.Add", DisplayName = "Add", Category = "Math")]
public partial class AddNode : NodeBase
{
    [ExecInputPin]  public object In { get; set; }
    [ExecOutputPin] public object Then { get; set; }

    [InputPin(DefaultValue = 0)] public int A { get; set; }
    [InputPin(DefaultValue = 0)] public int B { get; set; }
    [OutputPin] public int Sum { get; set; }

    public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext ctx)
    {
        var a = ctx.GetInput<int>(this, nameof(A));
        var b = ctx.GetInput<int>(this, nameof(B));
        ctx.SetOutput(this, nameof(Sum), a + b);
        return Task.FromResult(ExecutionFlow.Continue(nameof(Then)));
    }
}
```

Source Generator가 자동으로 emit하는 것:

1. **노드별 partial** — `override string TypeId`, `__VsmvvmPins` 정적 배열, `override GetPinDescriptors`.
2. **어셈블리당 1개** `__VsmvvmNodeRegistration.g.cs` — `[ModuleInitializer]`로 `NodeMetadataRegistry.Register(...)` 자동 호출.
3. **netstandard2.0 폴리필** — `ModuleInitializerAttribute` internal 정의 (`#if !NET5_0_OR_GREATER`).

#### `[MethodNode]` / `[ParameterNode]` — 동적 노드 등록

사용자 코드 (Roslyn 컴파일 또는 정적 어셈블리) 의 **메서드와 정적 필드**를 자동으로 노드로 노출. `[Node]` 가 클래스 레벨이라면 본 두 어트리뷰트는 **메서드 / 필드 레벨**.

```csharp
using VSMVVM.Core.Scheduler.Attributes;

public static class PlayerStats
{
    // 메서드 → 함수 노드. 매개변수가 입력 핀, 반환값이 출력 핀("Result").
    [MethodNode("Demo.Player.FormatReport", Category = "PlayerStats")]
    public static string FormatReport(string name, int score, bool isWinner)
        => $"{name}: {score} pts{(isWinner ? " 🏆" : "")}";

    // 정적 필드 → 파라미터 노드 (출력 핀 "Out" 1개, 매 EvaluateAsync 마다 필드의 현재 값 흘림).
    // 어셈블리에 상주하므로 그래프 실행 사이에 값이 유지 — 전역 변수 처럼 동작.
    [ParameterNode("Demo.Player.Name", Category = "PlayerStats")]
    public static string Name = "Alice";

    [ParameterNode("Demo.Player.Score", Category = "PlayerStats")]
    public static int Score = 1250;
}

// 호스트 어셈블리에서 한 줄로 일괄 스캔 + 등록.
CustomNodeFactory.RegisterFromAssembly(typeof(PlayerStats).Assembly);
```

`CustomNodeFactory.RegisterFromAssembly` 가 양쪽 어트리뷰트를 모두 탐지:
- 매개변수 0개 메서드 → `CustomConstantNode` 로 래핑 (pull 평가).
- 매개변수 ≥1 메서드 → `CustomFunctionNode` 로 래핑 (Exec 흐름 + 매개변수당 데이터 입력 핀).
- `[ParameterNode]` 정적 필드 → `CustomParameterNode` 로 래핑 (Out 1개, pull 평가).
- 인스턴스 필드는 silent skip.

#### 다형성 핀 (Phase K~L)

`List<T>` / `Variable<T>` / `Constant<T>` 류 노드는 **단일 typeId** 로 등록되고, 인스턴스의 `ItemType` 속성으로 T 가 결정됩니다. 팔레트는 `List.Add` 1개만 보여주고 (`List<int>.Add`, `List<double>.Add` 같은 N 배 폭주 X), 인스펙터의 NODE PROPERTIES 섹션에서 ItemType 드롭다운으로 변경.

```csharp
// 코드에서 명시적으로 만드는 경우:
var add = (ListAddNode)g.AddNode(ListAddNode.TypeIdConst, x, y);
add.ItemType = typeof(double);                  // 핀이 List<double> / double 로 재빌드.

var setVar = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, x, y);
setVar.ItemType = typeof(Mat);                   // 사용자 정의 타입도 가능.
setVar.VariableName = "src";                     // 그래프 변수 이름.
```

핵심 인터페이스:
- `IPolymorphicNode.TypeArguments` — `"T"` → 실제 CLR 타입 매핑. `NodeBase.BuildPins` 가 다형성 핀의 placeholder ValueType 을 이 매핑으로 치환.
- `INodeInstancePropertyHost.GetInstanceProperties()` — 인스펙터의 NODE PROPERTIES 섹션에 노출할 속성 목록 (ItemType / VariableName 같은 임의 속성).

### 그래프 실행

```csharp
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;

// 그래프 작성: Start --Then--> Branch --True--> ... --False--> ...
var graph = new NodeGraph();
var start  = graph.AddNode(StartNode.TypeIdConst,  100, 100);
var branch = graph.AddNode(BranchNode.TypeIdConst, 300, 100);
var endA   = graph.AddNode(EndNode.TypeIdConst,    500,   0);
var endB   = graph.AddNode(EndNode.TypeIdConst,    500, 200);

graph.Connect(start.Id, "Then",  branch.Id, "In");
graph.Connect(branch.Id, "True",  endA.Id, "In");
graph.Connect(branch.Id, "False", endB.Id, "In");

// 실행
var ctx = new ExecutionContext(graph, cancellationToken);
ctx.MaxNodesExecuted = 100_000;          // 무한 루프 가드
ctx.PerNodeTimeoutMs  = 5_000;            // 노드별 타임아웃 (ms)
var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

// result.Status / result.NodesExecuted / result.Elapsed / result.Error
```

### N:M 연결 규칙

| 핀 종류 | 출력 측 | 입력 측 |
|---|---|---|
| **Exec** | 1:N (한 exec-out → 여러 노드, 등록 순서대로 발화) | N:1 (여러 곳에서 같은 노드로 합류) |
| **Data** | 1:N (한 출력값 → 여러 소비자, DataCache로 1번만 평가) | 1:1 (한 입력은 한 소스 — 새 연결 시 기존 자동 disconnect) |

### 런타임 메시지 (`IMessenger`로 발화)

- `NodeEnteringMessage` — 노드 실행 직전 (디버거 하이라이트)
- `NodeExitedMessage` — 노드 종료 + `Elapsed` (프로파일링)
- `BreakpointHitMessage` — 브레이크포인트 발동
- `GraphCompletedMessage` — 실행 완료(성공/실패/취소)
- `GuardTriggeredMessage` — 시스템 가드(MaxNodes/Timeout/Memory) 발동

### 사용자 코드 노드 (Scripting 패키지)

`Microsoft.CodeAnalysis.CSharp`(Roslyn)을 통해 런타임에 C# 소스를 컴파일하고 collectible `AssemblyLoadContext`로 격리 로드. 호스트 어셈블리(OpenCvSharp, ML.NET, 사용자 도메인 모델 등 이미 로드된 모든 DLL)를 **자동 수집**하므로 사용자 코드에서 즉시 `using` 가능.

```csharp
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.Core.Scheduler.Scripting;

ICompilationService svc = new RoslynCompilationService();

var source = @"
namespace UserCode {
    public static class MyMath {
        public static int Add(int a, int b) => a + b;
    }
}";

var options = new CompilationOptions
{
    AssemblyName = "UserAdd",
    AutoCollectHostReferences = true,   // 호스트 로드 어셈블리 자동 참조
};
options.ImplicitUsings.Add("System");

var result = svc.Compile(source, options);
if (!result.Success)
{
    foreach (var d in result.Diagnostics) Console.WriteLine(d);
    return;
}

var addMethod = result.Assembly.GetType("UserCode.MyMath").GetMethod("Add");
var sum = (int)addMethod.Invoke(null, new object[] { 17, 25 });   // → 42

// 핫리로드 시: 어셈블리 언로드 후 다음 컴파일
svc.UnloadAssembly(result.Assembly);
```

에디터 인라인 진단(빨간 물결 밑줄용)에는 emit이 필요 없는 `Analyze()`를 사용 — 빠르고 디버그 정보 없이 진단만 반환.

### Custom Function Node (메소드 → 노드 자동 등록)

위에서 컴파일한 어셈블리의 메소드를 **그래프 노드로 즉시 등록**할 수 있다. `[MethodNode]` 속성을 부착한 메소드는 `SignatureToPinsBuilder`가 파라미터/반환 시그니처에서 자동으로 핀을 추출하고, `CustomFunctionNode`가 reflection으로 호출한다.

```csharp
// 사용자 코드 (Roslyn으로 컴파일하거나 호스트 어셈블리에 정적 정의)
namespace UserCode {
    using VSMVVM.Core.Scheduler.Attributes;
    public static class UserMath {
        [MethodNode("User.Add", Category = "Math")]
        public static int Add(int a, int b) => a + b;

        [MethodNode("User.MulAsync", Category = "Math")]
        public static async Task<int> MulAsync(int a, int b) {
            await Task.Yield();
            return a * b;
        }
    }
}

// 컴파일된 어셈블리를 NodeMetadataRegistry에 일괄 등록
var compResult = new RoslynCompilationService().Compile(sourceCode, options);
CustomNodeFactory.RegisterFromAssembly(compResult.Assembly);

// 이제 그래프에서 TypeId로 바로 사용 가능
var add = graph.AddNode("User.Add", 0, 0);
graph.Connect(producerA.Id, "Value", add.Id, "a");
graph.Connect(producerB.Id, "Value", add.Id, "b");
// add.Result 핀에 a + b 결과가 들어감
```

자동 핀 추출 규칙 (`SignatureToPinsBuilder`):

- **exec-in "In"** + **exec-out "Then"** 자동 추가
- 메소드 파라미터 → 데이터 입력 핀 (이름=파라미터명, 타입=파라미터형, `[DefaultParameterValue]` 존중)
- 반환값 → 데이터 출력 핀 **"Result"**
  - `void` / `Task` / `ValueTask` → "Result" 생략 (exec-out만)
  - `Task<T>` / `ValueTask<T>` → `T`로 unwrap
- `ref` / `out` / `in` / 포인터 파라미터 → `NotSupportedException` (등록 스킵)

`CustomNodeFactory.Options`로 동작 조정:

| 옵션 | 의미 |
| --- | --- |
| `RegisterPublicStaticMethods` | `[MethodNode]` 미부착이라도 `public static` 메소드 자동 등록 |
| `AutoTypeIdPrefix` | 자동 등록 시 TypeId 접두사 (충돌 방지). 예: `"Auto"` → `"Auto.UserMath.Square"` |
| `InstanceFactory` | 인스턴스 메소드 등록 시 대상 객체를 생성하는 콜백 (`Type` → `object`). null이면 인스턴스 메소드 스킵 |

### WPF 에디터 기반 (Phase 5)

`VSMVVM.WPF.Scheduler` 패키지 — 노드 그래프를 시각적으로 표시/편집할 수 있는 WPF 컨트롤 모음. 기존 `VSMVVM.WPF.Design`의 토큰(Spacing P*/M*, CornerRadius Rounded*, 색상 BgPrimary/TextPrimary/BorderDefault/...)을 그대로 사용해 다크/라이트 테마 전환이 자동 추종된다.

```bash
dotnet add package VSMVVM.WPF.Scheduler
```

```xml
<!-- App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/VSMVVM.WPF.Design;component/Index.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

```xml
<!-- MainWindow.xaml -->
<scheduler:NodeGraphCanvas
    xmlns:scheduler="clr-namespace:VSMVVM.WPF.Scheduler.Controls;assembly=VSMVVM.WPF.Scheduler"
    Graph="{Binding Graph}" />
```

```csharp
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.WPF.Scheduler.ViewModels;

var graph = new NodeGraph();
var start = graph.AddNode(StartNode.TypeIdConst, 100, 100);
var end = graph.AddNode(EndNode.TypeIdConst, 400, 100);
graph.Connect(start.Id, "Then", end.Id, "In");

var graphVm = new NodeGraphViewModel(graph);
// graph.AddNode / RemoveNode / Connect / Disconnect / MoveNode 호출 시
// graphVm.Nodes / Connections 가 자동 동기화됨.
```

구성 요소:

| 컴포넌트 | 역할 |
| --- | --- |
| `NodeGraphCanvas` | 4-layer (grid/connection/node/preview) z-스택 + 우클릭 팬 + 휠 줌(피벗 보정). [`IZoomPanViewport`](src/VSMVVM.WPF/Controls/Canvas/IZoomPanViewport.cs) 구현으로 기존 `MiniMapControl`과 호환 |
| `NodeView` | 노드 카드 (헤더 + 좌측 입력 / 우측 출력 핀). 헤더 드래그 → `NodeViewModel.X/Y` 갱신 |
| `PinView` | Exec(흰색 삼각형) / Data(`PinTypeBrushConverter`로 ValueType별 색상 원) + 라벨 |
| `ConnectionView` | 두 끝점 사이 큐빅 베지어 (`StreamGeometry` + `OnRender`) |
| `NodeGraphViewModel` | `NodeGraph.Changed` 이벤트 구독 → `ObservableCollection<NodeViewModel>` / `<ConnectionViewModel>` 동기화 + 5개 RelayCommand (AddNode/RemoveSelected/Connect/Disconnect/MoveNode) |
| `NodeViewModel` | X/Y/IsSelected/IsExecuting/HasBreakpoint + 입력/출력 핀 컬렉션 |
| `PinViewModel` | 미연결 데이터 입력의 사용자 리터럴 값을 보유 — 변경 시 `NodeBase.LiteralInputs`로 자동 전파(직렬화에 보존됨) |
| `ConnectionViewModel` | 양 끝점이 소스/타겟 노드의 X/Y 변화에 따라 자동 재계산 |
| `INodePaletteService` / `NodePaletteService` | `NodeMetadataRegistry`를 카테고리별로 그룹화 — 컨텍스트 메뉴/팔레트 사이드바 모두에서 사용 가능 |
| `NodeGraphMiniMap` | 기존 `MiniMapControl` 상속. 노드 union + 10% 패딩으로 자동 콘텐츠 바운드 + 노드를 작은 사각형으로 렌더링 |
| `PinConnectionRoutedEvents` | `PinView.MouseLeftButtonDown/Up` 시 bubble되는 라우티드 이벤트 → `NodeGraphCanvas`가 받아 프리뷰 베지어 + 연결 완료 처리 |
| `NodeGraphCanvas.ContextRequested` | 우클릭 (이동 없이) 시 발화. 캔버스 좌표를 인자로 받아 사용자가 컨텍스트 메뉴/팔레트 표시 가능 |

핀 타입 → 색상 매핑 (`PinTypeBrushConverter`):

| 타입 | Tailwind 토큰 / 폴백 |
| --- | --- |
| `bool` | `Red500Brush` / `#EF4444` |
| `int`, `long`, `short`, `byte` (and unsigned) | `Cyan500Brush` / `#06B6D4` |
| `float`, `double`, `decimal` | `Green500Brush` / `#22C55E` |
| `string` | `Fuchsia500Brush` / `#D946EF` |
| `Guid`, `DateTime`, `DateTimeOffset` | `Violet500Brush` / `#8B5CF6` |
| 그 외 | `Zinc500Brush` / `#71717A` |

### 에디터 UX (Phase 6)

#### Undo/Redo 자동 와이어링

`NodeGraphViewModel`이 `IUndoRedoService`를 옵션으로 받으면 모든 그래프 편집 작업이 자동으로 undo 스택에 기록된다.

```csharp
using VSMVVM.WPF.Services;

var undo = new UndoRedoService { Capacity = 100 };
var vm = new NodeGraphViewModel(graph, undo);

graph.AddNode(StartNode.TypeIdConst, 0, 0);
graph.Connect(start.Id, "Then", end.Id, "In");
undo.CanUndo.Should().BeTrue();
undo.Undo(); // 마지막 작업 되돌리기
undo.Redo(); // 다시 적용
```

기록되는 작업: `AddNode` / `RemoveNode` / `Connect` / `Disconnect` / `MoveNode`. Undo/Redo 자체가 그래프를 변경할 때는 자동으로 push를 suppress한다 (무한 루프 방지). PoC 단순화로 노드 제거 시 부산물로 disconnect된 연결은 복원 안 됨 — 추후 phase에서 JSON 스냅샷 기반 정밀 복원으로 강화.

#### 노드 팔레트

`INodePaletteService`로 `NodeMetadataRegistry`의 등록 항목을 카테고리별로 노출. 컨텍스트 메뉴, 사이드바, 검색 결과 등 동일 데이터를 여러 UX에서 재사용.

```csharp
INodePaletteService palette = new NodePaletteService();
foreach (var category in palette.GetCategories())
{
    Console.WriteLine($"== {category.Name} ==");
    foreach (var entry in category.Entries)
        Console.WriteLine($"  {entry.TypeId} — {entry.DisplayName}");
}
// == Diagnostics ==   Core.Log — Log
// == Flow ==          Core.Branch — Branch,  Core.Delay — Delay,  Core.End — End,  Core.Sequence — Sequence,  Core.Start — Start
```

#### 컨텍스트 메뉴 진입점

`NodeGraphCanvas.ContextRequested` 이벤트는 우클릭 후 이동 거리 < 5px일 때만 발화 (5px 이상은 팬으로 간주). 캔버스 좌표를 인자로 받아 `Popup`/`ContextMenu`/`Window`로 팔레트를 띄울 수 있다.

```csharp
canvas.ContextRequested += (s, e) =>
{
    // e.CanvasPoint = 줌/팬 적용 전 콘텐츠 좌표 — 노드 추가 시 그 위치에 배치
    ShowPalettePopup(e.CanvasPoint);
};
```

#### 핀 드래그 연결

`PinView`가 마우스 다운/업 시 두 가지 라우티드 이벤트(`PinConnectionRoutedEvents.ConnectionDragStartedEvent` / `ConnectionDragCompletedEvent`)를 bubble로 발화하고, `NodeGraphCanvas`가 자동으로 듣고 처리. 출력 핀에서 드래그를 시작하여 입력 핀에서 놓으면 `Graph.ConnectCommand`가 자동 호출된다. 드래그 중 프리뷰 베지어는 `_previewLayer`에 임시 표시.

#### 미니맵

```xml
<scheduler:NodeGraphMiniMap
    Graph="{Binding Graph}"
    Width="200" Height="120"/>
```

콘텐츠 바운드는 노드 union + 10% 패딩으로 자동 계산 (Phase 7에서 그래프 변경 자동 추종 추가). 노드를 작은 사각형으로 그리며 (`AccentSecondary` 채움 + `BorderFocus` 테두리), 기존 `MiniMapControl`의 뷰포트 사각형 표시는 그대로 상속.

### 디버깅 UI (Phase 7)

`NodeGraphViewModel` 생성 시 `ISchedulerService` + `IMessenger` 를 함께 주입하면 그래프 실행 / 일시정지 / 단계 진행 / 브레이크포인트가 자동으로 ViewModel 상태에 반영된다.

```csharp
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.WPF.Scheduler.ViewModels;

var messenger = new Messenger();
var scheduler = new SchedulerService();
var vm = new NodeGraphViewModel(graph,
                                undoRedo: undoSvc,
                                scheduler: scheduler,
                                messenger: messenger);
```

자동 구독되는 메시지:

| 메시지 | ViewModel 반응 |
| --- | --- |
| `NodeEnteringMessage` | 해당 `NodeViewModel.IsExecuting = true` |
| `NodeExitedMessage` | 해당 `NodeViewModel.IsExecuting = false` |
| `BreakpointHitMessage` | `NodeGraphViewModel.IsPaused = true` |
| `GraphCompletedMessage` | `IsRunning = false` + `IsPaused = false` + 모든 노드 `IsExecuting = false` |

노출되는 커맨드:

| 커맨드 | CanExecute |
| --- | --- |
| `RunCommand` (`AsyncRelayCommand`) | `Scheduler != null && !IsRunning && SelectedNode != null` — 선택 노드를 진입점으로 비동기 실행 |
| `StopCommand` | `IsRunning` — 내부 `CancellationTokenSource` 취소 |
| `ContinueCommand` | `IsPaused` — `Scheduler.Continue()` 호출 후 `IsPaused = false` |
| `StepOverCommand` | `IsPaused` — `Scheduler.StepOver()` 호출 |
| `ToggleBreakpointCommand` | 항상 — 대상 노드(인자 또는 `SelectedNode`)의 `HasBreakpoint` 토글 + `Scheduler.ToggleBreakpoint(nodeId)` 호출 |

ViewModel 상태가 바뀌면 `NodeView` 템플릿이 자동 시각화:

- **`IsExecuting`** → 보더 색을 `Warning`(Amber) 로, 두께 2 + `DropShadowEffect` (BlurRadius 14, Opacity 0.8) 글로우
- **`HasBreakpoint`** → 헤더 좌측에 8×8 `Error`(Red) 원 (Tooltip "Breakpoint")
- **`IsSelected`** → 보더를 `BorderFocus`(Blue400) 로, 두께 2

#### `NodeGraphToolbar`

`Run` / `Stop` / `Continue` / `Step Over` / `Toggle Breakpoint` 버튼을 한 줄에 배치. `IsRunning` / `IsPaused` 에 따라 적절한 버튼만 표시 (Run ↔ Stop, Continue/Step ↔ 숨김).

```xml
<DockPanel>
    <scheduler:NodeGraphToolbar DockPanel.Dock="Top" DataContext="{Binding Graph}"/>
    <scheduler:NodeGraphCanvas    Graph="{Binding Graph}"/>
</DockPanel>
```

### 코드 에디터 (Phase 8a)

`VSMVVM.WPF.Scheduler.Editor` 패키지 — AvalonEdit를 호스팅한 `CodeNodeEditor` UserControl과 `VSMVVM.WPF.Design` 팔레트 톤(400/600·700)을 그대로 매핑한 XSHD 신택스 정의를 제공한다. 사용자 코드 노드(Phase 3c) 의 소스 편집을 위한 표준 입력 컨트롤.

```bash
dotnet add package VSMVVM.WPF.Scheduler.Editor
```

```xml
<editor:CodeNodeEditor
    xmlns:editor="clr-namespace:VSMVVM.WPF.Scheduler.Editor.Controls;assembly=VSMVVM.WPF.Scheduler.Editor"
    SourceCode="{Binding UserCode, Mode=TwoWay}"
    Theme="Dark"
    IsReadOnly="False"
    Diagnostics="{Binding LastDiagnostics}"
    Height="320"/>
```

`CodeNodeEditor` 공개 DP/이벤트:

| 멤버 | 타입 | 역할 |
| --- | --- | --- |
| `SourceCode` | `string` (TwoWay) | 사용자 코드 본문 — 양방향 바인딩 |
| `Theme` | `EditorTheme` (`Dark`/`Light`) | 신택스 하이라이팅 테마 — 변경 시 즉시 XSHD 교체 |
| `IsReadOnly` | `bool` | 읽기 전용 토글 |
| `Diagnostics` | `IReadOnlyList<CompilationDiagnostic>` | Roslyn 진단 컬렉션 — Phase 8b의 인라인 어도너에서 소비 |
| `SourceCodeCommitted` event | `EventHandler<string>` | 에디터 포커스 손실 시 발화 — debounced 컴파일 트리거 등에 사용 |
| `InnerEditor` | `ICSharpCode.AvalonEdit.TextEditor` | 폴딩/캐럿 등 고급 기능을 외부에서 직접 조작할 때 노출 |

신택스 하이라이팅 (`HighlightingThemeService`):

- 두 XSHD(`CSharp-Dark.xshd` / `CSharp-Light.xshd`)는 어셈블리에 embedded resource로 패킹되어 `GetManifestResourceStream` 으로 로드
- 첫 호출 시 캐시 → 후속 호출은 같은 인스턴스 반환
- Dark: Tailwind 400 톤(Blue400 키워드 / Amber400 타입 / Emerald400 숫자 / Violet400 문자열 / Sky300 컨텍스트 키워드 / Orange400 전처리)
- Light: 600/700 톤으로 자동 대비 (Blue700 키워드 / Amber700 타입 / Green700 숫자 등)
- 호스트 앱이 다크/라이트 토글을 갖고 있다면 `editor.Theme = EditorTheme.Light;` 1줄로 즉시 추종

`Themes/Generic.xaml` 에서 `VSMVVM.WPF.Design;component/Index.xaml` 를 머지하므로 `TextEditor`/`TextArea` 기본 스타일이 `BgPrimary`/`TextPrimary`/`BorderDefault`/`AccentPrimaryMuted` 등 시맨틱 토큰을 자동 사용.

#### 인라인 진단 + 현재 줄 + 브레이크포인트 마진 (Phase 8b)

`CodeNodeEditor`가 자동으로 세 개의 어도너/마진을 설치한다:

| 어도너/마진 | 역할 | Severity별 색상 (디자인 토큰) |
| --- | --- | --- |
| `DiagnosticUnderlineRenderer` (BG `Selection` 레이어) | `Diagnostics` DP의 각 항목 위치에 sine-wave 물결 밑줄 | `Error` → Error(Red500) · `Warning` → Warning(Amber500) · `Info` → Info(Sky500) |
| `CurrentLineHighlighter` (BG `Background` 레이어) | 현재 캐럿 줄 배경 반투명 강조 | `BgSecondary` (Zinc900 35% 투명) |
| `BreakpointMargin` (좌측 마진) | 줄 번호 좌측 클릭으로 토글 + 활성 줄에 빨간 원 | `Error` (Red500) |

추가된 DP/이벤트:

| 멤버 | 역할 |
| --- | --- |
| `Breakpoints` DP | `IReadOnlyCollection<int>` — 활성 브레이크포인트 줄 번호. DP 변경 시 마진 자동 갱신 |
| `BreakpointToggled` 이벤트 | `EventArgs(LineNumber, IsSet)` — 사용자가 마진 클릭으로 토글 시 발화 (DP도 동시에 갱신) |
| `ShowCurrentLineHighlight` DP | 기본 `true` |
| `ShowBreakpointMargin` DP | 기본 `true` — `false`로 두면 마진 자동 제거 |

사용 흐름:

```csharp
// 컴파일 → 진단을 에디터에 표시
var result = compiler.Compile(editor.SourceCode, opts);
editor.Diagnostics = result.Diagnostics;     // 즉시 빨간 물결 밑줄

// 사용자가 마진 클릭으로 브레이크포인트 토글
editor.BreakpointToggled += (s, e) =>
{
    Console.WriteLine($"Line {e.LineNumber} breakpoint = {e.IsSet}");
};

// 또는 외부에서 DP로 일괄 설정
editor.Breakpoints = new[] { 3, 14, 27 };
```

진단 색상은 모두 `TryFindResource("Error"/"Warning"/"Info")` 로 시맨틱 토큰을 먼저 찾고, 없으면 Tailwind 폴백 RGB로 자동 대응 — 호스트가 다크/라이트 테마를 바꾸면 자동으로 추종된다.

#### Roslyn 자동완성 (Phase 8c)

`CodeNodeEditor`가 `EnableCompletion = true` (기본값) 상태에서 두 가지 트리거로 자동완성 팝업을 띄운다:

| 트리거 | 동작 |
| --- | --- |
| `.` 입력 | 멤버 접근 자동완성 (예: `Math.` → Min/Max/Sqrt/...) |
| **Ctrl+Space** | 명시 호출 — 키워드/타입/네임스페이스/지역 변수까지 광범위 |

내부 구조:

- `RoslynCompletionProvider` — `AdhocWorkspace` 안에 단일 C# 프로젝트를 보관하고 호출마다 `Document.WithText(...)` 로 텍스트 갱신
- `MetadataReference` 는 호스트 `AppDomain.CurrentDomain.GetAssemblies()` 에서 자동 수집 — 사용자가 호스트에 로드된 모든 라이브러리(OpenCvSharp 등)를 즉시 자동완성 받음
- 결과는 `RoslynCompletionItem` (DisplayText/InsertionText/Description/Kind) POCO로 매핑 → `AvalonCompletionData` 어댑터를 통해 AvalonEdit `CompletionWindow` 에 주입
- `CompletionWindow` / `CompletionList` / 항목 ListBoxItem 은 `Themes/CompletionWindow.xaml` 에서 `BgSecondary` / `BgTertiary` (hover) / `AccentPrimaryMuted` (selection) 토큰을 사용 — 다크/라이트 테마 자동 추종

```csharp
// 자동완성 끄려면
editor.EnableCompletion = false;
```

### 멀티 탭 / 사용자 코드 전역 흐름 (Phase M)

샘플 앱 (`SchedulerDemoViewModel` 컨테이너) 의 사용 패턴:

- **그래프 (`NodeGraph`) + 그래프 변수 (`NodeGraph.Variables`)** — 워크스페이스 (탭) 단위. 각 탭이 자기 그래프 + 변수 보관.
- **사용자 코드 조각 (`UserCodeSnippet`) + 컴파일 + `NodeMetadataRegistry` 등록** — **컨테이너 단위 전역**. `NodeMetadataRegistry` 가 프로세스 전역 싱글톤이므로 워크스페이스 단위로 두면 typeId 충돌 발생.
- **저장/복원** — 그래프 JSON 은 워크스페이스의 `Save JSON` / `Load JSON` (그래프 + 변수만). 사용자 코드 JSON 은 컨테이너의 `Save Code…` / `Load Code…` (디폴트 경로 `%APPDATA%/VSMVVM.WPF.Sample/userCode.json`, `Dictionary<string,string>` 포맷). 운영 흐름은 *Load Code → Load Graph* 순서 (typeId 가 먼저 등록되어 있어야 노드 복원 성공).
- **데모 (`LoadOpenCvDemo` / `LoadCollectionDemo` / `LoadParameterDemo` / `LoadRandomDemo` / **`LoadIrisDemo`** / **`LoadMnistDemo`**)** — 컨테이너의 `Snippets` 에 코드 조각을 주입 + `CompileAllSnippetsCommand` 실행 → 자기 워크스페이스의 그래프 빌드. Iris/MNIST 데모는 **`Core.Repeat` 노드 + `Core.Delay` + `ChartViewNode`** 조합으로 매 에폭 실시간 학습 곡선 (LineChart) / 혼동행렬 (ConfusionMatrix) / 입력 미리보기 (Heatmap) 를 별도 창에 스트리밍. MNIST 는 `MnistDataLoader` 로 `%LOCALAPPDATA%/VSMVVM/Data/mnist/` CSV 자동 다운로드/캐시.

인스펙터의 NODE PROPERTIES 섹션 (Phase K~M) 이 다형성 노드의 `ItemType` 과 Variable 노드의 `VariableName` 을 ComboBox 로 노출. Variable 드롭다운 마지막에 항상 `+ Add new variable…` 항목이 있어 즉시 변수 추가 가능 (Sample 은 `VariablesManagerWindow` 모달 호출, 기본 구현은 자동 `var{N}` 추가).

### Serialization (JSON 라운드트립)

`System.Text.Json` 기반의 `NodeGraphSerializer`로 그래프 ↔ JSON 양방향 변환. PoC가 그래프를 디스크에 저장/로드하고 나중에 동일 결과로 재실행할 수 있다.

```csharp
using VSMVVM.Core.Scheduler.Serialization;

// 직렬화
var json = NodeGraphSerializer.Serialize(graph);
File.WriteAllText("workflow.vsg.json", json);

// 또는 Stream API
using (var fs = File.Create("workflow.vsg.json"))
    NodeGraphSerializer.Save(graph, fs);

// 역직렬화
var restored = NodeGraphSerializer.Deserialize(File.ReadAllText("workflow.vsg.json"));
// 또는: using var fs = File.OpenRead(...); var restored = NodeGraphSerializer.Load(fs);
```

보존되는 항목:

- 노드 (`Id`, `TypeId`, 위치 `X`/`Y`)
- 연결 (`Id`, source/target node+pin, `Kind`)
- **미연결 데이터 입력 핀의 사용자 리터럴 값** — `NodeBase.SetLiteralInput(pinId, value)` / `LiteralInputs` 딕셔너리. ExecutionContext가 GetInput 풀 시 핀의 DefaultValue보다 우선
- 노드 인스턴스 상태 (`NodeBase.WriteState(Utf8JsonWriter)` / `ReadState(JsonElement)` 후크)

전제:

- Deserialize 호출 전 `NodeMetadataRegistry`에 해당 `TypeId`가 등록되어 있어야 함. 빌트인 노드는 Serializer가 자동으로 `BuiltInNodes.EnsureRegistered()` 호출하며, 사용자 노드(Source Generator) 는 ModuleInitializer가, 동적 컴파일된 메소드 노드는 호출자가 `CustomNodeFactory.RegisterFromAssembly(...)`를 먼저 호출해야 함

마이그레이션 추적:

- `$schema=1` 필드를 루트에 항상 emit. 미래 버전 차이 시 `UnsupportedSchemaException` (현재는 1만 허용)
- 알 수 없는 TypeId → `UnknownNodeTypeException(typeId, nodeId)`

JSON 예시 (Branch 노드 1개):

```jsonc
{
  "$schema": 1,
  "id": "...uuid...",
  "name": "BranchSample",
  "nodes": [
    {
      "id": "...uuid...",
      "typeId": "Core.Branch",
      "x": 300,
      "y": 50,
      "inputs": { "Condition": true }
    }
  ],
  "connections": [ /* ... */ ]
}
```

### 모듈 구성

```
src/VSMVVM.Core.Scheduler/
├── Attributes/                  # [Node], [ExecInputPin], [ExecOutputPin], [InputPin], [OutputPin], [MethodNode]
├── Pins/                        # IPin, ExecPin, DataPin<T>, PinDirection/Kind, PinCompatibility
├── Nodes/                       # INode, NodeBase, ExecutionFlow, NodeMetadata(Registry), PinDescriptor,
│   │                            # SignatureToPinsBuilder, CustomFunctionNode, CustomNodeFactory,
│   │                            # UserCodeCategoryExtractor (멀티 조각 에디터용 — [MethodNode(Category=...)] 빈도 집계)
│   └── BuiltIn/                 # StartNode, EndNode, SequenceNode, BranchNode, DelayNode, LogNode (Format + 가변 Args),
│                                # OutputNode (Outputs[Key]=Value), InputNode (Inputs[Key]→Value, IO 카테고리),
│                                # AssertNode, GuardNode, RangeAssertNode (I.2c + I.5)
├── Graph/                       # NodeGraph, NodeConnection, NodeLayout, GraphChange, ConnectionRules
├── Runtime/                     # ExecutionContext, ISchedulerService, SchedulerService, ExecutionResult,
│                                # SchedulerMessages, ExecutionStatus, SchedulerExceptions (+ Assertion/Memory),
│                                # ProfilingStats (I.3), ExecutionRun + NodeExecutionRecord (I.2b),
│                                # IExecutionHistoryStore + InMemoryExecutionHistoryStore (I.2b),
│                                # SchedulerLogEntry + ISchedulerLogSink + InMemorySchedulerLogSink (I.4)
├── Compilation/                 # ICompilationService, CompilationOptions/Result/Diagnostic (POCOs)
├── Serialization/               # NodeGraphSerializer, NodeGraphDto, NodeGraphJsonOptions, SerializationExceptions
└── CodeGen/                     # NodeRegistrationCodeGen (IIncrementalGenerator)

src/VSMVVM.Core.Scheduler.Scripting/
├── CollectibleAssemblyLoadContext.cs
├── MetadataReferenceProvider.cs
└── RoslynCompilationService.cs

src/VSMVVM.WPF.Scheduler/         # .NET 8 / .NET 9 (WPF) — Phase 5 + 6 + 7 + I.2a/b + I.3 + I.4
├── Controls/                     # NodeGraphCanvas, NodeView.xaml, PinView.xaml, ConnectionView,
│                                 # NodeGraphMiniMap, NodeGraphToolbar.xaml,
│                                 # NodeInspectorPanel.xaml (I.2a — 인라인 핀 값 인스펙터),
│                                 # SchedulerLogPanel.xaml (I.4 — 구조화된 로그 표시),
│                                 # ExecutionHistoryPanel.xaml + GanttChartView (I.2b — 누적 run + 간트)
├── Converters/                   # PinTypeBrushConverter, PinKindToVisibilityConverter, BooleanToVisibilityConverterSafe,
│                                 # LogLevelToBrushConverter (I.4)
├── Services/                     # INodePaletteService, NodePaletteService
├── ViewModels/                   # NodeGraphViewModel (+ HistoryStore/LogSink 옵션 주입),
│                                 # NodeViewModel (IsExecuting/HasBreakpoint/IsSelected + LastElapsed/IsSlow + LastInputs/LastOutputs),
│                                 # PinViewModel, ConnectionViewModel, PinValueSnapshot (I.2a),
│                                 # SchedulerLogViewModel (I.4), ExecutionHistoryViewModel (I.2b),
│                                 # UserCodeSnippet (멀티 조각 에디터의 한 행 — Category + SourceCode + LastCompile),
│                                 # GraphWorkspaceViewModel (멀티 탭 — 빈 그래프 + CodeSnippets + Variables +
│                                 #   HistoryStore/LogSink + Save/LoadJson + AddNodeFromPalette + CompileAllSnippets +
│                                 #   RefreshPaletteCategories — 단일 조각 컴파일 후 동기화용)
└── Themes/Generic.xaml           # VSMVVM.WPF.Design Index.xaml 머지 + 기본 NodeGraphCanvas 스타일

src/VSMVVM.WPF.Scheduler.Editor/  # .NET 8 / .NET 9 (WPF) — Phase 8a + 8b + 8c
├── Controls/                     # CodeNodeEditor.xaml (AvalonEdit 호스팅 + Diagnostics/Breakpoints/EnableCompletion DP)
├── Completion/                   # RoslynCompletionProvider (AdhocWorkspace + CompletionService),
│                                 # AvalonCompletionData 어댑터, RoslynCompletionItem POCO, CompletionItemKind enum
├── Renderers/                    # DiagnosticUnderlineRenderer, CurrentLineHighlighter, BreakpointMargin
└── Themes/                       # EditorTheme enum, HighlightingThemeService,
                                  # CSharp-Dark.xshd / CSharp-Light.xshd (embedded resources),
                                  # CompletionWindow.xaml (Phase 8c — CompletionWindow/CompletionList 토큰 스타일),
                                  # Generic.xaml (Design Index.xaml + CompletionWindow.xaml 머지)
```

---

## Sample

### `sample/VSMVVM.WPF.Sample`

전체 기능을 한 곳에서 확인할 수 있는 WPF 데모 애플리케이션.

```powershell
cd C:\workspace\_nuget\vsmvvm
dotnet run --project sample/VSMVVM.WPF.Sample
```

데모하는 기능:

- Source Generator (`[Property]`, `[RelayCommand]`, `[AsyncRelayCommand]`, `[Log]`) 동작
- DI 컨테이너 + Bootstrapper 패턴
- Region 네비게이션 + Back/Forward 히스토리
- Dialog / Window 서비스 (모달, 파일/폴더 다이얼로그)
- Splash + Progress Report
- Design 시스템 (Dark / Light 테마, 컴포넌트, 토큰)
- Localize (다국어)
- StateStore, Messenger
- Undo/Redo, Shortcut, Zoom
- **Scheduler Demo (Phase 9 PoC + 9a)** — 사이드바 "Scheduler Demo":
  - 빌트인 그래프 (Start → Branch(true) → Log(HIT) → End) 자동 구성 + Toolbar Run으로 즉시 실행
  - **좌측 노드 팔레트** (`INodePaletteService`) — 카테고리별 Expander, 항목 클릭으로 즉시 그래프에 노드 추가. 사용자 코드 컴파일/JSON 로드 후 자동 갱신
  - 우측 코드 에디터에서 `[MethodNode("Demo.User.Multiply")] static int Multiply(int a,int b) => a*b;` 편집 → **Compile User Code** 버튼 클릭 → Roslyn 동적 컴파일 + `CustomNodeFactory` 자동 등록 + 그래프 자동 배치 (a=6, b=7 기본 리터럴) + 팔레트 갱신
  - **편집 시 라이브 진단 (Phase 9a)** — `UserCode` 변경 후 300ms debounce → `ICompilationService.Analyze()` → `Diagnostics` 자동 갱신 → 에디터에 빨간 물결 밑줄 즉시 표시 (수동 Compile 클릭 불필요)
  - **Roslyn 자동완성 (Phase 8c)** — `.` 입력 또는 **Ctrl+Space** 로 멤버/키워드 후보 팝업. 호스트 어셈블리 자동 수집으로 `System.Math` 등 모든 .NET API 즉시 사용 가능
  - **좌측 라인 마진 클릭** 으로 코드 에디터 내 브레이크포인트 토글 (빨간 점)
  - **Save JSON** / **Load JSON** 으로 그래프(노드/연결/위치/리터럴) 라운드트립
  - **Toggle Theme** 로 AvalonEdit 신택스 하이라이팅 + CompletionWindow 색상 Dark↔Light 즉시 전환
  - 그래프 노드 선택 + 툴바의 **Toggle Breakpoint** → Run → BreakpointHit 시 노드 글로우 + Continue/Step 버튼 활성
  - 우상단 미니맵 오버레이로 그래프 전체 조망
  - **인스펙터 패널 (I.2a)** — 우측 도킹. 노드 선택 후 Run 하면 선택된 노드의 마지막 입출력 핀 값이 즉시 표시 (실행 시간 포함)
  - **하단 탭 (I.2b/I.4)**:
    - **Log** — Scheduler 가 발화한 모든 이벤트 (Run started/Node entering/exited/Failed/Run Completed) 가 레벨별 색상으로 표시
    - **Run History** — 누적된 ExecutionRun 목록 + 우측 간트 차트 (노드별 실행 시간 막대, 성공=AccentSecondary/실패=Error)
  - **신규 빌트인 노드 (I.2c/I.5)** — 팔레트의 IO 카테고리에 `Output` (그래프 결과 키-값 노출), Diagnostics 카테고리에 `Assert`/`Guard`/`RangeAssert` 노드 사용 가능

---

## Testing

xUnit + FluentAssertions 기반 단위 테스트. **Warning-free build (0 error)**, multi-targeting (net8.0 + net9.0).

```powershell
# 전체 테스트
dotnet test

# 개별 프로젝트
dotnet test tests/VSMVVM.Core.Tests/
dotnet test tests/VSMVVM.WPF.Tests/
```

### 현재 테스트 수치

| Suite | net8.0 | net9.0 | Total |
| ----- | -----: | -----: | ----: |
| VSMVVM.Core.Tests | 169 | 169 | **338** |
| VSMVVM.WPF.Tests | 225 | 225 | **450** |
| VSMVVM.Core.Scheduler.Tests | 232 | 232 | **464** |
| VSMVVM.Core.Scheduler.Scripting.Tests | 9 | 9 | **18** |
| VSMVVM.WPF.Scheduler.Tests | 103 | 103 | **206** |
| VSMVVM.WPF.Scheduler.Editor.Tests | 30 | 30 | **60** |
| **합계** | **768** | **768** | **1,536** |

주요 커버리지: DI / Messenger / RelayCommand / StateStore / ObservableValidator / Logging (`[Log]`) / RegionManager · WPF Canvas (Mask/Histogram), Imaging, Behaviors, dispatcher/window/shortcut services · Graph / Pin / NodeBase / Source Generator emit / SchedulerService (push exec + pull data, branch/sequence/취소/timeout/순환) / 빌트인 노드 (Flow, IO, Validation, Math, Variables, Collections, Diagnostics) / SignatureToPinsBuilder / CustomFunctionNode / CustomConstantNode / `[ParameterNode]` 정적 필드 + CustomParameterNode (`Phase L`) / NodeGraphSerializer 라운드트립 (그래프 + 변수만, `Phase M`) / **다형성 핀 시스템 (`Phase K~L`)** — PinDescriptor TypeParameterName, IPolymorphicNode TypeArguments, NodeBase.BuildPins 치환, INodeInstancePropertyHost (인스펙터 NODE PROPERTIES) · Roslyn (Compile/Analyze/UnloadAssembly), collectible ALC · NodeGraphViewModel / Undo-Redo / NodePaletteService (검색 포함, `Phase G`) / NodeGraphMiniMap / Profiling / Inspector 스냅샷 / N:M 시각화 / **인스펙터 NODE PROPERTIES (ItemType / VariableName ComboBox + "+ Add new variable…" 마법 항목, `Phase M`)** · CodeNodeEditor / RoslynCompletion. 샘플 앱의 인수 시나리오 (Run/Stop, 사용자 코드 컴파일, ParameterNode 시연 등) 는 `sample/VSMVVM.WPF.Sample` 의 *데모 버튼* 으로 직접 시연 — NuGet 배포 라이브러리 자체엔 포함되지 않음.

---

## CI/CD

GitHub Actions 기반. 워크플로우 2개.

### `.github/workflows/ci.yml` — 매 push / PR

| 이벤트 | 동작 |
|---|---|
| `main` / `develop` push | `dotnet restore` → `build` (Release) → `test` (전체 6개 프로젝트) → TRX artifact 업로드 |
| PR to `main` | 동일 |

빌드/테스트 회귀만 감지 — NuGet 배포는 발동하지 않습니다.

### `.github/workflows/publish-nuget.yml` — 태그 push 또는 수동 실행

| 트리거 | 동작 |
|---|---|
| `v*.*.*` 태그 push (예: `v1.2.18`) | 태그에서 버전 추출 → props 갱신 → build/test → pack (7종) → **`nuget.org` 에 push** |
| GitHub UI 의 "Run workflow" | `version` 입력 후 동일 흐름 |

Push 대상은 `secrets.NUGET_API_KEY` 로 인증. 이미 게시된 버전이면 `--skip-duplicate` 로 무시.

## Release

로컬에서 태그 만들고 push 하면 GitHub Actions 가 자동으로 nuget.org 에 게시합니다.

```powershell
# props 의 <Version> 을 원하는 버전으로 수정 후:
git add Directory.Build.props
git commit -m "release: 1.2.18"
git tag v1.2.18
git push origin main v1.2.18
```

또는 GitHub UI 의 Actions → "Publish to NuGet" → "Run workflow" 에서 `version` 만 입력하면 태그 없이도 즉시 게시 가능.

`scripts/publish-nuget.ps1` / `scripts/publish-nuget.sh` 는 **로컬에서 수동으로 배포**할 때 사용하는 스크립트입니다 (CI 대신 로컬 실행 시).

```powershell
$env:NUGET_API_KEY = "..."
.\scripts\publish-nuget.ps1 -Version 1.2.18
```

```bash
NUGET_API_KEY=... ./scripts/publish-nuget.sh --version 1.2.18
```

---

## Project Structure

```
vsmvvm/
├── src/
│   ├── VSMVVM.Core/              # .NET Standard 2.0
│   │   ├── Attributes/           # [Property], [RelayCommand], [AsyncRelayCommand], ...
│   │   ├── CodeGen/              # Source Generator 구현
│   │   ├── Guard/                # 방어적 검증
│   │   └── MVVM/                 # ViewModelBase, DI, Messenger, StateStore, RegionManager, ...
│   │
│   ├── VSMVVM.WPF/               # .NET 8 (WPF)
│   │   ├── Behaviors/            # EventTrigger, EventToCommand, InvokeCommandAction
│   │   ├── Controls/             # WPFRegion, ImageCanvas, LayeredCanvas
│   │   ├── Host/                 # VSMVVMHost (Fluent Builder)
│   │   ├── MarkupExtensions/     # ViewModelLocator, Localize, BindingProxy
│   │   ├── Media/                # SVG 지원
│   │   └── Services/             # Dialog, Window, Splash, Dispatcher, BuildInfo,
│   │                             # VersionControl, WindowPlacement, Shortcut, Zoom, UndoRedo, ...
│   │
│   ├── VSMVVM.WPF.Design/        # .NET 8 / .NET 9 (WPF)
│   │   ├── Colors/               # Palette, ThemeDark, ThemeLight
│   │   ├── Components/           # DateTimePicker, JsonEditor, LoadingOverlay, ...
│   │   ├── Controls/             # Button, TextBox, DataGrid, Window, Dialog, ...
│   │   ├── Core/                 # SharedResourceDictionary, WindowChrome
│   │   └── Tokens/               # Spacing, Typography, Sizing, Effects
│   │
│   ├── VSMVVM.Core.Scheduler/    # .NET Standard 2.0 (Blueprint workflow engine)
│   │   ├── Attributes/           # [Node], [ExecInputPin], [ExecOutputPin], [InputPin], [OutputPin]
│   │   ├── CodeGen/              # NodeRegistrationCodeGen (IIncrementalGenerator)
│   │   ├── Compilation/          # ICompilationService + POCO 진단/결과/옵션
│   │   ├── Graph/                # NodeGraph, NodeConnection, ConnectionRules (N:M), GraphChange
│   │   ├── Nodes/                # INode, NodeBase, ExecutionFlow, NodeMetadataRegistry
│   │   │   └── BuiltIn/          # Start, End, Sequence, Branch, Delay, Log
│   │   ├── Pins/                 # IPin, ExecPin, DataPin<T>, PinCompatibility
│   │   └── Runtime/              # SchedulerService, ExecutionContext, Messages, Exceptions
│   │
│   └── VSMVVM.Core.Scheduler.Scripting/  # .NET 8 / .NET 9 (Roslyn 동적 컴파일)
│       ├── CollectibleAssemblyLoadContext.cs
│       ├── MetadataReferenceProvider.cs
│       └── RoslynCompilationService.cs
│
├── tests/
│   ├── VSMVVM.Core.Tests/                       # 169 × 2 framework
│   ├── VSMVVM.WPF.Tests/                        # 225 × 2 framework
│   ├── VSMVVM.Core.Scheduler.Tests/             # 222 × 2 framework
│   ├── VSMVVM.Core.Scheduler.Scripting.Tests/   # 9 × 2 framework
│   ├── VSMVVM.WPF.Scheduler.Tests/              # 72 × 2 framework
│   └── VSMVVM.WPF.Scheduler.Editor.Tests/       # 30 × 2 framework
│
├── sample/
│   └── VSMVVM.WPF.Sample/        # 데모 애플리케이션 (Scheduler Demo 포함)
│
├── scripts/
│   ├── publish-nuget.ps1         # 로컬에서 nuget.org 로 수동 배포 (PowerShell)
│   └── publish-nuget.sh          # 로컬에서 nuget.org 로 수동 배포 (Bash)
│
├── .github/workflows/
│   ├── ci.yml                    # push/PR 마다 build + test
│   └── publish-nuget.yml         # v*.*.* 태그 push 시 nuget.org 게시
│
├── Directory.Build.props         # 공통 메타데이터 + <Version>
└── NuGet.config
```

> `src/VSMVVM.WinForms` 및 `src/VSMVVM.WinForms.Design` 도 저장소에 존재하지만 NuGet 배포 대상이 아니며 본 문서에서 다루지 않습니다.

---

## License

MIT
