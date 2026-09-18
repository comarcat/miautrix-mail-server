---
name: "Iris Pay"
description: "Fintech-grade clarity on white. Whisper-weight 300 display headlines, deep navy ink, electric violet accents, and signature blue-tinted multi-layer shadows."
tags: [fintech, premium, minimal, saas, modern]
colors:
  primary:   "#061b31"
  secondary: "#64748d"
  tertiary:  "#533afd"
  neutral:   "#ffffff"
  surface:   "#ffffff"
typography:
  display: Manrope
  body:    Manrope
  mono:    "JetBrains Mono"
  scale:
    hero: "3.5rem / 1.03 / 300 / -1.4px"
    h1:   "3rem / 1.15 / 300 / -0.96px"
    h2:   "2rem / 1.1 / 300 / -0.64px"
    body: "1rem / 1.4 / 300 / 0"
radius:
  sm: 4px
  md: 5px
  lg: 8px
  pill: 9999px
shadows:
  card:   "rgba(50,50,93,0.25) 0px 30px 45px -30px, rgba(0,0,0,0.1) 0px 18px 36px -18px"
  button: "rgba(50,50,93,0.18) 0px 8px 16px -6px, rgba(0,0,0,0.08) 0px 4px 8px -4px"
borders:
  card:    "1px solid #e5edf5"
  divider: "#e5edf5"
buttons:
  primary:
    background: #635BFF
    color: #FFFFFF
    border: none
    shape: rounded
    padding: 10px 16px
    font: 500
    shadow: rgba(99,91,255,0.20) 0 4px 12px -2px, rgba(99,91,255,0.30) 0 0 0 1px
  secondary:
    background: #FFFFFF
    color: #0A2540
    border: 1px solid #E3E8EE
    shape: rounded
    padding: 10px 16px
    font: 500
    shadow: rgba(0,0,0,0.04) 0 1px 2px
  outline:
    background: transparent
    color: #635BFF
    border: 1px solid #635BFF
    shape: rounded
    padding: 10px 16px
    font: 500
  ghost:
    background: transparent
    color: #425466
    border: none
    shape: rounded
    padding: 10px 12px
    font: 500
    hover: underline
charts:
  variant: bars
  stroke_width: 2
  fill_opacity: 0.18
  gridlines: true
  bar_radius: "4px 4px 0 0"
  bar_gap: 10px
  highlight: single
  dot_marker: true
fonts_url: "https://fonts.googleapis.com/css2?family=Manrope:wght@300;400;500;600&family=JetBrains+Mono:wght@400&display=swap"
dependencies: ["lucide-react"]
---

# Iris Pay

## AI Build Instructions

> **Read this section before writing any code.** The rules below
> are non-negotiable. Every value used in the UI must come from this
> file's frontmatter — never substitute, approximate, or invent new
> colors, fonts, radii, or shadows. If a value is missing, ask the
> user before adding one.

### 1 · Your role

You are building UI for a project that has adopted **Iris Pay** as its
design system. Treat `DESIGN.md` as the single source of truth.
Your job is to translate the user's product requirements into
components and pages that look like they were designed by the same
person who authored this file.

### 2 · Token compliance

- Pull every color, font family, radius, shadow, and spacing value
  from the frontmatter at the top of this file.
- Use semantic roles (e.g. `primary`, `accent`, `muted`) — never
  hard-code hex values that bypass the system.
- When a token can be expressed as a CSS variable, declare it once
  in your global stylesheet and reference it everywhere downstream.
- The Google Fonts `<link>` is provided in the Typography section.
  Add it to `<head>` before any component renders.

### 3 · Component recipes

Use these recipes verbatim when building the corresponding component.

#### Buttons

Four variants are defined. Pick one — never blend variants or invent a fifth.

