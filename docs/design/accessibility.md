# Where accessibility changed the design

WCAG 2.2 AA wins over design fidelity (CLAUDE.md). Each change from the design handoff, or from its tokens on a page
the handoff doesn't cover, is recorded here.

## The Scriptorium

The Scriptorium uses the site's tokens on the same dark background, and the handoff's proposal for it (`Scriptorium
Proposal.dc.html`, `scriptorium-icons.md`): icons, grouped buttons, folded notes, and messages that name the rite.

- **Note fields have a `#b6a98f` (text-muted) border, not the faint `rgba(237,227,207,.22)` used for outline buttons.**
  An empty text field has no other visible edge. The faint border is about 1.7:1 against the card, below the 3:1
  WCAG 1.4.11 asks of a control's boundary. `#b6a98f` is well above 3:1 on the card and inside the faded Ashes cards.
- **Every action button names its rite for screen readers** ("Anoint: The Rite of Re-Run") with visually hidden text,
  since every draft has the same buttons.
- **Plain forms, no script:** every action is a button that posts and returns to the page with a `role="status"`
  message, so it works with a keyboard and a screen reader as it is.
- **The icons are decorative** (`aria-hidden="true"`, `focusable="false"`), next to each button's label, never instead
  of it. They're drawn in `currentColor`, so they have the label's contrast.
- **A folded note keeps its own label.** The `<summary>` ("Note for the Augury") opens the fold, but it doesn't name the
  text field, so the field has a visually hidden `<label>` too.
- **Exalt and Humble are a `role="group"` named "Reorder"**, so a screen reader announces the pair as one set of
  controls.

## Tags

The handoff has no tags. They use the site's tokens, on the dark background like every other control.

- **An archive row's link is its title, stretched over the row,** instead of the whole row being one link: a link can't
  hold the row's tag links. The row still takes a click anywhere and shows the focus ring around the whole row when its
  title has focus (`:has`). A screen reader now hears the title as the link's name, not the whole row.
- **Tag links are `#d9b874` (the link colour) on the page or a row,** well above 4.5:1 on both. Their pill border is
  decoration; the text says what they are. Each is at least 32px tall, above the 24px WCAG 2.5.8 asks.
- **A row's tags start with a visually hidden "Filed under",** which the rite card and the Scriptorium say visibly, so
  a screen reader doesn't hear bare words after the counts.
