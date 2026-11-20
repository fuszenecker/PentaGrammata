#!/usr/bin/env bash
# Builds the A4 PDF user guides from the Markdown sources in docs/ with pandoc and xelatex.
# Typography: scripts/docs-pdf-style.tex; Markdown-to-PDF adjustments: scripts/docs-pdf-filter.lua.
# Usage: scripts/Build-Docs.sh   ->  docs/pdf/PentaGrammata-User-Guide-en.pdf
#                                    docs/pdf/PentaGrammata-Felhasznaloi-Kezikonyv-hu.pdf
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
version="$(cut -d. -f1-3 "$root/version.txt")"
out="$root/docs/pdf"
mkdir -p "$out"

build() {
    local source="$1" target="$2" language="$3" subtitle="$4" toc="$5"
    local preamble
    preamble="$(mktemp --suffix=.tex)"
    # Language: hyphenation, the names of "Figure"/"Contents"; Hungarian captions read "1. ábra".
    case "$language" in
        en) printf '%s\n' '\usepackage[english]{babel}' ;;
        hu) printf '%s\n' '\usepackage{babel}' '\babelprovide[import,main]{hungarian}' \
                '\AtEndPreamble{\DeclareCaptionLabelFormat{hu}{#2.~#1}\captionsetup[figure]{name=ábra,labelformat=hu}}' ;;
    esac > "$preamble"
    cat "$root/scripts/docs-pdf-style.tex" >> "$preamble"

    # The first heading of the guide is its title; the chapters ("## 1. ...") become sections.
    pandoc "$source" \
        --from markdown+gfm_auto_identifiers \
        --shift-heading-level-by=-1 \
        --resource-path "$(dirname "$source")" \
        --lua-filter "$root/scripts/docs-pdf-filter.lua" \
        --include-in-header "$preamble" \
        --pdf-engine xelatex \
        --toc --toc-depth 2 \
        -V documentclass=scrartcl \
        -V classoption=a4paper,fontsize=11pt,parskip=half-,titlepage,headings=standardclasses \
        -V geometry:a4paper,top=25mm,bottom=25mm,inner=24mm,outer=24mm,headsep=8mm \
        -V subtitle="$subtitle $version" \
        -V toc-title="$toc" \
        -V colorlinks=true -V linkcolor=accent -V urlcolor=accent -V toccolor=black \
        -o "$out/$target"
    rm -f "$preamble"
    echo "Wrote $out/$target"
}

build "$root/docs/en/user-guide.md" "PentaGrammata-User-Guide-en.pdf" en "Version" "Contents"
build "$root/docs/hu/felhasznaloi-kezikonyv.md" "PentaGrammata-Felhasznaloi-Kezikonyv-hu.pdf" hu "Verzió" "Tartalom"
