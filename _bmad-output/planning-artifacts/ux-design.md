---
name: MicroslopWords
description: "A calm, paper-first DOCX editor whose chrome recedes behind the document."
status: final
created: "2026-10-22"
updated: "2026-10-22"
sources:
  - "AGENTS.md"
  - "README.md"
  - "docs/adr/0001-technology-stack.md"
  - "docs/adr/0002-document-model.md"
  - "docs/adr/0003-spec-driven-development.md"
  - "_bmad-output/planning-artifacts/product-brief-microslopwords.md"
  - "src/MicroslopWords.App"
colors:
  surface-desk: '#E9ECF0'
  surface-chrome: '#F4F6F8'
  surface-panel: '#F8F9FB'
  surface-raised: '#FFFFFF'
  surface-sunken: '#E2E6EB'
  surface-hover: '#E8ECF1'
  page-surface: '#FFFEFB'
  ink-primary: '#1B1F24'
  ink-secondary: '#4E5965'
  ink-tertiary: '#5F6A76'
  ink-disabled: '#969EA7'
  border-subtle: '#D8DEE5'
  border-strong: '#B6C0CA'
  accent: '#2A5F8F'
  accent-hover: '#224F78'
  accent-soft: '#E6F0F8'
  on-accent: '#FFFFFF'
  selection-bg: '#CFE3F5'
  selection-ink: '#162B3E'
  focus-ring: '#1A6FB5'
  success-ink: '#2D6A45'
  success-soft: '#E8F3EC'
  warning-ink: '#8A5A00'
  warning-soft: '#FFF4D6'
  error-ink: '#A3322B'
  error-soft: '#FBE9E7'
  header-band: '#F1F6FB'
  ruler-bg: '#F4F6F8'
  ruler-tick: '#66717D'
  overlay-scrim: '#52181C21'
  surface-desk-dark: '#101214'
  surface-chrome-dark: '#1A1D21'
  surface-panel-dark: '#191C20'
  surface-raised-dark: '#23272C'
  surface-sunken-dark: '#0D0F11'
  surface-hover-dark: '#2A2F35'
  page-surface-dark: '#FCFBF8'
  ink-primary-dark: '#EDEFF2'
  ink-secondary-dark: '#B3BBC4'
  ink-tertiary-dark: '#98A2AC'
  ink-disabled-dark: '#626B75'
  border-subtle-dark: '#2F353C'
  border-strong-dark: '#46505A'
  accent-dark: '#8ABBE8'
  accent-hover-dark: '#A7CDEF'
  accent-soft-dark: '#23394D'
  on-accent-dark: '#11202D'
  selection-bg-dark: '#2C4D6B'
  selection-ink-dark: '#F4F8FC'
  focus-ring-dark: '#9DCBF3'
  success-ink-dark: '#8FD2A8'
  success-soft-dark: '#1D3326'
  warning-ink-dark: '#F0C36A'
  warning-soft-dark: '#382F1C'
  error-ink-dark: '#F0A39D'
  error-soft-dark: '#3A2220'
  header-band-dark: '#1D2A36'
  ruler-bg-dark: '#1A1D21'
  ruler-tick-dark: '#A8B2BC'
  overlay-scrim-dark: '#8C0A0C0E'
typography:
  ui-title:
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, sans-serif'
    fontSize: 18px
    fontWeight: '600'
    lineHeight: '1.35'
  dialog-title:
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, sans-serif'
    fontSize: 16px
    fontWeight: '600'
    lineHeight: '1.35'
  ui-body:
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, sans-serif'
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.5'
  ui-body-strong:
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, sans-serif'
    fontSize: 14px
    fontWeight: '600'
    lineHeight: '1.4'
  ui-label:
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, sans-serif'
    fontSize: 12px
    fontWeight: '600'
    lineHeight: '1.3'
  ui-caption:
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, sans-serif'
    fontSize: 11px
    fontWeight: '500'
    lineHeight: '1.3'
  menu:
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, sans-serif'
    fontSize: 13px
    fontWeight: '400'
    lineHeight: '1.25'
  toolbar:
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, sans-serif'
    fontSize: 13px
    fontWeight: '500'
    lineHeight: '1.2'
  status:
    fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, sans-serif'
    fontSize: 12px
    fontWeight: '400'
    lineHeight: '1.25'
  document-text:
    note: 'Defined by the DOCX document and its style sheet; never replaced by the UI font.'
rounded:
  sm: 4px
  md: 6px
  lg: 8px
  xl: 12px
  full: 9999px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 24px
  '6': 32px
  '7': 48px
  '8': 64px
  gutter: 16px
  panel-padding: 12px
  toolbar-gap: 4px
  page-gap: 32px
components:
  menu-bar:
    height: '32px'
    background: '{colors.surface-chrome}'
    foreground: '{colors.ink-primary}'
    border-bottom: '1px solid {colors.border-subtle}'
  format-toolbar:
    height: '44px'
    background: '{colors.surface-chrome}'
    foreground: '{colors.ink-primary}'
    gap: '{spacing.2}'
    border-bottom: '1px solid {colors.border-subtle}'
  icon-button:
    size: '32px'
    radius: '{rounded.sm}'
    hover-background: '{colors.surface-hover}'
    active-background: '{colors.accent-soft}'
    active-foreground: '{colors.accent}'
    focus: '2px solid {colors.focus-ring}'
  page-canvas:
    background: '{colors.surface-desk}'
    page-background: '{colors.page-surface}'
    page-gap: '{spacing.6}'
    page-radius: '0'
    page-border: '1px solid {colors.border-subtle}'
    page-shadow: '0 1px 2px rgba(16, 24, 32, 0.12), 0 10px 30px rgba(16, 24, 32, 0.10)'
  ruler:
    height: '20px'
    background: '{colors.ruler-bg}'
    tick: '{colors.ruler-tick}'
    margin-fill: '{colors.surface-sunken}'
  styles-panel:
    width: '264px'
    background: '{colors.surface-panel}'
    border: '1px solid {colors.border-subtle}'
    padding: '{spacing.3}'
  navigation-panel:
    width: '320px'
    background: '{colors.surface-panel}'
    border: '1px solid {colors.border-subtle}'
    padding: '{spacing.3}'
  status-bar:
    height: '28px'
    background: '{colors.surface-chrome}'
    foreground: '{colors.ink-secondary}'
    font: '{typography.status}'
  dialog:
    background: '{colors.surface-raised}'
    radius: '{rounded.lg}'
    padding: '{spacing.5}'
    shadow: '0 8px 32px rgba(16, 24, 32, 0.22)'
  button-primary:
    background: '{colors.accent}'
    foreground: '{colors.on-accent}'
    radius: '{rounded.sm}'
    height: '32px'
    padding-x: '{spacing.3}'
  button-secondary:
    background: 'transparent'
    foreground: '{colors.ink-primary}'
    border: '1px solid {colors.border-strong}'
    radius: '{rounded.sm}'
    height: '32px'
  field:
    background: '{colors.surface-raised}'
    border: '1px solid {colors.border-strong}'
    focus-border: '1px solid {colors.focus-ring}'
    radius: '{rounded.sm}'
    height: '32px'
  warning-banner:
    background: '{colors.warning-soft}'
    foreground: '{colors.warning-ink}'
    border-left: '3px solid {colors.warning-ink}'
  error-banner:
    background: '{colors.error-soft}'
    foreground: '{colors.error-ink}'
    border-left: '3px solid {colors.error-ink}'
  focus:
    ring: '2px solid {colors.focus-ring}'
    offset: '2px'
