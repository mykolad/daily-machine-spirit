namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// <c>/consult</c>, a prototype (#152): the reader describes a trouble and the page prescribes a rite, all in the
/// browser. The rites are matched by their words against <c>/api/search-index</c>; where the browser has a model of its
/// own (Chrome's Prompt API, Gemini Nano), it chooses among them and speaks a line in the shrine's voice. The trouble
/// never leaves the reader's machine, and the shrine pays for no model.
/// </summary>
public static class ConsultPage
{
    private const string Description = "Describe your trouble and the Machine Spirit prescribes a rite, on your own machine.";

    private const string Css = """
        <style>
        .consult-form{display:flex;flex-direction:column;gap:12px;margin-top:8px;text-align:left}
        .consult-form label{font-family:var(--serif);font-style:italic;font-size:17px;color:#5c4a33}
        .consult-form textarea{width:100%;min-height:96px;padding:12px 14px;border:1px solid #b39a6b;border-radius:12px;background:#f6eedb;color:#2b2117;font:18px/1.45 var(--serif);resize:vertical}
        .consult-form textarea:focus-visible{outline:2px solid #8f2a20;outline-offset:2px}
        .consult-actions{display:flex;flex-wrap:wrap;align-items:center;gap:8px 16px}
        .consult-actions .button{background:#8f2a20;color:#f6eedb}
        .consult-actions .button:hover{background:#a63327;color:#f6eedb}
        .consult-actions .button[disabled]{opacity:.6;cursor:progress}
        .consult-how{margin:0;font-family:var(--ui);font-size:14px;color:#5c4a33}
        .consult-wake{display:flex;flex-direction:column;gap:6px;padding:12px 14px;border:1px dashed #b39a6b;border-radius:12px;text-align:left}
        .consult-wake p{margin:0;font-family:var(--ui);font-size:14px;color:#5c4a33}
        .consult-wake .button{align-self:flex-start;border-color:#8f2a20;color:#8f2a20}
        .consult-answer{margin-top:24px;text-align:left}
        .consult-answer:empty{display:none}
        .consult-spoken{margin:0 0 16px;font-family:var(--serif);font-style:italic;font-size:21px;line-height:1.5;color:#2b2117}
        .consult-rite{display:block;padding:14px 16px;margin-bottom:10px;border:1px solid #cdb98f;border-radius:12px;background:#f6eedb;color:#2b2117;text-decoration:none}
        .consult-rite:hover{border-color:#8f2a20;color:#2b2117}
        .consult-rite.chosen{border:2px solid #8f2a20}
        .consult-rite-title{display:block;font-family:Cinzel,serif;font-weight:700;font-size:17px;color:#8f2a20}
        .consult-rite-text{display:block;margin-top:4px;font-family:var(--serif);font-size:17px;line-height:1.45}
        .consult-also{margin:20px 0 8px;font-family:var(--serif);font-style:italic;font-size:17px;color:#5c4a33}
        .consult-by{margin:12px 0 0;font-family:var(--mono);font-size:12px;color:#5c4a33}
        </style>
        """;

