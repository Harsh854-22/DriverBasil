# DESIGN-DIRECTION.md — Art Direction Interview & Premium-Craft Playbook

> **Status:** The project-specific *aesthetic contract*. `DESIGN.md` is the universal floor
> (anti-AI rules, hierarchy, a11y, states — same everywhere). THIS file decides what *this*
> project looks and feels like: its style, fonts, color ratio, motion, flow, and the signature
> premium techniques it uses. Once filled, it is binding — it keeps design consistent across
> every screen, every author, every model.
>
> **HOW THIS FILE WORKS:** It ships as an **interview**. Before designing, an AI agent (or you)
> answers §1–§6. The answers are recorded in §7 as the locked direction. From then on, every
> screen conforms to §7. **Empty = ask the questions first. Never assume an aesthetic.**
>
> **THE CORE RULE THAT NEVER CHANGES:** Whatever style is chosen, it is executed with the
> discipline in `DESIGN.md` — intentional, not templated. A chosen style is *no excuse* for the
> §1 anti-patterns. Glassmorphism done lazily is still AI slop; brutalism done lazily is still ugly.

---

## 0. How to Use This File

- **AI agents:** if §7 is empty, you MUST run the §1–§6 interview and get human answers (or state
  explicit assumptions) **before** generating UI. Do not silently pick a style. §8 is binding.
- **Humans:** answer the interview once at project start. Revisit only on a deliberate rebrand.
- **The interview is a menu, not a checklist** — you don't need every option, you need a
  *coherent* set. The agent's job is to *recommend* a fitting direction and explain why, then
  confirm — not to dump every choice on you.

---

## 1. STYLE INTERVIEW — "What should this project look like?"

> The agent presents these options **with a recommendation** based on the product's domain and
> audience (from `DESIGN.md` §2 intent). It explains *why* a style fits, names the trade-offs,
> and asks for confirmation. It never just picks silently.

**Q1. Pick the primary aesthetic** (one dominant; at most one secondary accent style):

| Style | Feels like | Best for | Watch-outs |
|-------|-----------|----------|-----------|
| **Minimalism** | Calm, confident, lots of space | Most SaaS, productivity, premium brands | Boring if hierarchy is weak — type must carry it |
| **Bold / Editorial Typography** | Loud, opinionated, magazine | Agencies, portfolios, landing pages, brands | Needs a real display face; weak fonts kill it |
| **Glassmorphism** | Light, layered, frosted, modern | Dashboards, overlays, fintech, Apple-adjacent | Contrast/a11y is hard on glass — verify text legibility |
| **Skeuomorphism / Neumorphism** | Tactile, soft, physical | Audio/creative tools, novelty, nostalgia | Neumorphism fails contrast easily — use sparingly |
| **Claymorphism** | Soft, rounded, playful, 3D-ish | Consumer, kids, friendly products | Can read childish for serious tools |
| **Brutalism** | Raw, stark, anti-design, confident | Editorial, art, dev tools, statement brands | Intentional ugliness — easy to do *accidentally* badly |
| **Maximalism** | Dense, expressive, layered, loud | Music, fashion, culture, bold brands | Needs masterful hierarchy or it's chaos |
| **Bento Grid** | Modular, organized, varied tiles | Feature showcases, dashboards, marketing | Tiles must vary in size/content — not 9 equal boxes |
| **Spatial / Depth UI** | Layered z-depth, parallax, dimensional | Premium product, storytelling, Apple-style | Performance + reduced-motion discipline required |
| **Liquid Glass / Aurora** | Fluid, glowing, gradient-light, organic | Modern consumer, AI products, creative | The 2024–25 trend — date-sensitive; one gradient max (§DESIGN.md §1) |
| **Swiss / International** | Grid-precise, ordered, typographic | Editorial, data, institutional, premium | Rigor is the point — sloppy grid = failure |

**Q2. Overall mood (pick 2–3, they must cohere):**
`calm · confident · energetic · playful · serious · luxurious · technical · warm · futuristic · editorial · minimal · bold`

**Q3. Light, dark, or both?** (Both = each is a *designed theme*, not `invert()` — `DESIGN.md` §3.)

**Q4. Density:** breathing-room (consumer/landing) ↔ information-dense (pro tool/dashboard)?
*(`DESIGN.md` §4 — a deliberate choice, matched to audience.)*

> **Agent output for §1:** "For a [domain] product aimed at [audience] that should feel [mood],
> I recommend **[style]** because [reason], with [secondary] for [accent]. Trade-off: [x].
> Confirm or adjust?"