---

# Part 1 - DESIGN.md

This is the visual contract for MicroslopWords. It defines the desktop shell and editor chrome; the document itself is governed by its DOCX styles and page settings. The paired experience contract is the behavioral counterpart, and both spines win over any mock, wireframe, or imported visual.

## Brand & Style

MicroslopWords is a **quiet paper workspace**. The document page is the brightest, most important object on screen; the surrounding application is a neutral tool that gets out of the way. The product should feel calm during a long writing session, precise during layout work, and honest when a DOCX cannot be reproduced exactly.

The visual posture is **professional minimalism with print-shop discipline**. This is not a consumer note-taking app, a retro typewriter, or a miniature Microsoft Word. Chrome is flat, rectilinear, and compact. Accent color is a working signal, not decoration. There are no gradients, glass effects, illustrations, celebratory animations, or ornamental flourishes.

Key principles:

- **Paper first.** Pages sit on a quiet desk surface with clear boundaries. The page is not a card, and the desk is not a brand statement.
- **Ink over color.** Neutral ink carries hierarchy. Blue marks selection, focus, and the primary action. Semantic color appears only for status.
- **One visual voice.** Menus, panels, dialogs, and the canvas use one Inter-based UI voice. Document text keeps its own fonts and colors.
- **Density with air.** Controls are compact at 32px, but panels and dialogs use a 4px spacing base to avoid cramped clusters.
- **No deception.** Layout differences, missing fonts, and unsupported features are visible in the interface. The UI never implies Word parity it does not have.
- **Platform fluent, not platform mimic.** Windows, macOS, and Linux share the same identity, but native menus, shortcuts, file dialogs, and print dialogs follow each platform's conventions.

## Colors

The palette is a cool neutral gray shell around a warm near-white page. Light and dark themes are complete token sets, not an automatic inversion. The dark theme darkens the chrome and desk; it does **not** invert document colors, images, or the paper page. Document colors must remain trustworthy because they are what gets saved and printed.

- **Page (`{colors.page-surface}`)** is a warm near-white `#FFFEFB`, not pure white. It reduces glare without changing the user's perception of document colors. In dark mode the page remains `{colors.page-surface-dark}`; only the surround changes.
- **Desk (`{colors.surface-desk}`)** is a low-contrast neutral that separates pages from the window. It is never a gradient or texture.
- **Chrome (`{colors.surface-chrome}`, `{colors.surface-panel}`)** is slightly lighter than the desk so menus, toolbars, panels, and the status bar read as a frame, not as content.
- **Ink (`{colors.ink-primary}`, `{colors.ink-secondary}`, `{colors.ink-tertiary}`)** carries hierarchy. Primary ink is for labels and editable values; secondary is for supporting text; tertiary is for metadata and inactive hints. Disabled ink is never used for information the user needs.
- **Accent (`{colors.accent}`)** is a muted blue for primary buttons, focused controls, active tool states, links, caret, and selection edges. Accent-soft is for selected rows and active button backgrounds. Accent is not used for decorative headings or status categories.
- **Selection (`{colors.selection-bg}` / `{colors.selection-ink}`)** is a blue-tinted fill with dark text. It must remain legible when text is selected.
- **Semantic colors (`success`, `warning`, `error`)** are always paired with an icon and text. Warning uses amber, error uses muted brick red, success uses forest green. They appear in banners, dialogs, and inline validation only.
- **Borders (`{colors.border-subtle}`, `{colors.border-strong}`)** provide structure without heavy rules. Subtle borders separate regions; strong borders define fields and selected objects.
- **Focus (`{colors.focus-ring}`)** is a high-contrast blue ring. It is not the same token as accent so focus remains visible in high-contrast and dark contexts.

Contrast floor: primary text at least 12:1 on page/chrome, secondary text at least 7:1, tertiary text at least 4.5:1, and accent text on page at least 6:1 in both themes. Focus rings and object outlines must reach 3:1 against adjacent surfaces. Semantic ink on its soft background must reach 4.5:1.

Rules:

- Never use color alone to indicate dirty, saved, error, selection, or unsupported state.
- Never tint document text or images to match the theme.
- Never put white text on a light accent or light text on a pale semantic fill.
- Never use semantic red for destructive styling inside the document canvas; document colors belong to the document.

## Typography

UI typography uses **Inter** with a platform fallback stack: `Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, sans-serif`. Inter is bundled with the application so the shell is consistent across platforms. Document text uses only the DOCX document fonts, style sheet, and fallback rules; UI fonts never leak into the page.

- **Window and dialog titles** use `{typography.ui-title}` or `{typography.dialog-title}` at 600 weight. No display serif, no all-caps headings.
- **Body and control labels** use `{typography.ui-body}` or `{typography.ui-body-strong}`. Menu labels use `{typography.menu}`. Toolbar labels use `{typography.toolbar}`.
- **Metadata and status** use `{typography.ui-caption}` or `{typography.status}`. Status text is never smaller than 11px.
- **Numbers and measurements** use tabular figures where Avalonia supports them, so page counts, word counts, and zoom values do not jitter.
- **No letter-spaced all-caps** except for two-character keyboard hints inside menus; those are handled by the native menu renderer.
- **No text truncation in critical controls.** If a style name or file name is too long, truncate with an ellipsis and provide the full value in a tooltip and accessible name.

`[ASSUMPTION]` Inter is bundled and loaded before the first window paints. If a platform font stack is used instead, the metrics may differ; the UI layout must still hold at 12px-18px.

## Layout & Spacing

The application is a single desktop window with a fixed shell and a scrollable document stage.

- Menu bar: 32px.
- Formatting toolbar: 44px.
- Document stage: fills the remaining space.
- Status bar: 28px.
- Top ruler: 20px; left ruler: 20px.
- Side panels: 320px (Navigation/Find) and 264px (Styles), with 1px borders and 12px internal padding.
- Page stage: 32px gutters around pages, 32px vertical gap between pages.
- Dialog widths: 420px for compact forms, 560px for table/image/page setup, up to 720px for print/export previews. Maximum height is 90% of the window.
- Spacing scale: `{spacing.1}` 4px through `{spacing.8}` 64px. Use 4px for icon-to-label, 8px between related controls, 12px inside panels, 16px between groups, 24px inside dialogs, 32px between page and frame.

The document canvas uses a **page-first layout**, not a web-style content column. Pages keep their aspect ratio and are scaled by zoom. The left ruler aligns with the page's left edge, not the viewport; the top ruler aligns with the page's top edge. Rulers scroll with the page stage in the axis they measure.

At narrower windows the shell gives space to the page before it gives space to optional panels. Both side panels never dock simultaneously below 1100px. The formatting toolbar does not wrap; secondary actions move into an overflow menu.

## Elevation & Depth

Depth is restrained to three levels:

1. **Desk:** the flat canvas surround.
2. **Page:** a paper surface with a 1px subtle border and a soft shadow that separates it from the desk.
3. **Overlay:** dialogs, menus, popovers, and the Find panel when it floats over the canvas.

The page shadow is `0 1px 2px rgba(16, 24, 32, 0.12), 0 10px 30px rgba(16, 24, 32, 0.10)` in light mode. In dark mode, depth is expressed more by tone and border than by shadow; shadows remain subtle and never become glow effects. Dialogs use a single soft shadow and no border-heavy frame. Buttons, fields, toolbar groups, and banners are flat. There are no button drop shadows, no glass blur, and no floating cards inside the page.

