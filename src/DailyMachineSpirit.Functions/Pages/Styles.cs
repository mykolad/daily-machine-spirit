namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The site's stylesheet, inlined into every page: one small request per page, nothing to cache-bust. Values come from
/// the design handoff.
/// </summary>
public static class Styles
{
    public const string Css = """
        :root{color-scheme:dark}
        *,*::before,*::after{box-sizing:border-box}
        html,body{background:#100e0b;color:#ede3cf}
        body{margin:0;font-family:Figtree,system-ui,sans-serif;font-size:16px;line-height:1.5}
        :focus{outline:none}
        :focus-visible{outline:2px solid #f0b45a;outline-offset:4px}
        ::selection{background:rgba(201,162,90,.35)}
        a{color:#d9b874}
        a:hover{color:#f0d59a}
        .sr-only{position:absolute;left:-9999px;width:1px;height:1px;overflow:hidden}
        .skip{position:absolute;left:-9999px;top:12px;z-index:50;padding:10px 18px;border-radius:999px;background:#c9a25a;color:#100e0b;font-weight:600;text-decoration:none}
        .skip:focus{left:12px;color:#100e0b}
        .page{max-width:820px;margin:0 auto;padding:28px clamp(16px,4vw,40px) 0;min-height:100vh;display:flex;flex-direction:column;align-items:center;text-align:center}
        .site-header{display:flex;flex-direction:column;align-items:center;gap:14px;margin-bottom:clamp(28px,5vw,44px)}
        .brand{display:flex;align-items:center;gap:10px;font-family:Cinzel,serif;font-weight:700;font-size:16px;letter-spacing:.04em;color:#ede3cf;text-decoration:none;padding:4px 8px;border-radius:999px}
        .brand:hover{color:#ede3cf}
        nav{display:flex;gap:4px}
        nav a{padding:10px 16px;border-radius:999px;font-family:Cinzel,serif;font-weight:700;font-size:14px;letter-spacing:.12em;color:#ede3cf;text-decoration:none}
        nav a[aria-current=page]{background:rgba(201,162,90,.16)}
        nav a:hover{background:rgba(201,162,90,.22);color:#ede3cf}
        main{width:100%;flex:1;display:flex;flex-direction:column;align-items:center}
        .masthead{display:flex;flex-direction:column;align-items:center;gap:14px;margin-bottom:clamp(32px,5vw,52px)}
        .masthead-rule{max-width:100%;height:auto}
        .masthead h1{margin:0;font-family:Cinzel,serif;font-weight:700;font-size:clamp(32px,6vw,58px);line-height:1.05;letter-spacing:.04em;text-wrap:balance}
        .tagline{margin:0;font-family:'EB Garamond',serif;font-style:italic;font-size:clamp(18px,2.4vw,22px);line-height:1.35;color:#cdbd9e;text-wrap:balance}
        .back{align-self:flex-start;display:inline-flex;align-items:center;gap:8px;padding:10px 4px;margin-bottom:16px;font-weight:600;text-decoration:none}
        .rite{width:100%;display:flex;flex-direction:column;align-items:center}
        .parchment{position:relative;width:100%;border-radius:20px;background:#ebdfc2;color:#2b2117;padding:clamp(48px,8vw,76px) clamp(30px,9vw,92px) clamp(44px,7vw,64px);box-shadow:0 24px 70px rgba(0,0,0,.55),inset 0 0 80px rgba(140,100,40,.2)}
        .rule-outer,.rule-inner{position:absolute;pointer-events:none;border:2px solid #8f2a20}
        .rule-outer{inset:12px;border-radius:12px}
        .rule-inner{inset:18px;border-width:1px;border-radius:8px}
        .corner{position:absolute}
        .corner-tl{top:24px;left:24px}
        .corner-tr{top:24px;right:24px;transform:scaleX(-1)}
        .corner-bl{bottom:24px;left:24px;transform:scaleY(-1)}
        .corner-br{bottom:24px;right:24px;transform:scale(-1,-1)}
        .margin-text{display:none;position:absolute;top:110px;bottom:110px;writing-mode:vertical-rl;overflow:hidden;white-space:nowrap;font-family:'JetBrains Mono',monospace;font-size:10px;letter-spacing:.4em;color:#a88f62}
        .margin-left{left:34px}
        .margin-right{right:34px}
        @media (min-width:700px){.margin-text{display:block}}
        .parchment-body{position:relative;display:flex;flex-direction:column;align-items:center;gap:12px}
        .kicker{margin:0;font-family:Cinzel,serif;font-weight:700;font-size:14px;letter-spacing:.22em;color:#8f2a20}
        .rite-title{margin:0;font-family:Cinzel,serif;font-weight:700;font-size:clamp(28px,4.6vw,44px);line-height:1.1;color:#8f2a20;text-wrap:balance}
        .rite-date{font-family:'EB Garamond',serif;font-style:italic;font-size:18px;color:#5c4a33}
        .parchment .divider{margin:6px 0 10px}
        .prayer{margin:0;align-self:stretch;text-align:left;font-family:'EB Garamond',serif;font-size:clamp(20px,2.4vw,23px);line-height:1.65;color:#2b2117;text-wrap:pretty}
        .drop-cap{float:left;font-family:Cinzel,serif;font-weight:700;font-size:clamp(64px,8vw,84px);line-height:.82;margin:8px 12px 0 0;color:#8f2a20}
        .prayer code{font-family:'JetBrains Mono',monospace;font-size:.8em;padding:1px 6px;border-radius:6px;background:#d8c59a;color:#2b2117}
        .colophon{display:flex;flex-wrap:wrap;align-items:center;justify-content:center;gap:14px 22px;margin-top:22px}
        .inscribed{font-family:'EB Garamond',serif;font-style:italic;font-size:16px;color:#5c4a33;white-space:nowrap}
        .purity{display:flex;align-items:center;transform:rotate(-3deg)}
        .purity-strip{padding:4px 14px 4px 12px;margin-right:-10px;background:#f6eedb;border:1px solid #cdb98f;border-radius:4px 0 0 4px;font-family:'EB Garamond',serif;font-style:italic;font-size:15px;color:#2b2117;white-space:nowrap}
        .purity svg{position:relative}
        .ribbon{display:flex;gap:6px;margin-top:-2px}
        .ribbon span{width:14px;background:#8f2a20}
        .ribbon span:first-child{height:40px;clip-path:polygon(0 0,100% 0,100% 100%,50% 86%,0 100%)}
        .ribbon span:last-child{height:52px;background:#7a221a;clip-path:polygon(0 0,100% 0,100% 100%,50% 88%,0 100%)}
        .seal{display:flex;flex-direction:column;align-items:center;gap:10px;margin-top:-30px;padding:6px 22px 14px;border:0;border-radius:28px;background:transparent;color:#ede3cf;cursor:pointer;font:inherit}
        .seal:hover{background:rgba(201,162,90,.08)}
        .seal-wax{position:relative;width:92px;height:92px}
        .seal-half{position:absolute;inset:0;transition:transform 520ms cubic-bezier(.2,.8,.2,1)}
        .seal-left{clip-path:polygon(0 0,54% 0,47% 22%,56% 41%,45% 60%,53% 79%,48% 100%,0 100%)}
        .seal-right{clip-path:polygon(54% 0,100% 0,100% 100%,48% 100%,53% 79%,45% 60%,56% 41%,47% 22%)}
        .seal[aria-expanded=true] .seal-left{transform:translate(-9px,6px) rotate(-18deg)}
        .seal[aria-expanded=true] .seal-right{transform:translate(9px,8px) rotate(15deg)}
        .seal-label{font-family:Cinzel,serif;font-weight:700;font-size:19px;letter-spacing:.08em}
        .seal-sub{font-size:15px;color:#b6a98f}
        .truth{width:100%;max-width:640px;margin-top:20px;text-align:left;border-radius:16px;background:#141c1f;border:1px solid #2e4248;overflow:hidden;animation:reveal 460ms ease-out 180ms both}
        .truth[hidden]{display:none}
        @keyframes reveal{from{opacity:0;transform:translateY(-8px)}to{opacity:1;transform:none}}
        .truth-prompt{padding:12px 20px;border-bottom:1px solid #2e4248;font-family:'JetBrains Mono',monospace;font-size:14px;color:#8cc4ae}
        .truth-body{padding:20px 24px 24px;display:flex;flex-direction:column;gap:10px}
        .truth h2{margin:0;font-family:'JetBrains Mono',monospace;font-weight:500;font-size:14px;letter-spacing:.14em;text-transform:uppercase;color:#8cc4ae}
        .truth p{margin:0;font-size:18px;line-height:1.6;color:#dbe6e8;text-wrap:pretty}
        .truth code{font-family:'JetBrains Mono',monospace;font-size:.85em;padding:2px 7px;border-radius:6px;background:#0b1214;color:#f0d58c}
        .actions{display:flex;flex-wrap:wrap;justify-content:center;gap:12px;margin-top:28px}
        .button{display:inline-flex;align-items:center;gap:8px;white-space:nowrap;min-height:46px;padding:10px 22px;border-radius:999px;border:1px solid transparent;background:transparent;cursor:pointer;font-family:Figtree,sans-serif;font-size:16px;font-weight:600;line-height:1.2;text-decoration:none}
        .button-primary{background:#c9a25a;color:#100e0b}
        .button-primary:hover{background:#d6b170;color:#100e0b}
        .button-primary:active{background:#a8843f}
        .button-outline{border-color:rgba(237,227,207,.22);color:#ede3cf}
        .button-outline:hover{background:rgba(237,227,207,.08);border-color:rgba(237,227,207,.4);color:#ede3cf}
        .button-ghost{padding:10px 16px;color:#d9b874}
        .button-ghost:hover{background:rgba(201,162,90,.12);color:#f0d59a}
        .countdown{display:flex;flex-direction:column;align-items:center;gap:6px;margin-top:48px}
        .countdown-line{display:flex;align-items:center;gap:18px}
        .countdown-line p{margin:0;font-family:Cinzel,serif;font-weight:700;font-size:18px;letter-spacing:.06em;color:#b6a98f}
        .amber{color:#f0b45a;font-variant-numeric:tabular-nums}
        .countdown-note{margin:0;font-size:15px;color:#b6a98f}
        .candle ellipse{transform-box:fill-box;transform-origin:50% 100%;animation:flicker 2.4s ease-in-out infinite}
        .candle-late ellipse{animation-duration:3.1s;animation-delay:.7s}
        @keyframes flicker{0%,100%{transform:scaleY(1)}40%{transform:scaleY(.86) skewX(3deg)}70%{transform:scaleY(1.05) skewX(-2deg)}}
        .archive-title{margin:0 0 10px;font-family:Cinzel,serif;font-size:clamp(30px,4.4vw,46px);line-height:1.1}
        .archive-subtitle{margin:0 0 14px;font-family:'EB Garamond',serif;font-style:italic;font-size:21px;color:#cdbd9e}
        .archive-divider{margin-bottom:32px}
        .rows{width:100%;list-style:none;margin:0;padding:0;display:flex;flex-direction:column;gap:12px;text-align:left}
        .row{display:flex;flex-wrap:wrap;gap:8px 28px;padding:22px clamp(20px,3vw,28px);border-radius:20px;background:#1a1612;border:1px solid rgba(237,227,207,.07);color:#ede3cf;text-decoration:none}
        .row:hover{background:#231e18;border-color:rgba(201,162,90,.6);color:#ede3cf}
        .row-meta{flex:0 0 112px;display:flex;flex-direction:column;gap:4px;padding-top:4px}
        .row-date{font-family:'JetBrains Mono',monospace;font-size:14px;color:#b6a98f}
        .kind{font-family:Cinzel,serif;font-weight:700;font-size:13px;letter-spacing:.16em;color:#c9a25a}
        .row-main{flex:1 1 300px;min-width:0;display:flex;flex-direction:column;gap:6px}
        .row-title{font-family:Cinzel,serif;font-weight:700;font-size:21px;line-height:1.2}
        .row-first{font-family:'EB Garamond',serif;font-size:18px;line-height:1.45;color:#cdbd9e;text-wrap:pretty}
        .row-first code{font-family:'JetBrains Mono',monospace;font-size:.8em}
        .archive-more{display:flex;justify-content:center;padding-top:28px}
        .archive-end{margin:0;font-family:'EB Garamond',serif;font-style:italic;font-size:19px;color:#b6a98f}
        .state{display:flex;flex-direction:column;align-items:center;gap:16px;max-width:580px;padding:24px 0}
        .state h1,.state h2{margin:0;font-family:Cinzel,serif;font-size:clamp(26px,3.6vw,36px);line-height:1.15}
        .state h1{font-size:clamp(28px,4vw,42px);line-height:1.12}
        .state-body{margin:0;font-family:'EB Garamond',serif;font-size:21px;line-height:1.5;color:#d7c9ad;text-wrap:balance}
        .state-eyebrow{margin:0;font-family:'JetBrains Mono',monospace;font-size:14px;letter-spacing:.1em;color:#8cc4ae}
        .state-code{font-family:'JetBrains Mono',monospace;font-size:14px;padding:8px 14px;border-radius:12px;background:#141c1f;border:1px solid #2e4248;color:#dbe6e8}
        .state-actions{display:flex;flex-wrap:wrap;justify-content:center;gap:12px;margin-top:6px}
        .site-footer{margin-top:72px;padding-bottom:48px;display:flex;flex-direction:column;align-items:center;gap:12px;max-width:600px}
        .motto{margin:0;font-family:Cinzel,serif;font-weight:700;font-size:16px;letter-spacing:.08em;color:#c9a25a}
        .disclaimer{margin:0;font-size:14px;line-height:1.6;color:#b6a98f}
        .toast-region{position:fixed;left:50%;bottom:24px;transform:translateX(-50%);z-index:40;pointer-events:none}
        .toast{padding:12px 22px;border-radius:999px;background:#ede3cf;color:#100e0b;font-size:15px;font-weight:600;box-shadow:0 8px 28px rgba(0,0,0,.5);white-space:nowrap}
        @media (prefers-reduced-motion:reduce){*,*::before,*::after{animation:none!important;transition:none!important}}
        """;
}
