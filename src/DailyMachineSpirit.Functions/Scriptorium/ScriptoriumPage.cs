using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Scriptorium's HTML. Plain forms: every action is a button that posts, so it works with a keyboard and a screen
/// reader as it is, and without a script. <see cref="ScriptoriumScript"/> only shows the change in place. The colours and type follow the site's design tokens (docs: the design handoff).
/// </summary>
public static partial class ScriptoriumPage
{
    // What the status says while the Machine Spirit works on a slow action. Distilling's button has a hint beside it that
    // says how long it takes; rewriting's doesn't, so its message does.
    private const string Rewriting = "The Machine Spirit is rewriting it… usually a few seconds, at most a minute and a half.";
    private const string Distilling = "The Machine Spirit is distilling the Precepts…";

    private const string Styles = """
        :root { color-scheme: dark; }
        * { box-sizing: border-box; }
        body { margin: 0; background: #100e0b; color: #ede3cf; font: 16px/1.5 Figtree, system-ui, sans-serif; }
        .frame { overflow-x: clip; }
        header, main { max-width: 820px; margin: 0 auto; padding: 28px clamp(16px, 4vw, 40px) 0; }
        main { padding-bottom: 72px; }
        footer { max-width: 600px; margin: 0 auto; padding: 0 16px 48px; text-align: center; }
        .motto { margin: 0 0 8px; font: 700 16px Cinzel, serif; letter-spacing: .08em; color: #c9a25a; }
        .disclaimer { margin: 0; font: 400 14px/1.6 Figtree, sans-serif; color: #b6a98f; }
        .brand { display: inline-flex; align-items: center; gap: 10px; padding: 4px 8px 4px 0; font: 700 16px Cinzel, serif; letter-spacing: .04em; color: #c9a25a; text-decoration: none; }
        .brand:hover { color: #f0d59a; }
        .masthead { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px 24px; }
        .identity { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 14px; margin: 0; font-size: 14px; color: #b6a98f; }
        .identity strong { font-weight: 600; color: #ede3cf; }
        .tabs ul { list-style: none; display: flex; flex-wrap: wrap; gap: 0 8px; margin: 16px 0 0; padding: 0; border-bottom: 1px solid rgba(237,227,207,.16); }
        .tabs a { display: inline-flex; align-items: baseline; gap: 8px; white-space: nowrap; min-height: 44px; padding: 12px 10px 9px; margin-bottom: -1px; border-bottom: 3px solid transparent; font: 600 16px/1.2 Figtree, sans-serif; color: #cdbd9e; text-decoration: none; }
        .tabs a:hover { color: #f0d59a; border-bottom-color: rgba(201,162,90,.4); }
        .tabs a[aria-current=page] { color: #ede3cf; border-bottom-color: #c9a25a; }
        .tabs .count { font: 500 13px "JetBrains Mono", monospace; color: #b6a98f; }
        h1 { font: 700 clamp(30px, 4.4vw, 46px)/1.1 Cinzel, serif; margin: 32px 0 8px; }
        h2 { font: 700 26px/1.2 Cinzel, serif; margin: 0; }
        h3 { font: 700 21px/1.2 Cinzel, serif; margin: 6px 0 8px; }
        .lede { font: italic 400 20px/1.4 "EB Garamond", serif; color: #cdbd9e; margin: 0 0 16px; max-width: 62ch; }
        .status:not(:empty) { background: #1a1612; border-left: 4px solid #c9a25a; padding: 12px 16px; border-radius: 12px; }
        section { margin-top: 44px; }
        .section-head { display: flex; flex-wrap: wrap; gap: 12px 24px; align-items: center; justify-content: space-between; margin-bottom: 8px; }
        .actions, .buttons { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
        .buttons .burn { margin-left: auto; }
        .pair { display: inline-flex; }
        .pair button { border-radius: 0; }
        .pair button:first-child { border-radius: 999px 0 0 999px; }
        .pair button:last-child { border-radius: 0 999px 999px 0; }
        .pair button:only-child { border-radius: 999px; }
        .pair button + button { margin-left: -1px; }
        .pair button:focus-visible { position: relative; z-index: 1; }
        .actions form { margin: 0; }
        .calendar, .judgments { list-style: none; padding: 0; margin: 16px 0 0; display: grid; gap: 16px; }
        .suggestions { list-style: none; padding: 0; margin: 12px 0 0; display: flex; flex-wrap: wrap; gap: 8px; }
        .suggest-others { margin: 16px 0 0; }
        .suggest-others .button-link { border-style: dashed; }
        .rite { background: #1a1612; border: 1px solid rgba(237,227,207,.1); border-radius: 20px; padding: 20px clamp(16px, 3vw, 24px); scroll-margin-top: 16px; }
        .ash { opacity: .92; }
        .acted, .row.arrived { border-color: #f0b45a; animation: acted 1.4s ease-out; }
        @keyframes acted { from { box-shadow: 0 0 0 4px rgba(240, 180, 90, .45); } to { box-shadow: 0 0 0 4px rgba(240, 180, 90, 0); } }
        .filters { display: flex; align-items: flex-start; gap: 12px; margin: 18px 0 0; }
        .subjects { display: flex; flex-wrap: wrap; gap: 6px; margin: 0; }
        .every { margin-left: auto; flex: none; }
        .chip { display: inline-flex; align-items: baseline; gap: 6px; white-space: nowrap; min-height: 34px; padding: 5px 12px; border-radius: 999px; border: 1px solid rgba(201,162,90,.45); font: 600 14px/1.4 Figtree, sans-serif; color: #d9b874; text-decoration: none; }
        .chip:hover { border-color: #c9a25a; color: #f0d59a; }
        .chip[aria-current] { background: #c9a25a; border-color: #c9a25a; color: #100e0b; }
        .chip-count { font: 500 12px "JetBrains Mono", monospace; opacity: .8; }
        .group { margin-top: 32px; }
        .group-head { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 12px; padding-bottom: 10px; }
        .group-title { display: inline-flex; align-items: center; gap: 8px; margin: 0; font: 700 13px Cinzel, serif; letter-spacing: .16em; text-transform: uppercase; color: #d9b874; }
        .augury-group .group-title { color: #8cc4ae; }
        .group-title .icon { width: 16px; height: 16px; }
        .group-count { font: 500 13px "JetBrains Mono", monospace; letter-spacing: 0; color: #b6a98f; }
        .group-hint { margin: 0; font-size: 14px; color: #b6a98f; }
        .empty { margin: 0; padding: 16px 20px; border: 1px dashed rgba(237,227,207,.16); border-radius: 16px; font-size: 15px; color: #b6a98f; }
        .rites { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
        .row { padding: 0; overflow: hidden; }
        .row.scribes { border-color: rgba(201,162,90,.32); }
        .row.next { border: 2px solid #c9a25a; }
        .row.acted { border-color: #f0b45a; }
        .row form { margin: 0; }
        .next-head { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 4px 16px; margin: 0; padding: 10px 20px; background: #241c12; border-bottom: 1px solid rgba(201,162,90,.25); }
        .next-label { font: 700 12px Cinzel, serif; letter-spacing: .2em; text-transform: uppercase; color: #c9a25a; }
        .next-when { font: 500 13px "JetBrains Mono", monospace; color: #8cc4ae; }
        .row-head { display: flex; flex-wrap: wrap; align-items: center; gap: 10px 16px; padding: 12px 12px 12px 20px; }
        [data-folding] .row-head { cursor: pointer; }
        [data-folding] .row-head:hover { background: rgba(237,227,207,.03); }
        .where { display: flex; align-items: center; gap: 12px; flex: none; margin: 0; }
        .place { width: 40px; font: 700 20px/1 Cinzel, serif; color: #b6a98f; }
        .scribes .place { color: #c9a25a; }
        .day { display: grid; gap: 2px; width: 88px; }
        .day time { font: 500 13px "JetBrains Mono", monospace; color: #cdbd9e; white-space: nowrap; }
        .kind { font: 700 11px Cinzel, serif; letter-spacing: .14em; text-transform: uppercase; color: #b6a98f; }
        .heading { flex: 1 1 240px; min-width: 0; display: grid; gap: 6px; }
        .row h4 { margin: 0; font: 700 18px/1.25 Cinzel, serif; color: #ede3cf; text-wrap: pretty; }
        .row .tags { margin: 0; }
        .row .tag { padding: 0 10px; font-size: 13px; line-height: 1.6; }
        .cluster { display: flex; align-items: center; gap: 12px; margin-left: auto; flex: none; }
        .score { display: grid; justify-items: end; gap: 1px; }
        .score-label { font: 700 10px Cinzel, serif; letter-spacing: .16em; text-transform: uppercase; color: #b6a98f; }
        .score-value { font: 500 14px "JetBrains Mono", monospace; color: #8cc4ae; }
        .step, .expand { justify-content: center; width: 44px; height: 44px; min-height: 44px; padding: 0; background: transparent; }
        .step { color: #ede3cf; border-color: rgba(237,227,207,.22); }
        .step:hover:not(:disabled) { background: rgba(237,227,207,.08); }
        .pair .step:disabled { opacity: .3; cursor: default; }
        .expand { border: 0; color: #cdbd9e; }
        .expand:hover { background: rgba(237,227,207,.08); color: #f0d59a; }
        .expand .icon { width: 20px; height: 20px; transition: transform .2s; }
        .expand[aria-expanded=true] .icon { transform: rotate(180deg); }
        .body { display: grid; gap: 12px; padding: 4px 20px 20px; }
        .body > p { margin: 0; }
        .note-fold summary { display: inline-flex; align-items: center; gap: 6px; padding: 6px 0; font: 600 15px Figtree, sans-serif; color: #cdbd9e; cursor: pointer; list-style: none; }
        .note-fold summary::-webkit-details-marker { display: none; }
        .note-fold summary:hover { color: #f0d59a; }
        .note-fold summary .icon { width: 16px; height: 16px; }
        [data-folding] .note-fold[open] summary { display: none; }
        .note-fold label { display: block; margin: 0 0 6px; font-size: 15px; font-weight: 600; color: #cdbd9e; }
        .note-fold .optional { font-weight: 400; }
        .burn-panel { display: grid; gap: 12px; padding: 16px; background: #1f1210; border: 1px solid #5a2a22; border-radius: 16px; }
        .why, .reasons { margin: 0; }
        .why { font-weight: 600; color: #f6cfc6; }
        .reasons { display: flex; flex-wrap: wrap; gap: 6px; }
        .reason { min-height: 36px; padding: 6px 14px; font-size: 14px; background: transparent; color: #f6cfc6; border-color: #8a5a50; }
        .reason:hover { background: rgba(246,207,198,.08); }
        .reason[aria-pressed=true] { background: #d0604d; border-color: #d0604d; color: #100e0b; }
        .burn-panel textarea { margin: 0; border-color: #8a5a50; }
        .burn-buttons { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 12px; }
        .cancel { padding: 10px 16px; background: transparent; color: #cdbd9e; }
        .cancel:hover { background: rgba(237,227,207,.08); }
        .burn-help { flex: 1 1 220px; font-size: 14px; color: #b6a98f; }
        .more { font-weight: 600; }
        .recent { list-style: none; margin: 14px 0 0; padding: 6px 0; background: #1a1612; border-radius: 20px; }
        .recent li { display: grid; grid-template-columns: 20px 84px minmax(0, 1fr); gap: 4px 14px; align-items: baseline; padding: 10px 20px; }
        .recent p { margin: 0; }
        .recent time { font: 500 13px "JetBrains Mono", monospace; color: #b6a98f; white-space: nowrap; }
        .recent cite { font-style: normal; font-weight: 600; }
        .verb { color: #cdbd9e; }
        .judgment-icon { align-self: center; display: grid; color: #b6a98f; }
        .judgment-icon.burned { color: #d0604d; }
        .judgment-icon.anointed { color: #c9a25a; }
        .toasts { position: fixed; left: 50%; bottom: 24px; transform: translateX(-50%); z-index: 10; max-width: calc(100vw - 32px); }
        .toast { display: flex; align-items: center; gap: 16px; padding: 10px 10px 10px 20px; background: #241c12; border: 1px solid rgba(201,162,90,.4); border-radius: 999px; box-shadow: 0 12px 40px rgba(0,0,0,.5); font-size: 15px; }
        .toast form { margin: 0; }
        .undo { min-height: 44px; padding: 8px 16px 8px 12px; font-size: 15px; background: transparent; color: #f0d59a; border-color: rgba(237,227,207,.22); }
        .undo:hover { background: rgba(237,227,207,.08); }
        .undo .icon { width: 16px; height: 16px; }
        .kicker { margin: 0; font: 700 13px Cinzel, serif; letter-spacing: .16em; text-transform: uppercase; color: #c9a25a; }
        .placed { color: #f0b45a; }
        .resembles { margin: 0 0 12px; padding: 8px 12px; border-left: 4px solid #f0b45a; background: #241c12; border-radius: 8px; color: #ede3cf; }
        .text { font: 400 19px/1.55 "EB Garamond", serif; margin: 0 0 12px; }
        .truth { background: #141c1f; border: 1px solid #2e4248; border-radius: 16px; padding: 12px 16px; color: #dbe6e8; margin: 0 0 12px; }
        .truth-label { display: block; font: 500 14px "JetBrains Mono", monospace; letter-spacing: .14em; text-transform: uppercase; color: #8cc4ae; }
        code { font-family: "JetBrains Mono", monospace; font-size: .9em; background: #0b1214; color: #f0d58c; padding: 1px 6px; border-radius: 6px; }
        .text code { background: #2b2117; color: #f0d58c; }
        .meta, .when, .optional { color: #b6a98f; }
        .meta { margin: 0 0 12px; font-size: 15px; }
        .tags { display: flex; flex-wrap: wrap; gap: 6px; margin: 0 0 12px; }
        .tag { white-space: nowrap; padding: 2px 12px; border-radius: 999px; border: 1px solid rgba(201,162,90,.45); font: 600 14px/1.5 Figtree, sans-serif; color: #d9b874; }
        .fold summary { cursor: pointer; padding: 10px 0; font-weight: 600; color: #cdbd9e; }
        .fold summary .icon { width: 16px; height: 16px; vertical-align: -2px; margin: 0 4px; }
        .amend { max-width: 740px; }
        .amend label { display: block; margin: 20px 0 6px; font-weight: 600; }
        .hint { margin: 0 0 8px; font-size: 15px; color: #b6a98f; }
        .amend input.changed[type=text], .amend textarea.changed { border-color: #f0b45a; }
        .changed-mark { font-weight: 400; color: #f0b45a; }
        .amend .buttons { margin-top: 20px; }
        .amend input[type=text], textarea { width: 100%; background: #100e0b; color: #ede3cf; border: 1px solid #b6a98f; border-radius: 12px; padding: 8px 12px; font: 16px/1.4 Figtree, sans-serif; margin-bottom: 8px; }
        button { display: inline-flex; align-items: center; gap: 8px; min-height: 46px; padding: 10px 18px 10px 14px; border-radius: 999px; font: 600 16px/1.2 Figtree, sans-serif; cursor: pointer; border: 1px solid transparent; }
        .icon { width: 18px; height: 18px; flex: none; fill: none; stroke: currentColor; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }
        .primary { background: #c9a25a; color: #100e0b; }
        .button-link { display: inline-flex; align-items: center; gap: 8px; min-height: 46px; padding: 10px 18px 10px 14px; border-radius: 999px; border: 1px solid rgba(237,227,207,.22); font: 600 16px/1.2 Figtree, sans-serif; color: #ede3cf; text-decoration: none; }
        .button-link:hover { background: rgba(237,227,207,.08); border-color: rgba(237,227,207,.4); color: #ede3cf; }
        .primary:hover { background: #d6b170; }
        .outline, .secondary { background: transparent; color: #ede3cf; border-color: rgba(237,227,207,.22); }
        .outline:hover, .secondary:hover { background: rgba(237,227,207,.08); border-color: rgba(237,227,207,.4); }
        .danger { background: #3b1814; color: #f6cfc6; border-color: #d0604d; }
        .danger:hover { background: #4a1f19; }
        button:disabled { opacity: .55; cursor: not-allowed; }
        form[data-busy] button:disabled { cursor: progress; }
        .judgments li { background: #1a1612; border-radius: 12px; padding: 10px 14px; }
        .record { list-style: none; padding: 0; margin: 16px 0 0; display: grid; gap: 8px; }
        .record li { background: #1a1612; border-radius: 12px; padding: 10px 14px; }
        .record time { display: block; font: 500 13px "JetBrains Mono", monospace; color: #b6a98f; }
        .record-link { margin: 0 0 16px; }
        .agreement { width: 100%; border-collapse: collapse; margin-top: 12px; font-size: 15px; }
        .agreement th, .agreement td { text-align: left; vertical-align: top; padding: 8px 10px 8px 0; border-bottom: 1px solid rgba(237,227,207,.12); }
        .agreement thead th { font-weight: 600; color: #b6a98f; }
        .judgments cite { font-style: normal; font-weight: 600; }
        .note { display: block; font: italic 18px/1.4 "EB Garamond", serif; color: #cdbd9e; }
        a { color: #d9b874; }
        a:hover { color: #f0d59a; }
        :focus-visible { outline: 2px solid #f0b45a; outline-offset: 3px; }
        .skip { position: absolute; left: -9999px; }
        .skip:focus { left: 16px; top: 16px; background: #c9a25a; color: #100e0b; padding: 8px 16px; border-radius: 999px; }
        .nowrap { white-space: nowrap; }
        [hidden] { display: none !important; }
        .visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
        .number-form { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 12px; margin: 16px 0 0; }
        .number-form input { width: 7em; background: #100e0b; color: #ede3cf; border: 1px solid #b6a98f; border-radius: 12px; padding: 10px 12px; font: 16px Figtree, sans-serif; }
        .part { border: 1px solid rgba(237,227,207,.16); border-radius: 16px; padding: 8px 16px 16px; margin: 16px 0 0; }
        .part legend { padding: 0 6px; font: 500 14px "JetBrains Mono", monospace; letter-spacing: .14em; text-transform: uppercase; color: #8cc4ae; }
        .words { margin: 0; font: 400 19px/2 "EB Garamond", serif; }
        .word { position: relative; padding: 1px 2px; border-radius: 4px; cursor: pointer; }
        .word input { position: absolute; inset: 0; width: 100%; height: 100%; margin: 0; opacity: 0; cursor: pointer; }
        .word:hover { background: rgba(237,227,207,.08); }
        .word:has(input:checked) { background: #3b1814; color: #f6cfc6; text-decoration: line-through 2px #d0604d; }
        .word:has(input:focus-visible) { outline: 2px solid #f0b45a; outline-offset: 2px; }
        .distil-panel { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px 24px; margin: 24px 0 0; padding: 16px 16px 16px 20px; background: #1a1612; border: 1px solid rgba(237,227,207,.1); border-radius: 20px; }
        .distil-said { flex: 1 1 320px; display: grid; gap: 4px; }
        .distil-said p { margin: 0; }
        .distilled-at { font: 500 13px "JetBrains Mono", monospace; color: #8cc4ae; }
        .distil-about { font-size: 15px; color: #cdbd9e; }
        .distil-form { display: grid; justify-items: start; gap: 6px; margin: 0; }
        .distil-form .hint { margin: 0; font-size: 13px; }
        .busy-icon { display: none; }
        form[data-busy] .idle-icon { display: none; }
        form[data-busy] .busy-icon { display: block; animation: spin 1s linear infinite; }
        form[data-busy] .primary:disabled { opacity: .7; }
        @keyframes spin { to { transform: rotate(360deg); } }
        .precepts { list-style: none; margin: 24px 0 0; padding: 6px 0; background: #1a1612; border: 1px solid rgba(237,227,207,.1); border-radius: 20px; }
        .precept { display: grid; grid-template-columns: 48px minmax(0, 1fr) auto; gap: 4px 12px; align-items: start; padding: 14px 12px 14px 20px; }
        .precept + .precept { border-top: 1px solid rgba(237,227,207,.08); }
        .precept.acted { animation: flashbar 1.4s ease-out; }
        @keyframes flashbar { from { box-shadow: inset 3px 0 0 #f0b45a; } to { box-shadow: inset 3px 0 0 rgba(240,180,90,0); } }
        .numeral { font: 700 20px/1.45 Cinzel, serif; color: #c9a25a; }
        .precept-text { margin: 0; font: 400 20px/1.45 "EB Garamond", serif; color: #ede3cf; }
        [data-edits-in-place] .precept-text { cursor: pointer; }
        .precept-meta { margin: 4px 0 0; font-size: 14px; color: #b6a98f; }
        .pinned-by { display: inline-flex; align-items: center; gap: 6px; color: #d9b874; }
        .pinned-by .icon { width: 14px; height: 14px; }
        .icon.filled { fill: currentColor; }
        .precept-actions { display: flex; gap: 2px; margin: 0; }
        .icon-button { display: inline-grid; place-items: center; width: 44px; height: 44px; min-height: 44px; padding: 0; border-radius: 999px; border: 0; background: transparent; color: #cdbd9e; }
        .icon-button:hover { background: rgba(237,227,207,.08); color: #f0d59a; }
        .icon-button.pin[aria-pressed=true] { color: #c9a25a; background: rgba(201,162,90,.12); }
        .icon-button.delete { color: #b6a98f; }
        .icon-button.delete:hover { background: #3b1814; color: #f6cfc6; }
        .precept-edit { display: none; margin: 0; }
        .precept.editing .precept-edit { display: grid; gap: 8px; }
        .precept.editing .precept-shown, .precept.editing .precept-actions { display: none; }
        .precept-edit label, .precept-add label { font-size: 15px; font-weight: 600; color: #cdbd9e; }
        .precept-edit textarea { margin: 0; font: 19px/1.45 "EB Garamond", serif; }
        .precept-add { grid-column: 2 / 4; display: grid; gap: 8px; margin: 0; }
        .precept-add textarea { margin: 0; }
        .precept-add button:disabled { opacity: .45; }
        .agreement-card { margin-top: 12px; overflow-x: auto; padding: 8px 16px; background: #1a1612; border: 1px solid rgba(237,227,207,.1); border-radius: 20px; }
        .agreement-card .agreement { min-width: 520px; margin: 0; }
        .agreement-card thead th { font: 700 11px Cinzel, serif; letter-spacing: .14em; text-transform: uppercase; color: #b6a98f; }
        .agreement .number { text-align: right; }
        .agreement td.number { font: 500 15px "JetBrains Mono", monospace; }
        .agreement .muted, .agreement .quiet td, .agreement .quiet th { color: #b6a98f; }
        .agreement tbody tr:last-child th, .agreement tbody tr:last-child td { border-bottom: 0; }
        @media (max-width: 560px) { .precept { grid-template-columns: 36px minmax(0, 1fr); } .precept-actions { grid-column: 2; } .precept-add { grid-column: 2; } }
        @media (prefers-reduced-motion: reduce) { * { transition: none !important; animation: none !important; } }
        """;

