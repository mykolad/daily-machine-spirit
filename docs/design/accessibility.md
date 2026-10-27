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
- **The amending page's fields each have a visible label,** and the two hints (code in backticks; what the note is
  for) are tied to their fields with `aria-describedby`. "Amend" on the calendar is a link, since it only opens the
  page: the page's buttons change things. Why words were refused, or that the Machine Spirit rewrote them, is the
  page's `role="status"` message, and the fields keep what the Scribe typed.
- **The tab bar is a `<nav>` named "The Scriptorium",** its links in a list. The current page's tab carries
  `aria-current="page"` and a brass underline, not a colour change alone. Each count is text inside its tab's link, so
  it's read with the tab's name. The links are 44px tall, and the tabs wrap on a narrow screen rather than scroll.

## The Calendar (the Scriptorium redesign, step 1)

The handoff's `design_handoff_scriptorium_calendar`: the Scribes' and the Augury's orders as two groups of compact rows,
each opening into its card, subject chips, a reason asked at the moment of burning, and Undo.

- **Which order a rite is in is said in words:** each group has a heading ("Placed by the Scribes", "The Augury's
  order") that names its list, and a rite's place ("#3") is text. The brass border and the position's colour aren't
  the only sign (WCAG 1.4.1). A rite's card still says who placed it ("placed by Magos Ferrum") when the order knows.
- **Exalt and Humble are icons alone,** so each has visually hidden text naming the rite ("Exalt: The Rite of Re-Run"),
  and a `title` saying where it goes ("Exalt: one day earlier"). They're the anointed's alone: on the Augury's rows
  they're left out rather than disabled, since twenty pairs that can never be pressed would only be noise. At the
  ends of the anointed they're `disabled`, with a `title` saying why, rather than missing, so the anointed rows'
  controls line up.
- **Release to the Augury** stands in Anoint's stead on an anointed rite, before the burn, with its own icon (Anoint's
  oil struck through) and its name for screen readers, like every action button.
- **The chevron is the card's toggle,** a button with `aria-expanded` and `aria-controls`; clicking the row is only a
  shortcut for a pointer. Without the script every card is open and the chevron is hidden.
- **The subject chips are links,** so the filter works without the script; the chosen one carries
  `aria-current="true"` and the gold fill. They're 34px tall: above the 24px of WCAG 2.5.8, below the 44px of the
  buttons, as the handoff drew them.
