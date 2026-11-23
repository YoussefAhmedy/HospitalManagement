# CareAxis — Design System

The interface language for the whole product. One stylesheet,
`HospitalManagement/wwwroot/css/design-system.css`, owns every token and component. No page declares
its own palette.

**Intent:** restrained enterprise healthcare. Deep navy structure, teal accents used sparingly to
denote the active or primary path, neutral surfaces, precise status colour, dense but calm data
tables. Explicitly avoided: gradients, glassmorphism, neon accents, oversized display type, and the
generic "AI dashboard" look of floating cards over a dark field.

---

## 1. Colour

### Structure
| Token | Value | Use |
|---|---|---|
| `--navy-950` | `#0a1622` | Deepest surface, print headers |
| `--navy-900` | `#0f2233` | Sidebar background |
| `--navy-800` | `#16324a` | Primary buttons, brand marks |
| `--navy-700` | `#1e4260` | Primary hover |
| `--navy-600` | `#2a577a` | Secondary emphasis |

### Accent (teal)
| Token | Value | Use |
|---|---|---|
| `--teal-700` | `#0b5f63` | Accent text on light surfaces |
| `--teal-600` | `#0d7c73` | Focus border, active state |
| `--teal-500` | `#12958a` | KPI rail, active indicator |
| `--teal-300` / `--teal-200` / `--teal-100` / `--teal-050` | progressively lighter | Badges, tints, hover rows |

### Neutrals
`--ink-900` … `--ink-400` for type (900 = primary text, 400 = disabled/placeholder),
`--line-300`/`--line-200`/`--line-100` for borders, `--surface-0`…`--surface-3` for grounds.

### Status
Each status has a 700 (text), 600/500 (mark) and 050 (background) step. Status colour is *never*
the only signal — every badge also carries a text label, so the interface remains legible in
greyscale and to colour-blind users.

| Meaning | Token family |
|---|---|
| Success / released / paid | `--success-*` |
| Attention / pending / awaiting | `--warning-*` |
| Danger / critical / rejected | `--danger-*` |
| Information / scheduled | `--info-*` |
| Imaging / in-review | `--purple-*` |

---

## 2. Typography

| Token | Size | Use |
|---|---|---|
| `--text-2xs` | 11px | Table metadata, uppercase labels |
| `--text-xs` | 12px | Badges, hints, table sub-rows |
| `--text-sm` | 13px | Body in dense surfaces, table cells |
| `--text-base` | 14px | Default body |
| `--text-md` | 15px | Card titles |
| `--text-lg` | 17px | Section headings |
| `--text-xl` | 20px | Page subtitles |
| `--text-2xl` | 24px | Page titles |
| `--text-3xl` | 30px | Dashboard headline figure |
| `--text-4xl` | 36px | Marketing hero |

Weights: 400 / 500 / 600 / 700. Line heights: 1.2 tight, 1.4 snug, 1.6 normal, 1.75 relaxed
(relaxed is used for Arabic). Sans stack is `Inter → Segoe UI → system-ui`; numerals in tables use
`font-variant-numeric: tabular-nums` so columns align. Identifiers (MRN, order numbers, receipt
numbers) use the mono stack.

**No fluid clamp on interface type.** Only the marketing hero and page gutters scale with the
viewport; a clinical table must not change its density between a laptop and a monitor.

---

## 3. Spacing, radius, depth

Spacing is a 4px scale (`--space-1` … `--space-20`). Radii: `--radius-xs` 4px, `sm` 6px, `md` 8px,
`lg` 12px, `xl` 16px, `pill` 999px. Controls use `md`; cards use `lg`; badges use `pill`.

Shadows are deliberately shallow and few:
`--shadow-xs` (2px), `--shadow-sm`, `--shadow-md`, `--shadow-lg` (drawer only).
Focus is a two-part signal: a `2px` outline in `--teal-600` offset by 1px, plus a soft
`--shadow-focus` ring. Removing outlines is prohibited.

---

## 4. Components