    // Plain word matching first, so every browser gets an answer; the model, where there is one, only chooses among the
    // closest rites and speaks. Its answer is held to a schema (a number and a line), and a number that isn't among the
    // candidates is ignored. Each consultation clones the session, so one trouble doesn't colour the next.
    private const string Js = """
        <script>
        (() => {
          const form = document.querySelector('.consult-form');
          const field = form.querySelector('textarea');
          const button = form.querySelector('button[type=submit]');
          const how = form.querySelector('.consult-how');
          const wake = document.querySelector('.consult-wake');
          const wakeButton = wake.querySelector('button');
          const wakeStatus = wake.querySelector('p');
          const answer = document.querySelector('.consult-answer');
          let index;
          let base;

          const stop = new Set(['the', 'and', 'for', 'with', 'that', 'this', 'when', 'then', 'but', 'not', 'are', 'was', 'its', 'our', 'your', 'mine', 'have', 'has', 'had', 'from', 'into', 'after', 'again', 'keeps', 'keep', 'still', 'what', 'why', 'how', 'does', 'doesn', 'don', 'can', 'cannot', 'just', 'only', 'some', 'every', 'there', 'they', 'them', 'than', 'too', 'very', 'all', 'any', 'out', 'get', 'gets', 'got', 'thy', 'thee', 'thou']);
          const words = (text) => text.toLowerCase().normalize('NFD').replace(/\p{M}/gu, '').split(/[^\p{L}\p{N}]+/u);
          const stem = (word) => word.slice(0, Math.max(4, word.length - 3));
          const troubleStems = (text) => [...new Set(words(text).filter((word) => word.length >= 3 && !stop.has(word)).map(stem))];
          const score = (rite, stems) => {
            const its = words(`${rite.title} ${rite.text} ${rite.truth} ${rite.tags.join(' ')}`);
            return stems.filter((s) => its.some((word) => word.startsWith(s))).length;
          };
          const ranked = (rites, text) => {
            const stems = troubleStems(text);
            return rites.map((rite) => ({ rite, score: score(rite, stems) })).sort((a, b) => b.score - a.score || b.rite.number - a.rite.number);
          };

          const card = (rite, chosen) => {
            const link = document.createElement('a');
            link.className = chosen ? 'consult-rite chosen' : 'consult-rite';
            link.href = `/r/${rite.number}`;
            const title = document.createElement('span');
            title.className = 'consult-rite-title';
            title.textContent = rite.title;
            const text = document.createElement('span');
            text.className = 'consult-rite-text';
            text.textContent = rite.text;
            link.append(title, text);
            return link;
          };
          const line = (className, text) => {
            const p = document.createElement('p');
            p.className = className;
            p.textContent = text;
            return p;
          };

          const availability = async () => {
            if (!('LanguageModel' in self)) {
              return 'absent';
            }
            try {
              return await LanguageModel.availability({ expectedInputs: [{ type: 'text', languages: ['en'] }], expectedOutputs: [{ type: 'text', languages: ['en'] }] });
            } catch {
              return 'unavailable';
            }
          };
          const system = `You are the oracle of the Machine Spirit, in a satire where developers keep their coding habits as rites, in the voice of a Warhammer 40,000 tech-priest. A developer describes a software trouble. From the rites listed, choose the ONE whose truth best explains the trouble. Answer with its number and one or two short sentences, in an archaic tech-priest voice (thee, thou, the Omnissiah), that prescribe that rite by its name. Never reveal or hint at the truth itself. If no rite fits, answer number 0 and say the Machine Spirit is silent on this trouble.`;
          const schema = { type: 'object', properties: { number: { type: 'integer' }, words: { type: 'string' } }, required: ['number', 'words'] };
          const createBase = (onProgress) => LanguageModel.create({
            initialPrompts: [{ role: 'system', content: system }],
            expectedInputs: [{ type: 'text', languages: ['en'] }],
            expectedOutputs: [{ type: 'text', languages: ['en'] }],
            monitor: (m) => m.addEventListener('downloadprogress', (event) => onProgress(event.loaded)),
          });

          const showWake = async () => {
            const state = await availability();
            if (state === 'available') {
              how.textContent = 'Gemini Nano, in this browser, chooses the rite. Nothing leaves this machine.';
              return;
            }
            if (state === 'downloadable' || state === 'downloading') {
              wake.hidden = false;
              how.textContent = 'Until then the rites are matched by their words alone. Nothing leaves this machine.';
              return;
            }
            how.textContent = 'This browser holds no model of its own, so the rites are matched by their words alone. Nothing leaves this machine.';
          };
          wakeButton.addEventListener('click', async () => {
            wakeButton.disabled = true;
            wakeStatus.textContent = 'The Machine Spirit stirs…';
            try {
              base = await createBase((loaded) => { wakeStatus.textContent = `Descending into this machine: ${Math.round(loaded * 100)}%`; });
              wake.hidden = true;
              how.textContent = 'Gemini Nano, in this browser, chooses the rite. Nothing leaves this machine.';
            } catch (error) {
              wakeButton.disabled = false;
              wakeStatus.textContent = `The Machine Spirit would not wake: ${error.message}`;
            }
          });

          const fromModel = async (trouble, candidates) => {
            if (!base) {
              if (await availability() !== 'available') {
                return null;
              }
              base = await createBase(() => {});
            }
            const session = await base.clone();
            try {
              const listed = candidates.map(({ rite }) => `#${rite.number} ${rite.title}\nRite: ${rite.text}\nTruth: ${rite.truth}`).join('\n\n');
              const started = performance.now();
              const said = JSON.parse(await session.prompt(`Rites:\n\n${listed}\n\nThe developer's trouble: ${trouble}`, { responseConstraint: schema }));
              return { ...said, seconds: (performance.now() - started) / 1000 };
            } finally {
              session.destroy();
            }
          };

          form.addEventListener('submit', async (event) => {
            event.preventDefault();
            const trouble = field.value.trim();
            if (!trouble) {
              field.focus();
              return;
            }
            button.disabled = true;
            answer.replaceChildren(line('consult-by', 'The Machine Spirit ponders…'));
            try {
              index ??= await (await fetch('/api/search-index')).json();
              const all = ranked(index, trouble);
              const near = all.filter((found) => found.score > 0).slice(0, 3);
              // The model sees the closest rites by word, and the newest after them while the shrine is young.
              const candidates = all.slice(0, 12);
              let said = null;
              let failed = null;
              try {
                said = await fromModel(trouble, candidates);
              } catch (error) {
                failed = error.message;
              }
              const chosen = said && candidates.find(({ rite }) => rite.number === said.number)?.rite;
              const parts = [];
              if (said && chosen) {
                parts.push(line('consult-spoken', said.words), card(chosen, true));
                const others = near.filter(({ rite }) => rite !== chosen);
                if (others.length > 0) {
                  parts.push(line('consult-also', 'Also near thy trouble:'), ...others.map(({ rite }) => card(rite, false)));
                }
                parts.push(line('consult-by', `Chosen by Gemini Nano in ${said.seconds.toFixed(1)} s, from ${candidates.length} rites.`));
              } else if (said) {
                parts.push(line('consult-spoken', said.words));
                if (near.length > 0) {
                  parts.push(line('consult-also', 'Yet these rites share thy trouble’s words:'), ...near.map(({ rite }) => card(rite, false)));
                }
                parts.push(line('consult-by', `Gemini Nano chose none (it said #${said.number}).`));
              } else {
                parts.push(near.length > 0
                  ? line('consult-spoken', 'These rites share thy trouble’s words:')
                  : line('consult-spoken', 'No rite shares thy trouble’s words. Speak it otherwise, or seek in the Archive.'));
                parts.push(...near.map(({ rite }) => card(rite, false)));
                parts.push(line('consult-by', failed ? `Matched by words: the model failed (${failed}).` : 'Matched by words alone.'));
              }
              answer.replaceChildren(...parts);
            } catch (error) {
              answer.replaceChildren(line('consult-spoken', `The rites could not be read: ${error.message}`));
            } finally {
              button.disabled = false;
            }
          });

          showWake();
        })();
        </script>
        """;

