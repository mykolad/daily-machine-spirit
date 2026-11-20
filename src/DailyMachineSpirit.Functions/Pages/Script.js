(() => {
  const seal = document.querySelector('.seal');
  if (seal) {
    const panel = document.getElementById(seal.getAttribute('aria-controls'));
    const label = seal.querySelector('.seal-label');
    const line = seal.querySelector('.seal-sub');
    const shown = (open) => {
      seal.setAttribute('aria-expanded', String(open));
      panel.hidden = !open;
      label.textContent = open ? 'Reseal the truth' : 'Break the seal';
      line.textContent = open ? 'Hide the Heretical Truth' : 'Reveal the Heretical Truth';
    };
    const setOpen = (open) => {
      shown(open);
      // The reader has the whole joke now: the rite is witnessed (#198).
      if (open) witness(Number(document.querySelector('[data-rite]')?.dataset.rite));
    };

    // Guessing the Heretical Truth (#199): the first press cracks the seal and unfolds three readings under it; choosing
    // one breaks it. The truth is in the page already, so the guess is checked here and never sent. One guess a rite,
    // kept in this browser alone ({ "12": { right, pick } }): a rite guessed before opens revealed, so a reader can't try
    // again for a better score. "Just show me", or the cracked seal pressed again, breaks it unguessed, keeping nothing.
    const guess = document.querySelector('[data-guess]');
    const readings = guess?.querySelector('[data-readings]');
    const verdict = guess?.querySelector('[data-verdict]');
    const struck = guess?.querySelector('[data-guessed]');
    const guessedKey = 'dms-guessed-v1';
    const guessed = () => { try { return JSON.parse(localStorage.getItem(guessedKey) ?? '{}'); } catch { return {}; } };
    const keepGuess = (rite, pick, right) => {
      const all = guessed();
      all[rite] = { right, pick };
      try { localStorage.setItem(guessedKey, JSON.stringify(all)); } catch { /* not kept, then */ }
    };
    // Pen strokes, as the site's other marks, in colours that read on the dark ground: the tick one tapered stroke, the
    // cross as a broad nib writes an X.
    const tick = '<svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true" focusable="false"><path d="M2.6 12.4C5.4 12.3 7.6 14 9.3 16.9C12.3 11.2 15.9 6.8 21.8 3.2C17.6 7.9 13.8 13.1 10.9 19.8C10.4 20.9 8.9 21 8.4 19.9C7.1 17 5.2 14.4 2.6 12.4Z" fill="#d9b874"/></svg>';
    const cross = '<svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true" focusable="false"><path d="M3.2 4.2C5 4.4 7 5.8 9.2 7.9C12.6 11.1 16.4 15.4 20.9 20.6C17 19 13.8 17 10.8 14.4C8.2 12 5.9 9.1 4.2 6.8C4 5.8 3.5 5 3.2 4.2Z" fill="#e88a78"/><path d="M20.4 3.4C16.6 7 12.4 11.5 8.3 16.2C6.9 17.8 5.5 19.2 4 20.4C4.8 18.2 6 16.2 7.3 14.6C11.4 9.9 15.6 5.9 20.4 3.4Z" fill="#e88a78"/></svg>';
    const numerals = ['I.', 'II.', 'III.'];
    const rite = document.querySelector('[data-rite]')?.dataset.rite;
    const truth = Number(guess?.dataset.truth);
    // Once the step is played or skipped, the seal only hides and shows what it revealed: the readings never come back.
    let revealed = false;
    let picked = null;
    const crack = () => {
      seal.classList.add('cracked');
      seal.setAttribute('aria-expanded', 'true');
      seal.setAttribute('aria-label', 'Break the seal without guessing');
      // Expanded, the seal shows the readings; once broken, the truth again.
      seal.setAttribute('aria-controls', readings.id);
      line.textContent = 'Choose the truth to break it';
      readings.hidden = false;
      readings.querySelector('.reading').focus();
    };
    const uncrack = () => {
      seal.classList.remove('cracked');
      seal.setAttribute('aria-expanded', 'false');
      seal.removeAttribute('aria-label');
      seal.setAttribute('aria-controls', panel.id);
      line.textContent = 'Guess the Heretical Truth';
      readings.hidden = true;
      seal.focus();
    };
    // The readings give way to the verdict and, for a wrong guess, the guess struck through, above the truth.
    const settle = (pick) => {
      revealed = true;
      picked = pick;
      seal.classList.remove('cracked');
      seal.removeAttribute('aria-label');
      seal.setAttribute('aria-controls', panel.id);
      readings.hidden = true;
      const wrong = pick !== null && pick !== truth;
      const all = Object.values(guessed());
      const seen = `Seen through ${all.filter((kept) => kept.right).length} of ${all.length}.`;
      verdict.innerHTML = pick === null
        ? '<span class="guess-verdict-sub">Unguessed. The Truth lies below.</span>'
        : wrong
          ? `<span class="guess-verdict-line">${cross}THOU ART ONE OF US</span><span class="guess-verdict-sub">The rite would have worked on thee. ${seen}</span>`
          : `<span class="guess-verdict-line">${tick}THOU HAST SEEN THROUGH THE RITE</span><span class="guess-verdict-sub">No rite shall bind thee. ${seen}</span>`;
      verdict.hidden = false;
      if (wrong) {
        const words = readings.querySelector(`[data-reading="${pick}"] .reading-text`).innerHTML;
        struck.innerHTML = `<span class="guessed-mark">${cross}</span><div class="reading"><span class="reading-num">${numerals[pick - 1]}</span><span class="reading-text"><span class="struck">${words}</span> <span class="guessed-tag">· THY GUESS</span></span></div>`;
        struck.hidden = false;
        // Beside the struck guess the panel names the true reading's numeral; with no reading left, it would name nothing.
        document.getElementById('truth-heading').textContent = `Heretical Truth · ${numerals[truth - 1]}`;
      }
    };
    const breakWith = (pick) => {
      // A guess made meanwhile in another tab stands, and is the one shown: one guess a rite.
      const before = guessed()[rite];
      if (before) {
        settle(before.pick ?? before.choice);
      } else {
        if (pick !== null) keepGuess(rite, pick, pick === truth);
        settle(pick);
      }
      setOpen(true);
      panel.focus();
    };
    // Resealed, the verdict and the struck guess go with the truth, and come back with it.
    const showRevealed = (open) => {
      shown(open);
      verdict.hidden = !open;
      struck.hidden = !open || !struck.hasChildNodes();
      if (!open && picked !== null) line.textContent = 'Thy guess is cast';
    };
    if (guess) {
      const before = guessed()[rite];
      if (before) {
        // Witnessed when it was guessed: shown open, as it was left. Guesses kept before the readings moved under the
        // seal hold their reading as "choice".
        settle(before.pick ?? before.choice);
        shown(true);
      } else {
        line.textContent = 'Guess the Heretical Truth';
      }
      readings.addEventListener('click', (event) => {
        const chosen = event.target.closest('[data-reading]');
        if (chosen) {
          // The numeral is ringed for a moment, as the reader's pen would, before the seal breaks.
          chosen.classList.add('chosen');
          readings.querySelectorAll('button').forEach((button) => { button.disabled = true; });
          setTimeout(() => breakWith(Number(chosen.dataset.reading)), 150);
        } else if (event.target.closest('[data-guess-skip]')) {
          breakWith(null);
        }
      });
      // Esc closes the readings, the seal whole again, nothing kept.
      readings.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') uncrack();
      });
    }
    seal.addEventListener('click', () => {
      if (!guess) {
        setOpen(seal.getAttribute('aria-expanded') !== 'true');
      } else if (revealed) {
        showRevealed(panel.hidden);
      } else if (seal.classList.contains('cracked')) {
        breakWith(null);
      } else {
        crack();
      }
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

  // The nav's link to the reader's Breviary, for a signed-in reader alone. The page is the same for everyone, so a
  // cookie that says only that shows it (sign-in sets it; signing out, and /api/me for a sign-in run out, clear it).
  const breviaryLink = document.querySelector('.nav-breviary');
  const showBreviary = (signedIn) => { if (breviaryLink) breviaryLink.hidden = !signedIn; };
  showBreviary(document.cookie.split('; ').includes('dms-signed-in=1'));
  // After a vote, where it's kept. Longer than the other toasts, and kept while the pointer or focus is on it, to
  // follow the link; the nav's link leads there too.
  const sayKept = (vote) => {
    clearTimeout(hideToast);
    toast.innerHTML = '';
    const pill = document.createElement('div');
    pill.className = 'toast toast-lasting toast-kept';
    pill.append(vote === 'blessed' ? 'Blessed. Kept in your ' : 'Called heresy. Kept in your ');
    const link = document.createElement('a');
    link.href = '/breviary';
    link.textContent = 'Breviary';
    pill.append(link, '.');
    toast.appendChild(pill);
    const later = () => {
      hideToast = setTimeout(() => {
        if (pill.matches(':hover, :focus-within')) later();
        else if (pill.isConnected) toast.innerHTML = '';
      }, 6000);
    };
    later();
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
    // The page's link, a rite's text with its link, or a value (an address, a command) as it's shown.
    button.addEventListener('click', async () => {
      const kind = button.dataset.copy;
      if (kind === 'value') {
        say(await copy(button.dataset.text) ? 'Copied to clipboard' : 'Couldn’t copy. Your browser blocked the clipboard.');
        return;
      }
      const url = new URL(button.dataset.url, location.origin).href;
      const isLink = kind === 'link';
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
    // Confess (#200), apart from the verdicts but on the same vote: the same sign-in, the same request, the same kept
    // counts. Shown by the script alone, as voting needs it.
    const confession = document.querySelector('.confession');
    const confess = confession?.querySelector('[data-confess]');
    const confessed = confession?.querySelector('.confessed-count');
    const confessStatus = confession?.querySelector('.confess-status');
    if (confession) confession.hidden = false;
    const confessions = () => Number(confessed?.dataset.confessions ?? 0);
    const confessedWords = (count, mine) => mine && count === 1 ? 'Thou alone hast confessed'
      : count === 0 ? 'None have confessed'
        : count === 1 ? '1 hath confessed'
          : `${count.toLocaleString('en')} have confessed`;
    const showConfession = (mine, count) => {
      if (!confession) return;
      confess.setAttribute('aria-pressed', String(mine));
      confessed.dataset.confessions = String(count);
      confessed.textContent = confessedWords(count, mine);
    };
    const tellConfession = (words) => { if (confessStatus) confessStatus.textContent = words; };
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
        all[rite] = { blessed: answer.blessed, heresy: answer.heresy, confessions: answer.confessions, countedAt: answer.countedAt, rememberedAt: Date.now() };
        localStorage.setItem(votesKey, JSON.stringify(all));
      } catch { }
    };
    const kept = recalled()[rite];
    if (kept && typeof kept.blessed === 'number' && typeof kept.heresy === 'number' && kept.countedAt >= Number(reactions.dataset.countedAt)) {
      show(null, kept);
      // Kept before confessions were counted, an answer has none to show.
      if (typeof kept.confessions === 'number') showConfession(false, kept.confessions);
    }
    let reader = { available: false, signedIn: false, vote: null, confessed: false, voteLoaded: false };
    const showReader = () => statuses.forEach((s) => { s.hidden = !reader.available || s.dataset.signedIn !== String(reader.signedIn); });
    // The signed-in reader's own vote. Until it has loaded, the page doesn't know whether a click sets a vote or
    // takes one back, so voting waits for it.
    const loadVote = async () => {
      try {
        const votes = await fetch(`/api/votes?rites=${rite}`);
        // Signed out meanwhile (another tab, or the sign-in ran out): offer the sign-in again.
        if (votes.status === 401) {
          reader.signedIn = false;
          showBreviary(false);
        }
        else if (votes.ok) {
          const yours = await votes.json();
          const confessedTo = Array.isArray(yours.confessed) ? yours.confessed : [];
          reader = { ...reader, vote: yours[rite] ?? null, confessed: confessedTo.includes(Number(rite)), voteLoaded: true };
        }
      } catch { }
    };
    // Until this answers, a click waits for it rather than guessing who's reading.
    const known = (async () => {
      try {
        const me = await (await fetch('/api/me')).json();
        reader = { available: me.signInAvailable === true, signedIn: me.signedIn === true, vote: null, confessed: false, voteLoaded: false };
        showBreviary(reader.signedIn);
        if (reader.signedIn) await loadVote();
      } catch { }
      showReader();
      show(reader.vote, counts());
      showConfession(reader.confessed, confessions());
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
          reader.confessed = false;
          showReader();
          showBreviary(false);
          show(null, before);
          showConfession(false, confessions());
          say('Sign in with GitHub again to vote.');
          return;
        }
        if (!response.ok) throw new Error(String(response.status));
        // Saved: from here on the vote counts, even if the answer can't be read.
        reader.vote = vote;
        const answer = await response.json().catch(() => null);
        if (answer) remember(answer);
        show(vote, answer ?? hoped);
        if (vote) {
          sayKept(vote);
        } else if (toast.querySelector('.toast-kept')) {
          // Taken back while the toast still says it's kept: it no longer is.
          clearTimeout(hideToast);
          toast.innerHTML = '';
        }
      } catch {
        show(previous, before);
        say('Your vote wasn’t recorded. Try again.');
      } finally {
        reactions.removeAttribute('aria-busy');
      }
    }));

    // Signed out, a press stays unpressed and says how to confess, beside the button rather than in a toast.
    const offerSignIn = () => {
      const link = document.createElement('a');
      link.href = signInLink.getAttribute('href');
      link.textContent = 'Sign in with GitHub';
      confessStatus.replaceChildren(link, ' to confess. Thy name stays thine: only a one-way key is kept.');
    };
    confess?.addEventListener('click', async () => {
      await known;
      if (!reader.available) { tellConfession('Confessing isn’t open yet.'); return; }
      if (!reader.signedIn) { offerSignIn(); return; }
      if (confession.getAttribute('aria-busy') === 'true') return;
      if (!reader.voteLoaded) {
        // As a vote does: the reader's confession didn't load with the page, so it's shown rather than acted on.
        confession.setAttribute('aria-busy', 'true');
        await loadVote();
        confession.removeAttribute('aria-busy');
        showReader();
        if (!reader.signedIn) { offerSignIn(); return; }
        if (!reader.voteLoaded) { tellConfession('Thy confession couldn’t be loaded. Try again.'); return; }
        showConfession(reader.confessed, confessions());
        tellConfession(reader.confessed ? 'Thou hast confessed already. Press again to withdraw it.' : 'Press again to confess.');
        return;
      }
      const previous = reader.confessed;
      const mine = !previous;
      const before = confessions();
      const hoped = Math.max(0, before + (mine ? 1 : -1));
      showConfession(mine, hoped);
      confession.setAttribute('aria-busy', 'true');
      try {
        const response = await fetch(`/api/rites/${rite}/vote`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ confessed: mine }),
        });
        if (response.status === 401) {
          reader = { ...reader, signedIn: false, vote: null, confessed: false };
          showReader();
          showBreviary(false);
          show(null, counts());
          showConfession(false, before);
          offerSignIn();
          return;
        }
        if (!response.ok) throw new Error(String(response.status));
        // Saved: from here on the confession counts, even if the answer can't be read.
        reader.confessed = mine;
        const answer = await response.json().catch(() => null);
        if (answer) remember(answer);
        const count = typeof answer?.confessions === 'number' ? answer.confessions : hoped;
        showConfession(mine, count);
        tellConfession(!mine ? 'Thy confession is withdrawn.'
          : count === 1 ? 'Thy sin is recorded. Thou art the first to confess it.'
            : 'Thy sin is recorded. Thou art not alone.');
      } catch {
        showConfession(previous, before);
        tellConfession('Thy confession wasn’t recorded. Try again.');
      } finally {
        confession.removeAttribute('aria-busy');
      }
    });
  }

  // The margins beside a sheet follow the reader down it, their candles level beside the text (the stylesheet).
  // Taller than the window (the Archive's month index grows by a line a month), they'd hide their candles, so they
  // stop with their foot in view instead. Measured again whenever they may have changed: the fonts arriving, the
  // window resized, a search on the Archive bringing in new ones.
  const placeMargins = () => {
    const margins = document.querySelector('.margins');
    if (!margins) return;
    margins.style.setProperty('--margins-top', `${Math.min(32, innerHeight - margins.offsetHeight - 32)}px`);
  };
  placeMargins();
  addEventListener('resize', placeMargins);
  document.fonts.ready.then(placeMargins);

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
  // The margins' code runs as long as the sheet: each loaded page adds as much code as a page starts with, cut
  // where the sheet ends. Measuring instead would miss margins hidden on a narrow screen that later widens. A
  // search shown in place keeps the sheet and its code, so the code is never shorter than the rows.
  const lengthenMargins = () => document.querySelectorAll('.margin-text[data-code]').forEach((margin) => {
    margin.textContent += ` · ${Array(Number(margin.dataset.copies)).fill(margin.dataset.code).join(' · ')}`;
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
    // On the sheet, where the rows will come, rather than beside the link below it (in a wide page's grid, that
    // would put them under the left margin).
    document.querySelector('.months').append(placeholders);
    announcer.textContent = 'Loading older rites…';
    try {
      const response = await fetch(link.href);
      if (!response.ok) throw new Error(String(response.status));
      const page = new DOMParser().parseFromString(await response.text(), 'text/html');
      if (search !== searches) return;
      const added = takeRows(document.querySelector('.months'), page);
      lengthenMargins();
      const next = page.querySelector('.archive-more');
      // A keyboard user may have tabbed onto the link while scrolling loaded: replacing it mustn't drop focus.
      focusWhenLoaded ||= more.contains(document.activeElement);
      more.replaceChildren(...(next ? [...next.childNodes].map((node) => document.importNode(node, true)) : []));
      announcer.textContent = `${added.length} older ${added.length === 1 ? 'rite' : 'rites'} loaded.`;
      // Marked before focus moves, not after by the observer: with "Hide witnessed" a witnessed row is hidden,
      // and focus on it would be lost. If every new row is hidden, the link to the next page takes it.
      markWitnessed();
      const unwitnessedOnly = document.body.classList.contains('unwitnessed-only');
      const shown = added.find((row) => !unwitnessedOnly || !row.classList.contains('witnessed'));
      if (focusWhenLoaded && added.length > 0) (shown ?? more).querySelector('a')?.focus();
      // Watched again, so a short page that still shows the end loads the next one too. Only after a load
      // that worked: after a failure the link stays for the reader to try again, not a loop.
      watchOlder();
    } catch {
      if (search === searches) {
        announcer.textContent = '';
        say('Older rites couldn’t be loaded. Try again.');
      }
    } finally {
      placeholders.remove();
      loading = false;
      focusWhenLoaded = false;
      // A search replaced the rows meanwhile: its link may be in view already, waiting for this load to end.
      if (search !== searches) watchOlder();
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

  // The sheet's own field (the Archive's, the Breviary's) is a plain form without the script, and Enter loads the
  // results as a page. With it, the field is hidden and the nav's search fills it (#157), one search on the page:
  // a quarter of a second after the reader stops typing, the results page is fetched and swapped into this one,
  // and the address follows without a new history entry. One renderer, the server's, so results look the same
  // however they're reached.
  const field = document.querySelector('.sheet-search input[type=search]');
  const onTheArchive = field?.id === 'archive-search';
  // What the nav's search calls to search the page, on a page with a field of its own.
  let searchThePage = null;
  if (field) {
    const form = field.form;
    let typing;
    let request;
    // Everything on the sheet but the field (which keeps focus and what's typed), and everything beside and
    // below the sheet but the traces (the margins come anew, their candles with them).
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
      const stays = (part) => part.matches('.sheet, .traces');
      [...hero.children].filter((part) => !stays(part)).forEach((part) => part.remove());
      hero.querySelector('.sheet').after(...[...page.querySelector('.archive-hero').children]
        .filter((part) => !stays(part)).map((part) => document.importNode(part, true)));
      document.title = page.title;
      placeMargins();
    };
    // The form's own address and fields, as Enter would send them. Narrowed to a subject, the Archive's words are
    // sought within it; clearing them clears the search, the subject too. The Breviary's verdict stays.
    const show = async (typed) => {
      if (!typed) form.elements.tag?.remove();
      const fields = new URLSearchParams(new FormData(form));
      if (typed) fields.set('q', typed);
      else fields.delete('q');
      const query = fields.toString();
      const url = `${form.getAttribute('action')}${query ? `?${query}` : ''}`;
      request?.abort();
      request = new AbortController();
      try {
        const response = await fetch(url, { signal: request.signal });
        if (!response.ok) throw new Error(String(response.status));
        // Sent elsewhere (a subject with no words left, only quotes, is the tag's own page): that page isn't the
        // Archive's, so it's loaded rather than swapped in.
        if (response.redirected) {
          location.assign(response.url);
          return;
        }
        const page = new DOMParser().parseFromString(await response.text(), 'text/html');
        // A page without the field (the Breviary of a reader who signed out meanwhile) has nothing to swap in.
        if (!page.querySelector('.sheet-search')) {
          location.assign(url);
          return;
        }
        searches += 1;
        swap(page);
        history.replaceState(null, '', url);
        watchOlder();
        // What the line under the heading says as it's shown: "3 rites mention it", or the Archive's count.
        announcer.textContent = document.querySelector('.sheet-sub')?.innerText ?? '';
      } catch (error) {
        if (error.name !== 'AbortError') say('The search didn’t work. Try again.');
      }
    };
    form.style.display = 'none';
    searchThePage = (typed) => {
      clearTimeout(typing);
      field.value = typed;
      typing = setTimeout(() => show(typed), 250);
    };
    // The Archive's "Clear search" in the margin brings back the whole Archive (the nav's search closes too).
    document.addEventListener('click', (event) => {
      if (!event.target.closest('.margin-clear') || !isPlainClick(event)) return;
      event.preventDefault();
      clearTimeout(typing);
      field.value = '';
      show('');
    });
  }

  // The nav's search, the page's only one: the icon opens a field. On the Archive the Archive narrows under it as
  // the reader types; on the Breviary the reader's rites do while "Only my Breviary" is ticked, as it is at first.
  // Elsewhere, and unticked, the subjects and the rites the words typed point to drop down under it. The subjects
  // come with the page; the rites (up to five, holding every word) come from the search index (every rite's words
  // and subjects), loaded once when the field first opens (/api/search-index), so what the reader types never
  // leaves the browser until they search. Enter, or "Search all in the Archive", searches the Archive. Without the
  // script the icon is a link to the Archive's field.
  const navSearch = document.querySelector('.nav-search');
  if (navSearch) {
    const subjects = JSON.parse(navSearch.dataset.subjects ?? '[]');
    const box = document.createElement('div');
    box.className = 'nav-find';
    box.setAttribute('role', 'search');
    // Named for what it searches (nameTheField), so a screen reader moving by landmarks knows which rites.
    box.setAttribute('aria-label', 'All rites');
    box.innerHTML = `<button type="button" class="nav-find-open" aria-label="Search" aria-expanded="false" aria-controls="nav-find-field">${navSearch.innerHTML}</button>`
      + '<input type="search" id="nav-find-field" class="nav-find-field" role="combobox" aria-autocomplete="list" aria-controls="search-list" aria-expanded="false" aria-label="Search the rites" placeholder="Search the rites" tabindex="-1" maxlength="100" autocomplete="off" spellcheck="false">'
      + '<button type="button" class="nav-find-close" aria-label="Close search"><svg aria-hidden="true" focusable="false" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6 6 18"></path><path d="m6 6 12 12"></path></svg></button>'
      + '<div class="nav-find-drop" hidden><div role="listbox" id="search-list" aria-label="Suggestions">'
      + '<div role="group" aria-labelledby="search-subjects" hidden><p class="nav-find-label" id="search-subjects" role="presentation">Subjects</p></div>'
      + '<div role="group" aria-labelledby="search-rites"><p class="nav-find-label" id="search-rites" role="presentation"></p></div></div>'
      + '<p class="nav-find-none" hidden>The Machine Spirit keeps its silence. Try another word.</p><a class="nav-find-all" href="/archive">Search all in the Archive →</a></div>';
    navSearch.replaceWith(box);
    const opener = box.querySelector('.nav-find-open');
    const input = box.querySelector('.nav-find-field');
    const drop = box.querySelector('.nav-find-drop');
    const [subjectsGroup, ritesGroup] = box.querySelectorAll('[role=group]');
    const subjectsLabel = subjectsGroup.querySelector('.nav-find-label');
    const label = ritesGroup.querySelector('.nav-find-label');
    const none = box.querySelector('.nav-find-none');
    const all = box.querySelector('.nav-find-all');
    const status = document.createElement('div');
    status.className = 'sr-only';
    status.setAttribute('role', 'status');
    box.append(status);
    // The Archive holds every rite: only the Breviary has a narrower search to choose.
    const scope = searchThePage && !onTheArchive && document.createElement('label');
    if (scope) {
      scope.className = 'nav-find-scope';
      scope.innerHTML = '<input type="checkbox" checked> Only my Breviary';
      box.append(scope);
    }
    const scoped = () => (scope ? scope.querySelector('input').checked : Boolean(searchThePage));
    const nameTheField = () => {
      const mine = scoped();
      input.placeholder = !mine ? 'Search the rites' : onTheArchive ? 'Search the Archive' : 'Search your Breviary';
      input.setAttribute('aria-label', input.placeholder);
      box.setAttribute('aria-label', !mine ? 'All rites' : onTheArchive ? 'The Archive' : 'Your Breviary');
    };
    const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    // A rite's day as the pages show it (ImperialDate.Of): the thousandths of its year passed at its midnight, its
    // year within the millennium, and the millennium.
    const imperialOf = (year, month, day) => {
      const leap = (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
      const passed = (Date.UTC(year, month - 1, day) - Date.UTC(year, 0, 1)) / 86_400_000;
      const three = (number) => String(number).padStart(3, '0');
      return `0 ${three(Math.floor(passed * 1000 / (leap ? 366 : 365)))} ${three(year % 1000)}.M${Math.floor(year / 1000) + 1}`;
    };
    let rites;
    let typing;
    let active = -1;
    // As the server reads them (SearchWords): each word once, without quotes around it, at most six.
    const wordsOf = (typed) => [...new Set(typed.toLowerCase().split(/\s+/)
      .map((word) => word.replace(/^["`'“”‘’]+|["`'“”‘’]+$/g, '')).filter(Boolean))].slice(0, 6);
    // The text with every word found in a <mark>, overlapping ones joined (as Highlight does on the server); built
    // from text nodes, so nothing a model wrote is HTML here. A subject's are marked only at the starts of words,
    // where they matched.
    const marked = (text, words, atWordStart) => {
      const lower = text.toLowerCase();
      // An empty word would be found everywhere, forever.
      const ranges = words.filter(Boolean)
        .flatMap((word) => {
          const found = [];
          for (let at = lower.indexOf(word); at >= 0; at = lower.indexOf(word, at + 1)) {
            if (!atWordStart || at === 0 || !/[\p{L}\p{N}]/u.test(lower[at - 1])) found.push([at, at + word.length]);
          }
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
    // Accents and case aside: "Débug" finds Debugging.
    const fold = (text) => text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
    const startsEvery = (text, words) => {
      const its = fold(text).split(/[^\p{L}\p{N}]+/u);
      return words.every((word) => its.some((each) => each.startsWith(word)));
    };
    // A subject is suggested when every word typed starts a word of its name, or of what its rites are about
    // (so "flaky" suggests Builds): at most three, those the name matches first. A word that folds away to
    // nothing (a lone accent) would start every word, so it's no word.
    const subjectsFor = (words) => {
      const folded = words.map(fold).filter(Boolean);
      if (folded.length === 0) return [];
      const named = subjects.filter((subject) => startsEvery(subject.label, folded));
      const about = subjects.filter((subject) => !named.includes(subject) && startsEvery(`${subject.label} ${subject.about}`, folded));
      return [...named, ...about].slice(0, 3);
    };
    const optionOf = (id, href, titleContent, metaContent) => {
      const option = document.createElement('a');
      option.id = id;
      option.setAttribute('role', 'option');
      option.href = href;
      option.tabIndex = -1;
      const title = document.createElement('span');
      title.className = 'nav-find-title';
      title.append(titleContent);
      const meta = document.createElement('span');
      meta.className = 'nav-find-meta';
      meta.append(metaContent);
      option.append(title, meta);
      return option;
    };
    const fill = (subjectOptions, riteOptions) => {
      subjectsGroup.replaceChildren(subjectsLabel, ...subjectOptions);
      subjectsGroup.hidden = subjectOptions.length === 0;
      ritesGroup.replaceChildren(label, ...riteOptions);
    };
    const options = () => [...box.querySelectorAll('[role=option]')];
    const choose = (next) => {
      const shown = options();
      active = shown.length === 0 ? -1 : next;
      shown.forEach((option, place) => option.setAttribute('aria-selected', String(place === active)));
      if (active >= 0) {
        input.setAttribute('aria-activedescendant', shown[active].id);
        // Focus stays in the field, so the browser won't bring the chosen suggestion into view by itself.
        shown[active].scrollIntoView({ block: 'nearest' });
      } else {
        input.removeAttribute('aria-activedescendant');
      }
    };
    const showDrop = (shown) => {
      drop.hidden = !shown;
      input.setAttribute('aria-expanded', String(shown));
    };
    // Closed, typed on, or turned to the page's own search while the rites were loading: the next suggestions are on
    // their way, or none are wanted.
    const outdated = (typed) => !box.classList.contains('open') || input.value.trim() !== typed || scoped();
    const suggest = async () => {
      if (scoped()) return;
      const typed = input.value.trim();
      const words = wordsOf(typed);
      if (words.length === 0) {
        fill([], []);
        choose(-1);
        showDrop(false);
        status.textContent = '';
        return;
      }
      // They need no index, so they're shown at once, while it loads, and even when it can't be.
      const filed = subjectsFor(words);
      const folded = words.map(fold).filter(Boolean);
      const subjectOptions = filed.map((subject) => optionOf(`search-subject-${subject.key}`, `/tags/${subject.key}`,
        marked(subject.label, folded, true), marked(`Rites about ${subject.about}`, folded, true)));
      const announced = (text) => (filed.length === 0 ? text
        : `${filed.length} ${filed.length === 1 ? 'subject' : 'subjects'}. ${text}`);
      if (filed.length > 0) {
        label.textContent = 'Looking through the rites…';
        none.hidden = true;
        fill(subjectOptions, []);
        choose(-1);
        showDrop(true);
        status.textContent = announced(label.textContent);
      }
      try {
        rites ??= fetch('/api/search-index').then((response) => {
          if (!response.ok) throw new Error(String(response.status));
          return response.json();
        });
        const index = await rites;
        if (outdated(typed)) return;
        // As the Archive's search: each word in the rite's words, or naming one of its subjects (RiteTags.Names).
        // An index Cloudflare kept from before it had tags has none.
        const named = words.map((word) => subjects
          .filter((subject) => subject.label.toLowerCase().split(' ').some((part) => part.startsWith(word)))
          .map((subject) => subject.key));
        const found = index.filter((rite) => {
          // The ailment too (#197); an index Cloudflare kept from before it had ailments has none.
          const words2 = `${rite.title}\n${rite.text}\n${rite.truth}\n${rite.ailment ?? ''}`.toLowerCase();
          return words.every((word, at) => words2.includes(word) || named[at].some((key) => (rite.tags ?? []).includes(key)));
        });
        const quoted = `“${typed}”`;
        label.textContent = found.length > 0
          ? `${found.length} ${found.length === 1 ? 'rite mentions' : 'rites mention'} ${quoted}`
          : filed.length > 0 ? `No rite mentions ${quoted}` : `Nothing found for ${quoted}`;
        none.hidden = found.length > 0 || filed.length > 0;
        showFound(subjectOptions, found.slice(0, 5).map((rite) => {
          const [year, month, day] = rite.day.split('-').map(Number);
          const meta = document.createDocumentFragment();
          const shown = document.createElement('span');
          shown.setAttribute('aria-hidden', 'true');
          shown.textContent = imperialOf(year, month, day);
          const said = document.createElement('span');
          said.className = 'sr-only';
          said.textContent = `${day} ${monthNames[month - 1]} ${year}`;
          shown.title = said.textContent;
          meta.append(`${rite.kind === 'prayer' ? 'Prayer' : 'Ritual'} · `, shown, said);
          return optionOf(`search-option-${rite.number}`, `/r/${rite.number}`, marked(rite.title, words, false), meta);
        }));
        status.textContent = announced(label.textContent);
      } catch {
        // Tried again on the next keystroke; meanwhile the Archive can still search.
        rites = undefined;
        if (outdated(typed)) return;
        label.textContent = 'The rites couldn’t be loaded.';
        none.hidden = true;
        showFound(subjectOptions, []);
        status.textContent = announced(label.textContent);
      }
    };
    // The rites join the subjects shown while they loaded; a subject the reader chose meanwhile stays chosen.
    // By its id: an earlier search for the same words, settling later, brings its own copy of the option.
    const showFound = (subjectOptions, riteOptions) => {
      const chosen = options()[active]?.id;
      fill(subjectOptions, riteOptions);
      choose(options().findIndex((option) => option.id === chosen));
      showDrop(true);
    };
    const open = () => {
      box.classList.add('open');
      opener.setAttribute('aria-expanded', 'true');
      input.tabIndex = 0;
      input.focus();
    };
    const close = (refocus) => {
      // Closed, the page's search is over: the whole Archive, or Breviary, again.
      if (scoped() && input.value.trim()) searchThePage('');
      clearTimeout(typing);
      box.classList.remove('open');
      opener.setAttribute('aria-expanded', 'false');
      input.tabIndex = -1;
      input.value = '';
      fill([], []);
      choose(-1);
      showDrop(false);
      status.textContent = '';
      if (refocus) opener.focus();
    };
    opener.addEventListener('click', () => (box.classList.contains('open') ? input.focus() : open()));
    box.querySelector('.nav-find-close').addEventListener('click', () => close(true));
    // At once, so Enter and "Search all" search what's typed now, and no suggestion for older words can be opened;
    // only the suggestions wait for a pause.
    const pointAll = (typed) => {
      all.href = typed ? `/archive?q=${encodeURIComponent(typed)}#archive-search` : '/archive#archive-search';
    };
    input.addEventListener('input', () => {
      const typed = input.value.trim();
      pointAll(typed);
      // The page's rites narrow under it as the reader types: they're the results, so nothing drops down.
      if (scoped()) {
        searchThePage(typed);
        return;
      }
      fill([], []);
      choose(-1);
      clearTimeout(typing);
      typing = setTimeout(suggest, 150);
    });
    input.addEventListener('keydown', (event) => {
      // Keys an input method uses while it composes a word (Enter picks its candidate) aren't the field's.
      if (event.isComposing) return;
      const count = options().length;
      if (event.key === 'ArrowDown' && count > 0) {
        event.preventDefault();
        choose((active + 1) % count);
      } else if (event.key === 'ArrowUp' && count > 0) {
        event.preventDefault();
        choose(active <= 0 ? count - 1 : active - 1);
      } else if (event.key === 'Enter') {
        event.preventDefault();
        if (scoped()) return;
        if (active >= 0) location.assign(options()[active].href);
        else if (input.value.trim()) location.assign(all.href);
      } else if (event.key === 'Escape') {
        event.preventDefault();
        close(true);
      }
    });
    // A click or a tab anywhere else closes it, but not while it holds the page's search: the words stay over the
    // rites they found.
    const keepsTheSearch = () => scoped() && input.value.trim() !== '';
    document.addEventListener('click', (event) => {
      if (box.classList.contains('open') && !box.contains(event.target) && !keepsTheSearch()) close(false);
    });
    box.addEventListener('focusout', (event) => {
      if (box.classList.contains('open') && event.relatedTarget && !box.contains(event.relatedTarget) && !keepsTheSearch()) close(false);
    });
    if (scope) {
      scope.querySelector('input').addEventListener('change', () => {
        nameTheField();
        const typed = input.value.trim();
        if (scoped()) {
          clearTimeout(typing);
          fill([], []);
          choose(-1);
          showDrop(false);
          status.textContent = '';
          searchThePage(typed);
        } else {
          searchThePage('');
          if (typed) suggest();
        }
        input.focus();
      });
    }
    if (searchThePage) {
      nameTheField();
      // A search of the page, linked or reloaded: its words shown back in the open field. Arriving from another
      // page's search ("#archive-search"), to search on.
      if (field.value || location.hash === `#${field.id}`) {
        box.classList.add('open');
        opener.setAttribute('aria-expanded', 'true');
        input.tabIndex = 0;
        input.value = field.value;
        pointAll(field.value);
        if (location.hash === `#${field.id}`) input.focus();
      }
      // "Clear search" in the margin: the page's search is over, so the nav's is too.
      document.addEventListener('click', (event) => {
        if (event.target.closest('.margin-clear') && isPlainClick(event)) {
          input.value = '';
          close(true);
        }
      });
    }
  }

  // A page with a countdown shows what's newest (Today, the Archive, a tag's page), so it's stale once the next rite
  // is due: each countdown holds when, as of the page's render. A tab left open, or one the browser brings back from
  // its cache or wakes the next day, would show the day before under a countdown to tomorrow's. Looked up each
  // time: a search on the Archive brings in a new margin with its own.
  const dueAt = () => Math.min(...[...document.querySelectorAll('[data-countdown]')].map((element) => Date.parse(element.dataset.countdown)));
  const stale = () => Date.now() >= dueAt();
  // Reloaded when it's shown again, before it's read, but once for each page and due time: a clock running ahead,
  // or a copy the reload still finds cached, would otherwise reload it again and again, and one page's reload
  // mustn't stop another's (the Archive's, then Today's, brought back by Back). Without the storage to remember
  // that, it isn't reloaded, and the reader is told instead.
  const reloadIfStale = () => {
    if (!stale()) return false;
    const reloadedKey = `dms-reloaded:${location.pathname}${location.search}`;
    try {
      if (sessionStorage.getItem(reloadedKey) === String(dueAt())) return false;
      sessionStorage.setItem(reloadedKey, String(dueAt()));
    } catch { return false; }
    location.reload();
    return true;
  };
  // A reader looking at the page when the rite comes out is told, rather than have the page change under them,
  // with a link to Today: the Archive's first page leaves the newest rite out, and a later page or a search may.
  const tellOfNewRite = () => {
    if (toast.querySelector('.toast-new-rite')) return;
    clearTimeout(hideToast);
    toast.innerHTML = '';
    const pill = document.createElement('div');
    pill.className = 'toast toast-lasting toast-new-rite';
    pill.append('A new rite is out. ');
    const showIt = document.createElement('a');
    showIt.href = '/';
    showIt.textContent = 'Show it';
    pill.appendChild(showIt);
    toast.appendChild(pill);
  };
  // A computer that slept with the page in front says nothing when it wakes, so a long gap between ticks counts as
  // the page being shown again. Hidden, a tab's timers may slow to once a minute, well inside the five.
  let tickedAt = Date.now();
  const tick = () => {
    const asleep = Date.now() - tickedAt > 300000;
    tickedAt = Date.now();
    if (asleep && reloadIfStale()) return;
    if (stale()) {
      document.querySelectorAll('[data-countdown]').forEach((element) => { element.textContent = '0m'; });
      tellOfNewRite();
      return;
    }
    const now = new Date();
    const next = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1);
    const minutes = Math.ceil((next - now.getTime()) / 60000);
    const text = minutes >= 60 ? `${Math.floor(minutes / 60)}h ${minutes % 60}m` : `${minutes}m`;
    document.querySelectorAll('[data-countdown]').forEach((element) => { element.textContent = text; });
  };
  if (document.querySelector('[data-countdown]') && !reloadIfStale()) {
    // Ticked at once too: a cached page may have been rendered up to an hour ago.
    tick();
    setInterval(tick, 30000);
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') reloadIfStale(); });
    // A page the browser keeps whole (back, forward, a restored tab) runs no script again, but says it's shown.
    window.addEventListener('pageshow', (event) => { if (event.persisted) reloadIfStale(); });
  }

  // An Imperial date's day as we reckon it shows while the pointer is on it or it has focus (the stylesheet); a
  // tap shows or hides it too, for a touch screen, and a tap elsewhere hides it. Escape hides any, until the
  // pointer leaves or focus moves on (WCAG 1.4.13). Listened for on the document, since the Archive's search swaps
  // its margins, "Since" with them, for new ones.
  const dateOf = (target) => (target instanceof Element ? target.closest('.imperial') : null);
  const showing = (date) => date.matches('[data-open], :hover, :has(.imperial-date:focus-visible)') && !date.hasAttribute('data-dismissed');
  // Pressing a date shows its day, or hides it when it shows, whether a press, the pointer or focus showed it. A
  // touch screen keeps :hover after a tap, so there only the press itself counts.
  document.addEventListener('click', (event) => {
    const pressed = event.target instanceof Element && event.target.closest('.imperial-date') ? dateOf(event.target) : null;
    document.querySelectorAll('.imperial').forEach((date) => {
      if (date !== pressed) {
        // Dismissed, not only closed: a touch screen keeps :hover on the date tapped before.
        if (showing(date)) date.setAttribute('data-dismissed', '');
        date.removeAttribute('data-open');
      } else if (event.pointerType === 'touch' ? date.hasAttribute('data-open') : showing(date)) {
        date.removeAttribute('data-open');
        date.setAttribute('data-dismissed', '');
      } else {
        date.setAttribute('data-open', '');
        date.removeAttribute('data-dismissed');
      }
    });
  });
  document.addEventListener('focusout', (event) => {
    const date = dateOf(event.target);
    date?.removeAttribute('data-open');
    date?.removeAttribute('data-dismissed');
  });
  document.addEventListener('mouseout', (event) => {
    const date = dateOf(event.target);
    if (date && !date.contains(event.relatedTarget)) date.removeAttribute('data-dismissed');
  });
  // Only a day showing is dismissed: one hidden now must still show when focus or the pointer comes to it.
  document.addEventListener('keydown', (event) => {
    if (event.key !== 'Escape') return;
    document.querySelectorAll('.imperial').forEach((date) => {
      if (!showing(date)) return;
      date.removeAttribute('data-open');
      date.setAttribute('data-dismissed', '');
    });
  });

  // The rites the reader has witnessed (#198): those whose seal they broke, kept in this browser, and for a
  // signed-in reader with their account too (#215, below). The pages are the same for everyone, so the rows are
  // marked here, after loading, as the archive loads more of them.
  const witnessedKey = 'dms-witnessed-v1';
  const witnessed = () => {
    try { return new Set(JSON.parse(localStorage.getItem(witnessedKey) ?? '[]')); } catch { return new Set(); }
  };
  const keepWitnessed = (numbers) => {
    try { localStorage.setItem(witnessedKey, JSON.stringify([...numbers].sort((a, b) => a - b))); } catch { /* not kept, then */ }
  };
  // A witnessed row wears a broken seal at the end of its meta; the title's link says "witnessed" to a screen reader.
  const brokenSeal = '<svg class="witnessed-seal" aria-hidden="true" focusable="false" viewBox="0 0 24 24" width="20" height="20"><g transform="translate(-1.3 .5) rotate(-12 12 12)"><path d="M12 2A10 10 0 0 0 12 22L13 18L11 14L13 10L11 6Z" fill="#8f2a20"/><path d="M12 5.5A6.5 6.5 0 0 0 12 18.5" fill="none" stroke="#6b1c14" stroke-width="1.4"/></g><g transform="translate(1.4 1) rotate(9 12 12)"><path d="M12 2A10 10 0 0 1 12 22L13 18L11 14L13 10L11 6Z" fill="#8f2a20"/><path d="M12 5.5A6.5 6.5 0 0 1 12 18.5" fill="none" stroke="#6b1c14" stroke-width="1.4"/></g></svg>';
  const markWitnessed = () => {
    const known = witnessed();
    document.querySelectorAll('.rows > .row').forEach((row) => {
      const number = Number(row.querySelector('.row-title')?.getAttribute('href')?.split('/').pop());
      const was = row.classList.contains('witnessed');
      if (known.has(number) === was) return;
      row.classList.toggle('witnessed', !was);
      if (was) {
        row.querySelector('.witnessed-seal')?.remove();
        row.querySelector('.witnessed-said')?.remove();
      } else {
        row.querySelector('.row-meta').insertAdjacentHTML('beforeend', brokenSeal);
        row.querySelector('.row-title').insertAdjacentHTML('beforeend', '<span class="sr-only witnessed-said">, witnessed</span>');
      }
    });
    // Found again each time: the Archive's live search replaces its margins. Its text is changed only when it
    // differs, since every change is a mutation the observer below would answer again.
    // Two notes, the margin's and the narrow screen's line on the sheet, kept alike.
    const notes = document.querySelectorAll('[data-witnessed-note]');
    // A live search's results have no note, so no checkbox to undo the filter: it's dropped rather than hiding
    // witnessed matches unseen. The full Archive's note comes back unchecked.
    if (notes.length === 0) document.body.classList.remove('unwitnessed-only');
    notes.forEach((note) => {
      note.hidden = false;
      note.querySelectorAll('[data-witnessed]').forEach((count) => {
        if (count.textContent !== String(known.size)) count.textContent = String(known.size);
      });
      note.querySelector('[data-unwitnessed-only]').checked = document.body.classList.contains('unwitnessed-only');
      // Nothing to forget, nothing offered; nor while the note is asking.
      note.querySelector('[data-forget-ask]').hidden = known.size === 0 || !note.querySelector('[data-forget-question]').hidden;
    });
  };
  // A signed-in reader's list follows them to every browser they sign in on (#215): the account's list is the
  // truth, and this browser keeps a copy. A rite witnessed here waits as unsent until the account has it, so a
  // failed send is tried again on the next page. The account mark says the copy here is an account's: when the
  // reader signs out, or the sign-in runs out, it leaves this browser, so the next reader here doesn't inherit it.
  const unsentKey = 'dms-witnessed-unsent';
  const accountKey = 'dms-witnessed-account';
  const stored = (key) => { try { return localStorage.getItem(key); } catch { return null; } };
  const store = (key, value) => { try { localStorage.setItem(key, value); } catch { /* not kept, then */ } };
  const unstore = (key) => { try { localStorage.removeItem(key); } catch { /* nothing kept, then */ } };
  const unsent = () => { try { return JSON.parse(stored(unsentKey) ?? '[]'); } catch { return []; } };
  const signedIn = () => document.cookie.split('; ').includes('dms-signed-in=1');
  const leaveAccount = () => {
    if (stored(accountKey) === null) return;
    keepWitnessed([]);
    unstore(unsentKey);
    unstore(accountKey);
    markWitnessed();
  };
  // The account's list, or undefined when it couldn't be reached (signed out meanwhile, too).
  const witnessedApi = async (init) => {
    try {
      const response = await fetch('/api/witnessed', init);
      if (response.status === 401) leaveAccount();
      if (!response.ok) return undefined;
      return response.status === 204 ? [] : (await response.json()).rites;
    } catch {
      return undefined;
    }
  };
  // One sync or forget at a time, in the order asked: a reply can't undo a later one's, and forgetting comes after
  // any rite still being sent.
  let witnessedTurn = Promise.resolve();
  const inTurn = (task) => (witnessedTurn = witnessedTurn.then(task, task));
  // Sends what's waiting, a thousand at a time (the API's most), and takes the account's list, keeping any rite
  // witnessed while a request was on its way. On a browser's first sign-in, everything it remembered alone waits
  // to be sent too, so nothing is lost.
  const syncWitnessed = async () => {
    if (stored(accountKey) === null) store(unsentKey, JSON.stringify([...new Set([...unsent(), ...witnessed()])]));
    let rites;
    while (unsent().length > 0) {
      const batch = new Set(unsent().slice(0, 1000));
      rites = await witnessedApi({ method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rites: [...batch] }) });
      if (rites === undefined) return;
      const waiting = unsent().filter((number) => !batch.has(number));
      if (waiting.length > 0) store(unsentKey, JSON.stringify(waiting));
      else unstore(unsentKey);
    }
    rites ??= await witnessedApi({});
    if (rites === undefined) return;
    keepWitnessed(new Set([...rites, ...unsent()]));
    store(accountKey, '1');
    markWitnessed();
  };
  const sendWitnessed = () => inTurn(syncWitnessed);
  function witness(number) {
    if (!Number.isInteger(number) || number < 1) return;
    // Signed out while the page was open: an account's copy leaves first, so this rite stays as the browser's own.
    if (!signedIn()) leaveAccount();
    const known = witnessed();
    known.add(number);
    keepWitnessed(known);
    if (!signedIn()) return;
    store(unsentKey, JSON.stringify([...new Set([...unsent(), number])]));
    sendWitnessed();
  }
  // On arriving: a signed-out browser drops an account's copy; a signed-in one syncs where the list is shown (rows),
  // or when something waits to be sent; a page without rows needs no list.
  const arrive = () => {
    if (!signedIn()) leaveAccount();
    else if (document.querySelector('.rows') || unsent().length > 0 || stored(accountKey) === null) sendWitnessed();
  };
  markWitnessed();
  arrive();
  // Rows come later too: the Archive's next pages, and its live search (even from a search that found none).
  const rowLists = document.querySelector('main');
  if (rowLists) new MutationObserver(markWitnessed).observe(rowLists, { childList: true, subtree: true });
  // A page the browser kept on going back may predate the seal just broken, or a Forget or sign-out elsewhere.
  addEventListener('pageshow', (event) => {
    if (!event.persisted) return;
    markWitnessed();
    arrive();
  });
  // From the document, as the controls are replaced with the margins.
  document.addEventListener('change', (event) => {
    if (!event.target.matches?.('[data-unwitnessed-only]')) return;
    document.body.classList.toggle('unwitnessed-only', event.target.checked);
    // The other note's checkbox follows, for a window resized across the margins' width.
    markWitnessed();
  });
  // Forgetting asks first, in the note that was pressed. Keep (or Esc) goes back to "Forget them…"; Forget goes to
  // the checkbox, since "Forget them…" is hidden once there's nothing to forget.
  const closeForget = (note, backToAsk) => {
    note.querySelector('[data-forget-question]').hidden = true;
    const ask = note.querySelector('[data-forget-ask]');
    ask.hidden = witnessed().size === 0;
    if (backToAsk) ask.focus();
  };
  document.addEventListener('click', async (event) => {
    const note = event.target.closest?.('[data-witnessed-note]');
    if (!note) return;
    if (event.target.closest('[data-forget-ask]')) {
      note.querySelector('[data-forget-ask]').hidden = true;
      note.querySelector('[data-forget-question]').hidden = false;
      note.querySelector('[data-forget-keep]').focus();
    } else if (event.target.closest('[data-forget-keep]')) {
      closeForget(note, true);
    } else if (event.target.closest('[data-forget-witnessed]')) {
      // A signed-in reader's account forgets first, so every browser they sign in on follows; if it can't, this
      // one keeps them too, rather than getting them back on the next page. In turn, after any rite being sent.
      const forgotten = await inTurn(async () => {
        if (signedIn() && await witnessedApi({ method: 'DELETE' }) === undefined) return false;
        unstore(unsentKey);
        keepWitnessed([]);
        return true;
      });
      if (!forgotten) {
        closeForget(note, true);
        say('They couldn’t be forgotten. Try again.');
        return;
      }
      closeForget(note, false);
      markWitnessed();
      note.querySelector('[data-unwitnessed-only]').focus();
      say('Forgotten. Every rite is unwitnessed again.');
    }
  });
  document.addEventListener('keydown', (event) => {
    if (event.key !== 'Escape') return;
    const note = event.target.closest?.('[data-forget-question]')?.closest('[data-witnessed-note]');
    if (note) closeForget(note, true);
  });
  // Drawing a rite (#196): chosen here, from the search index Cloudflare keeps, so every page stays the same for
  // everyone and no request says what was drawn. Never the rite shown, nor one drawn this session until every one
  // has been. A failure falls back to /draw, which draws on the server.
  const drawnKey = 'dms-drawn';
  const drawnSoFar = () => {
    try { return JSON.parse(sessionStorage.getItem(drawnKey) ?? '[]'); } catch { return []; }
  };
  document.addEventListener('click', async (event) => {
    const draw = event.target.closest?.('a[href="/draw"]');
    if (!draw || event.button !== 0 || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
    event.preventDefault();
    try {
      // A signed-in reader's list is brought up to date meanwhile (#215): a page without rows didn't fetch it, and
      // rites read on another device count too. A failed sync leaves the copy as it was.
      const [response] = await Promise.all([fetch('/api/search-index'), signedIn() ? sendWitnessed() : undefined]);
      if (!response.ok) throw new Error(String(response.status));
      const shown = Number(document.querySelector('[data-rite]')?.dataset.rite);
      const all = (await response.json()).map((rite) => rite.number).filter((number) => number !== shown);
      let drawn = drawnSoFar();
      let left = all.filter((number) => !drawn.includes(number));
      if (left.length === 0) { drawn = []; left = all; }
      if (left.length === 0) { location.href = draw.href; return; }
      // A rite the reader hasn't witnessed (#198) first; once they've witnessed them all, any.
      const known = witnessed();
      const unwitnessed = left.filter((number) => !known.has(number));
      const lots = unwitnessed.length > 0 ? unwitnessed : left;
      const number = lots[Math.floor(Math.random() * lots.length)];
      try { sessionStorage.setItem(drawnKey, JSON.stringify([...drawn, number])); } catch { /* drawn again, then */ }
      location.href = `/r/${number}?drawn`;
    } catch {
      location.href = draw.href;
    }
  });
  // Arriving by a draw, the lots are announced, and the address drops ?drawn, so a link shared from here is the rite's.
  if (new URLSearchParams(location.search).has('drawn')) {
    say(`The lots have fallen · No. ${location.pathname.split('/').pop()}`);
    history.replaceState(null, '', location.pathname + location.hash);
  }
  // The rites before and after (#195): the arrow keys and a swipe follow the head's rel=prev and rel=next, as the
  // links under a rite do. Not while typing or choosing in a field, nor with a modifier (the browser's own keys).
  const stepTo = (rel) => document.querySelector(`link[rel=${rel}]`)?.href;
  document.addEventListener('keydown', (event) => {
    if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
    if (event.target.closest?.('input, textarea, select, [contenteditable], [role=combobox], [role=listbox]')) return;
    const to = event.key === 'ArrowLeft' ? stepTo('prev') : event.key === 'ArrowRight' ? stepTo('next') : undefined;
    if (to) location.href = to;
  });
  // A swipe in something the reader can scroll sideways is that thing's own; what only clips (the frame, around the
  // rite's glow) doesn't count.
  const scrollsSideways = (element) => {
    for (let at = element; at && at !== document.body; at = at.parentElement) {
      if (at.scrollWidth > at.clientWidth && ['auto', 'scroll'].includes(getComputedStyle(at).overflowX)) return true;
    }
    return false;
  };
  let touchStart;
  document.addEventListener('touchstart', (event) => {
    touchStart = event.touches.length === 1 && !scrollsSideways(event.target) ? event.touches[0] : undefined;
  }, { passive: true });
  document.addEventListener('touchend', (event) => {
    if (!touchStart) return;
    const across = event.changedTouches[0].clientX - touchStart.clientX;
    const down = event.changedTouches[0].clientY - touchStart.clientY;
    touchStart = undefined;
    // A long, mostly sideways swipe, as a page is turned: a scroll or a tap never counts. No slide, so reduced
    // motion has nothing to stop.
    if (Math.abs(across) < 80 || Math.abs(across) < 2 * Math.abs(down)) return;
    const to = stepTo(across > 0 ? 'prev' : 'next');
    if (to) location.href = to;
  }, { passive: true });
})();