    /// <summary>
    /// The Ashes: the burned rites, the most recently burned first, each with a way back into the calendar. Plain forms:
    /// restoring goes to the calendar, where the restored rite now waits.
    /// </summary>
    /// <param name="ashes">The rites burned most recently.</param>
    /// <param name="older">A rite burned before those, which a burn's message linked to.</param>
    /// <param name="acted">The rite the calendar's last burn sent here, marked so the eye finds it.</param>
    public static string RenderAshes(IReadOnlyList<Draft> ashes, Option<Draft> older, Option<TabCounts> counts, Option<Guid> acted)
    {
        var html = new StringBuilder();
        Head(html, "The Ashes", TabBar(ScriptoriumTab.Ashes, counts));
        html.Append("""<main id="main">""");
        Heading(html, "The Ashes",
            "Rites consigned to the flames. Nothing burned is lost: a restored rite waits again, where the Augury places it.");
        if (ashes.Count == 0)
        {
            html.Append("<p>Nothing has been consigned to the flames.</p>");
        }
        else
        {
            // The page shows the newest; the tab counts them all.
            counts.Filter(counted => counted.Ashes > ashes.Count)
                .IfSome(counted => html.Append($"<p>The {ashes.Count} burned most recently, of {Number(counted.Ashes)}.</p>"));
            html.Append("""<ul class="calendar">""");
            foreach (var draft in ashes)
            {
                Ash(html, draft, acted);
            }
            html.Append("</ul>");
        }
        older.IfSome(draft =>
        {
            html.Append("""<section aria-labelledby="older-heading"><h2 id="older-heading">Burned longer ago</h2><ul class="calendar">""");
            Ash(html, draft, acted);
            html.Append("</ul></section>");
        });
        html.Append("</main>");
        Footer(html, None);
        return html.ToString();
    }

