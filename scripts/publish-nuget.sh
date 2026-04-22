#!/usr/bin/env bash
# VSMVVM NuGet 패키지 빌드/팩/푸시 자동화 스크립트 (Git Bash / Linux / macOS)
#
# 사용법:
#   ./scripts/publish-nuget.sh                        # 현재 버전으로 빌드+팩+푸시 (NUGET_API_KEY 필요)
#   ./scripts/publish-nuget.sh -v 1.0.1               # 버전 갱신 후 빌드+팩+푸시
#   ./scripts/publish-nuget.sh -v 1.0.1 -k oy2...     # API 키 직접 전달
#   ./scripts/publish-nuget.sh --skip-push            # 빌드/팩만 수행
#
# 옵션:
#   -v, --version <ver>    배포할 버전 (생략 시 Directory.Build.props 현재값 사용)
#   -k, --api-key <key>    NuGet.org API 키 (생략 시 $NUGET_API_KEY 사용)
#   -o, --output <dir>     nupkg 출력 폴더 (기본: ./nupkgs)
#   -c, --config <conf>    빌드 Configuration (기본: Release)
#       --skip-push        푸시 건너뛰기
#   -h, --help             도움말 표시

set -euo pipefail

VERSION=""
API_KEY="${NUGET_API_KEY:-}"
SKIP_PUSH=0
OUTPUT_DIR="./nupkgs"
CONFIGURATION="Release"
SOURCE="https://api.nuget.org/v3/index.json"

usage() {
    sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'
    exit 0
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -v|--version)   VERSION="$2"; shift 2 ;;
        -k|--api-key)   API_KEY="$2"; shift 2 ;;
        -o|--output)    OUTPUT_DIR="$2"; shift 2 ;;
        -c|--config)    CONFIGURATION="$2"; shift 2 ;;
        --skip-push)    SKIP_PUSH=1; shift ;;
        -h|--help)      usage ;;
        *) echo "알 수 없는 옵션: $1" >&2; exit 1 ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$REPO_ROOT"

PROPS_FILE="$REPO_ROOT/Directory.Build.props"
if [[ ! -f "$PROPS_FILE" ]]; then
    echo "Directory.Build.props not found at $PROPS_FILE" >&2
    exit 1
fi

PROJECTS=(
    "src/VSMVVM.Core/VSMVVM.Core.csproj"
    "src/VSMVVM.WPF/VSMVVM.WPF.csproj"
    "src/VSMVVM.WPF.Design/VSMVVM.WPF.Design.csproj"
)

step() {
    echo ""
    echo -e "\033[36m==> $1\033[0m"
}

if [[ -n "$VERSION" ]]; then
    step "Directory.Build.props 버전 갱신: $VERSION"
    # cross-platform in-place sed: use a temp file
    sed -E "s|<Version>[^<]+</Version>|<Version>$VERSION</Version>|" "$PROPS_FILE" > "$PROPS_FILE.tmp"
    mv "$PROPS_FILE.tmp" "$PROPS_FILE"
    echo "   OK"
else
    VERSION=$(grep -oE '<Version>[^<]+</Version>' "$PROPS_FILE" | head -n1 | sed -E 's|<Version>([^<]+)</Version>|\1|')
    echo "현재 버전: $VERSION"
fi

if [[ $SKIP_PUSH -eq 0 && -z "$API_KEY" ]]; then
    echo "API 키가 없습니다. -k 옵션 또는 NUGET_API_KEY 환경 변수를 설정하세요. (빌드만 원하면 --skip-push)" >&2
    exit 1
fi

mkdir -p "$OUTPUT_DIR"

for proj in "${PROJECTS[@]}"; do
    step "빌드: $proj"
    dotnet build "$proj" -c "$CONFIGURATION"
done

for proj in "${PROJECTS[@]}"; do
    step "팩: $proj"
    dotnet pack "$proj" -c "$CONFIGURATION" -o "$OUTPUT_DIR" --no-build
done

if [[ $SKIP_PUSH -eq 1 ]]; then
    step "--skip-push 지정됨 - 푸시 건너뜀"
    echo "생성된 패키지:"
    ls -1 "$OUTPUT_DIR"/*."$VERSION".nupkg 2>/dev/null | sed 's|^|   |'
    exit 0
fi

PACKAGE_IDS=("VSMVVM.Core" "VSMVVM.WPF" "VSMVVM.WPF.Design")
for id in "${PACKAGE_IDS[@]}"; do
    PKG="$OUTPUT_DIR/$id.$VERSION.nupkg"
    if [[ ! -f "$PKG" ]]; then
        echo "패키지를 찾을 수 없음: $PKG" >&2
        exit 1
    fi
    step "푸시: $id $VERSION"
    dotnet nuget push "$PKG" --api-key "$API_KEY" --source "$SOURCE" --skip-duplicate
done

step "완료!"
echo "인덱싱에 최대 1시간 소요될 수 있습니다."
echo "   https://www.nuget.org/packages/VSMVVM.Core/$VERSION"
echo "   https://www.nuget.org/packages/VSMVVM.WPF/$VERSION"
echo "   https://www.nuget.org/packages/VSMVVM.WPF.Design/$VERSION"
