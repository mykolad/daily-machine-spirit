namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The pages' only script: breaking the seal, copying links and text, the toast, the countdown's tick, and voting.
/// Everything it touches is already in the server's HTML, and without it a rite's truth is still shown (a noscript style).
/// </summary>
public static class Script
{
    public const string Js = """
        (() => {
          const seal = document.querySelector('.seal');
          if (seal) {
            const panel = document.getElementById(seal.getAttribute('aria-controls'));
            seal.addEventListener('click', () => {
              const open = seal.getAttribute('aria-expanded') !== 'true';
              seal.setAttribute('aria-expanded', String(open));
              panel.hidden = !open;
              seal.querySelector('.seal-label').textContent = open ? 'Reseal the truth' : 'Break the seal';
              seal.querySelector('.seal-sub').textContent = open ? 'Hide the Heretical Truth' : 'Reveal the Heretical Truth';
            });
          }

          const toast = document.querySelector('.toast-region');
          let hideToast;
          const say = (message) => {
            toast.innerHTML = '';
            const pill = document.createElement('div');
            pill.className = 'toast';
            pill.textContent = message;
            toast.appendChild(pill);
            clearTimeout(hideToast);
            hideToast = setTimeout(() => { toast.innerHTML = ''; }, 2600);
          };
          // Whether the text reached the clipboard: the older fallback can fail quietly, and the toast must not lie.
          const copy = async (text) => {
            try {
              await navigator.clipboard.writeText(text);
              return true;
            } catch {
              const area = document.createElement('textarea');
              area.value = text;
              area.setAttribute('readonly', '');
              area.style.position = 'absolute';
              area.style.left = '-9999px';
              document.body.appendChild(area);
              area.select();
              let copied = false;
              try { copied = document.execCommand('copy'); } catch { copied = false; }
              area.remove();
              return copied;
            }
          };
          document.querySelectorAll('[data-copy]').forEach((button) => {
            button.addEventListener('click', async () => {
              const url = new URL(button.dataset.url, location.origin).href;
              const isLink = button.dataset.copy === 'link';
              const copied = await copy(isLink ? url : `${button.dataset.text}\n${url}`);
              say(!copied ? 'Couldn’t copy. Your browser blocked the clipboard.'
                : isLink ? 'Link copied to clipboard' : 'Text copied to clipboard');
            });
          });

          // Blessed or Heresy, one vote per GitHub account per rite. The page is the same for everyone (and cached), so
          // the script asks who's reading: a signed-in reader sees their own vote pressed and can switch or take it back;
          // anyone else is offered the sign-in. Without the script, the counts simply show.
          const reactions = document.querySelector('.reactions');
          if (reactions) {
            // The anonymous reactions kept each browser's own in storage; nothing reads it any more.
            try { localStorage.removeItem('dms-reactions-v1'); } catch { }
            const rite = reactions.dataset.rite;
            const buttons = [...reactions.querySelectorAll('[data-reaction]')];
            const statuses = [...document.querySelectorAll('.vote-status')];
            const signInLink = document.querySelector('.vote-status[data-signed-in=false] a');
            const counts = () => Object.fromEntries(buttons.map((b) => [b.dataset.reaction, Number(b.querySelector('.count').textContent.replace(/,/g, ''))]));
            const show = (vote, shown) => buttons.forEach((b) => {
              b.setAttribute('aria-pressed', String(b.dataset.reaction === vote));
              b.querySelector('.count').textContent = shown[b.dataset.reaction].toLocaleString('en');
            });
            let reader = { available: false, signedIn: false, vote: null };
            const showReader = () => statuses.forEach((s) => { s.hidden = !reader.available || s.dataset.signedIn !== String(reader.signedIn); });
            // Until this answers, a click waits for it rather than guessing who's reading.
            const known = (async () => {
              try {
                const me = await (await fetch('/api/me')).json();
                reader = { available: me.signInAvailable === true, signedIn: me.signedIn === true, vote: null };
                if (reader.signedIn) {
                  const votes = await fetch(`/api/votes?rites=${rite}`);
                  // Signed out meanwhile (another tab, or the sign-in ran out): offer the sign-in again.
                  if (votes.status === 401) reader.signedIn = false;
                  else if (votes.ok) reader.vote = (await votes.json())[rite] ?? null;
                }
              } catch { }
              showReader();
              show(reader.vote, counts());
            })();
            buttons.forEach((button) => button.addEventListener('click', async () => {
              await known;
              if (!reader.available) { say('Voting isn’t open yet.'); return; }
              if (!reader.signedIn) { location.assign(signInLink.href); return; }
              if (reactions.getAttribute('aria-busy') === 'true') return;
              const previous = reader.vote;
              const vote = previous === button.dataset.reaction ? null : button.dataset.reaction;
              const before = counts();
              const hoped = { ...before };
              if (previous) hoped[previous] = Math.max(0, hoped[previous] - 1);
              if (vote) hoped[vote] += 1;
              show(vote, hoped);
              reactions.setAttribute('aria-busy', 'true');
              try {
                const response = await fetch(`/api/rites/${rite}/vote`, {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ vote }),
                });
                if (response.status === 401) {
                  // The sign-in ran out (after 30 days) or was signed out in another tab.
                  reader.signedIn = false;
                  showReader();
                  show(previous, before);
                  say('Sign in with GitHub again to vote.');
                  return;
                }
                if (!response.ok) throw new Error(String(response.status));
                // Saved: from here on the vote counts, even if the answer can't be read.
                reader.vote = vote;
                show(vote, await response.json().catch(() => hoped));
              } catch {
                show(previous, before);
                say('Your vote wasn’t recorded. Try again.');
              } finally {
                reactions.removeAttribute('aria-busy');
              }
            }));
          }

          // The archive loads older rites as the reader nears the end, by fetching the next page the "Load older rites"
          // link points to and taking its rows. The link stays: a keyboard follows it here too, and focus moves to the
          // first new rite. Without the script, it's an ordinary link to that page.
          const rows = document.querySelector('.rows');
          const more = document.querySelector('.archive-more');
          if (rows && more) {
            let loading = false;
            // A keyboard user who follows the link while scrolling already started a load still gets focus moved.
            let focusWhenLoaded = false;
            // Announcements go to a live region that's on the page from the start: one inserted already filled in
            // isn't reliably read out.
            const announcer = document.createElement('div');
            announcer.className = 'sr-only';
            announcer.setAttribute('role', 'status');
            rows.after(announcer);
            const observer = new IntersectionObserver((entries) => {
              const link = more.querySelector('a');
              if (link && entries.some((entry) => entry.isIntersecting)) loadOlder(link, false);
            }, { rootMargin: '200px' });
            const loadOlder = async (link, moveFocus) => {
              focusWhenLoaded ||= moveFocus;
              if (loading) return;
              loading = true;
              // Not watched while loading: the placeholders push the link away, and taking them out after a failure
              // would bring it back into view and start the same failing load again.
              observer.unobserve(more);
              const placeholders = document.createElement('div');
              placeholders.className = 'archive-loading';
              placeholders.setAttribute('aria-hidden', 'true');
              placeholders.innerHTML = '<div class="skeleton-row"></div><div class="skeleton-row"></div>';
              more.before(placeholders);
              announcer.textContent = 'Loading older rites…';
              try {
                const response = await fetch(link.href);
                if (!response.ok) throw new Error(String(response.status));
                const page = new DOMParser().parseFromString(await response.text(), 'text/html');
                const added = [...page.querySelectorAll('.rows > li')].map((row) => rows.appendChild(document.importNode(row, true)));
                const next = page.querySelector('.archive-more');
                // A keyboard user may have tabbed onto the link while scrolling loaded: replacing it mustn't drop focus.
                focusWhenLoaded ||= more.contains(document.activeElement);
                more.replaceChildren(...(next ? [...next.childNodes].map((node) => document.importNode(node, true)) : []));
                announcer.textContent = `${added.length} older ${added.length === 1 ? 'rite' : 'rites'} loaded.`;
                if (focusWhenLoaded && added.length > 0) added[0].querySelector('a').focus();
                // Watched again, so a short page that still shows the end loads the next one too. Only after a load
                // that worked: after a failure the link stays for the reader to try again, not a loop.
                observer.observe(more);
              } catch {
                announcer.textContent = '';
                say('Older rites couldn’t be loaded. Try again.');
              } finally {
                placeholders.remove();
                loading = false;
                focusWhenLoaded = false;
              }
            };
            more.addEventListener('click', (event) => {
              const link = event.target.closest('a');
              // Ctrl, Cmd, Shift or a middle click: the browser opens the page in a new tab or window, as it should.
              if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
              event.preventDefault();
              loadOlder(link, true);
            });
            observer.observe(more);
          }

          const countdowns = document.querySelectorAll('[data-countdown]');
          const tick = () => {
            const now = new Date();
            const next = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1);
            const minutes = Math.ceil((next - now.getTime()) / 60000);
            const text = minutes >= 60 ? `${Math.floor(minutes / 60)}h ${minutes % 60}m` : `${minutes}m`;
            countdowns.forEach((element) => { element.textContent = text; });
          };
          if (countdowns.length > 0) setInterval(tick, 30000);
        })();
        """;
}
