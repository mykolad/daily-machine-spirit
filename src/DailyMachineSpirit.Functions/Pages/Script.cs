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

          // The anonymous reactions kept each browser's own in storage; nothing reads it any more, so every page clears it.
          try { localStorage.removeItem('dms-reactions-v1'); } catch { }

          // Blessed or Heresy, one vote per account per rite. The page is the same for everyone (and cached), so the
          // script asks who's reading: a signed-in reader sees their own vote pressed and can switch or take it back;
          // anyone else is offered the sign-in. Without the script, the counts simply show.
          const reactions = document.querySelector('.reactions');
          if (reactions) {
            const rite = reactions.dataset.rite;
            const buttons = [...reactions.querySelectorAll('[data-reaction]')];
            const statuses = [...document.querySelectorAll('.vote-status')];
            const signInLink = document.querySelector('.vote-status[data-signed-in=false] a');
            const counts = () => Object.fromEntries(buttons.map((b) => [b.dataset.reaction, Number(b.querySelector('.count').textContent.replace(/,/g, ''))]));
            const show = (vote, shown) => buttons.forEach((b) => {
              b.setAttribute('aria-pressed', String(b.dataset.reaction === vote));
              b.querySelector('.count').textContent = shown[b.dataset.reaction].toLocaleString('en');
            });
            // Cloudflare keeps the page until votes purge it, up to a minute after them (batched), and the reader's browser
            // keeps its copy a minute too, so a refresh may show the page from before their own vote. So the browser keeps
            // the counts each vote answered, with when the server counted them, and shows them over a page whose counts were
            // read earlier. Kept for two hours, past the hour Cloudflare keeps a page at most, timed by the browser's own
            // clock (rememberedAt): the server's countedAt only orders counts, as the two clocks may differ by hours.
            const votesKey = 'dms-votes-v1';
            // Every read drops what's past its two hours, so nothing stays for a reader who doesn't vote again.
            const recalled = () => {
              try {
                const stored = JSON.parse(localStorage.getItem(votesKey) ?? '{}');
                const entries = Object.entries(stored && typeof stored === 'object' ? stored : {});
                const fresh = entries.filter(([, entry]) => {
                  const age = entry ? Date.now() - entry.rememberedAt : NaN;
                  return age >= 0 && age < 7200000;
                });
                if (fresh.length === 0) localStorage.removeItem(votesKey);
                else if (fresh.length < entries.length) localStorage.setItem(votesKey, JSON.stringify(Object.fromEntries(fresh)));
                return Object.fromEntries(fresh);
              } catch { return {}; }
            };
            // Never throws: the vote is saved by now, whatever storage holds.
            const remember = (answer) => {
              if (typeof answer.countedAt !== 'number') return;
              try {
                const all = recalled();
                // Another tab's vote may have been counted later and answered sooner: the later count stays.
                if (all[rite] && all[rite].countedAt > answer.countedAt) return;
                all[rite] = { blessed: answer.blessed, heresy: answer.heresy, countedAt: answer.countedAt, rememberedAt: Date.now() };
                localStorage.setItem(votesKey, JSON.stringify(all));
              } catch { }
            };
            const kept = recalled()[rite];
            if (kept && typeof kept.blessed === 'number' && typeof kept.heresy === 'number' && kept.countedAt >= Number(reactions.dataset.countedAt)) show(null, kept);
            let reader = { available: false, signedIn: false, vote: null, voteLoaded: false };
            const showReader = () => statuses.forEach((s) => { s.hidden = !reader.available || s.dataset.signedIn !== String(reader.signedIn); });
            // The signed-in reader's own vote. Until it has loaded, the page doesn't know whether a click sets a vote or
            // takes one back, so voting waits for it.
            const loadVote = async () => {
              try {
                const votes = await fetch(`/api/votes?rites=${rite}`);
                // Signed out meanwhile (another tab, or the sign-in ran out): offer the sign-in again.
                if (votes.status === 401) reader.signedIn = false;
                else if (votes.ok) reader = { ...reader, vote: (await votes.json())[rite] ?? null, voteLoaded: true };
              } catch { }
            };
            // Until this answers, a click waits for it rather than guessing who's reading.
            const known = (async () => {
              try {
                const me = await (await fetch('/api/me')).json();
                reader = { available: me.signInAvailable === true, signedIn: me.signedIn === true, vote: null, voteLoaded: false };
                if (reader.signedIn) await loadVote();
              } catch { }
              showReader();
              show(reader.vote, counts());
            })();
            buttons.forEach((button) => button.addEventListener('click', async () => {
              await known;
              if (!reader.available) { say('Voting isn’t open yet.'); return; }
              if (!reader.signedIn) { location.assign(signInLink.href); return; }
              if (reactions.getAttribute('aria-busy') === 'true') return;
              if (!reader.voteLoaded) {
                // The vote didn't load with the page. Load it now, and show it rather than act on it: the reader clicked
                // without seeing it, so the click could mean the opposite.
                reactions.setAttribute('aria-busy', 'true');
                await loadVote();
                reactions.removeAttribute('aria-busy');
                showReader();
                if (!reader.signedIn) { say('Sign in with GitHub again to vote.'); return; }
                if (!reader.voteLoaded) { say('Your vote couldn’t be loaded. Try again.'); return; }
                show(reader.vote, counts());
                say(reader.vote ? 'Your vote is shown. Click again to change it.' : 'You haven’t voted yet. Click again to vote.');
                return;
              }
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
                  // The sign-in ran out (after 30 days) or was signed out in another tab: that account's vote is no
                  // longer this reader's to show.
                  reader.signedIn = false;
                  reader.vote = null;
                  showReader();
                  show(null, before);
                  say('Sign in with GitHub again to vote.');
                  return;
                }
                if (!response.ok) throw new Error(String(response.status));
                // Saved: from here on the vote counts, even if the answer can't be read.
                reader.vote = vote;
                const answer = await response.json().catch(() => null);
                if (answer) remember(answer);
                show(vote, answer ?? hoped);
              } catch {
                show(previous, before);
                say('Your vote wasn’t recorded. Try again.');
              } finally {
                reactions.removeAttribute('aria-busy');
              }
            }));
          }

          // The archive (and search results, and a tag's page) loads older rites as the reader nears the end, by fetching
          // the next page the "Load older rites" link points to and taking its rows. The link stays: a keyboard follows
          // it here too, and focus moves to the first new rite. Without the script, it's an ordinary link to that page.
          const months = document.querySelector('.months');
          const more = document.querySelector('.archive-more');
          // Each month's rows go under its rubric: a page can start in the middle of the month the last one ended in,
          // whose rubric is here already.
          const takeRows = (page) => [...page.querySelectorAll('.months > .month')].flatMap((month) => {
            const here = document.getElementById(month.querySelector('.month-rubric').id)?.closest('.month');
            if (!here) return [...months.appendChild(document.importNode(month, true)).querySelectorAll('.rows > li')];
            const rows = here.querySelector('.rows');
            return [...month.querySelectorAll('.rows > li')].map((row) => rows.appendChild(document.importNode(row, true)));
          });
          if (months && more) {
            let loading = false;
            // A keyboard user who follows the link while scrolling already started a load still gets focus moved.
            let focusWhenLoaded = false;
            // Announcements go to a live region that's on the page from the start: one inserted already filled in
            // isn't reliably read out.
            const announcer = document.createElement('div');
            announcer.className = 'sr-only';
            announcer.setAttribute('role', 'status');
            months.after(announcer);
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
                const added = takeRows(page);
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
          // Ticked at once too: a cached page may have been rendered up to an hour ago.
          if (countdowns.length > 0) { tick(); setInterval(tick, 30000); }
        })();
        """;
}