---

## 2. TYPOGRAPHY INTERVIEW — "What fonts match this project?"

> Type carries ~70% of the feeling (`DESIGN.md` §5). The agent recommends a **pairing** that
> matches the §1 style + §Q2 mood, with real font names, then confirms.

**Q5. Personality of the display/heading face:**
`geometric-clean · grotesque-neutral · humanist-warm · elegant-serif · editorial-serif ·
monospace-technical · expressive-display · rounded-friendly`

**Q6. Confirm a real pairing** (agent proposes 2–3 concrete options that fit; examples by mood):

| Mood / Style | Display face (example direction) | Body face (example direction) |
|--------------|----------------------------------|-------------------------------|
| Minimal / SaaS | Inter, Geist, General Sans, Söhne | same family, or Inter/system |
| Editorial / Bold | Clash Display, Cabinet Grotesk, a strong serif | a clean grotesque |
| Luxury / Elegant | A high-contrast serif (Canela, Editorial New) | a quiet humanist sans |
| Technical / Dev | Geist Mono, JetBrains Mono (accents) | Inter / IBM Plex |
| Playful / Consumer | Rounded (Gilroy, Poppins-with-care) | a friendly sans |
| Brutalist | system-ui *intentionally*, or a stark grotesque | monospace or system |

- **Rule:** max 2 families (3 with reason). Pair by **contrast** (expressive display + clean
  body) or commit to one variable family across weights. Prefer variable fonts. (`DESIGN.md` §5.)
- Record exact families, weights, and the modular scale in §7 → and in `DESIGN-TOKENS.md`.

> **Agent output:** "I propose **[Display] + [Body]** because [fit]. Alt: [B]. Confirm?"

---

## 3. COLOR & THE 60/30/10 LAW

> **The default law (applies unless §1 style explicitly overrides — see exceptions):**
> Every screen uses the **60 / 30 / 10** ratio so it reads as balanced and intentional:
> - **60% dominant** — neutral foundation (background/surfaces; off-white or near-black, never pure).
> - **30% secondary** — supporting surfaces/borders/muted text (a *tinted* neutral).
> - **10% accent** — the brand color: primary actions, focus, key highlights. Scarcity = power.
>
> This is the single easiest rule to make a screen "look designed." Eyeballing color amounts is
> the #1 amateur tell. **Allocate deliberately to 60/30/10 and verify it.**

**Q7. Pick the neutral foundation first** (the 60% — sets the whole tone before any color):
`warm sand · cool slate · pure-ish gray (tinted) · near-black (hue-tinted) · off-white cream`

**Q8. Pick ONE accent** (the 10%), drawn from the product's domain/emotion — *not* a default
purple/blue (`DESIGN.md` §1). Second accent only with a stated reason.

**⚠️ 60/30/10 EXCEPTIONS (the agent must apply these, not break the rule blindly):**
- **Maximalism / Bold Editorial:** may run richer (e.g. 50/30/20) — but still a *dominant →
  support → accent* relationship, never equal-weight chaos. The hierarchy of color must survive.
- **Brutalism:** often high-contrast 2-color or near-monochrome — 60/30/10 may compress to
  a stark dominant + one accent. Intentional, not random.
- **Everything else (the default ~90% of projects): 60/30/10 is law. Verify the proportions.**

> **Agent output:** "Neutral: [x] (60%). Accent: [y] (10%), from [domain reason]. Support: [z]
> (30%). This holds 60/30/10. Confirm?" — Full ramp + roles recorded in `DESIGN-TOKENS.md`
> (`DESIGN.md` §3).

---

## 4. MOTION INTERVIEW — "How should it move?"

> Motion = §9 of `DESIGN.md` (purposeful, fast, eased, reduced-motion-safe). This interview pins
> the *specific* motion personality so every button and transition matches.

**Q9. Button / micro-interaction feel:**
`subtle (lift + tint, 150ms) · springy (bounce, playful) · crisp (instant, snappy) ·
glow/pulse (accent emphasis) · depress (tactile press) · magnetic (cursor-follow, desktop)`

**Q10. Page / route transition style:**
`instant (none — fastest) · fade (calm) · slide (directional, spatial logic) ·
shared-element morph (premium continuity: card → detail) · cover/reveal (editorial) ·
scale-fade (modern app feel)`

**Q11. Scroll behavior:**
`static (content just appears) · reveal-on-scroll (fade+rise, staggered, once) ·
parallax/depth (spatial, restrained) · scroll-linked storytelling (heavy — only if core to brand)`

