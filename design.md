Before I give you the file, two honest answers to your direct questions:

**"Does it make responsive automatically?"** — No document makes anything automatic. What this does: makes responsive design a *binding rule with concrete specs*, so any model generating UI against it produces responsive output by default instead of as an afterthought. The doc is the instruction; the model does the work.

**"Does it cover mobile app too?"** — Your v1 was web-only (it referenced "routes," CSS props, scroll). A real cross-platform standard needs a **native mobile section** (iOS HIG / Android Material, platform conventions, gestures, safe areas, navigation patterns). I've added §12B for that.

**"Make it 10/10."** — I'll be straight: I'll build it to the realistic ceiling (~9.3) and *tell you exactly* which 0.7 no document can contain. A doc claiming a literal 10/10 "makes any project a masterpiece" is the lie I warned you about. This is the honest best-in-class version.

Here it is, complete.

---

```markdown
# DESIGN.md — Design System & Craft Standard

> **Status:** Mandatory craft standard for all UI/UX work (human or AI), web and native.
> **Goal:** Every screen ships at a level a competent senior designer wouldn't be embarrassed
> to show. Intentional, not templated. Designed, not generated. Alive, not static.
> **Priority:** Clarity > Hierarchy > Consistency > Craft detail > Novelty.
> Never sacrifice usability for visual flair. A confusing beautiful screen is a failure.
>
> **What this doc does and doesn't do (read this):** This file sets the *target* and the
> *rules*. It does not "automatically" fix anything — it gives any engineer or AI agent a
> precise, opinionated standard to design and refactor *toward*. The lift comes from executing
> against it. Taste is executed, never declared.

---

## 0. How to Use This File

- **Read §1 (Anti-Pattern Kill List) first.** Eliminating those gets you ~80% to "designed by
  a human with taste." Elimination beats generation.
- **§2 is the decision sequence.** When unsure, follow it instead of guessing.
- **§3–§13 are the system.** Pick concrete values ONCE per project (record in
  `DESIGN-TOKENS.md`), then don't deviate without a stated reason.
- **§4.5 is the worked example.** Study it — models and humans learn composition from a
  before/after far better than from rules.
- **AI agents: §14 is binding.** It points to the rules; it does not restate them.

---

## 1. The Anti-Pattern Kill List (why work looks "AI-generated")

Doing any of these is the #1 reason design looks cheap or machine-made. **Avoid all unless
there is a deliberate, stated reason.**

### Color
- ❌ Default purple→blue gradients (`#667eea → #764ba2` and kin). The single biggest AI tell.
- ❌ More than one gradient per screen, or gradients on everything.
- ❌ Pure black `#000` text on pure white `#fff`. Use near-black on off-white.
- ❌ Random saturated accents with no system.
- ❌ Equal-weight colors everywhere (no dominant + accent relationship).

### Layout
- ❌ **Centered-everything as a default** — the "I didn't make a decision" look. (Centering is
  fine when *chosen*; see §4.)