Elevation never substitutes for hierarchy. If a control needs attention, use position, spacing, typography, and a focus ring, not a larger shadow.

## Shapes

The shape language is **paper and precision**:

- Pages, rulers, table cells, and document images have square corners. The document is paper, not a rounded card.
- Buttons, fields, menus, and tool buttons use `{rounded.sm}` (4px).
- Docked panels use `{rounded.md}` only when floating; docked edges are square.
- Dialogs use `{rounded.lg}` (8px).
- Selection handles are 8px squares with a 1px light outline; they are not circles.
- Pills are reserved for compact status tokens only and are not used for buttons or navigation.

Corner radii are never mixed decoratively. A surface either belongs to the paper world (square) or the control world (4px/8px).

## Components

### Window shell and menu bar

The native window title shows the document name, with an asterisk appended when dirty, for example `grant-application.docx *`. On Windows and Linux the menu bar is rendered inside the window immediately below the title bar. On macOS the menu bar is the system menu bar; the in-window menu is hidden. The menu uses 32px height, `{typography.menu}`, `{colors.surface-chrome}`, and a 1px bottom border. Menu items show checkmarks for toggled states, shortcut labels aligned right, and disabled items in `{colors.ink-disabled}` without removing their labels.

### Formatting toolbar

The toolbar is a single compact row with clear groups: history, style, font, character formatting, paragraph formatting, insert, and find. Groups are separated by 1px vertical rules with 8px breathing room. Icon buttons are 32x32 with `{rounded.sm}`; active states use accent-soft fill, accent icon, and a 2px accent bottom bar so they are not color-only. The style picker is 160px wide, the font picker 160px, and the size field 64px. Text labels appear only for ambiguous actions; every icon has a tooltip and an accessible name with its shortcut.

At widths below 1200px, the least-used insert and paragraph actions move into a `More` overflow menu. The toolbar never becomes two rows and never scrolls horizontally. In read-only mode the toolbar remains visible but disabled, with the reason available from a read-only banner and accessible descriptions.

### Styles panel

The Styles panel is a 264px right dock. Its header uses `{typography.ui-body-strong}` and a compact close button. The current style is shown in a 48px summary row with a live preview. The style list starts with Normal and the document's heading styles, each row 40px high with a preview in the document's style and the style name in UI type. Selected rows use `{colors.accent-soft}` plus a 2px left accent bar. Hover uses `{colors.surface-hover}`. Style rows are keyboard-focusable; Enter or Space applies the style. A footer offers `New style` and `Modify` only when the document is editable.

`[ASSUMPTION]` v1 supports applying document styles and basic modification of the current style. A full style manager, theme gallery, and style inheritance editor are out of scope.

### Navigation / Find panel

The panel is a 320px left dock or a floating sheet at narrow widths. It has two tabs: `Navigation` and `Find`. The tab strip is 36px high. Navigation lists headings in document order with indentation by level; clicking or pressing Enter scrolls the canvas and places the caret at the heading. Find contains a 32px search field, previous/next buttons, a result count, a results list with one line of context, and a collapsible Replace row. Match highlights use `{colors.selection-bg}`; the active match uses a stronger accent outline. Empty, loading, no-result, and wrapped-search states are text messages, not blank areas.

### Document canvas and page stage

The desk uses `{colors.surface-desk}`. Each page is a square-cornered paper surface with `{colors.page-surface}`, a 1px `{colors.border-subtle}` border, and the page shadow. Pages are stacked vertically with `{spacing.6}` gaps. Each page has an accessible label such as `Page 2 of 6`. Page boundaries are always visible; the canvas never becomes an infinite white sheet.

The caret is a 1.5px accent line with a visible blink. The selection is `{colors.selection-bg}` with `{colors.selection-ink}`. IME composition is underlined with a 2px dotted accent line and never commits formatting early. Table cell selection uses a 2px accent outline; image selection uses a 2px accent outline with square handles. A selected object also receives an accessible state name, not just a visual outline.

The editor viewport shows a 2px inset focus ring when focus enters the canvas from chrome. Once the caret is active, the caret and selection carry focus. The page stage never hides a focused caret behind a ruler or panel.

### Rulers

Top and left rulers are 20px thick, use `{colors.ruler-bg}` and `{colors.ruler-tick}`, and show the page margins as shaded bands using `{colors.surface-sunken}`. Ticks follow the system measurement unit (inches for US locale, centimeters elsewhere). Major labels every inch or centimeter, minor ticks at eighths or millimeters. Rulers scale with zoom and align to the page, not the window. They are display-only in v1; page margins change through Page setup. The ruler has an accessible summary, for example `Top ruler, margins 1 inch left and right`.

### Header and footer editing mode

Header and footer zones sit inside the page boundaries, painted with `{colors.header-band}` when active and separated by a 1px `{colors.border-subtle}` rule. A label chip reads `Header - Section 1` or `Footer - Section 1`, with a `Close` button. While the mode is active, the body remains fully visible and legible but is not editable; the active zone has an accent border and a caret. A thin mode strip above the page stage repeats the mode name, the section, and `Esc to close`. Page-number fields render as their resolved value with a subtle field tint, never as raw XML.

`[ASSUMPTION]` v1 exposes one header and one footer per section and supports simple `Page N` and `Page N of M` fields. Per-section linking, different first-page headers, and odd/even headers remain open questions.

### Status bar

The status bar is 28px, `{colors.surface-chrome}`, `{typography.status}`, and `{colors.ink-secondary}`. It contains, from left to right: document save state (`Saved`, `Saving...`, `Unsaved changes`, or `Read-only`), page position (`Page 1 of 6`), word count (`842 words`), and on the right a zoom control (`100%`, minus, plus, Fit). Items are separated by 1px subtle dividers. The page position and word count are buttons that open small explanatory popovers. The zoom value opens a preset menu. All values have accessible names and are announced on change, not continuously while typing.

### Buttons, fields, menus, and focus

Buttons are 32px high, 4px radius, with 12px horizontal padding. Primary buttons use `{colors.accent}` and `{colors.on-accent}`. Secondary buttons are transparent with `{colors.border-strong}`. Destructive actions use a secondary button with `{colors.error-ink}` text plus a confirmation; they are not filled with bright red. Fields are 32px high, 4px radius, 1px `{colors.border-strong}`, and switch to a focus border plus ring. Menus and popovers are 1px bordered, 6px radius, with 4px vertical padding and 28px rows.

Focus is a 2px `{colors.focus-ring}` ring with a 2px offset. It is never suppressed for mouse users; pointer interactions may use hover, but keyboard focus always shows. Invalid fields show a 1px error border, an error icon, and an inline message linked by accessible name.

### Dialogs

Dialogs are app-modal, centered over the window, 420px to 720px wide, with 24px padding, `{rounded.lg}`, and a single soft shadow. The title uses `{typography.dialog-title}`. Actions sit bottom-right in platform order: Cancel then the primary action. Enter activates the default action; Escape cancels. Dialogs trap focus, restore focus to the invoking control on close, and never stack more than one level deep. Long content scrolls inside the dialog body; the action row stays pinned.

### Banners and notices