- **The burn panel is a `role="group"` named by its question,** its reasons toggle buttons (`aria-pressed`), one at a
  time, and its text field has a visually hidden label (the placeholder isn't one). Escape or Cancel closes it and
  returns focus to "Consign to the Flames". Only the script shows it; without it, the button burns at once with the
  note, as before.
- **The note's fold opens straight into its field:** "Add a note for the Augury" gives way to the labelled field, and
  focus moves into it, so it isn't lost with the hidden summary.
- **Undo doesn't vanish under the Scribe:** the toast sits in a `role="status"` region that's always in the page, so
  it's announced, and its seven seconds wait while the pointer or focus is on it (WCAG 2.2.1). After that, the Ashes
  still restore the rite, as without the script; the toast says what the status message would, so the message drops
  those words rather than say them twice.
- **Button labels don't wrap, except on a narrow screen:** the handoff's `white-space: nowrap` on every button holds
  down to 400px wide; below that, labels wrap again, so "Ask the Machine Spirit to Rewrite" never pushes the page
  sideways at 320px (WCAG 1.4.10, reflow).

## The Ashes (the Scriptorium redesign, step 2)

- **Each day is a heading** ("Burned Tue 20 Oct", with its count), so a screen reader can move between the days, and
  its rows are a list named by it. The burn-day red (`#e88a78`) is 7.6:1 on the page.
- **The row's Restore names its rite** with visually hidden text ("Restore from the Ashes: The Rite of the Blessed
  Seed") instead of the handoff's `aria-label`, so the name still begins with the visible word (WCAG 2.5.3).
- **Why it burned is said as such:** the quoted reason under the title starts with a visually hidden "Why it burned:",
  since the quotation marks alone tell a screen reader nothing.
- **Undo after a restore** is the calendar's toast, in the same `role="status"` region, waiting while the pointer or
  focus is on it. Focus goes to the rite now in the restored one's place, as after a burn on the calendar.

- **An Expurgator chooses words with checkboxes,** one per word, each labelled by its word, in a fieldset per part
  (Title, Text, Heretical Truth): a keyboard and a screen reader choose them as any checkbox, without a script. The box
  itself is invisible over its word; a chosen word is struck through on a dark red ground, so the choice isn't shown by
  colour alone, and the focused word gets the amber ring. Words are inline text, which WCAG 2.5.8 exempts from the
  target size. The rite then reads with emojis for the erased words, which a screen reader names ("skull"): the joke
  is that the words are lost.
- **Slow actions say they're under way** (#85): pressing Rewrite or Distil puts "The Machine Spirit is rewriting it…"
  (or distilling) in the `role="status"` message, which is announced, and disables the form's buttons until the answer
  comes. The status stays the same element when the rewrite's form replaces the old one, so its result is announced
  too. Rewrite is disabled while the note is empty, since it follows the note; the note's hint says so in words, for
  anyone without the script.
- **What a rewrite changed is said in each field's label** ("Text · rewritten"), so a screen reader hears it with the
  field's name, and shown by the field's amber edge; the colour is never the only sign. The first changed field takes
  focus, where the Scribe reads on.
- **A precept refused for breaking the writer's rules** (#86) comes back the same way: why is the `role="status"`
  message, and the field it was typed in keeps the words, marked `aria-invalid="true"` and described by that message
  (`aria-describedby`), so a screen reader on the field hears why. An edit's fold comes back open.
- **The name page** (#84) has one field, with a visible label and a hint tied to it, and takes focus, since choosing a
  name is all the page is for. The suggestions are links, each named by its name, under a heading, so a screen reader
  can list them; picking one only fills the field, so nothing is taken without pressing Take This Name. A name that
  can't be taken comes back in the field, marked `aria-invalid="true"` and described by the `role="status"` message
  that says why.

## Tags

The handoff has no tags. They use the site's tokens, on the dark background like every other control.

- **An archive row's link is its title, stretched over the row,** instead of the whole row being one link: a link can't
  hold the row's tag links. The row still takes a click anywhere and shows the focus ring around the whole row when its
  title has focus (`:has`). A screen reader now hears the title as the link's name, not the whole row.
- **Tag links are `#d9b874` (the link colour) on the dark page,** well above 4.5:1; on the Archive's parchment rows
  they're rubric red (see below). Their pill border is decoration; the text says what they are. Each is at least 28px
  tall, above the 24px WCAG 2.5.8 asks.
- **A row's tags start with a visually hidden "Filed under",** which the rite card and the Scriptorium say visibly, so
  a screen reader doesn't hear bare words after the counts.

## The Archive and search

The handoff's search and Archive update (`Search Options.dc.html`, 3a, 4a–4d): one parchment sheet of rows grouped by
month, with Today's masthead, margins and altar, and search as the Archive filtered by `?q=`.

- **Controls on the parchment get a red focus ring** (`#8f2a20`, 6.3:1 on parchment), the handoff's one exception to
  "every control sits on the dark background": the amber ring is too faint there. The rows, the search field, its
  clear button and the tag links all live on the sheet.
- **The sheet's quiet text is `#5c4a33` (ink-muted), not the handoff's brass-dim `#84662c`:** brass-dim is 4.05:1 on
  parchment, below the 4.5:1 that text this size needs; ink-muted is 6.4:1. That's the line under the heading, the
  dates, the counts and the field's placeholder.
- **The search field's edge is the full rubric red,** not the handoff's 45% rubric: that faint edge is under the 3:1
  WCAG 1.4.11 asks of a control's boundary, and the field's light fill alone barely differs from the parchment. The
  field is named "Search the Archive" (`aria-label`), and the form is a landmark (`role="search"`).
- **The plain Archive has no kicker:** the handoff's "THE ARCHIVE" over the heading "The Archive" says the same thing
  twice, and a screen reader would hear it twice. Search keeps its kicker ("Search the Archive"), which says something
  the heading (the words sought) doesn't.
- **A month's year is in Roman numerals for the eye** ("October MMXXVI"); a screen reader hears "October 2026" from
  hidden text, as the numerals are hidden from it. Each month in the index says "13 rites", not a bare "13".
- **Tags stay on the rows** (the handoff dropped them), as red pills on the parchment, each at least 28px tall, above the
  24px WCAG 2.5.8 asks. A row's link is still its title, stretched over the row, so the tag links can sit inside it.
- **What a rite was found by shows in its row:** the words are marked in the title and in a line around the first one
  in its text. A rite found by its Heretical Truth alone shows its opening words instead: the truth stays sealed until
  the reader breaks the seal on the rite's own page.
- **How many rites were found is in the page's title** ("“cache”: 3 rites"), as before: a search is a new page, so the
  title is what a screen reader reads first.
- **It works without the script:** the field is a plain form that sends `GET /archive?q=`, its clear button and "Clear
  search" are links to `/archive`, and "Load older rites" and "More results" are links. The old `/search?q=` address
  redirects there for good.
- **Narrowing to a subject** (#69): the chip under the field says "Filed under" visibly and is a link named "Remove
  filter: Caches", which keeps its visible label in its name (WCAG 2.5.3) and says what it does. It's 32px tall, above
  the 24px WCAG 2.5.8 asks, rubric red on the parchment like the field's edge, with the parchment's red focus ring.
  The subjects to narrow to are tag links on the dark background, under the sheet below 1280px and beside it above,
  each with its count ("Builds (2)"). The page's title names the subject ("“cache” in Caches: 1 rite"), so a screen
  reader hears what the results are before anything else.
- **The sheet wears Today's frame, as decoration only:** the double rule, the corners and the code in its margins
  ("NOTHING IS FORGOTTEN" in hex, "KEPT" in binary) are hidden from screen readers, and the corners and code show from
  700px, where the sheet's padding leaves them room clear of the rows. The seal hanging from the sheet at the
  Archive's end is hidden too: "The archive ends here" says the same in words.
- **Each row's number reads "NO. 214" to the eye and "Rite 214" to a screen reader,** which would say "no" for "NO.".
- **The margins say nothing the sheet says:** the Archive's left margin counts every rite kept and when they began, the
  right one indexes the months. Below 1280px there are no margins, and the count moves into the line under the heading;
  only one of the two is ever displayed, so a screen reader hears one.
- **The nav's search field is an ARIA combobox** (the handoff's 3a, after the WAI-ARIA Authoring Practices pattern):
  `role="combobox"`, `aria-autocomplete="list"`, `aria-controls` and `aria-expanded`, with the arrow keys moving
  `aria-activedescendant` through the suggestions, so focus stays in the field. Enter opens the chosen rite (or
  searches the Archive when none is chosen), Escape closes the field, clears it and returns focus to the icon, and a
  click or a tab elsewhere closes it. A polite live region says how many rites were found ("3 rites mention “cache”"),
  after the subjects when there are some ("1 subject. 3 rites mention “cache”"). The suggestions are one listbox in two
  groups, Subjects and the rites, each named by its heading, so the arrow keys go through both.
  Each suggestion is at least 44px tall. The chosen one has a red outline as well as its shade (the shade alone is
  1.18:1), and it scrolls into view when the drop-down is cut short (zoomed in, a keyboard on screen); keys an input
  method uses while composing a word are left to it. Without the script the icon is a link to the Archive's field.
- **The drop-down's count line is ink-muted, not brass-dim,** for the same 4.05:1 reason as the sheet's.
- **The Archive's results follow the typing without moving focus:** the field stays focused while the rows are
  swapped, and the line under the heading ("3 rites mention it") is announced in a live region. Escape and the clear
  buttons bring back the whole Archive with focus still in the field. Enter still loads the results as a page.

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
  burn, the first button of the rite that took its place, without scrolling away; the Undo is announced all the same.
  After a subject's chip, the chosen chip.
- **What the Scribe had open stays open:** the cards opened and the notes being written on other rites survive the new
  calendar, even while a subject hides them.
- **Reduced motion** (`prefers-reduced-motion`) turns off the slide and the glow; the rite acted on keeps its amber
  border, which isn't motion.
- **One action at a time:** while one is under way, `aria-busy` marks the page, and a second click is ignored until
  the first is done.
- **Summoned rites arrive without taking the Scribe away:** each one the script brings in is announced in the status
  message ("1 new rite arrived.") and marked like a rite acted on, while focus stays on the control it was on (found
  again by its rite and action) and the page doesn't scroll. It waits while the Scribe is typing or about to burn, so
  a field never loses its focus or its words under them. A full calendar's Summon is disabled, its `title` saying why,
  like Exalt and Humble at the ends; while a summon's rites are being written, it waits too, saying so.
- **Opening and folding every rite** is two icon buttons, each named for screen readers ("Open every rite", "Fold
  every rite") and in its `title`, rather than one toggle that changes its name. Without the script they stay hidden:
  every rite is open then.

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
- **The Scriptorium has the same altar, not new content.** The handoff drew only Today (and later the Archive); the
  Scriptorium puts its heading between two candles (from 1280px), with the glow behind it and the traces beyond, and
  nothing in the margins to read: there's nothing it would say there that it doesn't say already. The
  candles and traces are hidden from screen readers, and the flames stop under reduced motion.

## The Precepts (the Scriptorium redesign, step 2)

- **The numerals are decoration:** the list is an `<ol>`, so a screen reader already counts the precepts; the Roman
  numerals are `aria-hidden`. It says `role="list"`, since Safari drops the semantics of a list styled without markers.
- **The icon buttons name their precept,** with visually hidden text ("Delete: Prefer the real fix."), as the
  calendar's arrows do. Each is 44 by 44 pixels, and their tooltips (`title`) say what they do.
- **Pin is a toggle with one name,** "Pin, so distilling keeps it", and `aria-pressed` says whether it's pinned. The
  handoff changed the label to "Unpin" when pinned; a toggle whose name changes as well as its state reads twice
  over. The tooltip still says "Unpin…" to the pointer.
- **Editing doesn't hang on the click:** clicking the words is a shortcut for the pointer. The pencil is the way for
  a keyboard and a screen reader, and without the script it's a link to the page with the words open. Escape or
  Cancel closes the words unsaved, and focus goes back to the pencil.
- **Undo after a delete has no time limit:** the handoff's toast faded after seven seconds, but the page reloads after
  a delete, so a keyboard or a screen reader could lose it before reaching it (WCAG 2.2.1). It stays until another
  change ends it anyway, and comes right after the status message in the page's order, so it's reached first. The
  status message doesn't say the delete again.
- **The table scrolls in its own card** on a narrow screen, rather than the page. The card is a region named by the
  table's heading, with a tab stop, so a keyboard can scroll it where the browser doesn't make it focusable itself
  (Safari). Months without a judgment share one
  row, its "No judgments" spanning the figures' columns.

## The Record (the Scriptorium redesign, step 2)

- **The filters are links,** so they work without the script and a filtered Record can be shared. The chosen ones say
  `aria-current="true"` (the handoff's buttons said `aria-pressed`, which is for toggles, not places). The kinds start
  with "All", which the handoff left to clicking the chosen chip again: a link that leads back to every kind is easier
  to find than one that undoes itself.
- **A day's heading says what its number counts** ("Tuesday 20 October, 12 entries"), with visually hidden words,
  where the handoff showed the bare number.
- **The badges are decoration:** what was done is in the words after them, so their icons are `aria-hidden`, and
  their colours (who acted, or a burn) are said by the name and the verb too.
- **A folded run is a `<details>`,** so its Show all and Hide are the browser's own, keyboard and expanded state
  included, without the script the handoff's button needed. Its summary is at least 24 pixels high (WCAG 2.5.8).
- **The lists keep their semantics:** the day's entries and a run's rites are lists styled without markers, so they
  say `role="list"`, as the Precepts do, since Safari drops the semantics otherwise.

## Imperial dates

- **The day as we reckon it is a toggletip, not a hover-only tooltip.** The Imperial date ("0 805 026.M3") on the rite
  card and the Archive's "Since" is a button: the day shows while the pointer is on it (and on the day itself, so it
  can be read), while the button has focus, or once tapped, for a touch screen; Escape hides it until the pointer
  leaves or focus moves on (WCAG 1.4.13). Without the script, hover and focus still show it.
- **A screen reader hears both:** the button's name is the Imperial date it shows (WCAG 2.5.3), and the day describes
  it (`aria-describedby`), so it's said without opening anything. The date stays a `<time datetime>`.
- **Inside a link** (the Archive's rows, recent rites, More rites, the search's suggestions), where a button can't be,
  the day is visually hidden text after the Imperial date, which is hidden from screen readers, so they hear the day
  alone; the pointer gets it from a `title` (an Archive row's on its link, which covers the row). A touch screen
  reads it on the rite's own page, a tap away.
- **On the parchment the button's focus ring is red** (`#8f2a20`), as the sheet's controls are: amber is too faint there.

## The Catechism (the About page)

- **The headings are the questions alone.** Their numerals ("I.") are `aria-hidden`, so a screen reader's list of
  headings reads as the six questions. On a wide screen the index beside the sheet repeats them as links (an `aside`
  named "Questions"); below 1280px it isn't there, as the sheet already lists them in the same order.
- **The index and the answers' lists say `role="list"`,** since Safari drops the semantics of a list drawn without
  markers.
- **Commands scroll inside their block, and the block takes focus** (`tabindex="0"`, named "Commands"), so a keyboard
  can scroll it too; a wrapped command would read as two. The connector's address wraps instead, so it needs no
  scrolling. The `$` before each command is drawn, hidden from screen readers, and never selected or copied.
- **The code blocks are slate on the parchment,** so their focus ring is the amber one, not the sheet's red, which
  wouldn't show on slate. The commands' block draws it inside its edge, since the block clips what's outside.
- **Each copy button says what it copies** ("Copy address", "Copy commands"), and the toast says whether it worked.
- **Smooth scrolling** to a question, only without reduced motion.
