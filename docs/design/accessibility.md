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