    public static string RenderError()
    {
        var html = new StringBuilder();
        Head(html, "The Scriptorium is silent");
        html.Append("""
            <main id="main">
            """);
        Heading(html, "The Scriptorium is silent", "The Machine Spirit could not open the records. Try again in a moment.");
        html.Append("""
            <p><a href="/scriptorium">Return to the Scriptorium</a></p>
            </main>
            """);
        Footer(html, None);
        return html.ToString();
    }

    /// <summary>
    /// The page for amending a waiting draft's words (#8). Plain form posts: saving goes back to the calendar, and asking
    /// for a rewrite comes back here with the rewrite in the fields, unsaved. Its script (<see cref="SlowActionScript"/>)
    /// asks for the rewrite in the background, saying it's under way.
    /// </summary>
    /// <param name="words">What the fields hold: the draft's words, or what the Scribe or the rewrite put there.</param>
    /// <param name="message">Why it came back: a rule the words break, or that the Machine Spirit rewrote them.</param>
    /// <param name="rewrittenFrom">The words a rewrite started from, when <paramref name="words"/> are one: the fields it
    /// changed say so, and the first is focused.</param>
    /// <param name="subject">The subject the calendar was narrowed to, which the page carries back to it.</param>
    public static string RenderAmend(
        AmendableDraft amendable, RiteContent words, string note, Option<string> message, Option<RiteContent> rewrittenFrom, Option<Tag> subject)
    {
        var draft = amendable.Draft;
        var id = draft.Id.ToString("D");
        var changed = rewrittenFrom.Map(before => ChangedFields(before, words)).IfNone([]);
        // Marked in the label, so a screen reader hears it with the field's name, and on the field's edge, for the eye.
        string Label(string field, string name)
            => $"""<label for="amend-{field}">{name}{(changed.Contains(field) ? """<span class="changed-mark"> · rewritten</span>""" : string.Empty)}</label>""";
        string Marked(string field)
            => !changed.Contains(field) ? string.Empty : changed[0] == field ? " class=\"changed\" autofocus" : " class=\"changed\"";
        var html = new StringBuilder();
        Head(html, $"Amend “{draft.Writing.Title}”");
        html.Append("""<main id="main">""");
        Heading(html, "Amend the Rite",
            "Change its words by hand, or write a note and let the Machine Spirit rewrite them. Whatever you save is judged again.");
        html.Append($"""
            <p class="status" role="status">{message.Map(Encode).IfNone(string.Empty)}</p>
            <form method="post" class="amend" action="/scriptorium/drafts/{id}/amend">
            <input type="hidden" name="etag" value="{Encode(amendable.ETag)}">{SubjectField(subject)}
            <p class="kicker">{Kind(draft.Writing.Kind)} · written by {Encode(draft.Writing.GeneratedByModel)}</p>
            {Label("title", "Title")}
            <input type="text" id="amend-title" name="title" value="{Encode(words.Title)}" maxlength="{RiteWriting.MaxTitleLength}"{Marked("title")} required>
            {Label("text", "Text")}
            <textarea id="amend-text" name="text" rows="6" maxlength="{RiteWriting.MaxTextLength}"{Marked("text")} required>{Encode(words.Text)}</textarea>
            {Label("truth", "Heretical Truth")}
            <textarea id="amend-truth" name="truth" rows="3" maxlength="{RiteWriting.MaxHereticalTruthLength}" aria-describedby="amend-code-hint"{Marked("truth")} required>{Encode(words.HereticalTruth)}</textarea>
            <p class="hint" id="amend-code-hint">Commands, flags and file names go in `backticks`, as the Machine Spirit writes them.</p>
            <label for="amend-note">Note</label>
            <p class="hint" id="amend-note-hint">Why it needs amending. Needed to ask for a rewrite, which follows it; the Augury learns from it too.</p>
            <textarea id="amend-note" name="note" rows="3" maxlength="{ScribeDecision.MaxNoteLength}" aria-describedby="amend-note-hint">{Encode(note)}</textarea>
            <div class="buttons">
            <button class="primary">{ScriptoriumIcons.Save}Save the Amendment</button>
            <button class="secondary" formaction="/scriptorium/drafts/{id}/rewrite" data-needs-note data-in-place data-slow="{Encode(Rewriting)}" data-busy-label="Rewriting…">{ScriptoriumIcons.Summon}Ask the Machine Spirit to Rewrite</button>
            <a class="button-link" href="/scriptorium{subject.Map(tag => $"?subject={tag.Key}").IfNone(string.Empty)}#rite-{id}">Leave It as It Was</a>
            </div>
            </form>
            </main>
            """);
        Footer(html, Some(SlowActionScript.Js));
        return html.ToString();
    }