Banners sit above the page stage, never over document text. Warning banners use `{colors.warning-soft}`, `{colors.warning-ink}`, and a 3px left rule. Error banners use the error pair. Each banner has an icon, a short title, one sentence of context, and at most two actions. An `x` dismissal is available only for non-critical notices; dismissing collapses the notice to a persistent status-bar indicator rather than making it disappear silently. Critical errors never auto-dismiss.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Make the page the visual focus. | Make the toolbar or side panel the loudest surface. |
| Use neutral ink for hierarchy and blue for working state. | Use multiple accent colors or decorative gradients. |
| Keep document fonts, colors, and images untouched. | Apply the UI theme to the page or invert the document in dark mode. |
| Use 4px/8px geometry and square paper edges. | Round every surface or turn pages into floating cards. |
| Pair every status color with an icon and text. | Use red, amber, or green alone to communicate state. |
| Keep the toolbar compact and overflow predictable. | Wrap the toolbar into a second row or hide actions without a menu. |
| Show focus with a 2px ring and a clear caret. | Remove focus outlines or rely on hover only. |
| Use dialogs for blocking decisions and banners for recoverable context. | Open a modal for every warning or stack dialogs. |
| Preserve the document's page metrics at every zoom. | Introduce UI margins or scaling that changes pagination. |
| State unsupported or missing content plainly. | Imply Word-level fidelity or silently drop content. |
| Use Inter for chrome and tabular figures for counts. | Use a display serif, all-caps labels, or tiny status text. |
| Respect platform menu, shortcut, file, and print conventions. | Invent a platform-neutral shell that ignores native expectations. |

---

# Part 2 - EXPERIENCE.md

This is the behavioral contract for MicroslopWords. Visual tokens are referenced from Part 1 with `{path.to.token}` syntax. The experience spine owns layout behavior, states, interaction, accessibility, and journeys; the design spine owns appearance. Both win over any mock.

## Foundation

**Form factor:** native desktop application for Windows, macOS, and Linux. The target window is 1280x840; the minimum is 900x600. There is no mobile or web layout in v1.

**UI system:** Avalonia 12.1.4 with Fluent as the base control theme, restyled by the tokens in Part 1. MVVM owns commands and state. The page is a custom Skia-rendered canvas; surrounding chrome uses Avalonia controls. The UI chrome uses Inter; the page uses document fonts.

**Document model:** sections contain blocks and runs, all edits flow through commands, and layout is a deterministic function of document state, styles, page settings, and constraints. The UX assumes deterministic pagination and a command-driven undo history.

**Window model:** one document per window, no tabs. Opening or creating another document from an existing window prompts to save unsaved changes, then replaces the document in that window. `[ASSUMPTION]` Multiple windows and tabbed documents are not part of v1.

**Scope boundary:** no accounts, sign-in, sharing, comments, presence, coauthoring, cloud sync, add-in marketplace, or macro execution. Any surface that would imply those capabilities is absent.

## Information Architecture

| Surface | Reached from | Purpose |
|---|---|---|
| Welcome / first launch | App launch with no previous document | Start a blank document, open a DOCX, or recover unsaved work |
| Document workspace | New or successful open | Menu, toolbar, panels, page canvas, rulers, and status |
| Menu bar | Persistent | Complete command surface, including View and Help |
| Formatting toolbar | Persistent | Fast character, paragraph, style, insert, and find actions |
| Styles panel | View menu, toolbar `Styles`, `Ctrl/Cmd+Alt+Shift+S` | Browse and apply document styles |
| Navigation / Find panel | View menu or `Ctrl/Cmd+F` | Navigate headings; find and replace body text |
| Document canvas and rulers | Workspace center | Edit pages, inspect boundaries and margins, select objects |
| Header/footer mode | Insert menu or double-click a header/footer zone | Edit the active section's header or footer |
| Open dialog | File > Open, `Ctrl/Cmd+O`, drop a DOCX | Choose a `.docx` file using the native picker |
| Save As dialog | File > Save As, first save, or recovery action | Choose file name and location using the native picker |
| Page setup dialog | Format > Page setup | Set page size, orientation, and margins |
| Insert table dialog | Insert > Table | Choose rows, columns, and header row |
| Image insertion dialog | Insert > Image | Choose an image, set alt text and initial size |
| Print / Export PDF dialog | File > Print or File > Export PDF | Preview, choose page range, print or export |
| Unsupported feature notice | Import or save | Explain what is preserved, read-only, or omitted |
| Recovery / error dialog | App launch, save, open, or export failure | Recover work, retry, save a copy, or inspect details |
| Status bar | Persistent | Save state, page count, word count, zoom |

### Window map

```text
+------------------------------------------------------------------+
| Menu bar (macOS uses the system menu bar)                        |
+------------------------------------------------------------------+
| Formatting toolbar                                               |
+-----------+------------------------------------------------------+
| Nav/Find  | Top ruler                                            |
| (optional)+------------------------------------------------------+
|           | Left ruler | Page stage (pages stacked)  | Styles    |
|           |            |                             | (optional)|
|           |            |                             |           |
+-----------+------------------------------------------------------+
| Status bar: Saved | Page 1 of 6 | 842 words | 100%  -  +  Fit  |
+------------------------------------------------------------------+
```

### Menu map

| Menu | Items |
|---|---|
| File | New, Open, Open Recent, Save, Save As, Export PDF, Print, Close / Exit |
| Edit | Undo, Redo, Cut, Copy, Paste, Select All, Find, Find and Replace |
| View | Navigation, Styles, Rulers, Zoom (In, Out, 100%, Fit Width, Fit Page), Theme (System, Light, Dark), Document text |
| Insert | Table, Image, Page break, Header, Footer, Page number |
| Format | Bold, Italic, Underline, Text color, Align left/center/right/justify, Bulleted list, Numbered list, Styles, Clear formatting, Page setup |
| Table | Insert row above/below, Insert column left/right, Delete row, Delete column, Toggle header row, Delete table |
| Help | Keyboard shortcuts, Documentation, About MicroslopWords |

`[ASSUMPTION]` v1 table formatting is intentionally basic: insert/delete rows and columns, toggle a header row, and delete the table. Cell merging, formulas, and nested tables are out of scope.

## Voice and Tone

The voice is plain, calm, and factual. It speaks to an adult doing focused work. It never celebrates ordinary operations, never blames the user, and never hides a fidelity gap behind vague language.

| Do | Don't |
|---|---|
| "Open a DOCX" | "Import your file now!" |
| "Saved to C:\Documents\release-notes.docx" | "Success! Your file was saved successfully." |
| "Couldn't open 'paper.docx'. It may be damaged or use a feature this version can't read." | "Oops! Something went wrong." |
| "3 features can't be edited here. They are preserved when you save." | "Unsupported file. Proceed at your own risk." |
| "Calibri isn't available. Using Carlito for display; the document font is unchanged." | "Font error." |
| "No matches for 'colour'." | "Nothing found. Try a different search!" |
| "Your changes are still in this window. Save a copy?" | "Save failed. All is lost." |
| "This document is open read-only. Save a copy to edit." | "Permission denied." |
| "Replace 3 occurrences?" | "Are you really sure?" |

Rules:

- Use sentence case. No exclamation marks. No emojis. No anthropomorphic error messages.
- Buttons name the action: `Save a copy`, `Replace all`, `Close without saving`, not `OK` when a verb is clearer.
- Numbers are formatted with the system locale. File names are shown in full when they are the subject of a dialog.
- Unsupported content is described by what it is and what will happen, not by an internal XML name.
- Save success is reported in the status bar; a modal is never used to announce a successful save.

