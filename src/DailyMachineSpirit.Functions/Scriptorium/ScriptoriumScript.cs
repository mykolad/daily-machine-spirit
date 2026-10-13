namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Scriptorium's only script, on top of its forms: an action is posted in the background and the new calendar takes
/// the old one's place, every rite that moved sliding from where it was, so a Scribe sees what changed. Without it (or
/// when anything goes wrong) the forms work as they are: post, redirect, a new page.
/// </summary>
public static class ScriptoriumScript
{
    public const string Js = """
        (() => {
          const reduced = matchMedia('(prefers-reduced-motion: reduce)');
          let busy = false;

          // Where each rite is, by its id (rite-<draft>), measured from the top of the calendar's section: the status
          // message above it appearing or changing length moves every rite down, which isn't a move worth showing.
          const positions = () => {
            const from = document.querySelector('#main section').getBoundingClientRect().top;
            return new Map([...document.querySelectorAll('article.rite[id]')]
              .map((card) => [card.id, card.getBoundingClientRect().top - from]));
          };
          // What a button does: its formaction, or its form's action (Summon, handing the order back to the Augury).
          const actionOf = (button) =>
            button?.getAttribute('formaction') ?? button?.form?.getAttribute('action') ?? null;

          document.addEventListener('submit', async (event) => {
            const form = event.target;
            const button = event.submitter;
            if (!(form instanceof HTMLFormElement) || form.method !== 'post') return;
            event.preventDefault();
            // One action at a time: a second click while the first is under way would act on a calendar it hasn't seen.
            if (busy) return;
            busy = true;
            const main = document.getElementById('main');
            main.setAttribute('aria-busy', 'true');
            const pressed = actionOf(button);
            const before = positions();
            // The rite that takes a burned one's place in the list (or the one above, when it was the last), for focus.
            const row = form.closest('li');
            const neighbour = (row?.nextElementSibling ?? row?.previousElementSibling)?.querySelector('article')?.id;
            try {
              const response = await fetch(button?.formAction || form.action, {
                method: 'POST',
                body: new URLSearchParams(new FormData(form, button)),
              });
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

              const acted = main.querySelector('.acted');
              // A burn can be refused (a note too long, a calendar changed meanwhile), so it counts only when the rite
              // is in the Ashes now.
              const burned = (pressed?.endsWith('/burn') ?? false) && (acted?.classList.contains('ash') ?? false);
              history.replaceState(null, '', response.url + (acted && !burned ? `#${acted.id}` : ''));

              // Each rite that moved starts where it was and slides to its new place (First, Last, Invert, Play).
              let arrived = Promise.resolve();
              if (!reduced.matches) {
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
              // the rite that took the burned one's place, so the page doesn't jump away from it; the message (with
              // its link to restore the rite) is announced all the same.
              const burnedNeighbour = burned && neighbour ? document.getElementById(neighbour) : null;
              const again = burned ? burnedNeighbour?.querySelector('button')
                : [...main.querySelectorAll('button')].find((other) => actionOf(other) === pressed)
                  ?? acted?.querySelector('button');
              status.tabIndex = -1;
              (again ?? status).focus({ preventScroll: true });
              // Once the rite has arrived, it's brought into view. Not awaited: a hidden tab pauses the slide, and the
              // page mustn't stay busy until it's shown again.
              const shown = burned ? burnedNeighbour : again && acted;
              arrived.then(() => (shown || status)
                .scrollIntoView({ block: 'nearest', behavior: reduced.matches ? 'auto' : 'smooth' }));
            } catch {
              // Whether the action happened isn't known (Cloudflare Access may want a new sign-in, or the network
              // failed): the page as it is now says.
              location.assign('/scriptorium');
            } finally {
              main.removeAttribute('aria-busy');
              busy = false;
            }
          });
        })();
        """;
}