- **Primary** — rounded shape, bg `#635BFF`, text `#FFFFFF`, padding `10px 16px`, weight `500`, shadow `rgba(99,91,255,0.20) 0 4px 12px -2px, rgba(99,91,255,0.30) 0 0 0 1px`.
- **Secondary** — rounded shape, bg `#FFFFFF`, text `#0A2540`, border `1px solid #E3E8EE`, padding `10px 16px`, weight `500`, shadow `rgba(0,0,0,0.04) 0 1px 2px`.
- **Outline** — rounded shape, text `#635BFF`, border `1px solid #635BFF`, padding `10px 16px`, weight `500`.
- **Ghost** — rounded shape, text `#425466`, padding `10px 12px`, weight `500`.

Reach for **primary** as the single dominant CTA per screen.
**Secondary** for the supporting action. **Outline** for tertiary
actions in toolbars. **Ghost** for inline links and table actions.

#### Cards

- Background: `#ffffff`
- Border: `1px solid #e5edf5`
- Shadow: `rgba(50,50,93,0.25) 0px 30px 45px -30px, rgba(0,0,0,0.1) 0px 18px 36px -18px`
- Radius: `radius.lg` (`8px`)
- Internal padding: `20px` for compact cards, `24–28px` for content cards.

#### Charts

- Bar/line variant: `bars`
- Bar radius: `4px 4px 0 0`
- Highlight strategy: `single` — emphasize a single bar/point per chart.

#### Typography pairings

- **Display (`Manrope`)** — h1, h2, hero headlines, brand wordmarks.
- **Body (`Manrope`)** — paragraphs, labels, button text, form inputs.
- **Mono (`JetBrains Mono`)** — code, eyebrows, metadata, numerals in tables.

### 4 · Hard constraints

Never do any of the following without explicit instruction from the user:

- Introduce a new color, font, radius, or shadow that isn't declared above.
- Mix this system with another (e.g. don't paste in Material or Bootstrap defaults).
- Use generic gradient defaults (purple→blue, peach→pink) — they break the system's voice.
- Reach for emoji icons. Use a consistent icon library and size icons in line with body type.
- Add motion that exceeds the system's restraint — keep transitions short (≤200ms) and subtle.

### 5 · Before you finish — verify

Run through this checklist for every screen you produce:

- [ ] Every color used appears in the Colors table above.
- [ ] Headlines use the display font; body copy uses the body font.
- [ ] Buttons match one of the declared variants exactly (shape, padding, weight).
- [ ] Border-radius values come from `radius.sm` / `radius.md` / `radius.lg` / `radius.pill`.
- [ ] Cards and dividers use the declared border + shadow tokens.
- [ ] No values were invented; if you needed something missing, you stopped and asked.

---

## 1. Atmosphere

Iris Pay is fintech design at its most refined. A clean white canvas (`#ffffff`) anchored by deep navy headings (`#061b31`) — never pure black — and a saturated electric violet (`#533afd`) that does double duty as brand anchor and interactive accent. The impression is of a financial institution redesigned by a world-class type foundry: simultaneously technical and luxurious, precise and warm.

The signature typographic move is **weight 300 at display sizes**. Where most product pages shout in 600/700, Iris Pay whispers — confident enough not to need the volume. Negative tracking compresses headlines into dense, engineered blocks (-1.4px at 56px, -0.96px at 48px). Every text element should run on a geometric variable sans with stylistic alternates enabled for a contemporary, rounded letterform.

What truly distinguishes Iris Pay is its **shadow system**. Forget flat shadows or single-layer drops. Iris Pay uses multi-layer, blue-tinted shadows — `rgba(50,50,93,0.25)` paired with `rgba(0,0,0,0.1)` — creating depth that reads as cool, atmospheric, almost twilight. The blue-gray undertone ties shadows directly to the navy-violet palette, so even elevation feels on-brand.

**Signature moves**
- Geometric variable sans at weight 300 — lightness as luxury
- Negative tracking at display: -1.4px at 56px, progressively relaxed below
- Blue-tinted multi-layer shadows: `rgba(50,50,93,0.25) 0 30px 45px -30px, rgba(0,0,0,0.1) 0 18px 36px -18px`
- Deep navy (`#061b31`) headings instead of black — warm, premium, financial-grade
- Conservative border-radius (4–8px) — nothing pill-shaped, nothing harsh
- Ruby (`#ea2261`) and magenta (`#f96bee`) for decorative gradients only — never CTAs

## 2. Palette

### Primary
- **Iris Violet** `#533afd` — primary CTA, links, interactive accent
- **Deep Navy** `#061b31` — headings, strong labels
- **Pure White** `#ffffff` — page, card surfaces

### Brand Dark
- **Brand Indigo** `#1c1e54` — immersive dark sections, footer
- **Dark Navy** `#0d253d` — darkest neutral, blue-tinted near-black

### Accent (decorative only)
- Ruby `#ea2261` — gradients, icon highlights
- Magenta `#f96bee` — gradient stops, decorative pops
- Magenta Light `#ffd7ef` — tinted surface for accent badges

### Interactive
- Hover `#4434d4` — darker violet for primary hover
- Light `#b9b9f9` — soft lavender for selected states
- Mid `#665efd` — input range/highlight

### Neutral
- Label `#273951`
- Body `#64748d`
- Border `#e5edf5`
- Success `#15be53` (text `#108c3d`)

## 3. Typography

| Role | Size | Weight | Leading | Tracking |
|------|------|--------|---------|----------|
| Display Hero | 56px | 300 | 1.03 | -1.4px |
| Display | 48px | 300 | 1.15 | -0.96px |
| Section | 32px | 300 | 1.10 | -0.64px |
| Sub-heading L | 26px | 300 | 1.12 | -0.26px |
| Body L | 18px | 300 | 1.40 | normal |
| Body | 16px | 300–400 | 1.40 | normal |
| Button | 16px | 400 | 1.00 | normal |
| Caption | 13px | 400 | 1.50 | normal |
| Tabular num | 12px | 300 | 1.33 | -0.36px |

**Two-weight discipline.** 300 for body and headings, 400 for UI/buttons. No bold (700) in the primary face. Use `"tnum"` for any tabular financial number — never in flowing prose.

## 4. Buttons

### Primary Iris
```css
background: #533afd;
color: #ffffff;
padding: 8px 16px;
border-radius: 4px;
```
Hover: `#4434d4`.

### Outline
- Transparent background, `#533afd` text
- `1px solid #b9b9f9`, 4px radius
- Hover: `rgba(83,58,253,0.05)` tint

### Disabled / Muted
- Transparent, `rgba(16,16,16,0.3)` text
- `1px solid rgb(212,222,233)`

## 5. Cards

- Background: `#ffffff`
- Border: `1px solid #e5edf5`
- Radius: 5–8px (4px tight, 8px featured)
- Standard shadow: `rgba(50,50,93,0.25) 0 30px 45px -30px, rgba(0,0,0,0.1) 0 18px 36px -18px`
- Ambient: `rgba(23,23,23,0.08) 0 15px 35px`

## 6. Charts

Bar charts use **gridline backgrounds** and a single highlighted bar in iris violet. Line charts run thin (2px stroke) with subtle area fills. Tabular numerals everywhere. The chart card itself carries the signature blue-tinted shadow, never a flat border.

## 7. Spacing

- Base: 8px
- Scale: `1, 2, 4, 6, 8, 10, 11, 12, 14, 16, 18, 20` — dense at the small end for precision data UI

## 8. Depth & elevation

| Level | Treatment | Use |
|-------|-----------|-----|
| 0 | No shadow | Page background |
| 1 | `rgba(23,23,23,0.06) 0 3px 6px` | Subtle lift, hover |
| 2 | `rgba(23,23,23,0.08) 0 15px 35px` | Standard cards |
| 3 | Multi-layer blue tint | Featured cards, dropdowns |
| 4 | `rgba(3,3,39,0.25) 0 14px 21px -14px, rgba(0,0,0,0.1) 0 8px 17px -8px` | Modals |

**Chromatic depth.** Shadows aren't just dark — they're *blue*. The `rgba(50,50,93,...)` tint echoes the navy-violet palette so elevation feels brand-colored.

## 9. Do's & don'ts

✅ **Do**
- Use weight 300 for all display and body — lightness is the brand voice
- Apply blue-tinted shadows on every elevated element
- Use deep navy (`#061b31`) for headings — never `#000000`
- Keep radius between 4–8px — nothing pill-shaped on cards/buttons
- Use violet (`#533afd`) for CTAs only

❌ **Don't**
- Use weight 600+ on display — it betrays the brand voice
- Use neutral gray shadows — always tint with blue
- Use ruby or magenta on buttons — they're decorative-only
- Apply pill radius (>12px) on buttons or cards
- Use pure black (`#000`) on text

---

## Tokens

> Generated from the same source the live preview renders from.
> Treat the values below as the contract — never substitute approximations.

### Colors

| Role      | Value |
|-----------|-------|
| primary   | `#061b31` |
| secondary | `#64748d` |
| tertiary  | `#533afd` |
| neutral   | `#ffffff` |
| surface   | `#ffffff` |

### Typography

- **Display:** Manrope
- **Body:** Manrope
- **Mono:** JetBrains Mono

| Role | size / leading / weight / tracking |
|------|------------------------------------|
| Hero | 3.5rem / 1.03 / 300 / -1.4px |
| H1   | 3rem / 1.15 / 300 / -0.96px |
| H2   | 2rem / 1.1 / 300 / -0.64px |
| Body | 1rem / 1.4 / 300 / 0 |

### Radius

- sm: `4px`
- md: `5px`
- lg: `8px`
- pill: `9999px`

### Shadows

- **card:** `rgba(50,50,93,0.25) 0px 30px 45px -30px, rgba(0,0,0,0.1) 0px 18px 36px -18px`
- **button:** `rgba(50,50,93,0.18) 0px 8px 16px -6px, rgba(0,0,0,0.08) 0px 4px 8px -4px`

### Borders

- **card:** `1px solid #e5edf5`
- **divider:** `#e5edf5`

### Buttons

Four variants, each fully tokenized. The preview renders from these exact values.

#### Primary

| Property | Value |
|----------|-------|
| shape | `rounded` |
| background | `#635BFF` |
| color | `#FFFFFF` |
| border | `none` |
| padding | `10px 16px` |
| fontWeight | `500` |
| shadow | `rgba(99,91,255,0.20) 0 4px 12px -2px, rgba(99,91,255,0.30) 0 0 0 1px` |

#### Secondary

| Property | Value |
|----------|-------|
| shape | `rounded` |
| background | `#FFFFFF` |
| color | `#0A2540` |
| border | `1px solid #E3E8EE` |
| padding | `10px 16px` |
| fontWeight | `500` |
| shadow | `rgba(0,0,0,0.04) 0 1px 2px` |

#### Outline

| Property | Value |
|----------|-------|
| shape | `rounded` |
| background | `transparent` |
| color | `#635BFF` |
| border | `1px solid #635BFF` |
| padding | `10px 16px` |
| fontWeight | `500` |

#### Ghost

| Property | Value |
|----------|-------|
| shape | `rounded` |
| background | `transparent` |
| color | `#425466` |
| border | `none` |
| padding | `10px 12px` |
| fontWeight | `500` |
| hoverHint | `underline` |

### Charts

| Property | Value |
|----------|-------|
| variant | `bars` |
| strokeWidth | `2` |
| fillOpacity | `0.18` |
| gridlines | `true` |
| barRadius | `4px 4px 0 0` |
| barGap | `10px` |
| highlight | `single` |
| dotMarker | `true` |

---

## Pro tokens

> Production-fidelity tokens. States, density, motion, elevation,
> content rules and a measured WCAG contract — derived from the
> resting tokens unless explicitly authored.

### States

#### Button

- **hover** — shadow: `0 4px 12px -2px rgba(15,23,42,0.18)`, filter: `brightness(0.97)`
- **focus** — outline: `2px solid rgba(83, 58, 253, 0.5)`, outline-offset: `2px`
- **active** — shadow: `0 1px 2px rgba(15,23,42,0.1)`, transform: `scale(0.98)`
- **disabled** — opacity: `0.4`, filter: `saturate(0.5)`
- **loading** — opacity: `0.7`
- **selected** — bg: `#533afd`, color: `#ffffff`

#### Input

- **hover** — border: `1px solid rgba(83, 58, 253, 0.5)`
- **focus** — border: `1.5px solid #533afd`, shadow: `0 0 0 4px rgba(83, 58, 253, 0.15)`
- **disabled** — bg: `rgba(6, 27, 49, 0.04)`, opacity: `0.4`
- **error** — border: `1.5px solid #DC2626`, shadow: `0 0 0 4px rgba(220,38,38,0.15)`

#### Card

- **hover** — shadow: `0 12px 28px -12px rgba(15,23,42,0.18)`, transform: `translateY(-2px)`
- **selected** — bg: `rgba(83, 58, 253, 0.04)`, border: `1.5px solid #533afd`
- **dragging** — shadow: `0 20px 48px -16px rgba(15,23,42,0.3)`, transform: `scale(1.02) rotate(-0.5deg)`, opacity: `0.9`

#### Tab

- **hover** — bg: `rgba(83, 58, 253, 0.06)`, color: `#533afd`
- **focus** — outline: `2px solid rgba(83, 58, 253, 0.5)`, outline-offset: `2px`
- **selected** — color: `#533afd`, border: `0 0 2px 0 solid #533afd`

### Density

| Mode | padding × | row × | body | radius × | Use for |
|------|-----------|-------|------|----------|---------|
| compact | 0.72 | 0.78 | 0.8125rem | 0.85 | Information-dense — tables, IDEs, dashboards |
| comfortable | 1 | 1 | 0.9375rem | — | Default — most product UI |
| spacious | 1.35 | 1.3 | 1rem | 1.15 | Editorial — marketing, long-form, settings |

### Motion

**Signature — Quiet ease.** 240 ms ease-out for all standard transitions. Reliable, invisible — motion stays out of the way.

```css
transition: all 240ms cubic-bezier(0.4, 0, 0.2, 1);
```

| Token | Value |
|-------|-------|
| duration.instant | `80ms` |
| duration.fast | `160ms` |
| duration.base | `240ms` |
| duration.slow | `380ms` |
| easing.standard | `cubic-bezier(0.4, 0, 0.2, 1)` |
| easing.decelerate | `cubic-bezier(0.0, 0, 0.2, 1)` |
| easing.accelerate | `cubic-bezier(0.4, 0, 1, 1)` |
| easing.spring | `cubic-bezier(0.34, 1.4, 0.64, 1)` |

### Elevation

Five-level scale, system-specific recipe.

| Level | Shadow | Recipe |
|-------|--------|--------|
| level0 | `none` | Flat — hairline border separates. |
| level1 | `0 1px 2px rgba(15,23,42,0.06), 0 1px 3px rgba(15,23,42,0.04)` | List rows, resting cards. |
| level2 | `0 4px 12px -2px rgba(15,23,42,0.1), 0 2px 6px rgba(15,23,42,0.06)` | Hover cards, popover. |
| level3 | `0 12px 32px -8px rgba(15,23,42,0.16), 0 4px 12px rgba(15,23,42,0.08)` | Sheets, side panels. |
| level4 | `0 28px 64px -16px rgba(15,23,42,0.28), 0 8px 24px rgba(15,23,42,0.12)` | Modals — scrim required. |

### Content

- **measure:** `68ch` (max line length for body prose)
- **paragraph spacing:** `1.2em`
- **list indent:** `1.5em`
- **list gap:** `0.5em`
- **link:** color `#533afd`, underline `hover`
- **blockquote:** border `3px solid rgba(83, 58, 253, 0.6)`, padding `0.5em 0 0.5em 1.25em`
- **code:** background `rgba(6, 27, 49, 0.06)`, color `#061b31`

### Accessibility (WCAG 2.1)

**Overall:** AA

| Pair | Ratio | Required | Grade | Suggested fix |
|------|-------|----------|-------|---------------|
| Body text on surface | 17.37:1 | AA | AAA | — |
| Body text on canvas | 17.37:1 | AA | AAA | — |
| Muted text on surface | 4.75:1 | AA | AA | — |
| Accent on surface | 6.19:1 | AA-Large | AA | — |
| Accent on canvas | 6.19:1 | AA-Large | AA | — |