## Component Patterns

| Component | Use | Behavioral rules |
|---|---|---|
| Menu bar | Global commands | Click, `Alt`/`F10` on Windows/Linux, or the native macOS menu. Arrow keys navigate; `Esc` closes and restores focus. Checkable View items show a check. Shortcuts are always displayed. |
| Formatting toolbar | Fast formatting | Reflects the current selection. Mixed values show `Mixed` or an indeterminate mark, not a blank. Toggling a mixed property applies it to the whole selection. Overflow menu is keyboard reachable. Read-only disables editing actions. |
| Style picker / Styles panel | Apply paragraph styles | Current style is highlighted. Applying a style is immediate and undoable. The panel shows the document's style names and previews; it never invents a parallel style taxonomy. `Enter` or Space applies the focused row. |
| Navigation tab | Jump through structure | Lists heading-styled paragraphs in order. Click/Enter scrolls and places the caret. Updates after layout settles. Shows an empty message when there are no headings. |
| Find / Replace | Locate and replace body text | `Ctrl/Cmd+F` opens the panel and pre-fills the selected text. Search is case-insensitive by default; Match case and Whole words are toggles. Results update after a short debounce. `Enter` next, `Shift+Enter` previous, `Esc` closes and returns to the document at the active match. Replace all asks for confirmation with a count and is one undo step. |
| Document canvas | Edit text and objects | Click places the caret; double-click selects a word; triple-click selects a paragraph; drag selects; `Shift`+click extends. Clicking in the gray area beyond the text places the caret at the nearest text position on that page. The canvas never scrolls horizontally without a reason; it never snaps zoom unexpectedly. |
| Rulers | Show page geometry | Align with the page, scale with zoom, show margins and current section geometry. Display-only in v1; Page setup is the editable source. Accessible summary is available from the canvas. |
| Header/footer mode | Edit page furniture | Enter by Insert menu or double-clicking the header/footer zone. Focus moves into the zone. Body is visible but not editable. `Esc` or `Close` exits and returns focus to the nearest body caret. Page-number fields update after layout. |
| Status bar | Persistent document telemetry | Page count and word count update after layout settles. Word count excludes headers and footers. Zoom opens presets and supports keyboard adjustment. Save state is always text, not a colored dot. |
| Open dialog | Choose a DOCX | Native picker with `DOCX` and `All files` filters. Single selection. Invalid files produce an inline error in the open-error dialog, not a silent failure. Recent files are local only. |
| Save As dialog | Choose a destination | Native picker with `.docx` filter. Default name based on the document title. If the user omits the extension, the app adds `.docx` after confirming. Overwrite confirmation comes from the platform. |
| Page setup | Fix document geometry | Page size (Letter, A4, and current size), orientation, and margins. A live summary shows the resulting content width and height in the user's unit. Apply is undoable and triggers repagination. |
| Insert table | Create a table | Rows and columns use numeric steppers with direct entry. Defaults: 3 rows, 3 columns, header row on. Limits are 1-20 rows and 1-12 columns. A small grid preview shows the result. `Enter` inserts; `Esc` cancels. |
| Image insertion | Place an image | Native file picker for PNG, JPEG, and GIF in v1. The image settings step shows a preview, file name, dimensions, width, maintain-aspect toggle, and an Alt text field. Natural size is capped to the content width. Blank alt text falls back to the file name and shows a non-blocking hint. |
| Image editing | Adjust an inserted image | Selection shows square handles, a 2px accent outline, and a contextual toolbar or context menu for Alt text, size, replace, and delete. Resizing preserves aspect ratio by default; `Shift` unlocks it. |
| Table editing | Edit tabular content | `Tab` moves to the next cell, `Shift+Tab` to the previous. `Tab` in the last cell adds a row `[ASSUMPTION]`. Arrow keys move within cell text; `Ctrl/Cmd+Arrow` moves by word. Context menu and Table menu offer row/column operations. Selected cells have an accent outline and an announced row/column position. |
| Print / Export PDF | Produce output | Shows a page preview, page range (All, Current page, Pages), and include headers/footers. Print hands off to the platform print dialog; Export PDF asks for a destination, shows progress with Cancel, and reports the output path on completion. Both use the same pagination as the canvas. |
| Unsupported notice | Explain fidelity limits | A warning banner appears above the page stage with a count and a `Review` action. The review dialog lists each feature, its location, and whether it is preserved, read-only, or omitted. Dismissing collapses to a persistent status-bar indicator. Critical omissions cannot be dismissed without acknowledgement. |
| Recovery / error | Protect work | Recovery appears on launch when a recent unsaved snapshot exists. Error dialogs preserve the current buffer. `Save a copy`, `Retry`, `Choose another file`, and `Details` are available where applicable. Destructive choices require explicit names, not just `Yes`. |
| Context menus | Local actions | Right-click or the context-menu key / `Shift+F10` opens actions for text, tables, images, and header/footer zones. Menus never contain actions that are unavailable elsewhere. |

## State Patterns

| State | Surface | Treatment |
|---|---|---|
| First launch / no document | Welcome | Paper-shaped start area. Title `Start writing`. Actions: `New blank document`, `Open DOCX`; local recent list below. No sign-in, no sample cloud content. If a recovery snapshot exists, show it first. |
| Empty document | Canvas | One blank page, caret in the first paragraph, status `Page 1 of 1`, `0 words`, zoom `Fit width` or `100%`. No placeholder text printed inside the page. |
| Open / import loading | Welcome or workspace | Show a compact `Opening document...` overlay after 150ms. First page appears before the full document if layout takes longer; progress is available after 1s. Cancel is available for large files. The previous document never disappears until the new one is ready. |
| Layout updating | Canvas | No blocking spinner. Status bar shows `Updating layout...` when repagination exceeds 200ms. Keyboard input remains responsive; the caret is never detached from the text. |
| Dirty | Title bar and status bar | Title `name.docx *`; status `Unsaved changes`. Save button is enabled. Closing, opening another document, or creating a new one asks to save. No color-only indicator. |
| Saving | Status bar | `Saving...` for the duration; success becomes `Saved`. Failure becomes a modal with `Retry`, `Save a copy`, and `Details`; the document stays open and dirty. |
| Read-only | Banner, title, toolbar | Banner `This document is open read-only. Save a copy to edit.` Title includes `(read-only)`. Editing commands and formatting controls are disabled. Selection, find, print, and Export PDF remain available. `Save a copy` is the primary action. |
| Unsupported feature | Banner and review dialog | Banner states the count and whether content is preserved. Review lists feature, location, and outcome. The document opens editable if safe; otherwise read-only. Macros never run. The notice remains visible until the feature is no longer present or the user acknowledges a critical omission. |
| Missing font | Banner above canvas | `Calibri isn't available. Using Carlito for display; the document font is unchanged.` Action `Review fonts`. The banner is informational, not a save blocker. If no fallback exists, show the closest available font and a stronger warning. |
| Recovery available | Launch dialog | `Recover unsaved work?` with document name, last snapshot time, and page count. Actions `Open recovered draft` and `Discard`. Discard requires a second confirmation. Never overwrite the original automatically. |
| Find: no results | Navigation/Find panel | `No matches for 'term'.` and `Clear search`. The document selection and scroll position do not move. |
| Navigation: no headings | Navigation tab | `No headings yet. Apply Heading 1-3 to build navigation.` No dead list. |
| Image load failure | Canvas or image dialog | Labeled placeholder with `Image unavailable`, file name, and `Replace` / `Remove`. The document does not crash or silently collapse layout. |
| Export progress | Print/Export dialog | `Exporting page 3 of 7` with Cancel. Completion shows the path and `Show in folder`. Failure stays in the dialog with `Choose another location` and `Details`. |
| Zoom extremes | Canvas and status bar | Range 50%-400%. At high zoom the canvas scrolls in both axes; at low zoom pages stay centered. The zoom value is always readable and keyboard-adjustable. |
| Window inactive | Chrome | Chrome and page keep their colors; the title bar follows the platform. No custom dimming that could be confused with read-only. |

