
## 5. `DESIGN-TOKENS.md`

```markdown
# DESIGN-TOKENS.md — Project-Specific Design System

> Concrete companion to DESIGN.md. Holds the actual colors/type/spacing/components. FILLED PER PROJECT.
> **Skip this file entirely if the project has no UI.**

---
## 0. FILL PROTOCOL (binding)
> **AI agents: detect or ASK — never invent a brand palette.**
> 1. DETECT: existing token files, Tailwind/theme config, component library in deps, existing components.
> 2. If a design system exists → record it here. If NEW UI and no system → **ASK** for brand/
>    personality direction (DESIGN.md §13): "What feeling — calm/precise/bold/playful? Any brand
>    color? Light/dark/both?" Then propose tokens starting from a *neutral* (DESIGN.md §3), not a default purple.
> 3. Fill below. NEVER ship the generic placeholder values.

## 1. Color (DESIGN.md §3 — 60/30/10, tinted neutrals, no pure #000/#fff)  `[FILL]`
- Neutral base (light): `[FILL]`  · (dark): `[FILL]`  · Text: `[FILL near-black/near-white]`
- Brand/accent: `[FILL — one disciplined accent, NOT default purple→blue]`
- Semantic: success `[__]` warning `[__]` danger `[__]` border `[__]`
- Theme: `[FILL: light | dark | both]`

## 2. Type (DESIGN.md §5)  `[FILL]`
- Body font: `[FILL]`  · Display/heading font: `[FILL]`
- Scale: `[FILL: e.g. 12,14,16,20,24,32,40,56]`  · Weights: `[FILL]`

## 3. Spacing/radius/shadow (DESIGN.md §6–§7)  `[FILL]`
- Spacing scale: `[FILL: e.g. 4,8,12,16,24,32,48,64]`
- Radii (2–3): `[FILL: e.g. 6 inputs / 12 cards / 999 pills]`  · Shadow elevation levels: `[FILL]`

## 4. Component library & inventory  `[FILL]`
- **Library:** `[FILL: shadcn/ui | MUI | Chakra | custom | …]`
- **Component inventory:** `[FILL: Storybook URL | generated COMPONENTS.md — DESIGN.md §8 "reuse before invent"]`
- **Icon set (one family):** `[FILL]`
```

---