    // The amending page's fields whose words a rewrite changed, in the page's order.
    private static List<string> ChangedFields(RiteContent before, RiteContent after)
    {
        var fields = new List<string>();
        if (after.Title != before.Title)
        {
            fields.Add("title");
        }
        if (after.Text != before.Text)
        {
            fields.Add("text");
        }
        if (after.HereticalTruth != before.HereticalTruth)
        {
            fields.Add("truth");
        }
        return fields;
    }

    /// <summary>
    /// The Record (#9): every change to the drafts and rites, and who made it, newest first; or one draft's, from its
    /// first draft to its publishing.
    /// </summary>
    /// <param name="names">The names the Scribes chose (#84); the others show by the end of their ids.</param>
    public static string RenderRecord(IReadOnlyList<AuditEntry> entries, IReadOnlyList<ScribeName> names, Option<TabCounts> counts, Option<Guid> draft)
    {
        var html = new StringBuilder();
        Head(html, "The Record", TabBar(ScriptoriumTab.Record, counts));
        html.Append("""<main id="main">""");
        Heading(html, "The Record",
            "Every change to the calendar, the rites and the Precepts, and who made it, newest first: the Scribes' actions and the Machine Spirit's own.");
        draft.IfSome(_ => html.Append("""<p>One rite's record. <a href="/scriptorium/record">The whole record</a></p>"""));
        if (entries.Count == 0)
        {
            html.Append("<p>Nothing is recorded yet.</p>");
        }
        else
        {
            html.Append("""<ol class="record">""");
            foreach (var entry in entries)
            {
                html.Append($"""<li><time datetime="{entry.AtUtc:yyyy-MM-ddTHH:mm:ssZ}">{When(entry.AtUtc)}</time> {Encode(ScribeNames.Of(entry.Actor, names))} {Did(entry)}</li>""");
            }
            html.Append("</ol>");
        }
        html.Append("</main>");
        Footer(html, None);
        return html.ToString();
    }