## Interaction Primitives

### Editing and selection

- Click to place the caret. Double-click selects a word. Triple-click selects a paragraph. Drag extends the selection.
- `Shift`+click extends the selection from the current anchor.
- Keyboard selection uses `Shift` with arrows, `Home`/`End`, `Ctrl/Cmd+Arrow` by word, and `Ctrl/Cmd+Shift+Arrow` by word selection.
- Typing replaces a selection. Undo groups contiguous typing into natural units (word or short pause) and treats formatting actions as separate undo steps. `[ASSUMPTION]`
- `Ctrl/Cmd+Z` and `Ctrl/Cmd+Shift+Z` (or `Ctrl+Y` on Windows/Linux) undo and redo through the command history.
- The caret is always visible. It is never hidden by a toolbar, ruler, panel, or header/footer mode strip.

### Menus, toolbar, and focus

- Keyboard focus order follows the visual order: menu, toolbar, left panel, canvas, right panel, status. `F6` cycles major regions; `Shift+F6` reverses.
- `Tab` moves within a region. `Enter`/`Space` activates. `Esc` closes the topmost menu, popover, panel, or mode.
- Menus open with `Alt`/`F10` on Windows/Linux; macOS uses system menu conventions. Arrow keys navigate menus, `Left`/`Right` move between top-level menus, and `Esc` returns focus to the invoker.
- Toolbar buttons show tooltips with names and shortcuts. The overflow menu is a normal menu, not a custom canvas popup.
- Focus changes are never used as a substitute for selection changes. Moving focus to a panel does not move the document caret.

### Panels and surfaces

- `Ctrl/Cmd+F` opens or focuses Find and selects the current word, if any. Closing Find returns focus to the document at the active match.
- `Ctrl/Cmd+Alt+Shift+S` toggles Styles. Opening a panel does not steal focus from the document unless the panel is explicitly invoked by keyboard.
- At narrow widths, opening one side panel closes the other. Both panels can be open when docked if the window is wide enough.
- Header/footer mode is a canvas mode, not a dialog. `Esc` exits and returns focus to the last body caret. `F6` can move to chrome without leaving the mode; focus returns to the header when the canvas is re-entered.

### Mouse, wheel, and drag

- Wheel scrolls vertically; `Shift`+wheel scrolls horizontally; `Ctrl/Cmd`+wheel zooms around the pointer.
- Middle-drag and space-drag panning are not part of v1. Scrollbars are always available.
- Dropping a `.docx` on the window opens it after the dirty prompt. Dropping a supported image while the canvas is focused inserts it at the caret `[ASSUMPTION]`.
- Dragging selected text to move it is not part of v1; cut and paste is the reliable path.
- Right-click opens a context menu for text, tables, images, and header/footer zones. The keyboard context-menu key and `Shift+F10` do the same.

### Structural editing

- Tables are inserted as editable document content, not floating UI. `Tab` moves cells and creates a row at the end. Row and column commands are available from the Table menu and context menu.
- Images are inline objects by default. They flow with text and paginate. Selection handles resize the object; Alt text is editable from the context menu and the Image settings surface.
- Header and footer edits are bound to the current section. Page-number fields are document fields, not static text, and update after repagination.
- Page setup changes are undoable and never silently discard content. If a margin change makes content overflow, the page count changes visibly in the status bar.

### Banned interactions

- No hover-only actions that have no keyboard equivalent.
- No nested modal dialogs. A dialog may open a confirmation sheet or a details popover, but not another full dialog.
- No color-only state. No blinking status icons. No animated page flips.
- No auto-saving into the original DOCX without an explicit save. Recovery snapshots are separate local artifacts.
- No cloud, share, comment, presence, or account affordances anywhere in v1.

## Accessibility Floor

This is the behavioral accessibility contract. Visual contrast is defined in Part 1.

- **Standard:** WCAG 2.2 AA for the application chrome, dialogs, menus, and status. Document content remains the document's responsibility, but editor-inserted UI (selection, caret, handles, banners) meets the same floor.
- **Visible focus:** every interactive element has a visible 2px `{colors.focus-ring}` ring with a 2px offset. Focus is never removed. On the custom canvas, focus is shown by the viewport inset ring, caret, and selection.
- **Full keyboard path:** create, open, edit, select, format, apply styles, insert table, insert image, edit alt text, add header/page number, find/replace, save, print, export PDF, recover, and dismiss unsupported notices are all reachable without a pointer.
- **Keyboard shortcuts:** all core actions have platform-appropriate shortcuts and are discoverable in menus and Help > Keyboard shortcuts.
- **Screen-reader names:** menus, toolbar buttons, fields, tabs, dialogs, banners, style rows, find results, table position, and image selections have role, name, and state. Icon-only buttons never expose only an icon.
- **Canvas accessibility:** the Skia canvas exposes document name, page count, current page, caret context, selected text, and active formatting through the platform automation layer. A read-only `View > Document text` surface provides a keyboard-navigable text representation when the platform text pattern is unavailable. `[ASSUMPTION]`
- **Announcements:** save state, find result count, page-count changes after layout, validation errors, unsupported notices, and recovery availability are announced through a polite live region. Typing is not announced character by character.
- **High contrast:** respect Windows High Contrast and platform forced-colors settings. Use system colors for chrome when forced colors are active; the page canvas switches to a high-contrast page palette with visible object outlines. No meaning depends on the blue accent.
- **No color-only meaning:** dirty uses `*` plus status text; errors use icon + title + text; active states use fill + border or underline + text; read-only uses a lock icon plus a banner label; selection is not the only indicator of focus.
- **Targets:** desktop interactive targets are at least 32x32px with at least 4px spacing. Text fields are at least 32px high.
- **Zoom and scaling:** application chrome follows platform display scaling at 100%-200%. The document canvas supports 50%-400% zoom. At 200% OS scaling, dialogs remain usable and never clip primary actions.
- **Reduced motion:** honor the platform reduced-motion setting. Disable caret blink smoothing, zoom easing, and any panel slide animation; transitions are 100ms or less and never required to understand state.
- **IME and composition:** composition text is visible, underlined, and never committed early or formatted mid-composition. Candidate windows are positioned at the caret.
- **Errors and recovery:** every error message names the failed action, preserves the current buffer, and offers a next step. Recovery is keyboard reachable on launch and never auto-discards work.
- **Accessible text fallback:** `View > Document text` opens a read-only, scrollable text view with headings and paragraph boundaries. It is a fallback for assistive technology, not a second editable document.

### Keyboard shortcut contract

`Cmd` is used on macOS; `Ctrl` on Windows and Linux. Shortcuts are shown in menus with platform glyphs.

