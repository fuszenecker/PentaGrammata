# PentaGrammata documentation

- **English:** [User guide](en/user-guide.md), A4 PDF: [pdf/PentaGrammata-User-Guide-en.pdf](pdf/PentaGrammata-User-Guide-en.pdf)
- **Magyar:** [Felhasználói kézikönyv](hu/felhasznaloi-kezikonyv.md), A4 PDF: [pdf/PentaGrammata-Felhasznaloi-Kezikonyv-hu.pdf](pdf/PentaGrammata-Felhasznaloi-Kezikonyv-hu.pdf)

Both guides describe every window of the application, with pictures, and end with a chapter that maps the features to the source code.

## Keeping the guides up to date

The two guides are maintained together: every user-visible change (a new feature, a changed default, a new setting, a changed window) updates **both** in the same piece of work, along with the About window, `README.md`, `AGENTS.md` and `RELEASE_NOTES.md`. The two guides have the same chapters in the same order.

## The pictures

The pictures in `images/` are not taken by hand: the headless test `DocumentationScreenshots` (`tests/PentaGrammata.Headless.Tests/Headless/DocumentationScreenshots.cs`) opens every window of a realistic profile with two weeks of seeded history and renders it. It runs with every test run as a smoke test; it writes the pictures only when `PENTAGRAMMATA_DOCS_IMAGES` names a directory:

```bash
PENTAGRAMMATA_DOCS_IMAGES=$PWD/docs/images \
  dotnet test tests/PentaGrammata.Headless.Tests/PentaGrammata.Headless.Tests.csproj \
  --filter FullyQualifiedName~DocumentationScreenshots
```

Regenerate them whenever a window changes, and add a new shot to the test when a new window or tab appears.

## The PDFs

The Markdown files are the sources; the A4 PDFs in `pdf/` are built from them with pandoc and xelatex, with the version from `version.txt` on the title page. The typography (KOMA-Script, TeX Gyre Pagella for the text, Lato for headings and captions, DejaVu Sans Mono for code, every chapter on a new page, headings kept with the picture that follows, Hungarian hyphenation and "1. ábra" captions) is in `scripts/docs-pdf-style.tex`; the Markdown-to-PDF adjustments (breakable code in table cells, no Markdown links or chapter list in the PDF) are in `scripts/docs-pdf-filter.lua`:

```bash
scripts/Build-Docs.sh
```

Rebuild and commit the PDFs whenever a guide or a picture changes. Every release attaches them; the release workflow refuses a tag whose PDFs were not built for its version.