    private static void Ash(StringBuilder html, Draft draft, Option<Guid> acted)
    {
        var id = draft.Id.ToString("D");
        html.Append($"""<li><article class="rite ash{Acted(draft, acted)}" id="rite-{id}" aria-labelledby="title-{id}">""");
        html.Append($"""<p class="kicker">{Kind(draft.Writing.Kind)}""");
        draft.BurnedAtUtc.IfSome(burned => html.Append($" · burned {Date(DateOnly.FromDateTime(burned))}"));
        html.Append("</p>");
        RiteBody(html, draft, id);
        DecisionForm(html, id, [Button(id, draft.Writing.Title, "restore", ScriptoriumIcons.Restore, "Restore from the Ashes", "secondary")]);
        html.Append("</article></li>");
    }

    private static void RiteBody(StringBuilder html, Draft draft, string id)
    {
        html.Append($"""<h3 id="title-{id}">{WithCode(draft.Writing.Title)}</h3>""");
        // Under the title, where an eye matches them against the calendar's subjects above. Not links: following one
        // would leave the Scriptorium for the public tag page.
        var tags = RiteTags.Of(draft.Tags);
        if (tags.Count > 0)
        {
            var labels = string.Concat(tags.Select(tag => $"""<span class="tag">{Encode(tag.Label)}</span>"""));
            html.Append($"""<p class="tags"><span class="visually-hidden">Filed under </span>{labels}</p>""");
        }
        html.Append($"""<p class="text">{WithCode(draft.Writing.Text)}</p>""");
        html.Append($"""<p class="truth"><span class="truth-label">Heretical Truth</span> {WithCode(draft.Writing.HereticalTruth)}</p>""");
    }