| Action | Shortcut |
|---|---|
| New / Open / Save / Save As | `Ctrl/Cmd+N`, `Ctrl/Cmd+O`, `Ctrl/Cmd+S`, `Ctrl/Cmd+Shift+S` |
| Print / Export PDF | `Ctrl/Cmd+P`, `Ctrl/Cmd+Shift+E` |
| Undo / Redo | `Ctrl/Cmd+Z`, `Ctrl/Cmd+Shift+Z` (Windows/Linux also `Ctrl+Y`) |
| Cut / Copy / Paste / Select all | `Ctrl/Cmd+X`, `Ctrl/Cmd+C`, `Ctrl/Cmd+V`, `Ctrl/Cmd+A` |
| Find / Replace / Find next / previous | `Ctrl/Cmd+F`, `Ctrl/Cmd+H`, `F3`, `Shift+F3` |
| Bold / Italic / Underline | `Ctrl/Cmd+B`, `Ctrl/Cmd+I`, `Ctrl/Cmd+U` |
| Increase / decrease font size | `Ctrl/Cmd+Shift+>`, `Ctrl/Cmd+Shift+<` |
| Clear formatting | `Ctrl/Cmd+Space` |
| Zoom in / out / 100% / fit width | `Ctrl/Cmd+=`, `Ctrl/Cmd+-`, `Ctrl/Cmd+0`, `Ctrl/Cmd+Shift+0` |
| Cycle panes / reverse | `F6`, `Shift+F6` |
| Open menu (Windows/Linux) | `Alt` or `F10` |
| Close top layer / exit header mode | `Esc` |
| Context menu | Menu key or `Shift+F10` |

## Key Flows

### Flow 1 - First launch and create (Noor, graduate student, first launch on a Linux laptop)

1. Noor launches MicroslopWords. The Welcome surface shows `Start writing`, `New blank document`, `Open DOCX`, and an empty local recent list. There is no sign-in and no cloud content.
2. She tabs to `New blank document` and presses Enter. A Letter page appears with the caret in the first paragraph, zoom at `Fit width`, and status `Page 1 of 1`, `0 words`.
3. She types a working title and a first paragraph. The title bar reads `Untitled`, then `Untitled *` once the first character lands.
4. She selects the title, opens the style picker, and applies Heading 1. The page reflows; the style panel shows Heading 1 as current.
5. **Climax:** The empty window has become a real document. The title, page, status, and save state all agree, and the next action is obvious: `Ctrl+S` to choose a file name.

Failure: if a recovery snapshot exists, the Welcome surface shows `Recover unsaved work?` before any new document action. New document never overwrites a recovery file.

### Flow 2 - Open a real DOCX and fix layout (Elena, researcher, Windows)

1. Elena presses `Ctrl+O` and chooses `grant-application.docx` from the native picker.
2. After 150ms the workspace shows `Opening document...`. The first page appears as soon as layout is ready; the rest follows.
3. The document opens with a non-blocking notice: `Calibri isn't available. Using Carlito for display; the document font is unchanged.` The page count is 7.
4. She opens `Format > Page setup`, changes the page to A4 and margins to 2.54 cm, and applies. The document repaginates to 6 pages.
5. She uses the Navigation panel to jump through headings and applies the document's Heading 1 style where the imported formatting drifted.
6. **Climax:** The page boundaries, ruler margins, status count, and heading hierarchy all settle into a coherent document. The font notice is visible but does not block editing, and no content was dropped.

Failure: if a font has no usable fallback, the notice becomes a stronger warning and names the substituted font. The document still opens.

### Flow 3 - Select and format text (Tomas, writer, macOS)

1. Tomas clicks into a sentence, double-clicks a word, then `Shift`+clicks to extend the selection. The toolbar reflects the selection's font and size.
2. He presses `Cmd+B`. The selected words become bold and the selection remains. The toolbar's bold button is active with fill and underline, not color alone.
3. He opens the Styles panel with `Cmd+Alt+Shift+S`, navigates to Heading 2, and presses Enter.
4. He presses `Cmd+Z`; the style application is undone in one step, then redone with `Cmd+Shift+Z`.
5. **Climax:** The formatting is visible in the page, reflected in the toolbar and Styles panel, and reversible without losing the selection. The title bar marks the document dirty.

Failure: with a mixed selection, the toolbar shows `Mixed`; applying bold makes the entire selection bold. No silent partial formatting.

### Flow 4 - Insert a table and an image (Priya, product manager, Windows)

1. Priya places the caret after a paragraph and chooses `Insert > Table`. The dialog defaults to 3 rows, 3 columns, and Header row on.
2. She sets 4 columns, presses Enter, and the table appears with the caret in the first header cell.
3. She tabs through cells and types. `Tab` in the last cell adds a row. The Table menu remains available from the context menu and menu bar.
4. She chooses `Insert > Image`, picks `chart.png`, enters alt text `Quarterly revenue by region`, keeps `Maintain aspect ratio` on, and inserts at content width.
5. **Climax:** The table and image stay inside the page margins, pagination reflows cleanly, the image keeps its aspect ratio, and the status bar's word count updates without blocking her typing.

Failure: a corrupt image produces `Couldn't insert image. The file may be damaged.` The table and existing content stay intact.

### Flow 5 - Add a header and page number (Marcus, student, Linux)

1. Marcus chooses `Insert > Header > Default`. The canvas enters header mode with a `Header - Section 1` label; the body remains visible but inactive.
2. He types `Research Methods - Draft` and presses `Esc` to return to the body.
3. He chooses `Insert > Page number > Bottom of page > Right`. A page-number field appears in the footer.
4. He scrolls to page 2. The footer shows `2`; the header repeats.
5. **Climax:** The header and footer are visibly part of the page, page numbers resolve correctly on every page, and focus returns to the body caret when he finishes.

Failure: if the document is read-only, the header controls are disabled and the read-only banner explains `Save a copy to edit`.

### Flow 6 - Find and replace (Aisha, editor, macOS)

1. Aisha presses `Cmd+F`. The Navigation/Find panel opens on Find and pre-fills the current selection, if there is one.
2. She types `colour`. Results list matching lines with context and a count. She presses Enter to move to the next match; the canvas scrolls and highlights it.
3. She opens the Replace row with `Cmd+H`, types `color`, and chooses `Replace all`.
4. A confirmation says `Replace 3 occurrences?` She confirms. All three change; the panel reports `Replaced 3 occurrences. 0 matches.` The document is dirty.
5. **Climax:** The document reads consistently, the panel keeps its place and context, and one `Cmd+Z` restores all three replacements.

Failure: no matches shows `No matches for 'colour'.` with `Clear search`; the document scroll position does not move.

### Flow 7 - Save and export PDF (Ben, contributor, Windows)

1. Ben edits `release-notes.docx` and presses `Ctrl+S`. The status bar shows `Saving...`, then `Saved`; the title asterisk clears.
2. He chooses `File > Export PDF`. The dialog shows a page preview, page range `All`, include headers/footers checked, and a destination path.
3. He exports. The dialog shows `Exporting page 3 of 7` with Cancel. On completion it reports the PDF path with `Show in folder`.
4. He opens the PDF and compares it with the editor. The page count and header/footer placement match.
5. **Climax:** The document is saved as DOCX, the PDF is a faithful render of the same pagination, and the document buffer is untouched and still editable.