| Component | Class | Notes |
|---|---|---|
| Application shell | `_AppShell.cshtml`, `.app`, `.sidebar`, `.topbar` | Sidebar collapses to a drawer under 1024px |
| Public shell | `.public-shell`, `.public-nav`, `.public-footer` | Used for signed-out pages |
| Card | `.card`, `.card__header`, `.card__body`, `.card__footer` | `--flush` for edge-to-edge tables |
| KPI tile | `.kpi`, `.kpi__label`, `.kpi__value`, `.kpi__hint` | Left rail in teal; tone modifiers |
| Table | `.table`, `.table-wrap`, `.table-footer` | Sticky header, tabular numerals, scroll on narrow screens |
| Status badge | `.badge`, `.badge--{success,warning,danger,info,teal,purple,muted}` | Coloured dot + text label |
| Buttons | `.btn` + `--primary`, `--navy`, `--danger`, `--quiet`, `--sm`, `--lg`, `--block` | 38px default, 30px small, 44px large |
| Form fields | `.field`, `.input`, `.select`, `.textarea`, `.field__hint`, `.field__error` | Label always visible above the control |
| Filter bar | `.filter-bar`, `.filter-bar__actions` | Wraps on narrow screens |
| Notice | `.notice` + `--info`, `--success`, `--warning`, `--danger` | Inline, not a toast |
| Dialog | `dialog.dialog` | Native `<dialog>` with backdrop |
| Drawer | `.drawer` | Right-side (or left in RTL) detail panel |
| Timeline | `.timeline`, `.timeline__item` | Encounter and laboratory history |
| Description list | `.dl` | Two-column key/value for record summaries |
| Pager | `.pager`, `.pager__link`, `.pager__current` | Windowed page numbers, `aria-current` |
| Empty state | `.empty-state`, `_EmptyState.cshtml` | Icon, title, one-line explanation |
| Print header | `.print-header` | Only visible in print |

### Interaction states
Every interactive element defines `:hover`, `:focus-visible`, `:active` and `[disabled]`. Disabled
controls remain readable at 55% opacity rather than being removed from the flow, so the layout does
not jump.

---

## 5. Data display

- **Numbers right-align** (`text-align: end`) and use tabular figures; text left-aligns.
- **Identifiers use mono** and never wrap mid-token.
- **Secondary information** (MRN, timestamps) sits on a second line at `--text-xs` in `--ink-500`.
- **Actions** are in the last column, right-aligned, and limited to the two most likely next steps;
  everything else is behind the record view.
- **Empty tables** render `_EmptyState` rather than a bare header row.
- **Loading** uses skeleton rows at the final row height to avoid layout shift.

---

## 6. Breakpoints

| Name | Width | Behaviour |
|---|---|---|
| Mobile | < 640px | Single column, sidebar becomes drawer, filter bars stack, user meta hides |
| Tablet | 640–1024px | Two-column form grids, sidebar still a drawer |
| Laptop | 1024–1280px | Persistent sidebar, split grids collapse to one column below 1180px |
| Desktop | > 1280px | Full layout, content capped at 1560px |

Layout is built with `grid` and logical properties; there are **no fixed pixel widths** on content.

---

## 7. Directionality and locale

`<html lang dir>` is set from the request culture. All layout uses logical properties
(`margin-inline-start`, `border-inline-end`, `inset-inline-start`), so RTL requires no second
stylesheet. Overrides in the RTL block are limited to glyph-level concerns:

- Directional icons mirror (`transform: scaleX(-1)`).
- Identifiers, numerals and `<time>` are `unicode-bidi: isolate` with `direction: ltr`, so an MRN
  inside Arabic text still reads left-to-right.
- Letter-spacing is reset for Arabic, because tracking breaks joined letterforms.
- The select chevron is repositioned, since it is a background image rather than a box edge.
- Arabic body text uses a taller line height and an Arabic-first font stack.

---

## 8. Accessibility (target WCAG 2.2 AA)

| Requirement | Implementation |
|---|---|
| 1.4.3 Contrast | Body text ≥ 4.5:1; large text ≥ 3:1. Badge text on tinted grounds uses the 700 step. |
| 1.4.11 Non-text contrast | Borders and controls ≥ 3:1 against their background |
| 1.4.10 Reflow | No horizontal scrolling at 320px CSS width except data tables, which scroll inside `.table-wrap` |
| 2.1.1 Keyboard | Every control is reachable and operable; dialogs trap focus and restore it |
| 2.4.1 Bypass blocks | Skip link to `#main-content` |
| 2.4.7 Focus visible | Outline plus ring, never removed |
| 2.5.8 Target size | 30px minimum for small controls, 38–44px preferred |
| 3.3.1 Error identification | Validation summary with `role="alert"`, per-field messages linked by `aria-describedby` |
| 4.1.3 Status messages | `role="status"` on success notices, `role="alert"` on errors |
| Reduced motion | `prefers-reduced-motion` reduces all animation to ~0 |
| Forced colours | Badges and KPI tiles get explicit `CanvasText` borders |

**Not verified:** automated axe/Pa11y runs and a screen-reader pass have not been executed — the
sandbox has no browser and no network access to install one. Section 8 documents the *design
intent and implementation*; it is not a conformance claim. See
`commercial-readiness-assessment.md`.

---

## 9. Rules for contributors

1. **No raw hex** in a view or a component rule. Use a token; if a token is missing, add it here
   with a comment explaining the need.
2. **No inline colour or spacing** in Razor. Layout modifiers belong in the stylesheet.
3. **No page-level `<style>` blocks.** A section is available for exceptional cases, but using it
   is a review flag.
4. **State must be text**, not colour alone.
5. **Destructive actions** require `data-confirm` and a danger-style control.
6. **Every new component** gets a section here before it is used in two places.
