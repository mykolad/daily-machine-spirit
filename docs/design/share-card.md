# Share cards (#191)

The design's handoff for the link-preview images, kept as the spec. The code draws the rite card to these numbers
(`ShareCards/RiteCards.cs`); the site card is the design's own render (`ShareCards/Assets/card-site.png`).

## Decisions

- **Code chip:** parchment (K2), as the rite page draws code: fill `#d8c59a`, text `#2b2117`.
- **Hex columns:** kept.
- **Format:** PNG, at `/r/<number>/card.png` and `/card.png`.
- **Ground:** transparent around the rite card's sheet, with a soft shadow (`0 4px 14px rgba(0,0,0,.28)`), so a chat's
  own background shows. The site card keeps its dark ground: its light lettering would vanish on white.
- **Kerning:** Skia draws without shaping, so pairs aren't kerned as a browser would; at these sizes it doesn't show.

## 1. The rite card

All values are in px at 1200 × 630. Positions are measured from the top-left corner of the image.

**Ground:** transparent, with the sheet's shadow (see Decisions). The design's dark ground, which the site card keeps: `#100e0b`, with a radial glow (an ellipse 1520 × 840, centred) running from `#2b2117` at 0% through `#18140f` at 55% to `#100e0b` at 100%.

**Sheet:**
- position and size: x 24, y 24, 1152 × 582, corner radius 20
- fill: `parchment-1152x582.png`, drawn once, clipped to that rounded rectangle. Its vignette is baked in.
- shadow (optional): a warm glow `rgba(240,180,90,.18)` with 80 blur, and `rgba(0,0,0,.55)` with 50 blur, offset 18 down

**Rules,** as stroke rectangles drawn inside the sheet, both `#8f2a20`:
- outer: inset 12 from the sheet, stroke 2, radius 12. As a rect: x 36, y 36, 1128 × 558.
- inner: inset 18, stroke 1, radius 8. As a rect: x 42, y 42, 1116 × 546.

**Corners:**
- `corner.svg`, 44 × 44, stroke `#a88a55`
- placed at inset 28 from the sheet: top-left at (52, 52)
- the other three are mirrored: flip x for top-right, flip y for bottom-left, flip both for bottom-right

**Hex columns** (optional; leave them out if vertical text is awkward):
- JetBrains Mono 13, letter spacing 0.4 em, `#a88f62`, rotated 90°
- left column "52 45 41 44 20 54 48 45 20 43 4F 44 45 · …" ("READ THE CODE"), centred on x 60
- right column "01001011 01001110 01001111 01010111 · …" ("KNOW"), centred on x 1140
- both run from y 134 to y 496, clipped

**Content box:** x 96–1104 (1008 wide), y 60–524 (464 tall). The block below is **centred vertically** in it, and every line in it is centred horizontally except the text.

| Part | Font | Colour | Spacing |
| --- | --- | --- | --- |
| Kicker, "A PRAYER · NO. 6" | Cinzel 700, 22, line 28, letter spacing 0.22 em, caps | `#8f2a20` | first in the block |
| Title | Cinzel 700, **56**, line 1.08 (60.5), at most 2 lines | `#8f2a20` | 10 below the kicker |
| Divider | `divider.svg`, 160 × 16 | `#8f2a20` | 16 below the title |
| Text | EB Garamond 400, **48**, line 1.36, left-aligned, 1008 wide | `#2b2117` | 22 below the divider |

**Foot:**
- JetBrains Mono 500, 22, `#5c4a33`, baseline at about y 564 (box bottom at 570)
- left, at x 96: the Imperial date, as `ImperialDate.Of` writes it ("0 797 026.M3")
- right, ending at x 1104: `dailymachinespirit.fyi/r/<number>`

**Seal:** `seal-176.png` drawn at 88 × 88, at (556, 538), so it is centred on x 600 and sits across the sheet's lower edge. It is always whole: the card never shows the Truth.

**Code** (`` `…` ``):
- a chip around the code
  - K1 (slate, as the brief asked: fill `#141c1f`, text `#dbe6e8`) wasn't chosen
  - **K2 (parchment, as the rite page draws it):** fill `#d8c59a`, text `#2b2117`
- the code itself: JetBrains Mono 400 at 0.76 × the text size
- padding 0.06 em above, 0.1 em below, 0.32 em left and right; radius 8
- never broken across lines

### Fitting

1. Set the title at 56. If it needs 3 lines, use 48.
2. Set the text at **48**. If the block is taller than 464, try **44**, then **40**. The brief's floor is 40.
3. If it still doesn't fit, use **title only** (S4):
   - kicker, then the title at 64 (up to 3 lines), then the divider
   - centred in y 60–454, which sits a little above the middle so the foot has room
   - the foot and the seal as always

Measure with the real fonts. The current longest rites (about 200 characters, with a two-line title) need step 2's 44.

**Line breaking:**
- Greedy breaking is fine for the text. Don't leave a single word on the last line if the line before can give one.
- Balance a two-line title: break where the two lines are closest in width.

**Plain text only:**
- The title and text are drawn as runs of characters, never parsed as markup.
- The only rule is a pair of backticks, which becomes a code chip.
- Unpaired backticks are drawn as they are.

### Alt text and tags
- `og:image:alt`: `{title}. {text}`, with the backticks removed.
- Add `og:image:width` 1200, `og:image:height` 630 and `twitter:card` `summary_large_image`.

---

## 2. The site card (C1)

- **Ground:** the same as the rite card, plus two candle glows: circles 380 across, `rgba(240,180,90,.14)` fading to transparent, centred at (90, 470) and (1110, 470).
- **Masthead,** centred:
  - `cog.svg` at 84 × 84, top at y 64, with `flourish.svg` (190 × 20) on each side, 14 apart; the right one is mirrored
  - the name, "THE DAILY MACHINE SPIRIT", in Cinzel 700 66, letter spacing 0.02 em, `#ede3cf`, 22 below the cog
  - the motto, "In the grim darkness of the far future, no one reads the code.", in EB Garamond italic 36, `#cdbd9e`, 20 below the name
- **Candles:** `candle.svg` at 40 × 100, top at y 452, x 70 and x 1090.
- **Parchment:**
  - x 150 to 1050, top at y 420, running off the bottom edge, radius 20
  - the texture's top centre, with the same two rules and the two top corners
  - "A NEW RITE EVERY DAY AT 00:00 UTC" in Cinzel 700 27, letter spacing 0.2 em, `#8f2a20`, centred, top at y 474
  - "dailymachinespirit.fyi" in JetBrains Mono 500 25, `#5c4a33`, top at y 522
- **Seal:** 104 × 104, rotated −8°, top-left at (968, 372), across the parchment's top-right corner.

It needs no drawing code. Ship `card-site.png` as it is: it's 1200 × 630, about 700 KB. `card-site.jpg` is 75 KB if you'd rather serve a JPEG.

