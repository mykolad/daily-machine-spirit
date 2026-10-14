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

## Search

The handoff has no search. It uses the site's tokens, on the dark background.

- **The field has a visible label** ("Words in the rite") and a `#b6a98f` border, well above the 3:1 WCAG 1.4.11 asks
  of a control's edge, like the Scriptorium's note fields. The form is a landmark (`role="search"`).
- **How many rites were found is in the page's title** ("“cache”: 3 rites"), not only on the page: the results are a
  new page, so the title is what a screen reader reads first, and a live region wouldn't be announced on load.
- **It works without the script:** a plain form that sends `GET /search?q=`, and "Load older rites" is a link, as in
  the archive.

## The main region

- **`<main>` shows no focus ring.** It takes focus (`tabindex="-1"`) only so "Skip to content" can move focus to it, and
  isn't a control. A click inside it focuses it too, and a reload restored that focus with the amber ring around the
  whole page for a moment. Its first link or button still shows the ring, as every control does.

## The Scriptorium, shown in place

- **The forms still work without the script,** and the script only changes how the result is shown, never what is
  sent: the same request, then the page the redirect leads to.
- **The status message is never replaced, only given new text,** so a screen reader announces it as after a reload: a
  live region that's removed and added again may not be.
- **Focus stays where the Scribe is working:** the same button on the rite that moved, else its first button. After a
  burn, the first button of the rite that took its place, without scrolling away; the message, with its link to the
  rite in the Ashes, is announced all the same.
- **Reduced motion** (`prefers-reduced-motion`) turns off the slide and the glow; the rite acted on keeps its amber
  border, which isn't motion.
- **One action at a time:** while one is under way, `aria-busy` marks the page, and a second click is ignored until
  the first is done.

## The wide screen

The handoff's desktop update (`Desktop Options.dc.html`, 2a, 2b, 2g, 2h): margins beside the page from 1280px, a glow
and candles behind it, and the recent rites under Today.

- **The margins don't repeat the parchment.** The handoff's left margin had the rite's number and date, which the
  parchment already shows; a screen reader would hear them twice. The left margin holds the countdown and the tags
  instead, the right one the votes.
- **The markup stays in reading order:** the rite, the seal and the truth, then the votes, the share link and the
  margin notes. The stylesheet places them in the margins, so a keyboard goes from the seal to the votes, then on.
- **One countdown at a time.** The margin's and the line under the actions are both in the page; the one not shown is
  `display: none`, so a screen reader reads one.
- **The votes' margin label ("The faithful say") is hidden from screen readers:** the votes are already a group named
  "Reactions".
- **A recent rite's kicker is 12px, not the handoff's 10px:** the smallest label on the site, and still red on
  parchment at 7.9:1. Each card is one link, and its focus ring falls outside it on the dark ground.
- **The circuit traces stay in the space outside the margins,** cut off where the margins begin, so they never pass
  behind text; they show from 1440px, where that space is wide enough to see them.
