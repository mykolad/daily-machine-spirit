namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The calendar's script, on top of its forms and links. It folds every rite but the next to publish into its row, and
/// asks why before a burn, then offers to undo it. An action is posted in the background and the new calendar takes the
/// old one's place, every rite that moved sliding from where it was, so a Scribe sees what changed; a subject's chip
/// loads the calendar narrowed to it the same way. Without it (or when anything goes wrong) the forms and links work as
/// they are: post, redirect, a new page, every rite open.
/// </summary>
public static class ScriptoriumScript
{
    public const string Js = """
        (() => {
          const reduced = matchMedia('(prefers-reduced-motion: reduce)');
          const main = document.getElementById('main');
          const toasts = document.getElementById('toasts');
          const openKey = 'dms-scriptorium-open';
          let busy = false;
          let toastTimer = 0;
          // The note fold a Scribe just opened, whose field takes the focus; not one reopened to keep its note.
          let openingNote = null;

          // The rites a Scribe opened, by their ids (rite-<draft>), kept for the session; the next to publish is always
          // open, and so is the rite an address points at (back from amending it).
          const opened = (() => {
            try { return new Set(JSON.parse(sessionStorage.getItem(openKey) ?? '[]')); } catch { return new Set(); }
          })();
          const keepOpened = () => {
            try { sessionStorage.setItem(openKey, JSON.stringify([...opened])); } catch { /* the session forgets */ }
          };
          const cards = () => [...main.querySelectorAll('article.row')];
          const titleOf = (card) => card.querySelector('h4')?.textContent ?? '';
          const fold = (card, open) => {
            const body = card.querySelector('.body');
            const toggle = card.querySelector('.expand');
            if (!body || !toggle) return;
            body.hidden = !open;
            toggle.hidden = false;
            toggle.setAttribute('aria-expanded', String(open));
            toggle.setAttribute('aria-label', `${open ? 'Collapse' : 'Expand'} ${titleOf(card)}`);
          };
          const foldAll = () => {
            const next = main.querySelector('article.row.next');
            if (next) opened.add(next.id);
            for (const card of cards()) fold(card, opened.has(card.id));
          };
          if (main.hasAttribute('data-open-all')) cards().forEach((card) => opened.add(card.id));
          if (location.hash.startsWith('#rite-')) opened.add(location.hash.slice(1));
          main.setAttribute('data-folding', '');
          foldAll();

          // Consign to the Flames asks why first, in place of the note and the buttons.
          const showPanel = (card, shown) => {
            const panel = card.querySelector('.burn-panel');
            if (!panel) return;
            panel.hidden = !shown;
            card.querySelector('.note-fold').hidden = shown;
            card.querySelector('.buttons').hidden = shown;
            if (shown) {
              panel.querySelector('.reason')?.focus();
            } else {
              panel.querySelectorAll('.reason').forEach((reason) => reason.setAttribute('aria-pressed', 'false'));
              panel.querySelector('.reason-text').value = '';
              card.querySelector('.buttons .burn')?.focus();
            }
          };

          // After a burn, a few seconds to undo it: never gone while the pointer or focus is on it.
          const hideToast = () => {
            clearTimeout(toastTimer);
            toasts?.replaceChildren();
          };
          const showToast = ({ id, title, place }) => {
            const template = document.getElementById('undo-toast');
            if (!toasts || !(template instanceof HTMLTemplateElement)) return;
            const toast = template.content.firstElementChild.cloneNode(true);
            toast.querySelector('.toast-text').textContent = `“${title}” was consigned to the flames.`;
            const form = toast.querySelector('form');
            form.setAttribute('action', `/scriptorium/drafts/${id}/restore`);
            const input = form.querySelector('[name=place]');
            if (place) input.value = place; else input.remove();
            const later = () => {
              clearTimeout(toastTimer);
              toastTimer = setTimeout(() => (toast.matches(':hover, :focus-within') ? later() : hideToast()), 7000);
            };
            toast.addEventListener('mouseleave', later);
            toast.addEventListener('focusout', later);
            toasts.replaceChildren(toast);
            later();
          };

          // Where each rite is, by its id, measured from the top of the calendar's section: the status message above it
          // appearing or changing length moves every rite down, which isn't a move worth showing.
          const positions = () => {
            const from = document.querySelector('#main section').getBoundingClientRect().top;
            return new Map(cards().map((card) => [card.id, card.getBoundingClientRect().top - from]));
          };
          // What a button does: its formaction, or its form's action (Summon, handing the order back to the Augury, Undo).
          const actionOf = (button) =>
            button?.getAttribute('formaction') ?? button?.form?.getAttribute('action') ?? null;
          const firstButton = (card) => card?.querySelector('button:not(:disabled):not([hidden])') ?? null;

          // The notes being written on other rites survive the new calendar; the acted-on rite's went with its action.
          const notesNow = (acting) => new Map(cards()
            .filter((card) => card !== acting)
            .map((card) => [card.id, card.querySelector('textarea[name=note]')?.value ?? ''])
            .filter(([, note]) => note));

          // Loads the calendar from the address (an action's answer, or a subject's) in place of the one shown.
          const load = async (url, init, { pressed, card, burning }) => {
            busy = true;
            main.setAttribute('aria-busy', 'true');
            const before = positions();
            // Where a burned rite stood among the rites shown, for focus. Not its neighbour: the Augury orders what's
            // left anew, so the rite after it before the burn needn't be the one in its place after.
            const place = card ? cards().indexOf(card) : -1;
            const notes = notesNow(card);
            try {
              const response = await fetch(url, init);
              const page = new DOMParser().parseFromString(await response.text(), 'text/html');
              const fresh = page.getElementById('main');
              const freshStatus = fresh?.querySelector('.status');
              const status = main.querySelector('.status');
              if (!response.ok || !freshStatus || !status) throw new Error(String(response.status));

              // Everything but the status message is replaced; the message stays the same element, given the new text,
              // so a screen reader announces it as it would after a reload.
              const parts = [...fresh.children].map((part) => document.importNode(part, true));
              const at = parts.findIndex((part) => part.classList.contains('status'));
              [...main.children].filter((part) => part !== status).forEach((part) => part.remove());
              status.before(...parts.slice(0, at));
              status.after(...parts.slice(at + 1));
              status.innerHTML = freshStatus.innerHTML;
              document.title = page.title;
              // The tabs count what the action changed (a burn adds to the Ashes), and sit outside the main part.
              const tabs = page.querySelector('nav.tabs');
              if (tabs) document.querySelector('nav.tabs')?.replaceWith(document.importNode(tabs, true));

              foldAll();
              for (const [id, note] of notes) {
                const field = document.getElementById(id)?.querySelector('textarea[name=note]');
                if (!field) continue;
                field.value = note;
                field.closest('details').open = true;
              }

              const acted = main.querySelector('.acted');
              // A burn can be refused (a note too long, a calendar changed meanwhile), which leaves the rite marked in the
              // calendar, so it counts only when the rite has left it for the Ashes.
              const burned = (pressed?.endsWith('/burn') ?? false) && !acted;
              history.replaceState(null, '', response.url + (acted && !burned ? `#${acted.id}` : ''));
              if (burned && burning) {
                // The Undo says it, in place of the message's own words and its way back from the Ashes.
                status.querySelectorAll('[data-undoable]').forEach((said) => said.remove());
                showToast(burning);
              }

              // Each rite that moved starts where it was and slides to its new place (First, Last, Invert, Play).
              let arrived = Promise.resolve();
              if (pressed && !reduced.matches) {
                for (const [id, top] of positions()) {
                  const was = before.get(id);
                  if (was === undefined || Math.abs(was - top) < 1) continue;
                  const slide = document.getElementById(id).animate(
                    [{ transform: `translateY(${was - top}px)` }, { transform: 'none' }],
                    { duration: 500, easing: 'cubic-bezier(.2,.8,.2,1)' });
                  if (id === acted?.id) arrived = slide.finished.catch(() => {});
                }
              }

              // Focus stays where the Scribe is working: the same button on the rite, else its first one. After a burn,
              // the rite now in the burned one's place (the last, when it was the last), so the page doesn't jump away
              // from it. A subject's chip keeps it on the chips.
              const rows = cards();
              const neighbour = burned && place >= 0 ? rows[place] ?? rows.at(-1) ?? null : null;
              const again = !pressed ? main.querySelector('.subjects [aria-current]')
                : burned ? firstButton(neighbour)
                : [...main.querySelectorAll('button')].find((other) => actionOf(other) === pressed) ?? firstButton(acted);
              status.tabIndex = -1;
              (again ?? status).focus({ preventScroll: true });
              // Once the rite has arrived, it's brought into view. Not awaited: a hidden tab pauses the slide, and the
              // page mustn't stay busy until it's shown again.
              const shownCard = burned ? neighbour : again && acted;
              if (pressed) {
                arrived.then(() => {
                  // A rite taller than the window can't be shown whole, and 'nearest' leaves it where it is: then its
                  // focused button is shown instead.
                  const shown = !shownCard ? status
                    : shownCard.getBoundingClientRect().height <= innerHeight ? shownCard : again ?? status;
                  shown.scrollIntoView({ block: 'nearest', behavior: reduced.matches ? 'auto' : 'smooth' });
                });
              }
            } catch {
              // Whether the action happened isn't known (Cloudflare Access may want a new sign-in, or the network
              // failed): the page as it is now says.
              location.assign(pressed ? '/scriptorium' : url);
            } finally {
              main.removeAttribute('aria-busy');
              busy = false;
            }
          };

          document.addEventListener('submit', (event) => {
            const form = event.target;
            const button = event.submitter;
            if (!(form instanceof HTMLFormElement) || form.method !== 'post') return;
            event.preventDefault();
            // One action at a time: a second click while the first is under way would act on a calendar it hasn't seen.
            if (busy) return;
            const pressed = actionOf(button) ?? form.getAttribute('action');
            const card = form.closest('article.row');
            const confirming = button?.hasAttribute('data-confirm') ?? false;
            if (card && pressed?.endsWith('/burn') && !confirming) {
              showPanel(card, true);
              return;
            }
            const body = new URLSearchParams(new FormData(form, button));
            // The reason in the Scribe's own words, else the one picked; none is needed.
            let burning = null;
            if (card && confirming) {
              const panel = card.querySelector('.burn-panel');
              const picked = panel.querySelector('.reason[aria-pressed=true]')?.textContent ?? '';
              body.set('note', panel.querySelector('.reason-text').value.trim() || picked);
              burning = { id: card.id.slice('rite-'.length), title: titleOf(card), place: card.classList.contains('scribes') ? card.dataset.place : '' };
            }
            hideToast();
            // Not the button's formAction: without the attribute, that's the page's own address (Summon's and the
            // Augury's buttons have none).
            load(pressed, { method: 'POST', body }, { pressed, card, burning });
          });

          main.addEventListener('click', (event) => {
            const target = event.target;
            if (!(target instanceof Element)) return;
            const chip = target.closest('.subjects a');
            if (chip) {
              if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
              event.preventDefault();
              if (!busy) load(chip.href, {}, { pressed: null, card: null, burning: null });
              return;
            }
            const card = target.closest('article.row');
            if (!card) return;
            if (target.closest('.note-fold summary')) {
              openingNote = target.closest('details');
              return;
            }
            const reason = target.closest('.reason');
            if (reason) {
              const on = reason.getAttribute('aria-pressed') !== 'true';
              card.querySelectorAll('.reason').forEach((other) => other.setAttribute('aria-pressed', String(other === reason && on)));
              return;
            }
            if (target.closest('.cancel')) {
              showPanel(card, false);
              return;
            }
            // The chevron, or anywhere on the row that isn't a control of its own (or words being selected).
            const onRow = target.closest('.row-head') && !target.closest('button, a') && !getSelection()?.toString();
            if (!target.closest('.expand') && !onRow) return;
            const open = card.querySelector('.body').hidden;
            if (open) opened.add(card.id); else opened.delete(card.id);
            keepOpened();
            fold(card, open);
          });

          main.addEventListener('keydown', (event) => {
            const panel = event.target instanceof Element ? event.target.closest('.burn-panel') : null;
            if (event.key === 'Escape' && panel) showPanel(panel.closest('article.row'), false);
          });

          // Opening the note's fold goes straight to its field: the fold's own button gives way to it.
          main.addEventListener('toggle', (event) => {
            const note = event.target;
            if (note === openingNote && note.open) {
              openingNote = null;
              note.querySelector('textarea')?.focus();
            }
          }, true);
        })();
        """;
}