    // One form per draft, so its note goes with the button pressed. The note is folded away: most decisions go without one.
    private static void DecisionForm(StringBuilder html, string id, IEnumerable<string> buttons)
        => html.Append($"""
            <form method="post" class="decide">
            <details class="fold">
            <summary>{ScriptoriumIcons.Note}Note for the Augury <span class="optional">(optional)</span></summary>
            <label class="visually-hidden" for="note-{id}">Note for the Augury</label>
            <textarea id="note-{id}" name="note" rows="2" maxlength="{ScribeDecision.MaxNoteLength}"></textarea>
            </details>
            <div class="buttons">{string.Concat(buttons)}</div>
            </form>
            """);

    // Each button names the rite for screen readers, since every entry has the same buttons.
    private static string Button(string id, string title, string action, string icon, string label, string style)
        => $"""<button title="{Encode(About(action))}" class="{style}" formaction="/scriptorium/drafts/{id}/{action}">{icon}{label}<span class="visually-hidden">: {Encode(title)}</span></button>""";

    // A link, not a button: it opens the amending page, and nothing changes until that page's form is saved.
    private static string AmendLink(string id, string title, Option<Tag> subject)
        => $"""<a class="button-link" title="{Encode(About("amend"))}" href="/scriptorium/drafts/{id}/amend{subject.Map(tag => $"?subject={tag.Key}").IfNone(string.Empty)}">{ScriptoriumIcons.Note}Amend<span class="visually-hidden">: {Encode(title)}</span></a>""";

