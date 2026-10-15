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
          // A search on the Archive replaces the rows and the link, so they're looked up anew each time.
          // Announcements go to a live region that's on the page from the start: one inserted already filled in isn't
          // reliably read out.
          const announcer = document.createElement('div');
          announcer.className = 'sr-only';
          announcer.setAttribute('role', 'status');
          document.getElementById('main').append(announcer);
          // Each month's rows go under its rubric: a page can start in the middle of the month the last one ended in,
          // whose rubric is here already.
          const takeRows = (months, page) => [...page.querySelectorAll('.months > .month')].flatMap((month) => {
            const here = document.getElementById(month.querySelector('.month-rubric').id)?.closest('.month');
            if (!here) return [...months.appendChild(document.importNode(month, true)).querySelectorAll('.rows > li')];
            const rows = here.querySelector('.rows');
            return [...month.querySelectorAll('.rows > li')].map((row) => rows.appendChild(document.importNode(row, true)));
          });
          let loading = false;
          // A keyboard user who follows the link while scrolling already started a load still gets focus moved.
          let focusWhenLoaded = false;
          // Counts the searches shown in place: a load started before one mustn't add its rows to the new ones.
          let searches = 0;
          const observer = new IntersectionObserver((entries) => {
            const link = document.querySelector('.archive-more a');
            if (link && entries.some((entry) => entry.isIntersecting)) loadOlder(link, false);
          }, { rootMargin: '200px' });
          const watchOlder = () => {
            observer.disconnect();
            const more = document.querySelector('.archive-more');
            if (more && document.querySelector('.months')) observer.observe(more);
          };
          const loadOlder = async (link, moveFocus) => {
            focusWhenLoaded ||= moveFocus;
            if (loading) return;
            loading = true;
            const search = searches;
            const more = link.closest('.archive-more');
            // Not watched while loading: the placeholders push the link away, and taking them out after a failure
            // would bring it back into view and start the same failing load again.
            observer.disconnect();
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
              if (search !== searches) return;
              const added = takeRows(document.querySelector('.months'), page);
              const next = page.querySelector('.archive-more');
              // A keyboard user may have tabbed onto the link while scrolling loaded: replacing it mustn't drop focus.
              focusWhenLoaded ||= more.contains(document.activeElement);
              more.replaceChildren(...(next ? [...next.childNodes].map((node) => document.importNode(node, true)) : []));
              announcer.textContent = `${added.length} older ${added.length === 1 ? 'rite' : 'rites'} loaded.`;
              if (focusWhenLoaded && added.length > 0) added[0].querySelector('a').focus();
              // Watched again, so a short page that still shows the end loads the next one too. Only after a load
              // that worked: after a failure the link stays for the reader to try again, not a loop.
              watchOlder();
            } catch {
              announcer.textContent = '';
              say('Older rites couldn’t be loaded. Try again.');
            } finally {
              placeholders.remove();
              loading = false;
              focusWhenLoaded = false;
            }
          };
          // Ctrl, Cmd, Shift or a middle click: the browser opens the page in a new tab or window, as it should.
          const isPlainClick = (event) => event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey;
          document.addEventListener('click', (event) => {
            const link = event.target.closest('.archive-more a');
            if (!link || !isPlainClick(event)) return;
            event.preventDefault();
            loadOlder(link, true);
          });
          watchOlder();

          // The Archive's own field shows the results as the reader types: a quarter of a second after they stop, the
          // results page is fetched and swapped into this one, and the address follows without a new history entry.
          // One renderer, the server's, so results look the same however they're reached. Without the script it's a
          // plain form, and Enter loads the results as a page.
          const field = document.getElementById('archive-search');
          if (field) {
            const form = field.form;
            let typing;
            let request;
            // Everything on the sheet but the field (which keeps focus and what's typed), and everything beside and
            // below the sheet but the candles and traces.
            const swap = (page) => {
              const fresh = [...page.querySelector('.sheet-inner').children].map((part) => document.importNode(part, true));
              const at = fresh.findIndex((part) => part.matches('.sheet-search'));
              [...form.parentElement.children].filter((part) => part !== form).forEach((part) => part.remove());
              form.before(...fresh.slice(0, at));
              form.after(...fresh.slice(at + 1));
              form.querySelector('.sheet-clear')?.remove();
              const clearLink = fresh[at].querySelector('.sheet-clear');
              if (clearLink) form.append(clearLink);
              const hero = document.querySelector('.archive-hero');
              const stays = (part) => part.matches('.sheet, .altar, .traces');
              [...hero.children].filter((part) => !stays(part)).forEach((part) => part.remove());
              hero.querySelector('.sheet').after(...[...page.querySelector('.archive-hero').children]
                .filter((part) => !stays(part)).map((part) => document.importNode(part, true)));
              document.title = page.title;
            };
            const show = async (typed) => {
              const url = typed ? `/archive?q=${encodeURIComponent(typed)}` : '/archive';
              request?.abort();
              request = new AbortController();
              try {
                const response = await fetch(url, { signal: request.signal });
                if (!response.ok) throw new Error(String(response.status));
                const page = new DOMParser().parseFromString(await response.text(), 'text/html');
                searches += 1;
                swap(page);
                history.replaceState(null, '', url);
                watchOlder();
                // What the line under the heading says as it's shown: "3 rites mention it", or the Archive's count.
                announcer.textContent = document.querySelector('.sheet-sub')?.innerText ?? '';
              } catch (error) {
                if (error.name !== 'AbortError') say('The Archive couldn’t be searched. Try again.');
              }
            };
            field.addEventListener('input', () => {
              clearTimeout(typing);
              typing = setTimeout(() => show(field.value.trim()), 250);
            });
            // Esc, the field's clear button and "Clear search" bring back the whole Archive, focus staying in the field.
            const clear = () => {
              clearTimeout(typing);
              field.value = '';
              field.focus();
              show('');
            };
            field.addEventListener('keydown', (event) => {
              if (event.key !== 'Escape' || field.value === '') return;
              event.preventDefault();
              clear();
            });
            document.addEventListener('click', (event) => {
              const link = event.target.closest('.sheet-clear, .margin-clear, .nav-search');
              if (!link || !isPlainClick(event)) return;
              event.preventDefault();
              // The nav's search icon leads to this field: here it's the field itself.
              if (link.matches('.nav-search')) field.focus();
              else clear();
            });
            // Arriving from the nav's search, to search on.
            if (location.hash === '#archive-search') field.focus();
          }

          // The nav's search, on every page but the Archive (whose field the icon leads to): the icon opens a field, and
          // the rites holding every word typed drop down under it, up to five. They come from every rite's words, loaded
          // once when the field first opens (/api/rites), so what the reader types never leaves the browser until they
          // search. Enter, or "Search all in the Archive", searches the Archive. Without the script the icon is a link
          // to the Archive's field.
          const navSearch = document.querySelector('.nav-search');
          if (navSearch && !field) {
            const box = document.createElement('div');
            box.className = 'nav-find';
            box.setAttribute('role', 'search');
            box.innerHTML = `<button type="button" class="nav-find-open" aria-label="Search" aria-expanded="false" aria-controls="nav-find-field">${navSearch.innerHTML}</button>`
              + '<input type="search" id="nav-find-field" class="nav-find-field" role="combobox" aria-autocomplete="list" aria-controls="search-list" aria-expanded="false" aria-label="Search the rites" placeholder="Search the rites" tabindex="-1" maxlength="100" autocomplete="off" spellcheck="false">'
              + '<button type="button" class="nav-find-close" aria-label="Close search"><svg aria-hidden="true" focusable="false" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6 6 18"></path><path d="m6 6 12 12"></path></svg></button>'
              + '<div class="nav-find-drop" hidden><p class="nav-find-label"></p><p class="nav-find-none" hidden>The Machine Spirit keeps its silence. Try another word.</p>'
              + '<div role="listbox" id="search-list" aria-label="Rites"></div><a class="nav-find-all" href="/archive">Search all in the Archive →</a></div>';
            navSearch.replaceWith(box);
            const opener = box.querySelector('.nav-find-open');
            const input = box.querySelector('.nav-find-field');
            const drop = box.querySelector('.nav-find-drop');
            const label = box.querySelector('.nav-find-label');
            const none = box.querySelector('.nav-find-none');
            const list = box.querySelector('[role=listbox]');
            const all = box.querySelector('.nav-find-all');
            const status = document.createElement('div');
            status.className = 'sr-only';
            status.setAttribute('role', 'status');
            box.append(status);
            const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
            let rites;
            let typing;
            let active = -1;
            // As the server reads them (SearchWords): each word once, without quotes around it, at most six.
            const wordsOf = (typed) => [...new Set(typed.toLowerCase().split(/\s+/)
              .map((word) => word.replace(/^["`'“”‘’]+|["`'“”‘’]+$/g, '')).filter(Boolean))].slice(0, 6);
            // The text with every word found in a <mark>, overlapping ones joined (as Highlight does on the server); built
            // from text nodes, so nothing a model wrote is HTML here.
            const marked = (text, words) => {
              const lower = text.toLowerCase();
              const ranges = words
                .flatMap((word) => {
                  const found = [];
                  for (let at = lower.indexOf(word); at >= 0; at = lower.indexOf(word, at + 1)) found.push([at, at + word.length]);
                  return found;
                })
                .sort((a, b) => a[0] - b[0])
                .reduce((merged, range) => {
                  const last = merged.at(-1);
                  if (last && range[0] <= last[1]) last[1] = Math.max(last[1], range[1]);
                  else merged.push([...range]);
                  return merged;
                }, []);
              const parts = document.createDocumentFragment();
              let at = 0;
              for (const [start, end] of ranges) {
                const mark = document.createElement('mark');
                mark.textContent = text.slice(start, end);
                parts.append(text.slice(at, start), mark);
                at = end;
              }
              parts.append(text.slice(at));
              return parts;
            };
            const choose = (next) => {
              const options = [...list.children];
              active = options.length === 0 ? -1 : next;
              options.forEach((option, place) => option.setAttribute('aria-selected', String(place === active)));
              if (active >= 0) input.setAttribute('aria-activedescendant', options[active].id);
              else input.removeAttribute('aria-activedescendant');
            };
            const showDrop = (shown) => {
              drop.hidden = !shown;
              input.setAttribute('aria-expanded', String(shown));
            };
            const suggest = async () => {
              const typed = input.value.trim();
              const words = wordsOf(typed);
              all.href = typed ? `/archive?q=${encodeURIComponent(typed)}#archive-search` : '/archive#archive-search';
              if (words.length === 0) {
                list.replaceChildren();
                choose(-1);
                showDrop(false);
                status.textContent = '';
                return;
              }
              try {
                rites ??= fetch('/api/rites').then((response) => {
                  if (!response.ok) throw new Error(String(response.status));
                  return response.json();
                });
                const index = await rites;
                // Typed on meanwhile: the next suggestions are on their way.
                if (input.value.trim() !== typed) return;
                const found = index.filter((rite) => {
                  const words2 = `${rite.title}\n${rite.text}\n${rite.truth}`.toLowerCase();
                  return words.every((word) => words2.includes(word));
                });
                const quoted = `“${typed}”`;
                label.textContent = found.length === 0 ? `Nothing found for ${quoted}`
                  : `${found.length} ${found.length === 1 ? 'rite mentions' : 'rites mention'} ${quoted}`;
                none.hidden = found.length > 0;
                list.replaceChildren(...found.slice(0, 5).map((rite) => {
                  const option = document.createElement('a');
                  option.id = `search-option-${rite.number}`;
                  option.setAttribute('role', 'option');
                  option.href = `/r/${rite.number}`;
                  option.tabIndex = -1;
                  const title = document.createElement('span');
                  title.className = 'nav-find-title';
                  title.append(marked(rite.title, words));
                  const [, month, day] = rite.day.split('-').map(Number);
                  const meta = document.createElement('span');
                  meta.className = 'nav-find-meta';
                  meta.textContent = `${rite.kind === 'prayer' ? 'Prayer' : 'Ritual'} · ${day} ${monthNames[month - 1]}`;
                  option.append(title, meta);
                  return option;
                }));
                status.textContent = label.textContent;
              } catch {
                // Tried again on the next keystroke; meanwhile the Archive can still search.
                rites = undefined;
                label.textContent = 'The rites couldn’t be loaded.';
                none.hidden = true;
                list.replaceChildren();
                status.textContent = label.textContent;
              }
              choose(-1);
              showDrop(true);
            };
            const open = () => {
              box.classList.add('open');
              opener.setAttribute('aria-expanded', 'true');
              input.tabIndex = 0;
              input.focus();
            };
            const close = (refocus) => {
              clearTimeout(typing);
              box.classList.remove('open');
              opener.setAttribute('aria-expanded', 'false');
              input.tabIndex = -1;
              input.value = '';
              list.replaceChildren();
              choose(-1);
              showDrop(false);
              status.textContent = '';
              if (refocus) opener.focus();
            };
            opener.addEventListener('click', () => (box.classList.contains('open') ? input.focus() : open()));
            box.querySelector('.nav-find-close').addEventListener('click', () => close(true));
            input.addEventListener('input', () => {
              clearTimeout(typing);
              typing = setTimeout(suggest, 150);
            });
            input.addEventListener('keydown', (event) => {
              const count = list.children.length;
              if (event.key === 'ArrowDown' && count > 0) {
                event.preventDefault();
                choose((active + 1) % count);
              } else if (event.key === 'ArrowUp' && count > 0) {
                event.preventDefault();
                choose(active <= 0 ? count - 1 : active - 1);
              } else if (event.key === 'Enter') {
                event.preventDefault();
                if (active >= 0) location.assign(list.children[active].href);
                else if (input.value.trim()) location.assign(all.href);
              } else if (event.key === 'Escape') {
                event.preventDefault();
                close(true);
              }
            });
            // A click or a tab anywhere else closes it.
            document.addEventListener('click', (event) => {
              if (box.classList.contains('open') && !box.contains(event.target)) close(false);
            });
            box.addEventListener('focusout', (event) => {
              if (box.classList.contains('open') && event.relatedTarget && !box.contains(event.relatedTarget)) close(false);
            });
          }

          // Looked up at each tick: a search on the Archive brings in a new margin with its own.
          const tick = () => {
            const now = new Date();
            const next = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1);
            const minutes = Math.ceil((next - now.getTime()) / 60000);
            const text = minutes >= 60 ? `${Math.floor(minutes / 60)}h ${minutes % 60}m` : `${minutes}m`;
            document.querySelectorAll('[data-countdown]').forEach((element) => { element.textContent = text; });
          };
          // Ticked at once too: a cached page may have been rendered up to an hour ago.
          if (document.querySelector('[data-countdown]')) { tick(); setInterval(tick, 30000); }
        })();
        """;
}