**Q12. Page flow / navigation model:**
`linear (wizard/onboarding — one path) · hub-and-spoke (dashboard → sections → back) ·
tabbed (peer sections) · feed/infinite (content stream) · spatial (canvas/zoom)`

- **Locked constraints (from `DESIGN.md` §9, non-negotiable regardless of choices above):**
  transitions 150–350ms, `ease-out` entrances, animate only `transform`/`opacity`, no layout
  shift, `prefers-reduced-motion` honored, restraint (1–2 signature moments per flow).

> **Agent output:** "Buttons: [feel]. Transitions: [style] with [spatial logic]. Scroll: [x].
> Flow: [model]. All within §9 timing/easing/reduced-motion rules. Confirm?"

---

## 5. PREMIUM-CRAFT TECHNIQUES — "What makes it look expensive?"

> These are the signature moves designers use to push from "clean" to "premium." Pick the few
> that fit the §1 style — **used sparingly and intentionally** (one or two signature moments,
> `DESIGN.md` §9). Overusing them is as amateur as using none.

**Q13. Choose 2–4 signature techniques to deploy:**

- **Inverted / masked typography** — text clipped to fill (image/video/gradient shows *through*
  the letters via `background-clip: text`). One hero moment, not everywhere.
- **Text masked behind objects** — headline passes *behind* a product image/photo subject for
  depth (z-layering an image over part of large type). Strong editorial/spatial signal.
- **Consistent image treatment** — pick ONE: square/`1:1` crops, uniform `4:5`, or fixed aspect
  ratios across the project. Inconsistent crops are the #1 "thrown together" tell.
- **Duotone / unified photo grade** — all imagery shares one color grade/treatment so photos feel
  curated, not stock-pile.
- **Overlap & layering** — let cards/images/sections overlap their boundaries slightly (negative
  margins) instead of stacking in neat rows — reads as designed, not assembled.
- **Oversized type as graphic** — huge display type *is* the visual (editorial/bold styles).
- **Grid-breaking accent** — one element deliberately escapes the grid to draw the eye (only
  works *because* everything else obeys the grid).
- **Fine detail polish** — hairline borders, subtle noise/grain texture on flat backgrounds,
  layered tinted shadows (`DESIGN.md` §7), gradient *meshes* over flat fills (one, subtle).
- **Sticky / scroll-pinned reveal** — a visual pins while content scrolls past (spatial styles).
- **Generous, asymmetric whitespace** — the cheapest premium signal (`DESIGN.md` §4).

> **Rule:** these serve hierarchy and the chosen style — never decoration for its own sake.
> If a technique doesn't reinforce intent, cut it. (Restraint = confidence — `DESIGN.md` §2.)

---

## 6. ANTI-AI GUARANTEE (carried from DESIGN.md §1, restated as a direction-level promise)

Whatever the interview selects, the result must NOT exhibit the §1 AI tells. Specifically, even
inside a chosen trendy style:
- No default purple→blue gradient (even in "liquid glass" — pick a *characterful* gradient or none).
- No centered-everything by reflex; deliberate composition (§4).
- No equal-weight 3-card rows, no emoji icons, no pure #000/#fff, no single-weight type.
- Real content, designed states, motion + responsiveness baked in.
> A chosen aesthetic is a *direction*, never a *shortcut past craft*.

---

## 7. THE LOCKED DIRECTION (fill from the interview — this becomes binding)

> **⚠️ Fill this once from §1–§6 answers. After this, every screen conforms. This is the
> consistency contract. Empty = run the interview first.**

```
PROJECT: __________
LAST UPDATED: __________

STYLE (§1)
- Primary aesthetic: __________
- Secondary accent style (if any): __________
- Mood: __________ / __________ / __________
- Theme: light | dark | both
- Density: breathing-room | balanced | dense

TYPOGRAPHY (§2) — also in DESIGN-TOKENS.md
- Display face: __________ (weights: ___)
- Body face: __________ (weights: ___)
- Modular scale: __________

COLOR (§3) — full ramp in DESIGN-TOKENS.md
- Neutral foundation (60%): __________
- Secondary (30%): __________
- Accent (10%): __________
- 60/30/10 status: standard | documented exception: __________

MOTION (§4)
- Button feel: __________
- Page transition: __________  (spatial logic: __________)
- Scroll behavior: __________
- Page-flow model: __________

PREMIUM TECHNIQUES (§5) — the 2–4 signatures for this project
- __________ , __________ , __________ , __________
- Image treatment (locked): __________ (aspect: ___ )

NON-NEGOTIABLES
- Inherits all of DESIGN.md (anti-AI §1, a11y §11, states §10, responsive §12/§12B).
```