    // What an action does, shown when the pointer rests on its button: the names are the rite's own words, and a Scribe
    // who acts once a month needn't remember what "Let the Augury Decide" means. Each place in the calendar is a day.
    private static string About(string action) => action switch
    {
        "summon" => "Writes new drafts in the background until the calendar is full again; each appears as it's written.",
        "augury" => "Forgets the Scribes' order: the Augury orders every waiting rite again, by quality and variety.",
        "anoint" => "Anoints this rite: it's published after the rites anointed before it, ahead of the Augury's order.",
        "release" => "Gives this rite back to the Augury, which orders it with the rest by quality and variety.",
        "burn" => "Takes this rite out of the calendar, into the Ashes. Nothing is deleted: it can be restored.",
        "amend" => "Changes its words by hand, or asks the Machine Spirit to rewrite them as your note says. It's judged again when saved.",
        "judge" => "Asks the Augury to judge this rite now, as the next refill would: judging it failed, or was off, when it was written.",
        _ => "Puts this rite back in the calendar, where the Augury places it.",
    };

    // On an altar, as the archive's heading is (Pages.AltarHeading): the glow behind it, and candles beside it on a wide
    // screen. The script replaces it with the rest of the page after an action, which changes nothing in it.
    private static void Heading(StringBuilder html, string title, string lede)
        => html.Append($"""
            <div class="altar-head">
            <div>
            <h1>{Encode(title)}</h1>
            <p class="lede">{Encode(lede)}</p>
            </div>
            {Pages.Svg.AltarCandles}
            {Pages.Svg.Traces}
            </div>
            """);

    // Every page carries the site's motto and the Games Workshop disclaimer (CLAUDE.md, Ground rules).
    // The calendar has its own script (ScriptoriumScript); the amending page and the Precepts have the one for their slow
    // actions (SlowActionScript); the rest have none: their forms simply post.
    private static void Footer(StringBuilder html, Option<string> script)
        => html.Append("""
            <footer>
            <p class="motto">Knowledge is lost. The rituals remain.</p>
            <p class="disclaimer">An unofficial fan satire, not affiliated with or endorsed by Games Workshop. Warhammer 40,000 and related names are trademarks of Games Workshop Ltd. Prayers are generated by AI.</p>
            </footer>
            </div>
            """).Append(script.Map(js => $"<script>{js}</script>").IfNone(string.Empty)).Append("</body></html>");

    // The pages under a tab (amending a rite, choosing a name) have none: they lead back where they came from.
    private static void Head(StringBuilder html, string title) => Head(html, title, string.Empty);

    private static void Head(StringBuilder html, string title, string tabs) => Head(html, title, tabs, string.Empty);

    // The brand, and beside it who's looking (only the calendar says so, for now), then the tabs.
    private static void Head(StringBuilder html, string title, string tabs, string identity)
    {
        html.Append($"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex">
            <link rel="icon" type="image/svg+xml" href="{Pages.Svg.Favicon}">
            <title>{Encode(title)} · The Daily Machine Spirit</title>
            <style>{Pages.Styles.FontFaces}{Styles}{Pages.Styles.Altar}</style>
            </head>
            <body>
            <a class="skip" href="#main">Skip to content</a>
            <div class="frame">
            <header><div class="masthead"><a class="brand" href="/">{Pages.Svg.BrandCog}The Daily Machine Spirit</a>{identity}</div>{tabs}</header>
            """);
    }

    // Each page's tab says what it holds: the calendar's, every rite waiting (the calendar itself says which order each is
    // in). The current tab is marked by its underline, as well as for screen readers.
    // In the header, outside the main part the calendar's script replaces, which swaps the bar itself.
    private static string TabBar(ScriptoriumTab current, Option<TabCounts> counts)
    {
        string Tab(ScriptoriumTab tab, string path, string name, Func<TabCounts, string> count)
            => $"""<li><a href="{path}"{(tab == current ? " aria-current=\"page\"" : string.Empty)}>{name}{counts.Map(counted => $"""<span class="count">{count(counted)}</span>""").IfNone(string.Empty)}</a></li>""";
        return $"""
            <nav class="tabs" aria-label="The Scriptorium"><ul>
            {Tab(ScriptoriumTab.Calendar, "/scriptorium", "The Calendar", CalendarCount)}
            {Tab(ScriptoriumTab.Ashes, "/scriptorium/ashes", "The Ashes", counted => Number(counted.Ashes))}
            {Tab(ScriptoriumTab.Precepts, "/scriptorium/precepts", "The Precepts", counted => Number(counted.Precepts))}
            {Tab(ScriptoriumTab.Record, "/scriptorium/record", "The Record", counted => Number(counted.RecordEntries))}
            </ul></nav>
            """;
    }

    private static string Number(int count) => count.ToString("N0", CultureInfo.InvariantCulture);

    // The calendar's two orders, as its groups show them: short to the eye, said in words to a screen reader, which
    // would read the slash as one.
    private static string CalendarCount(TabCounts counted)
    {
        var (scribes, augury) = (Number(counted.PlacedByScribes), Number(counted.PlacedByAugury));
        return $"""<span aria-hidden="true" title="Anointed / in the Augury’s order">{scribes}/{augury}</span><span class="visually-hidden">: {scribes} anointed, {augury} in the Augury’s order</span>""";
    }

    private static string Acted(Draft draft, Option<Guid> acted) => acted.Exists(id => id == draft.Id) ? " acted" : string.Empty;

    private static string Kind(RiteKind kind) => kind == RiteKind.Prayer ? "A Prayer" : "A Ritual";

    private static string Date(DateOnly day) => day.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    // What the last action did, as HTML. It names the rite and where it went when the rite is still on the page (it may
    // have been published or moved again since), and links a burned rite to its place in the Ashes, to restore it.
    // A refill writes only what's missing, so a full calendar summons nothing.
    private static string Summoning(ScriptoriumView view)
    {
        var missing = view.FullAt - view.Calendar.Count;
        return missing switch
        {
            <= 0 => $"The calendar is full: {view.FullAt} rites wait, so there's nothing to summon.",
            1 => "Summoning 1 new rite: it's written over the next few minutes.",
            _ => $"Summoning {missing} new rites: they're written over the next few minutes.",
        };
    }

    private static Option<string> Status(string outcome, ScriptoriumView view, Option<Guid> rite, Option<Draft> burned)
    {
        var entry = rite.Bind(id => view.Calendar.Where(waiting => waiting.Draft.Id == id).HeadOrNone());
        var title = (entry.Map(waiting => waiting.Draft) | burned).Map(draft => $"“{WithCode(draft.Writing.Title)}”");
        var date = entry.Map(waiting => Date(waiting.PublishesOnUtc));
        var augury = entry.Bind(waiting => waiting.Draft.Augury)
            .Map(judged => $", {Percent(judged.Quality)}").IfNone(string.Empty);
        string Named(string done) => title.Map(name => $"{done} {name}").IfNone(done);
        // What a burn says that the script's Undo says instead (data-undoable): that it burned, and the way back.
        var restore = burned.Map(ash => $"""<span data-undoable> <a href="/scriptorium/ashes?rite={ash.Id:D}#rite-{ash.Id:D}">Restore it from the Ashes</a>.</span>""").IfNone(string.Empty);
        return outcome switch
        {
            "anoint" => $"{Named("Anointed")}{date.Map(day => $": it goes out {day}").IfNone(string.Empty)}.",
            "release" => $"{Named("Released")} to the Augury{date.Map(day => $": it waits for {day}").IfNone(string.Empty)}.",
            "exalt" => $"{Named("Exalted")}{date.Map(day => $" to {day}").IfNone(string.Empty)}.",
            "humble" => $"{Named("Humbled")}{date.Map(day => $" to {day}").IfNone(string.Empty)}.",
            "burn" => $"""<span data-undoable>{Named("Consigned")} to the flames.</span>{restore}""",
            "burn-last" => $"""<span data-undoable>{Named("Consigned")} to the flames.</span> No rite waits now, so new ones are being summoned: they're written over the next few minutes.{restore}""",
            "restore" => $"{Named("Restored")} from the ashes{date.Map(day => $": it waits for {day}").IfNone(string.Empty)}.",
            "summon" => Summoning(view),
            "amend" => $"{Named("Amended")}: judged again, it waits{date.Map(day => $" for {day}").IfNone(string.Empty)}.",
            "amend-unjudged" => $"{Named("Amended")}, but it couldn't be judged again, so it waits unjudged{date.Map(day => $" for {day}").IfNone(string.Empty)}.",
            "judged" => $"{Named("Judged")} again{augury}: the Augury places it{date.Map(day => $" for {day}").IfNone(string.Empty)}.",
            "still-unjudged" => $"{Named("Still")} can't be judged: the judge is failing or off. The next refill tries again.",
            "augury" => "The Augury orders every draft again.",
            "changed" => "Another Scribe changed the calendar meanwhile. Look again before deciding.",
            "unmoved" => "It was already there, so nothing moved. Another Scribe may have moved it: look again.",
            "note-too-long" => "That note is too long, so nothing was changed. Shorten it and try again.",
            "named" => "The Record shows your name now, on every change you made.",
            _ => None,
        };
    }

