#!/usr/bin/env bash
#
# Build the pinned Texconv-Custom-DLL `texconv` executable used by the parity tests.
#
#   scripts/build-texconv.sh [--force]
#
# Produces: artifacts/texconv/texconv
#
# Pinned sources:
#   Texconv-Custom-DLL 40b28a0  (release v0.6.0)
#   DirectXTex         232bbf2e (submodule)
#
# The build is done with OpenMP disabled: gcc 16 fails on omp.h /
# __GOMP_DEFAULT_NULL, and the BC6H/BC7 CPU codec is used instead of a GPU one.
# The resulting binary has no PNG support (no libpng), so the parity harness
# uses TGA as its interchange format.
#
# Fast path: TEXCONV_PATH, or /tmp/texbuild/bin/texconv, is reused verbatim
# instead of cloning/building. Pass --force to ignore it and build from source.

set -euo pipefail

TEXCONV_URL="https://github.com/matyalatte/Texconv-Custom-DLL"
TEXCONV_COMMIT="40b28a07e760a145e8677fb65b5e1ffb7e712e48" # v0.6.0
DIRECTXTEX_COMMIT="232bbf2e39a37ea2e9b7b8331653fa69bd4797a2"
FALLBACK_BIN="/tmp/texbuild/bin/texconv"

step() { printf '\n==> %s\n' "$*"; }
info() { printf '    %s\n' "$*"; }
die() {
    printf '\nERROR: %s\n' "$*" >&2
    exit 1
}

FORCE=0
for arg in "$@"; do
    case "$arg" in
        --force) FORCE=1 ;;
        -h | --help)
            sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        *) die "unknown argument: $arg (expected --force)" ;;
    esac
done

# Resolve the repository root from this script's location, independent of cwd.
SCRIPT_PATH="${BASH_SOURCE[0]}"
while [ -L "$SCRIPT_PATH" ]; do
    link_target="$(readlink "$SCRIPT_PATH")"
    case "$link_target" in
        /*) SCRIPT_PATH="$link_target" ;;
        *) SCRIPT_PATH="$(dirname "$SCRIPT_PATH")/$link_target" ;;
    esac
done
SCRIPT_DIR="$(cd -P "$(dirname "$SCRIPT_PATH")" && pwd)"
REPO_ROOT="$(cd -P "$SCRIPT_DIR/.." && pwd)"

CHECKOUT_DIR="$REPO_ROOT/artifacts/texconv"
BUILD_DIR="$CHECKOUT_DIR/build"
OUT_BIN="$CHECKOUT_DIR/texconv"

for tool in git cmake ninja; do
    command -v "$tool" >/dev/null 2>&1 || die "required tool not found: $tool"
done

step "Repository root: $REPO_ROOT"
mkdir -p "$CHECKOUT_DIR"

sanity_check() {
    local bin="$1" out
    [ -x "$bin" ] || die "not executable: $bin"
    out="$("$bin" -h 2>&1 || true)"
    case "$out" in
        *DirectXTex*) info "sanity check OK: $("$bin" -h 2>&1 | head -1 || true)" ;;
        *) die "file does not look like texconv (no DirectXTex banner): $bin" ;;
    esac
}

# ---------------------------------------------------------------- fast path
copy_prebuilt() {
    local src="$1" reason="$2"
    step "Fast path ($reason): reusing $src"
    info "skipping clone and build"
    if [ "$(readlink -f "$src")" = "$(readlink -f "$OUT_BIN" 2>/dev/null || echo "$OUT_BIN")" ]; then
        info "source is already $OUT_BIN; nothing to copy"
    else
        cp -f "$src" "$OUT_BIN.tmp"
        mv -f "$OUT_BIN.tmp" "$OUT_BIN"
        info "installed $OUT_BIN"
    fi
    chmod 0755 "$OUT_BIN"
    sanity_check "$OUT_BIN"
    step "Done"
    exit 0
}

if [ "$FORCE" -eq 0 ]; then
    if [ -n "${TEXCONV_PATH:-}" ] && [ -x "${TEXCONV_PATH:-}" ]; then
        copy_prebuilt "$TEXCONV_PATH" "TEXCONV_PATH is set"
    fi
    if [ -x "$FALLBACK_BIN" ]; then
        copy_prebuilt "$FALLBACK_BIN" "prebuilt $FALLBACK_BIN exists"
    fi
fi

# ------------------------------------------------------------------- checkout
step "Pinning Texconv-Custom-DLL to $TEXCONV_COMMIT"
if [ -d "$CHECKOUT_DIR/.git" ]; then
    info "existing checkout found, updating instead of cloning"
    git -C "$CHECKOUT_DIR" remote set-url origin "$TEXCONV_URL"
    git -C "$CHECKOUT_DIR" fetch --depth 1 origin "$TEXCONV_COMMIT"
    git -C "$CHECKOUT_DIR" checkout --force --detach "$TEXCONV_COMMIT"
elif [ -e "$CHECKOUT_DIR" ] && [ -n "$(ls -A "$CHECKOUT_DIR" 2>/dev/null)" ]; then
    die "$CHECKOUT_DIR exists but is not a git checkout; remove it and re-run"
else
    info "cloning $TEXCONV_URL"
    git clone --no-checkout --filter=blob:none "$TEXCONV_URL" "$CHECKOUT_DIR"
    git -C "$CHECKOUT_DIR" fetch --depth 1 origin "$TEXCONV_COMMIT"
    git -C "$CHECKOUT_DIR" checkout --force --detach "$TEXCONV_COMMIT"
fi

HEAD_SHA="$(git -C "$CHECKOUT_DIR" rev-parse HEAD)"
[ "$HEAD_SHA" = "$TEXCONV_COMMIT" ] || die "Texconv-Custom-DLL HEAD is $HEAD_SHA, expected $TEXCONV_COMMIT"
info "Texconv-Custom-DLL HEAD: $HEAD_SHA"

step "Initialising submodules"
git -C "$CHECKOUT_DIR" submodule sync --recursive
git -C "$CHECKOUT_DIR" submodule update --init --depth 1 --recursive

step "Verifying DirectXTex submodule pin"
DIRECTXTEX_SHA="$(git -C "$CHECKOUT_DIR/DirectXTex" rev-parse HEAD)"
[ "$DIRECTXTEX_SHA" = "$DIRECTXTEX_COMMIT" ] ||
    die "DirectXTex HEAD is $DIRECTXTEX_SHA, expected $DIRECTXTEX_COMMIT"
info "DirectXTex HEAD: $DIRECTXTEX_SHA"

# ---------------------------------------------------------------------- build
step "Configuring (Release, exe, no texassemble, no OpenMP)"
(
    cd "$CHECKOUT_DIR"
    cmake -G Ninja -S . -B build \
        -DCMAKE_BUILD_TYPE=Release \
        -DTEXCONV_BUILD_AS_EXE=ON \
        -DTEXCONV_USE_TEXASSEMBLE=OFF \
        -DBC_USE_OPENMP=OFF
)

step "Building"
(
    cd "$CHECKOUT_DIR"
    cmake --build build
)

step "Installing binary"
[ -x "$BUILD_DIR/bin/texconv" ] || die "expected binary not found: $BUILD_DIR/bin/texconv"
cp -f "$BUILD_DIR/bin/texconv" "$OUT_BIN.tmp"
chmod 0755 "$OUT_BIN.tmp"
mv -f "$OUT_BIN.tmp" "$OUT_BIN"
info "installed $OUT_BIN"

sanity_check "$OUT_BIN"

step "Done"
