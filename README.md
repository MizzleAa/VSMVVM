# VSMVVM

[![CI](https://github.com/MizzleAa/VSMVVM/actions/workflows/ci.yml/badge.svg)](https://github.com/MizzleAa/VSMVVM/actions/workflows/ci.yml)

Lightweight, modular MVVM framework for WPF with built-in Source Generator, DI Container, and Tailwind-inspired Design System.

## Architecture

```
┌─────────────────────────────────────────────────┐
│                    Application                   │
├─────────────────────────────────────────────────┤
│  VSMVVM.WPF.Design   │      VSMVVM.WPF         │
│  (Styles & Tokens)    │  (Host, Services, Ctrl) │
├───────────────────────┴─────────────────────────┤
│                  VSMVVM.Core                     │
│          (MVVM, DI, Source Generator)            │
└─────────────────────────────────────────────────┘
```

| Package | Target | Description |
|---------|--------|-------------|
| **VSMVVM.Core** | .NET Standard 2.0 | MVVM base, DI, Source Generator, Messenger, Guard |
| **VSMVVM.WPF** | .NET 8 (WPF) | Host, Services, Controls, Behaviors, SVG |
| **VSMVVM.WPF.Design** | .NET 8 (WPF) | Tailwind-inspired design tokens, themed controls |

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

코드 생성기가 `partial class`의 boilerplate를 자동 생성합니다.

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

### Messenger

타입 기반 Pub/Sub 메시징.

```csharp
// 구독
messenger.Register<MyMessage>(this, msg => Handle(msg));

// 발행
messenger.Send(new MyMessage { Data = "Hello" });

// 해제
messenger.Unregister<MyMessage>(this);
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

대량 추가/제거 시 단일 CollectionChanged 이벤트 발생.

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

### Services

| Service | Interface | Description |
|---------|-----------|-------------|
| **DialogService** | `IDialogService` | XAML 디자인 시스템 기반 모달 다이얼로그 (OK, OK/Cancel, Yes/No) |
| **SplashService** | `ISplashService` | 별도 STA 스레드 스플래시 (Report + 자동 Close) |
| **DispatcherService** | `IDispatcherService` | UI 스레드 디스패칭 |
| **BuildInfoService** | `IBuildInfoService` | 빌드 정보 (버전, 시간, SHA) |
| **VersionControlService** | `IVersionControlService` | Git 버전 정보 |
| **WindowPlacementService** | `IWindowPlacementService` | 창 위치/크기 저장 및 복원 |
| **ShortcutService** | — | 글로벌 키보드 단축키 |
| **ZoomService** | — | UI 줌 레벨 관리 |

### Controls

| Control | Description |
|---------|-------------|
| **WPFRegion** | ContentControl 기반 Region (Navigation + INavigateAware) |
| **ImageCanvas** | 줌/팬 캔버스, 자식 선택/리사이즈/드래그 지원 |
| **LayeredCanvas** | Z-Order 레이어 캔버스 |
| **CanvasSelectionAdorner** | 8방향 리사이즈 핸들 Adorner |

### Behaviors (Interaction)

XAML에서 이벤트→커맨드 바인딩.

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

Tailwind CSS에서 영감받은 유틸리티 기반 WPF 디자인 시스템.

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

---

## Testing

xUnit + FluentAssertions 기반 단위 테스트 (40 tests).

```bash
# 전체 테스트 실행
dotnet test --verbosity normal

# 개별 프로젝트
dotnet test test/VSMVVM.Core.Tests/
dotnet test test/VSMVVM.WPF.Tests/
```

| Suite | Tests | Coverage |
|-------|-------|----------|
| **VSMVVM.Core.Tests** | 37 | DI, Guard, Messenger, ViewModelBase, RelayCommand, AsyncRelayCommand, StateStore, ObservableValidator, BatchObservableCollection |
| **VSMVVM.WPF.Tests** | 3 | ServiceLocator, DialogResult |

### CI/CD

GitHub Actions로 `main`/`develop` 브랜치 push 및 PR 시 자동 빌드 + 테스트:

```yaml
# .github/workflows/ci.yml
on:
  push: [main, develop]
  pull_request: [main]
jobs:
  build-and-test:
    runs-on: windows-latest
    steps: [checkout, setup-dotnet, restore, build, test]
```

---

## Project Structure

```
VSMVVM/
├── src/
│   ├── VSMVVM.Core/              # .NET Standard 2.0
│   │   ├── Attributes/           # [Property], [RelayCommand], [AsyncRelayCommand], ...
│   │   ├── CodeGen/              # Source Generator 구현
│   │   ├── Guard/                # 방어적 검증
│   │   └── MVVM/                 # ViewModelBase, DI, Messenger, StateStore, ...
│   │
│   ├── VSMVVM.WPF/               # .NET 8 (WPF)
│   │   ├── Behaviors/            # EventTrigger, EventToCommand, InvokeCommandAction
│   │   ├── Controls/             # WPFRegion, ImageCanvas, LayeredCanvas
│   │   ├── Host/                 # VSMVVMHost (Fluent Builder)
│   │   ├── MarkupExtensions/     # ViewModelLocator, Localize, BindingProxy
│   │   ├── Media/                # SVG 지원
│   │   └── Services/             # Dialog, Splash, Dispatcher, BuildInfo, ...
│   │
│   └── VSMVVM.WPF.Design/        # .NET 8 (WPF)
│       ├── Colors/               # Palette, ThemeDark, ThemeLight
│       ├── Components/           # DateTimePicker, JsonEditor, LoadingOverlay, ...
│       ├── Controls/             # Button, TextBox, DataGrid, Window, Dialog, ...
│       ├── Core/                 # SharedResourceDictionary, WindowChrome
│       └── Tokens/               # Spacing, Typography, Sizing, Effects
│
├── test/
│   ├── VSMVVM.Core.Tests/        # Core 단위 테스트
│   └── VSMVVM.WPF.Tests/         # WPF 단위 테스트
│
├── .github/workflows/ci.yml      # GitHub Actions CI
│
└── sample/
    └── VSMVVM.WPF.Sample/        # 샘플 애플리케이션
```

## License

MIT