    private static string Done(ScribeAction action) => action switch
    {
        ScribeAction.Anoint => "Anointed",
        ScribeAction.Exalt => "Exalted",
        ScribeAction.Humble => "Humbled",
        ScribeAction.Burn => "Consigned to the flames",
        ScribeAction.Amend => "Amended",
        ScribeAction.Restore => "Restored from the ashes",
        _ => "Released to the Augury",
    };

    // What an entry records, after who: the rite (or precept) named by its words then, a rite linking to its own record.
    private static string Did(AuditEntry entry)
    {
        var rite = entry.DraftId.Match(
            Some: id => $"""<a href="/scriptorium/record?draft={id:D}">“{WithCode(entry.Title)}”</a>""",
            None: () => $"“{WithCode(entry.Title)}”");
        var what = entry.Action switch
        {
            AuditAction.Anoint => $"anointed {rite}",
            AuditAction.Exalt => $"exalted {rite}",
            AuditAction.Humble => $"humbled {rite}",
            AuditAction.Burn => $"consigned {rite} to the flames",
            AuditAction.Restore => $"restored {rite} from the ashes",
            AuditAction.Amend => $"amended {rite}",
            AuditAction.Summon => "summoned new rites",
            AuditAction.LetTheAuguryDecide => "let the Augury decide",
            AuditAction.Release => $"released {rite} to the Augury",
            AuditAction.Write => $"wrote {rite}",
            AuditAction.Distil => """distilled <a href="/scriptorium/precepts">the Precepts</a>""",
            AuditAction.AddPrecept => $"added the precept {rite}",
            AuditAction.EditPrecept => $"edited the precept {rite}",
            AuditAction.PinPrecept => $"pinned the precept {rite}",
            AuditAction.UnpinPrecept => $"unpinned the precept {rite}",
            AuditAction.DeletePrecept => $"deleted the precept {rite}",
            AuditAction.RestorePrecept => $"restored the precept {rite}",
            AuditAction.JudgeAgain => $"judged {rite} again",
            AuditAction.ChooseName => $"chose the name {rite}",
            AuditAction.Expurgate => $"erased words from {rite}{entry.RiteNumber.Map(number => $" (NO. {number})").IfNone(string.Empty)}",
            _ => $"published {rite}{entry.RiteNumber.Map(number => $" as NO. {number}").IfNone(string.Empty)}",
        };
        return what + entry.Detail.Map(detail => $""" <span class="when">({Encode(detail)})</span>""").IfNone(string.Empty);
    }

    private static string When(DateTime atUtc) => atUtc.ToString("ddd d MMM, HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    // The rites put commands and file names in `backticks`.
    private static string WithCode(string text) => InlineCode().Replace(Encode(text), "<code>$1</code>");

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex InlineCode();
}
