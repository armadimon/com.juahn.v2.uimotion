#!/usr/bin/env bash
#
# Unity 계층 컴파일 게이트.
#
# Unity 에디터를 열지 않고 Runtime/Core + Runtime/Unity 전체를 컴파일한다.
# Unity 파일을 건드렸으면 커밋 전에 이것을 돌린다.
#
#   ./Tools~/compile-check/run.sh
#   UNITY_ROOT=/path/to/Editor/6000.x.y ./Tools~/compile-check/run.sh
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# --- Unity 설치를 찾는다 ------------------------------------------------
if [ -z "${UNITY_ROOT:-}" ]; then
  UNITY_ROOT="$(ls -d /Applications/Unity/Hub/Editor/*/ 2>/dev/null | sort -V | tail -1 || true)"
fi

if [ -z "${UNITY_ROOT}" ] || [ ! -d "${UNITY_ROOT}" ]; then
  echo "Unity 설치를 찾지 못했습니다. UNITY_ROOT를 지정하세요." >&2
  echo "  예: UNITY_ROOT=/Applications/Unity/Hub/Editor/6000.5.3f1 $0" >&2
  exit 2
fi

UNITY_MANAGED="${UNITY_ROOT}/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine"
if [ ! -d "${UNITY_MANAGED}" ]; then
  echo "매니지드 어셈블리 폴더가 없습니다: ${UNITY_MANAGED}" >&2
  echo "이 스크립트는 macOS 레이아웃을 가정합니다. 다른 OS면 UNITY_MANAGED를 직접 넘기세요." >&2
  exit 2
fi

# uGUI는 패키지라 에디터 본체가 아니라 템플릿 캐시에 들어 있다.
if [ -z "${UNITY_UGUI:-}" ]; then
  UNITY_UGUI="$(ls "${UNITY_ROOT}"/Unity.app/Contents/Resources/PackageManager/ProjectTemplates/libcache/*/ScriptAssemblies/UnityEngine.UI.dll 2>/dev/null | head -1 || true)"
fi

if [ -z "${UNITY_UGUI}" ] || [ ! -f "${UNITY_UGUI}" ]; then
  echo "UnityEngine.UI.dll을 찾지 못했습니다. UNITY_UGUI로 지정하세요." >&2
  exit 2
fi

echo "Unity:  ${UNITY_ROOT}"
echo "uGUI:   ${UNITY_UGUI}"
echo

dotnet build "${HERE}/UiMotion.Unity.Compile.csproj" \
  -p:UnityManaged="${UNITY_MANAGED}" \
  -p:UnityUgui="${UNITY_UGUI}" \
  -v quiet --nologo

echo
echo "Unity 계층 컴파일 통과."
