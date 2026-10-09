namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The pages' only script: breaking the seal, copying links and text, the toast, and the countdown's tick. Everything it
/// touches is already in the server's HTML, and without it a rite's truth is still shown (a noscript style).
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

          // Blessed or Heresy, one per rite: this browser remembers its own reaction (and nothing else) and sends it
          // back as "previous", so the server can move it without knowing who reacted.
          const reactions = document.querySelector('.reactions');
          if (reactions) {
            const key = 'dms-reactions-v1';
            // Read once, then the page's own copy is the truth: where storage is blocked or full, saving fails quietly but
            // the page still remembers, so one reaction per rite holds while it's open.
            let memory = (() => { try { return JSON.parse(localStorage.getItem(key)) || {}; } catch { return {}; } })();
            const remembered = () => ({ ...memory });
            // Changes only this rite's entry, on top of whatever is stored right now: another tab may have saved its own
            // reaction while this one's request was on its way.
            const remember = (number, reaction) => {
              try { memory = { ...memory, ...JSON.parse(localStorage.getItem(key)) }; } catch { }
              memory = { ...memory };
              if (reaction) memory[number] = reaction; else delete memory[number];
              try { localStorage.setItem(key, JSON.stringify(memory)); } catch { }
            };
            const rite = reactions.dataset.rite;
            const buttons = [...reactions.querySelectorAll('[data-reaction]')];
            const counts = () => Object.fromEntries(buttons.map((b) => [b.dataset.reaction, Number(b.querySelector('.count').textContent.replace(/,/g, ''))]));
            const show = (mine, shown) => buttons.forEach((b) => {
              b.setAttribute('aria-pressed', String(b.dataset.reaction === mine));
              b.querySelector('.count').textContent = shown[b.dataset.reaction].toLocaleString('en');
            });
            show(remembered()[rite] ?? null, counts());
            // Another tab of this site reacted: take its memory too, so this tab doesn't count the same visitor twice.
            window.addEventListener('storage', (event) => {
              if (event.key !== key) return;
              try { memory = JSON.parse(event.newValue) || {}; } catch { return; }
              show(memory[rite] ?? null, counts());
            });
            buttons.forEach((button) => button.addEventListener('click', async () => {
              if (reactions.getAttribute('aria-busy') === 'true') return;
              const previous = remembered()[rite] ?? null;
              const reaction = previous === button.dataset.reaction ? null : button.dataset.reaction;
              const before = counts();
              const hoped = { ...before };
              if (previous) hoped[previous] = Math.max(0, hoped[previous] - 1);
              if (reaction) hoped[reaction] += 1;
              show(reaction, hoped);
              reactions.setAttribute('aria-busy', 'true');
              try {
                const response = await fetch(`/api/rites/${rite}/reaction`, {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ reaction, previous }),
                });
                if (!response.ok) throw new Error(String(response.status));
                // Saved: from here on the reaction counts, even if the answer can't be read.
                remember(rite, reaction);
                show(reaction, await response.json().catch(() => hoped));
              } catch {
                show(previous, before);
                say('Your reaction wasn’t recorded. Try again.');
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
            const observer = new IntersectionObserver((entries) => {
              const link = more.querySelector('a');
              if (link && entries.some((entry) => entry.isIntersecting)) loadOlder(link, false);
            }, { rootMargin: '200px' });
            const loadOlder = async (link, moveFocus) => {
              if (loading) return;
              loading = true;
              const status = document.createElement('div');
              status.className = 'archive-loading';
              status.setAttribute('role', 'status');
              status.innerHTML = '<span class="sr-only">Loading older rites…</span><div class="skeleton-row" aria-hidden="true"></div><div class="skeleton-row" aria-hidden="true"></div>';
              more.before(status);
              try {
                const response = await fetch(link.href);
                if (!response.ok) throw new Error(String(response.status));
                const page = new DOMParser().parseFromString(await response.text(), 'text/html');
                const added = [...page.querySelectorAll('.rows > li')].map((row) => rows.appendChild(document.importNode(row, true)));
                const next = page.querySelector('.archive-more');
                more.replaceChildren(...(next ? [...next.childNodes].map((node) => document.importNode(node, true)) : []));
                if (moveFocus && added.length > 0) added[0].querySelector('a').focus();
              } catch {
                say('Older rites couldn’t be loaded. Try again.');
              } finally {
                status.remove();
                loading = false;
                // Watched afresh, so a short page that still shows the end loads the next one too.
                observer.unobserve(more);
                observer.observe(more);
              }
            };
            more.addEventListener('click', (event) => {
              const link = event.target.closest('a');
              if (!link) return;
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