Failure: if the save location is not writable, `Couldn't save 'release-notes.docx'. Your changes are still in this window.` appears with `Retry`, `Save a copy`, and `Details`. Export uses the same recovery pattern.

### Flow 8 - Keyboard-only editing (Sam, developer with RSI, Linux)

1. Sam launches the app. `Tab` reaches `New blank document`; `Enter` opens a page. Focus moves to the canvas with a visible focus ring and caret.
2. He types, selects with `Shift`+arrows, and applies bold with `Ctrl+B`.
3. `F6` moves to the toolbar. He arrows to the alignment button and presses Enter. `F6` returns to the canvas.
4. `Ctrl+F` opens Find, he searches, presses Enter, and `Esc` returns to the active match. `Ctrl+S` saves and `Ctrl+P` opens print.
5. **Climax:** Sam completes the edit, search, save, and print path without touching a pointer. Every focus change is visible and every state change is announced or written in the status bar.

Failure: closing a dialog always returns focus to the invoking control, never to the top of the window.

### Flow 9 - Unsupported DOCX feature (Ravi, open-source maintainer, macOS)

1. Ravi opens `legacy-report.docx`, which contains tracked changes, a SmartArt object, and a macro. The macro never runs.
2. A warning banner appears: `3 features can't be edited here.` The document opens with labelled placeholders for content that cannot be represented safely.
3. He chooses `Review`. The dialog lists each feature, its location, and its outcome: preserved, read-only, or omitted. Nothing is silently discarded.
4. Ravi edits the surrounding body text, then chooses `File > Save As`. The save flow keeps unsupported package parts where possible and warns about any omission before writing.
5. **Climax:** Ravi can keep working on the editable content, the unsupported content is visibly accounted for, and the application never claims full fidelity. The macro did not execute.

Failure: if safe editing cannot be guaranteed, the document opens read-only with `Save a copy` as the primary action. Export PDF remains available and clearly excludes content it cannot render.

## Responsive & Platform

### Window behavior

| Window state | Behavior |
|---|---|
| 1280x840 and wider | Both side panels may dock. Page stage centers the page with 32px gutters. Toolbar shows the full command set. |
| 1100-1279px | One side panel at a time. The second panel opens as a floating sheet when invoked. Toolbar labels for secondary insert actions collapse to icons. |
| 900-1099px | Both panels float over the page when opened. The toolbar moves insert and paragraph extras into `More`. Page stage retains at least 480px of usable width before horizontal scrolling. |
| Minimum 900x600 | All required status values remain visible: page count, word count, zoom. Dialogs scroll internally; primary actions never move off-screen. |
| Height below 700px | Menu, toolbar, and status bar remain pinned. The canvas scrolls. Long dialogs cap at 90% of window height. |
| Zoom 50% | Pages remain centered; text is not expected to be readable, only navigable. |
| Zoom 400% | Both axes scroll; the caret is kept in view after keyboard movement. |

- The window opens at 1280x840 unless a valid saved size exists. Remembered positions are clamped to the current monitor on launch.
- At HiDPI, layout is measured in device-independent units; the document is rendered at `DPI scale x zoom` so text stays sharp without changing pagination.
- The page stage never adds UI padding to the document's page geometry. Gutters and page gaps are chrome, not page margins.
- The toolbar never wraps. The overflow menu is always available.
- When one side panel is open at 900px, the canvas remains scrollable and the caret remains visible. Opening the second panel closes the first.

### Platform conventions

| Area | Windows | macOS | Linux |
|---|---|---|---|
| Menu location | In-window menu below title bar | System menu bar | In-window menu below title bar |
| Primary modifier | `Ctrl` | `Cmd` | `Ctrl` |
| File pickers | Native Windows dialogs | Native macOS panels | Desktop-native chooser; Avalonia fallback |
| Print | Native print dialog | Native print panel | Desktop print dialog via platform |
| Window controls | Standard title bar | Traffic lights and system window behavior | Desktop window manager controls |
| Exit/Quit | `File > Exit` | App menu `Quit MicroslopWords` | `File > Exit` |
| About / settings | Help > About; View > Theme | App menu About and Settings equivalents | Help > About; View > Theme |
| Shortcut display | `Ctrl+...` | `Cmd+...` | `Ctrl+...` |

- macOS uses the system menu bar and hides the in-window menu. The same commands and keyboard access remain.
- The app respects platform high-contrast, reduced-motion, and font-scale settings.
- File associations and drag-and-drop use the native `.docx` handling. The app never registers itself as a cloud provider.
- Platform file pickers may expose network locations; the app itself has no cloud UI, account model, or sync state.
- Destructive dialogs use explicit verbs, and platform-default button order is preserved.
- Keyboard focus is never trapped by the custom canvas. `Esc`, `F6`, and menu accelerators always provide an exit path.

## Assumptions and Open Questions

These are recorded as non-blocking design assumptions. They should be confirmed before implementation where they affect layout, DOCX fidelity, or accessibility.

### Assumptions

- `[ASSUMPTION]` One document per window, no tabs. Opening another document replaces the current one after the dirty prompt.
- `[ASSUMPTION]` Inter is bundled; if it is unavailable, the platform sans-serif fallback is acceptable.
- `[ASSUMPTION]` The document page stays paper-white in both themes. Dark mode changes chrome and desk only; document colors are never inverted.
- `[ASSUMPTION]` Recent files and recovery snapshots are local-only and never synced.
- `[ASSUMPTION]` Recovery snapshots are written about every 30 seconds while the document is dirty.
- `[ASSUMPTION]` Page setup supports Letter, A4, current size, portrait/landscape, and margins. Custom paper sizes are optional.
- `[ASSUMPTION]` Tables cap at 20 rows by 12 columns in the insert dialog. Tab at the last cell adds a row.
- `[ASSUMPTION]` Find/replace covers the main body only, supports Match case and Whole words, and does not support regular expressions in v1.
- `[ASSUMPTION]` Word count is whitespace-separated tokens in the body, excluding headers and footers. Counts refresh after a short debounce.
- `[ASSUMPTION]` Image alt text is optional at insertion; the file name is the fallback accessible name and a non-blocking hint recommends a description.
- `[ASSUMPTION]` Unsupported package parts are preserved where the format allows. Unsafe constructs open read-only rather than risk silent loss. Macros never execute.
- `[ASSUMPTION]` Print and file dialogs are platform-native. PDF export uses the same internal pagination as the canvas.
- `[ASSUMPTION]` The exported PDF is a render of the editor layout, not a re-layout by a separate PDF engine.

### Open questions

1. **Section model:** Does v1 support multiple sections with independent headers, footers, and page-number restarts, or one section per document? This affects the header/footer mode and page-number fields.
2. **Fidelity policy:** Which unsupported constructs must be preserved byte-for-byte, which are allowed to become read-only placeholders, and which are acceptable to omit with a warning? The answer determines the unsupported-feature surface and save behavior.
3. **Canvas accessibility:** Is a platform text automation pattern feasible for the Skia canvas in v1, or should the read-only `Document text` view be the primary screen-reader path?
4. **Rulers:** Are rulers display-only for v1, or should margin and indent handles be draggable? The current design assumes display-only with Page setup as the source of truth.
5. **Table depth:** Are merged cells, cell shading, and column-width adjustment required for the first usable milestone? The current design assumes basic row/column editing only.
6. **Page setup scope:** Are mirrored margins, gutter, and custom paper sizes required, or is Letter/A4 with standard margins sufficient for v1?