---

## 8. Instructions for AI Agents (binding)

1. **If §7 is empty, run the §1–§6 interview before designing.** Present options *with a
   reasoned recommendation* tied to the product's intent/audience (`DESIGN.md` §2); get
   confirmation or state explicit assumptions. **Never silently choose an aesthetic.**
2. **Once §7 is filled, it is binding.** Every screen conforms to the locked style, fonts, color
   ratio, motion, and techniques. Consistency across screens/authors/models is the goal.
3. **Enforce 60/30/10 (§3) as default law.** Allocate color to the ratio deliberately and verify
   it. Only deviate per a §3-documented style exception, and say so.
4. **Apply premium techniques (§5) sparingly** — 2–4 signatures, in service of hierarchy, never
   as decoration. One hero moment beats ten gimmicks.
5. **A chosen style never excuses a §1 anti-pattern (§6).** Trendy ≠ exempt from craft. Lazy
   glassmorphism/brutalism/liquid-glass is still AI slop.
6. **All of `DESIGN.md` still binds** — this file chooses the *aesthetic*; `DESIGN.md` enforces
   the *craft floor* (a11y, states, responsive, motion safety). On conflict, the stricter
   (safer/more accessible) rule wins.
7. **Honesty:** recommend the style that genuinely fits the product — not the trendiest one.
   If the user requests a style that fights their goal (e.g. brutalism for a trust-critical
   bank), say so and propose the fitting alternative. Don't claim a look is "premium" you didn't
   actually execute (`DESIGN.md` §14 honesty rule).
8. **Output `DESIGN-GUIDELINES.md`** once §7 is locked (see §9) so the direction is documented
   for the whole team/all future work.

---

## 9. Generate `DESIGN-GUIDELINES.md` (the living consistency record)

Once §7 is locked, produce a `DESIGN-GUIDELINES.md` that turns the direction into a concrete,
referenceable spec so every future screen matches. It contains:

```
# DESIGN-GUIDELINES.md — [Project] Visual Specification

1. DIRECTION SUMMARY      — the locked §7 (style, mood, theme, density).
2. TYPOGRAPHY             — exact families, the full type scale w/ sizes/weights/line-heights/
                            tracking (DESIGN.md §5 table), usage rules (when to use each level).
3. COLOR                  — the full ramp, semantic roles, and the explicit 60/30/10 allocation
                            with example proportions per layout. Contrast pairs verified (AA).
4. SPACING & RADII        — the scale, the 2–3 radii, border/shadow tokens (DESIGN.md §6–§7).
5. COMPONENTS             — buttons (all variants + states), inputs, cards, nav, modals: each
                            with spacing, radius, shadow, and motion spec.
6. MOTION                 — button/transition/scroll specs with exact durations + easing;
                            the spatial logic; reduced-motion fallbacks.
7. PREMIUM TECHNIQUES     — the 2–4 chosen signatures, WHERE/how-often to use each, with rules.
8. IMAGERY                — locked aspect ratios/crops, grade/treatment, do's & don'ts.
9. EXAMPLES               — at least one BEFORE/AFTER (DESIGN.md §4.5 style) in THIS project's
                            direction, so future work has a concrete reference.
10. DON'TS                — project-specific anti-patterns (the §1 list + any style-specific ones).
```

> `DESIGN-GUIDELINES.md` is the *output*; this file (`DESIGN-DIRECTION.md`) is the *process that
> generates it*. Tokens stay machine-readable in `DESIGN-TOKENS.md`; guidelines stay
> human-readable. Same split as the rest of the doc set.

---

## References
- `DESIGN.md` (the universal craft floor this file sits on top of).
- *Refactoring UI* (color ratio, hierarchy, premium detail); Apple HIG / Material (native).
- Style study: Awwwards, Godly, Mobbin, SiteInspire — for *executed* examples of each §1 style.
- **The discipline is universal; the chosen direction is project-specific** — exactly why this
  file is an interview, not a fixed answer.

---

*A chosen aesthetic doesn't make design good — disciplined execution of a chosen aesthetic does.
This file picks the direction deliberately and locks it so every screen agrees; `DESIGN.md`
guarantees the craft underneath. The interview adapts to the project; the standard never bends.*
```