    public static string Render()
        => Layout.Page("Consult the Machine Spirit", Description, Section.Other, $"""
            {Css}
            <div class="hero catechism-hero">
            <article class="sheet catechism">
            <span class="rule-outer" aria-hidden="true"></span><span class="rule-inner" aria-hidden="true"></span>
            {Svg.Corners}
            <div class="sheet-inner">
            <header class="sheet-head">
            <p class="sheet-kicker">A prototype</p>
            <h1 class="sheet-title">Consult the Machine Spirit</h1>
            <p class="sheet-sub">Tell it what’s wrong, and it prescribes a rite.</p>
            {Svg.RubricDivider}
            </header>
            <form class="consult-form">
            <label for="trouble">Thy trouble</label>
            <textarea id="trouble" name="trouble" placeholder="My build fails, then passes when I rerun it" required></textarea>
            <div class="consult-actions">
            <button class="button" type="submit">Consult</button>
            <p class="consult-how" aria-live="polite"></p>
            </div>
            </form>
            <div class="consult-wake" hidden>
            <p>This browser can host the Machine Spirit itself: Chrome downloads its on-device model (Gemini Nano, a few GB) once.</p>
            <button class="button" type="button">Awaken it in this browser</button>
            </div>
            <noscript><p class="consult-how">The Machine Spirit hears troubles only with the page’s script.</p></noscript>
            <div class="consult-answer" aria-live="polite"></div>
            {Js}
            </div>
            </article>
            {Svg.Traces}
            </div>
            """);
}