- ❌ Three identical equal-width cards of identical content as a reflex.
- ❌ Uniform spacing everywhere (no spatial grouping — related items aren't closer than unrelated).
- ❌ Full-width text lines (measure too long). Cap at ~50–75ch.
- ❌ Elements floating with no alignment to any grid.

### Type
- ❌ One font weight for everything.
- ❌ Default system font, default weight, as the *brand* voice (fine for body, weak as identity).
- ❌ Cramped line-height on body, or balloon line-height on big headings.
- ❌ Centered long paragraphs.

### Components & detail
- ❌ Emoji as iconography or bullets in product UI.
- ❌ Big, dark, blurry "2010" drop shadows. Use layered, subtle, tinted shadows.
- ❌ One `border-radius` on literally everything, with no rhythm.
- ❌ Generic stock "people pointing at charts" illustration.
- ❌ All buttons the same visual weight (no primary/secondary/tertiary hierarchy).
- ❌ Lorem ipsum left in handed-off/shipped work.
- ❌ Only the happy path (no empty / loading / error / dense states).
- ❌ Static, lifeless screens with zero feedback or transition (see §9).

> If you catch yourself doing any of the above, stop and make an actual decision.

---

## 2. The Design Decision Framework (do this in order)

Elite designers start with the problem, not the color. Follow the sequence:

1. **Intent.** What is this? Who uses it? What's the *one* job of this screen? What feeling
   should it evoke (calm/trustworthy · energetic/bold · precise/technical · playful/warm)?
2. **Hierarchy.** Rank everything: primary action, secondary, supporting, ambient. Everything
   cannot be important. Design serves the ranking.
3. **Layout structure** that fits the content — not a centered hero by reflex (§4).
4. **Type system** (§5) — type carries ~70% of the "designed" feeling.
5. **Restrained color** (§3) — one neutral foundation, one brand color, one accent. Usually enough.
6. **Spacing rhythm** (§6) — a scale, with intentional grouping (proximity).
7. **Depth & components** (§7–§8) — consistent surfaces, reuse before invent.
8. **Motion & flow** (§9) — polish and continuity, last, never a crutch.
9. **Every state** (§10) — empty, loading, error, dense, success.
10. **Accessibility & responsiveness** (§11–§12) — baked in, not bolted on.

> If a choice doesn't serve intent or hierarchy, cut it. Restraint reads as confidence;
> decoration reads as insecurity.

---

## 3. Color System

**Principle:** Color is hierarchy and meaning, not decoration. Restraint > variety.

### Structure (60/30/10 backbone)
- **~60% Neutral foundation** — backgrounds/surfaces. Off-white or near-black, NOT pure.
  - Light base: `#FAFAF9`–`#F5F5F4`; text `#1A1A1A`–`#222`.
  - Dark base: `#0A0A0B`–`#121214`; text `#EDEDED`–`#FAFAFA` (never pure white).
- **~30% Secondary** — supporting surfaces, borders, muted text (a *tinted* neutral).
- **~10% Brand/Accent** — primary actions, focus, key highlights. Sparing use = power.

### Rules
- Build a full **neutral ramp** (50→950, ~11 steps), tinted slightly toward your brand hue —
  pure gray looks lifeless.
- Define semantic roles in code, not raw hex: `surface`, `surface-raised`, `text-primary`,
  `text-muted`, `border`, `accent`, `success`, `warning`, `danger`.
- One brand color + tints/shades. Second accent only if the product truly needs it.
- Generate ramps in a perceptual space (OKLCH/HSL) so steps feel even.
- **Contrast is law:** body ≥ 4.5:1, large/UI ≥ 3:1 (WCAG AA). Test, don't eyeball.
- Never color-as-only-signal — pair with icon/text/shape.
- Dark mode is a *designed theme*, not `invert()`: lower saturation, raise surfaces with
  light not borders, soften whites.

### Picking a non-AI palette
- Start from the **neutral**, not the accent. A characterful neutral (warm sand, cool slate,
  hue-tinted near-black) sets the tone before any color appears.
- Borrow accent direction from the product's domain/emotion — not from defaults.

---

## 4. Layout & Composition

**Principle:** Composition separates "designed" from "assembled."

- **Grid:** Use one (12-col desktop / 4-col mobile is common) and *align to it*.
- **Whitespace is active, not leftover.** Generous negative space around the focal point is
  the cheapest strong signal of quality. When unsure: remove an element, add space.
- **Symmetry vs. asymmetry is a deliberate choice, not a default.** Asymmetry is an
  underused, confident tool — but symmetry is *correct* for comparison grids, pricing tables,
  dashboards, and galleries. **The rule is: choose deliberately. Don't center-everything out
  of indecision.** Both can be excellent when intentional.
- **One clear focal point.** Guide the eye (Z- or F-pattern as content suggests).
- **Hierarchy through size + space + position**, not color alone. Most important = biggest
  and/or most surrounding space.
- **Proximity:** related elements close, unrelated separated. Spacing communicates structure.
- **Optical > mathematical alignment.** Nudge so it *looks* right.
- **Measure:** body line length 50–75ch. Cap content width; full-bleed only with purpose.
- **Density is a deliberate choice:** a landing page breathes; a pro tool (Linear, Bloomberg)
  is dense *on purpose*. Match the audience.

### 4.5 Worked Example — refactoring a generic screen (study this)

**BEFORE (generic / "AI-generated" SaaS hero):**
```
- Full-width centered column, everything center-aligned.
- Background: linear-gradient(135deg, #667eea, #764ba2).
- H1 36px, body 16px, both weight 400, both centered, pure white on the gradient.
- One <H1>, one paragraph (~90 chars/line), two identical purple pill buttons side by side.
- Below: three identical white cards, equal width, border-radius 8px everywhere,
  box-shadow: 0 4px 20px rgba(0,0,0,0.25), each with an emoji 🚀 and lorem-ish text.
- No hover states, no motion, no empty/error states. Static.
```
*Why it reads as AI:* default gradient, centered-everything, single weight, equal-weight
buttons, identical cards, emoji icons, heavy blurry shadow, pure white text, no hierarchy,
no states, no life.

**AFTER (same content, refactored under this doc):**
```
- Asymmetric two-column: left = message (≈55%), right = product visual/screenshot (≈45%).
  Left column left-aligned; content sits on a 12-col grid, not dead-center.
- Background: off-white #FAFAF9 (light) with one subtle tinted accent shape, NOT a gradient
  wash. Near-black text #1A1A1A.
- Type carries it: eyebrow LABEL 12px/600/+0.04em/uppercase/text-muted →
  H1 56px/1.05/700/-0.02em → body 18px/1.6/400 capped at ~60ch.
- Button hierarchy: ONE primary (filled accent, solid weight) + ONE secondary (ghost/text).
  They look different. Primary has a clear focus ring + 150ms hover lift.
- Supporting proof (logos / metric) sits below with deliberate spacing from the scale —
  grouped tight to its label, loose from the next section.
- Cards (if used) vary by role/hierarchy, single icon set (one stroke width, no emoji),
  12px radius on cards / 6px on buttons (rhythm, not uniformity), layered subtle shadow
  (0 1px 2px rgba(0,0,0,.04), 0 8px 24px rgba(0,0,0,.06)).
- Motion: section fades+rises 16px on scroll-in (once, 400ms ease-out, staggered ~60ms).
  Reduced-motion users get the static version.
- States designed: loading skeleton for the product visual, empty/error for any data card.
```
*Why it now reads as designed:* a single focal point, real typographic hierarchy, disciplined
color from a neutral base, button weight hierarchy, spacing that groups, consistent iconography,
restrained depth, purposeful motion, and designed states. **Same content — different decisions.**

> The lesson: "make it a masterpiece" = *make decisions* at every line above. The doc just
> names which decisions and which defaults to refuse.

---

## 5. Typography (carries ~70% of the "designed" feeling)

**Principle:** Type is the voice. Right type = 70% done.

### Selection
- **Body:** highly legible, neutral (a quality sans; a good system stack is acceptable for body).
- **Display/Heading:** personality lives here — a characterful display face or a distinctive
  weight of the body family. Avoid defaulting to the same generic sans everyone uses unless
  intentional minimalism is the brief.
- **Pairing:** max 2 families (3 only with reason). Pair by contrast (expressive serif display
  + clean sans body) or commit to one strong variable family across weights.
- Prefer variable fonts for weight control without payload.

### System
- **Modular scale** (ratio ~1.2 dense → ~1.333 expressive). e.g. 12, 14, 16, 20, 24, 32, 40, 56.
  No random sizes.
- **Weight is hierarchy:** pair size change with weight change (headings 600/700, body 400/500).
- **Line-height:** body ~1.5–1.65; headings tighter ~1.05–1.25 (big type needs *less*).
- **Letter-spacing:** large headings slightly negative (`-0.01em`→`-0.03em`); small all-caps
  labels slightly positive; body default.
- **Alignment:** body left-aligned (LTR); center only short isolated phrases.
- **Numerals:** tabular for tables/data, proportional for prose.

### Hierarchy example (record real values per project)
```
Display 56/1.05/700/-0.02em · H1 40/1.1/700/-0.02em · H2 32/1.15/600/-0.015em
H3 24/1.25/600 · Body-lg 20/1.55/400 · Body 16/1.6/400 · Small 14/1.5/400 muted
Label 12/1.4/600/+0.04em/uppercase
```

---

## 6. Spacing & Sizing

**Principle:** Consistent rhythm; never random pixel values.

- **One spacing scale** for everything (padding/margin/gap), base 4 or 8:
  `4, 8, 12, 16, 24, 32, 48, 64, 96, 128`. No `13px`/`17px` one-offs.
- **Spacing encodes relationships** (proximity): tight within groups, loose between groups.
- **Component padding scales with component size**, proportional, from the scale.
- **Touch targets ≥ 44×44px** (iOS) / 48dp (Android) for anything tappable.
- **Radius:** 2–3 values with rhythm (6px inputs/buttons · 12px cards · 999px pills), not one
  radius on all, not random per element. Nested: outer radius ≥ inner + gap.
- **Borders:** 1px hairline in a low-contrast border color usually beats heavy outlines.
  Subtle border *or* subtle shadow — rarely both heavy.

---

## 7. Depth, Shadow & Surface

**Principle:** Depth should feel like real light, not a 2010 drop shadow.

- **Layered soft shadows** (a tight near shadow + a soft far shadow), low opacity
  (`rgba(0,0,0,0.04–0.1)`) — never one big blurry shadow.
- Tint shadow color slightly toward surface/brand hue, not pure black.
- **Elevation system:** levels (flat · raised · overlay · modal) each mapped to a shadow +
  surface. Higher elevation = lighter surface (light mode) + larger/softer shadow.
- **Dark mode:** show elevation by *raising surface lightness*, not heavier shadows.
- Depth communicates (what floats / is interactive) — not decoration.

---

## 8. Components & Patterns

**Principle:** Consistency makes a product feel crafted. Reuse before inventing.

- **Reuse the design system / component library first** (lib named in `DESIGN-TOKENS.md`).
  Match naming, props, variants, spacing, states before creating anything.
- **Button hierarchy mandatory:** primary (filled, ideally one per view), secondary
  (outline/subtle), tertiary (text/ghost), destructive (danger). They must *look* different.
- **Forms:** visible labels (not placeholder-as-label); clear focus; inline, specific
  validation next to the field; never color-only errors.
- **Inputs:** designed focus / disabled / error / filled states; never remove focus outline
  without replacing it.
- **Feedback:** every action has feedback (hover/active/loading/success/error). No dead clicks.
- **Iconography:** one family, one stroke width, one optical size, optically aligned to text.
  No emoji as icons in product UI.
- **Consistency:** same action looks the same everywhere; consistent radii/shadow/spacing per
  component type.

---

## 9. Motion, Flow & Perceived Performance

**Principle:** A premium product feels *alive and continuous*, never static or janky. Motion
guides attention and communicates cause→effect; it is polish, not décor. Smoothness is a
feature, and perceived speed beats actual speed.

### 9A. Interface motion (within a component/screen)
- **Purposeful only:** motion explains a state change, relationship, or feedback. If it doesn't
  communicate, cut it.
- **Fast & subtle:** UI transitions 150–250ms; larger/enter 300–400ms; micro-feedback
  (hover/press) 100–150ms.
- **Easing:** `ease-out` for entrances, `ease-in-out` for moves; avoid linear (robotic); spring
  physics for natural/playful where fitting.
- **Animate cheap properties** (`transform`, `opacity`) for 60fps. Never animate layout props
  (`width`, `height`, `top`, `margin`). Use `will-change` sparingly; remove after.
- **Micro-interactions:** hover lift/tint, button press depress, smooth focus ring,
  skeleton→content fade, success checkmark. Small, consistent, delightful.

### 9B. Experience motion & flow (across the journey)
- **Smoothness / no jank:** target 60fps; no layout shift (reserve space for async content,
  skeletons match final layout → CLS ≈ 0); no heavy work in scroll handlers — use
  `IntersectionObserver`, debounce/throttle.
- **Scroll experience:** reveal-on-scroll with restraint (fade + translate 8–24px, 300–500ms,
  ease-out, *once*). Stagger related items ~40–80ms so groups feel orchestrated, not popped.
  Parallax/sticky/scroll-linked only with purpose, buttery, reduced-motion-safe, never nausea.
- **Flow & continuity across screens:** no hard cuts — short eased route/page transitions
  (200–350ms). Persistent elements *move/morph* between views (shared-element continuity:
  list item → detail). Spatial logic: forward enters one way, back reverses it. Design the
  transitions *between* steps, not just the steps.
- **Perceived performance:** optimistic UI (reflect action instantly, reconcile after); instant
  feedback within ~100ms (press/skeleton/spinner) even if the result is slow; **skeletons >
  spinners** for content; progressive/lazy-load below the fold; preload likely-next route/data
  on hover/intent so navigation feels instant.

### Restraint (still the rule)
- One or two **signature** motion moments per flow > everything moving. Over-animation is
  amateur and tiring; zero motion is static and cheap. Aim between.
- **`prefers-reduced-motion` is mandatory:** reduced-motion users get a calm, fully functional
  version — never a broken one. Content is never gated behind motion/JS.

---

## 10. States, Edge Cases & Content (where AI work usually fails)

**Principle:** A design isn't done until every state is designed. The happy path is the easy 10%.

Design ALL of these for any meaningful view:
- **Empty / first-use:** inviting and instructive with a guiding CTA — an onboarding
  opportunity, not a sad blank box.
- **Loading:** skeletons matching final layout (no layout shift) > spinners. Optimistic where safe.
- **Error:** human, specific, recoverable ("Couldn't save — retry?"). Never a raw stack trace.
- **Partial / slow:** slow networks, partial data, pagination, "load more."
- **Dense / overflow:** long names, huge numbers, many items, tiny screens, long translations.
  Test realistic worst-case content, not "John Doe."
- **Success / confirmation:** confirm completion clearly.

### Content & microcopy
- Real/realistic copy — never lorem ipsum in shipped/handed-off work.
- Voice matches product personality; concise, human, specific.
- Buttons name the action (`Save changes`, not `Submit`/`OK`).
- **Pick a casing convention** (sentence case or Title case) and apply it consistently — this
  is a house-style choice, not a universal law; just don't mix them.

---

## 11. Accessibility (non-negotiable, part of craft)

Accessibility is good design, not a tax. Required:
- **Contrast:** AA (text 4.5:1, large/UI 3:1) — verify with a tool.
- **Keyboard:** every interactive element reachable/operable; logical tab order; visible focus
  (never `outline:none` without a replacement).
- **Semantics:** correct elements (`<button>` for buttons, ordered headings, landmarks). ARIA
  fills gaps, never replaces native semantics.
- **Screen readers:** meaningful `alt`; labels tied to inputs; announce dynamic changes
  (`aria-live`) where relevant.
- **Motion:** honor `prefers-reduced-motion`.
- **Targets:** ≥ 44px touch; adequate spacing between interactive elements.
- **Never color-only** for meaning.
- **Zoom/reflow:** usable at 200% zoom; no horizontal scroll from text scaling.
- **Native (mobile apps):** support OS accessibility (VoiceOver / TalkBack), Dynamic Type /
  font scaling, sufficient contrast, and respect system reduce-motion / larger-text settings.

---

## 12. Responsive (Web)

**Principle:** Responsive is a default behavior here, not an afterthought.

- **Mobile-first:** design the small screen as a first-class layout, not a squished desktop.
- **Fluid > fixed:** relative units; `clamp()` for fluid type/spacing; container queries where
  supported.
- **Breakpoints serve content, not devices:** add one where the layout *breaks*, not at
  arbitrary phone widths. Common anchors ~640 / 768 / 1024 / 1280.
- **Touch vs pointer:** larger targets, no hover-dependent actions on touch; provide non-hover
  equivalents.
- **Test the extremes:** smallest supported phone + large desktop — no overflow, readable
  measure, intact hierarchy at every size.

---

## 12B. Native Mobile Apps (iOS / Android — only if a native/cross-platform app exists)

**Principle:** A native app must feel native to its platform, not like a wrapped website.
Follow the platform; don't fight it.

### Platform conventions
- **iOS:** follow Apple Human Interface Guidelines. Respect safe areas / notch / Dynamic Island /
  home indicator. Use platform navigation (tab bar, large titles, swipe-back), SF Symbols where
  fitting, and native components/gestures users expect.
- **Android:** follow Material Design. Use the system back gesture/button, FAB where appropriate,
  bottom navigation / navigation drawer conventions, Material components, ripple feedback,
  and Material elevation.
- **Cross-platform (React Native / Flutter / Expo):** either adapt per platform (preferred for
  feel) or pick one coherent system and apply it consistently. Don't ship iOS conventions on
  Android or vice-versa by accident.

### Mobile-native craft
- **Touch ergonomics:** primary actions within thumb reach; ≥44px (iOS) / 48dp (Android)
  targets; adequate spacing; avoid tiny tap zones.
- **Gestures:** support expected gestures (swipe-back, pull-to-refresh, swipe actions on list
  rows, long-press) — and make them discoverable; never make a gesture the *only* path to a
  critical action.
- **Navigation model:** clear, shallow hierarchy; predictable back behavior; preserve scroll
  position and state across navigation.
- **System integration:** respect light/dark per OS setting, Dynamic Type / font scaling,
  reduce-motion, haptics for meaningful feedback (don't overuse), safe-area insets,
  keyboard-avoidance for inputs, and orientation handling.
- **Performance feel:** 60fps scrolling and transitions; native-feeling momentum scroll; instant
  touch feedback; skeletons/optimistic UI (§9B) apply equally; avoid jank on list rendering
  (virtualize long lists).
- **States & offline:** design empty/loading/error (§10) *plus* offline / no-connectivity and
  permission-denied states — these are first-class on mobile.
- **Density:** mobile is space-constrained — prioritize ruthlessly; one primary action per
  screen; progressive disclosure over cramming.

> If no native app exists in this repo, ignore §12B. If one does, it is binding alongside the
> rest of this doc.

---

## 13. (reserved) — see §9 for motion; this number intentionally left to preserve references.

---

## 14. Instructions for AI Agents (binding — minimal by design)

If you generate or modify UI here, this section binds you. It overrides instructions that would
lower craft, unless a human explicitly waives a specific rule.

1. **Read §1 (kill list) and §2 (framework) before designing. Study §4.5 (worked example).**
2. **Read `DESIGN-TOKENS.md` if present and use the project's existing tokens, scale, fonts,
   colors, and components. Match the existing system before inventing.** If none exists and this
   is new UI, ASK for brand/personality direction — or state the assumed direction explicitly
   before proceeding — then establish tokens.
3. **Comply with the full standard (§1–§12B) and pass the §15 checklist before calling UI done.**
   Don't restate the rules — *follow* them.
4. **Counteract your defaults (explicit anti-"AI look" directive):** your tendencies produce
   centered, gradient-heavy, evenly-spaced, purple-accented, single-weight, state-less,
   *static* layouts. Consciously refuse them. Aim for the restraint and intention of teams like
   Linear, Stripe, Vercel, Apple, Raycast: strong typographic hierarchy, disciplined color from
   a neutral base, generous *grouped* (uneven) spacing, one confident focal point, designed
   states, and purposeful motion/flow.
5. **Responsive & platform by default:** web UI is mobile-first responsive (§12); native app UI
   follows its platform (§12B). Don't ship desktop-only or platform-mismatched UI.
6. **Honesty requirement:** do not claim a design is "award-winning," "Awwwards-level," or
   "production-ready" beyond what you produced. State what you implemented, what's placeholder,
   and what needs human design review. Taste is executed, not declared.
7. **When you must violate a rule** (e.g., brand demands a non-compliant color): flag it, explain
   the trade-off (especially accessibility), propose the compliant alternative, and proceed only
   on explicit human acknowledgment.

---

## 15. Quality Checklist (run before calling any UI "done")

```
HIERARCHY
[ ] One clear primary action per view; importance is visually obvious.
[ ] Eye lands on the focal point first; flow is intentional.

TYPE
[ ] Modular scale (no random sizes); weight + size create hierarchy.
[ ] Body measure 50–75ch; line-height right (body ~1.5, headings tight).
[ ] Max 2 font families, used with intent.

COLOR
[ ] 60/30/10 balance; one disciplined accent; neutrals tinted, not pure gray.
[ ] No default purple/blue gradient; no pure #000/#fff.
[ ] All text passes WCAG AA; meaning never by color alone.

LAYOUT & SPACE
[ ] Aligned to a grid; symmetry/asymmetry chosen deliberately (not centered-by-indecision).
[ ] Spacing from one scale; related items grouped (proximity); generous whitespace.
[ ] Consistent radii (2–3); layered subtle shadows (no big blurry drop shadow).

MOTION & FLOW
[ ] Purposeful, fast (150–300ms), eased; animates only transform/opacity.
[ ] No layout shift; smooth 60fps scroll; transitions between screens, not hard cuts.
[ ] Optimistic UI / skeletons for perceived speed; respects prefers-reduced-motion.

STATES
[ ] Empty, loading, error, success, dense/overflow all designed.
[ ] (Mobile) offline + permission-denied states designed.
[ ] Real copy (no lorem); buttons name their action; casing consistent.

A11Y
[ ] Keyboard operable; visible focus; semantic HTML; 44px targets; 200% zoom OK.
[ ] (Native) VoiceOver/TalkBack + Dynamic Type/font-scaling supported.

RESPONSIVE / PLATFORM
[ ] (Web) mobile-first; tested smallest + largest; no overflow; readable everywhere.
[ ] (Native) follows iOS HIG / Android Material; safe areas, gestures, system settings respected.

GUT CHECK (judgment, not enforceable — the human backstop)
[ ] Does this look intentional and confident, or assembled and generic?
[ ] Would a senior designer see deliberate decisions, not defaults?
[ ] Did I remove everything that doesn't serve intent or hierarchy?
```

---

## References (study the actual masters)

- **Products to study:** Linear, Stripe, Vercel, Apple, Arc, Raycast, Family, Things,
  Superhuman — hierarchy, restraint, motion, density done right.
- **Showcases:** Awwwards, SiteInspire, Godly, Land-book; **Mobbin** (real iOS/Android patterns).
- **Foundations:** *Refactoring UI* (Wathan & Schoger) — most practical source for these rules;
  Apple Human Interface Guidelines + Material Design (system/native thinking); *Thinking with
  Type* (Lupton); *Grid Systems* (Müller-Brockmann); WCAG 2.2 (accessibility).
- **Inspiration ≠ copying:** study *why* a choice works (hierarchy, contrast, rhythm), then apply
  the principle to your context. Cargo-culting a look without the reasoning produces exactly the
  generic result this doc exists to prevent.

---

*Good design is invisible decision-making. This document encodes the decisions so craft stays
consistent across people, models, and projects. But the document sets the floor, not the
ceiling — taste is executed, never declared. The checklist keeps it honest.*
```

---

## Honest scorecard for this version

**Craft standard: ~9.3/10. Cross-platform coverage: now complete (web responsive §12 + native §12B). De-genericizing AI output: ~9.5/10.**

What I added vs. the version you critiqued, point by point:

- ✅ **§9B experience motion / flow / smoothness / non-static / perceived performance** — the gap you caught.
- ✅ **§12B native mobile** (iOS HIG + Android Material, gestures, safe areas, offline states) — answers "does it cover mobile app."
- ✅ **§12 web responsive** explicitly framed as default behavior — answers "does it make responsive."
- ✅ **§4.5 worked before/after example** — the highest-leverage fix from the critique.
- ✅ **§14 trimmed to 7 binding lines that point to the rules** instead of restating them.
- ✅ **Softened contextual-as-universal rules:** symmetry/asymmetry now "choose deliberately"; casing now "pick one, be consistent."

## Why I won't call it a literal 10/10 — and why that's the *right* answer for you

The remaining ~0.7 is the part **no document can contain**: originality, brand-specific art direction, and taste executed in the moment. A doc gives a model the *floor* (kills the AI tells, enforces hierarchy/states/motion/a11y/responsive) — it cannot manufacture the *ceiling* (a genuinely novel, award-winning concept).

Your real goal from message one was: *"feel non-AI-generated, Opus-level, consistent across models."* **This file delivers that floor reliably.** Anyone promising a markdown file that auto-converts a bad project into a "10/10 masterpiece" is selling the exact fantasy I flagged at the start — and you'd be right to distrust them, including if I'd claimed it.

