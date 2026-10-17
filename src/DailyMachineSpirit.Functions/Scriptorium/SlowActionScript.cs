namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The amending page's and the Precepts' script (#85): their slow actions, a rewrite and a distillation, take seconds,
/// so a pressed button says what's happening and can't be pressed again. A rewrite is posted in the background and its
/// form takes the old one's place, so the address stays the amending page's and a reload never resends it. Asking for
/// a rewrite needs a note, so its button waits for one. Without the script (or when anything goes wrong) the forms
/// work as they are.
/// </summary>
public static class SlowActionScript
{
    public const string Js = """
        (() => {
          const status = document.querySelector('main .status');
          const say = (text) => { if (status) status.textContent = text; };

          // Rewriting follows the note, so without one there's nothing to ask; saving needs none.
          const noteNeeded = () => {
            const note = document.getElementById('amend-note');
            document.querySelectorAll('[data-needs-note]').forEach((button) => {
              button.disabled = !note || note.value.trim() === '';
            });
          };
          document.addEventListener('input', (event) => {
            if (event.target.id === 'amend-note') noteNeeded();
          });
          noteNeeded();

          document.addEventListener('submit', async (event) => {
            const button = event.submitter;
            const form = event.target;
            if (!button?.dataset.slow || !(form instanceof HTMLFormElement)) return;
            if (form.dataset.busy) {
              event.preventDefault();
              return;
            }
            form.dataset.busy = 'true';
            say(button.dataset.slow);
            // The button says it too, where the Scribe is looking: the status message may be scrolled out of sight.
            const label = button.lastChild;
            const idle = label?.textContent;
            if (label && button.dataset.busyLabel) label.textContent = button.dataset.busyLabel;
            const buttons = [...form.querySelectorAll('button')];
            // A distillation loads its page as usual; the buttons are disabled once the form has been sent.
            if (!button.hasAttribute('data-in-place')) {
              setTimeout(() => buttons.forEach((each) => { each.disabled = true; }));
              return;
            }

            event.preventDefault();
            buttons.forEach((each) => { each.disabled = true; });
            try {
              const response = await fetch(button.formAction, {
                method: 'POST',
                body: new URLSearchParams(new FormData(form, button)),
              });
              // Sent elsewhere (the draft was burned or published meanwhile): that page is loaded instead.
              if (response.redirected) {
                location.assign(response.url);
                return;
              }
              const page = new DOMParser().parseFromString(await response.text(), 'text/html');
              const fresh = page.querySelector('form.amend');
              if (!fresh) throw new Error(String(response.status));
              form.replaceWith(document.importNode(fresh, true));
              say(page.querySelector('main .status')?.textContent ?? '');
              noteNeeded();
              // The first field the rewrite changed, else the first field, as the page itself would focus it.
              (document.querySelector('form.amend [autofocus]') ?? document.getElementById('amend-title'))?.focus();
            } catch {
              delete form.dataset.busy;
              if (label) label.textContent = idle;
              buttons.forEach((each) => { each.disabled = false; });
              noteNeeded();
              say('The Machine Spirit couldn’t be reached. Try again in a moment.');
            }
          });
        })();
        """;
}
