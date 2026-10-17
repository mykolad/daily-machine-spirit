namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The site's stylesheet, inlined into every page: one small request per page, nothing to cache-bust. Values come from
/// the design handoff.
/// </summary>
public static class Styles
{
    // The site's own copies of the fonts (FontsFunction): variable fonts, one file per family and style.
    public const string FontFaces = """
        @font-face{font-family:Cinzel;font-style:normal;font-weight:400 900;font-display:swap;src:url(/fonts/cinzel.woff2) format('woff2')}
        @font-face{font-family:'EB Garamond';font-style:normal;font-weight:400 800;font-display:swap;src:url(/fonts/eb-garamond.woff2) format('woff2')}
        @font-face{font-family:'EB Garamond';font-style:italic;font-weight:400 800;font-display:swap;src:url(/fonts/eb-garamond-italic.woff2) format('woff2')}
        @font-face{font-family:Figtree;font-style:normal;font-weight:300 900;font-display:swap;src:url(/fonts/figtree.woff2) format('woff2')}
        @font-face{font-family:'JetBrains Mono';font-style:normal;font-weight:100 800;font-display:swap;src:url(/fonts/jetbrains-mono.woff2) format('woff2')}
        """;

    public const string Css = FontFaces + """
        :root{color-scheme:dark}
        *,*::before,*::after{box-sizing:border-box}
        html{background:#100e0b;color:#ede3cf}
        .frame{overflow-x:clip}
        body{margin:0;font-family:Figtree,system-ui,sans-serif;font-size:16px;line-height:1.5;overflow-wrap:break-word}
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
        nav>a{padding:10px 16px;border-radius:999px;font-family:Cinzel,serif;font-weight:700;font-size:14px;letter-spacing:.12em;color:#ede3cf;text-decoration:none}
        nav>a[aria-current=page]{background:rgba(201,162,90,.16)}
        nav>a:hover{background:rgba(201,162,90,.22);color:#ede3cf}
        main{width:100%;flex:1;display:flex;flex-direction:column;align-items:center}
        main:focus-visible{outline:none}
        .masthead{display:flex;flex-direction:column;align-items:center;gap:14px;margin-bottom:clamp(32px,5vw,52px)}
        .masthead-rule{max-width:100%;height:auto}
        .masthead .site-name{margin:0;font-family:Cinzel,serif;font-weight:700;font-size:clamp(32px,6vw,58px);line-height:1.05;letter-spacing:.04em;text-wrap:balance}
        .tagline{margin:0;font-family:'EB Garamond',serif;font-style:italic;font-size:clamp(18px,2.4vw,22px);line-height:1.35;color:#cdbd9e;text-wrap:balance}
        .back{align-self:flex-start;display:inline-flex;align-items:center;gap:8px;padding:10px 4px;margin-bottom:16px;font-weight:600;text-decoration:none}
        .hero{position:relative;width:100%;display:flex;flex-direction:column;align-items:center}
        .rite{position:relative;width:100%;display:flex;flex-direction:column;align-items:center}
        .rite::before{content:'';position:absolute;left:50%;top:50%;z-index:-1;width:600px;height:920px;transform:translate(-50%,-50%);background:radial-gradient(ellipse 300px 460px,#2b2117 0%,#18140f 55%,rgba(16,14,11,0) 100%);pointer-events:none}
        @media (min-width:700px){.rite::before{width:1520px;height:1280px;background:radial-gradient(ellipse 760px 640px,#2b2117 0%,#18140f 55%,rgba(16,14,11,0) 100%)}}
        .parchment{position:relative;width:100%;border-radius:20px;background:#ebdfc2;color:#2b2117;padding:clamp(48px,8vw,76px) clamp(30px,9vw,92px) clamp(44px,7vw,64px);box-shadow:0 0 120px rgba(240,180,90,.16),0 24px 70px rgba(0,0,0,.55),inset 0 0 80px rgba(140,100,40,.2)}
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
        .faithful,.sharing{display:contents}
        .faithful>.margin-label{display:none}
        .button{display:inline-flex;align-items:center;gap:8px;white-space:nowrap;min-height:46px;padding:10px 22px;border-radius:999px;border:1px solid transparent;background:transparent;cursor:pointer;font-family:Figtree,sans-serif;font-size:16px;font-weight:600;line-height:1.2;text-decoration:none}
        .button-primary{background:#c9a25a;color:#100e0b}
        .button-primary:hover{background:#d6b170;color:#100e0b}
        .button-primary:active{background:#a8843f}
        .button-outline{border-color:rgba(237,227,207,.22);color:#ede3cf}
        .button-outline:hover{background:rgba(237,227,207,.08);border-color:rgba(237,227,207,.4);color:#ede3cf}
        .reactions{display:flex;flex-wrap:wrap;justify-content:center;gap:12px}
        .reaction{gap:10px;padding:10px 20px;border-color:rgba(237,227,207,.22);color:#ede3cf}
        .reaction:hover{border-color:#c9a25a;color:#ede3cf}
        .reaction .count{font-variant-numeric:tabular-nums}
        .reaction[aria-pressed=true] svg{fill:currentColor}
        .reaction-blessed[aria-pressed=true]{background:#3a2f1a;border-color:#c9a25a;color:#f2e1bd}
        .reaction-heresy[aria-pressed=true]{background:#3b1814;border-color:#d0604d;color:#f6cfc6}
        .reactions[aria-busy=true] .reaction{cursor:progress}
        .vote-status{flex-basis:100%;margin-top:2px;font-size:15px;color:#bfb39a;text-align:center}
        .vote-status[hidden]{display:none}
        .vote-status form{display:inline}
        .vote-privacy{display:block;margin-top:4px;font-size:13px;color:#a89d86}
        .vote-status a,.text-button{color:#d9b874;text-decoration:underline;text-underline-offset:3px}
        .vote-status a:hover,.text-button:hover{color:#f0d59a}
        .text-button{padding:0;border:0;background:none;font:inherit;cursor:pointer}
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
        .margin-countdown{display:none}
        .recent{width:100%;margin-top:44px;display:flex;flex-direction:column;align-items:center;gap:18px}
        .recent h2{display:flex;align-items:center;gap:16px;margin:0;font-family:Cinzel,serif;font-weight:700;font-size:18px;letter-spacing:.2em;text-transform:uppercase;color:#c9a25a}
        .recent h2::before,.recent h2::after{content:'';width:clamp(40px,12vw,100px);height:1px;background:#84662c}
        .recent-list{align-self:stretch;list-style:none;margin:0 calc(-1*clamp(16px,4vw,40px));padding:8px clamp(16px,4vw,40px);display:flex;gap:12px;overflow-x:auto;scroll-snap-type:x mandatory;scroll-padding-inline:clamp(16px,4vw,40px)}
        .recent-list li{flex:none;width:150px;display:flex;scroll-snap-align:start}
        .recent-card{position:relative;flex:1;display:flex;flex-direction:column;align-items:center;gap:10px;padding:22px 16px 20px;border-radius:14px;background:#ebdfc2;color:#8f2a20;text-align:center;text-decoration:none;box-shadow:0 12px 30px rgba(0,0,0,.45)}
        .recent-card::before{content:'';position:absolute;inset:8px;border:1px solid #8f2a20;border-radius:8px;pointer-events:none}
        .recent-card:hover{background:#f6eedb;color:#8f2a20}
        .recent-kicker{font-family:Cinzel,serif;font-weight:700;font-size:12px;letter-spacing:.2em}
        .recent-title{flex:1;font-family:Cinzel,serif;font-weight:700;font-size:18px;line-height:1.15;text-wrap:balance}
        .recent-archive{display:inline-flex;align-items:center;min-height:44px;font-weight:600;text-decoration:none}
        @media (min-width:700px){
        .recent{margin-top:clamp(44px,10vw,128px);gap:24px}
        .recent-list{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px;margin:0;padding:0;overflow:visible}
        .recent-list li{width:auto}
        }
        @media (min-width:1024px){
        .recent{width:min(100vw - 48px,1240px)}
        .recent-list{grid-template-columns:repeat(4,minmax(0,1fr))}
        }
        .more{width:100%;margin-top:72px;display:flex;flex-direction:column;align-items:center;gap:20px}
        .more h2{margin:0;font-family:Cinzel,serif;font-size:26px}
        .more-list{width:100%;list-style:none;margin:0;padding:0;display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:16px;text-align:left}
        .more-list li{display:flex}
        .more-card{flex:1;display:flex;flex-direction:column;gap:10px;padding:22px;border-radius:20px;background:#1a1612;border:1px solid rgba(237,227,207,.1);color:#ede3cf;text-decoration:none}
        .more-card:hover{background:#231e18;border-color:#c9a25a;color:#ede3cf}
        .more-title{font-family:Cinzel,serif;font-weight:700;font-size:19px;line-height:1.2}
        .sheet{position:relative;width:100%;max-width:740px;padding:12px;border-radius:18px;background:#ebdfc2;color:#2b2117;text-align:left;box-shadow:0 0 120px rgba(240,180,90,.16),0 24px 70px rgba(0,0,0,.55),inset 0 0 80px rgba(140,100,40,.2)}
        .sheet::before{content:'';position:absolute;left:50%;top:320px;z-index:-1;width:600px;height:920px;transform:translate(-50%,-50%);background:radial-gradient(ellipse 300px 460px,#2b2117 0%,#18140f 55%,rgba(16,14,11,0) 100%);pointer-events:none}
        .sheet-inner{padding:28px 18px 14px}
        .sheet .corner{display:none}
        .sheet :focus-visible{outline-color:#8f2a20;outline-offset:2px}
        .sheet-head{text-align:center}
        .sheet-kicker{margin:0 0 8px;font-family:Cinzel,serif;font-weight:700;font-size:12px;letter-spacing:.22em;text-transform:uppercase;color:#8f2a20}
        .sheet-title{margin:0;font-family:Cinzel,serif;font-weight:700;font-size:26px;line-height:1.1;color:#8f2a20;overflow-wrap:anywhere}
        .sheet-sub{margin:8px 0 0;font-family:'EB Garamond',serif;font-style:italic;font-size:15px;color:#5c4a33}
        .sub-wide{display:none}
        .sheet-search{display:flex;align-items:center;gap:8px;width:100%;max-width:400px;height:44px;margin:24px auto 8px;padding:0 6px 0 14px;border:1px solid #8f2a20;border-radius:999px;background:#f6eedb;color:#8f2a20}
        .sheet-search:focus-within{outline:2px solid #8f2a20;outline-offset:2px}
        .sheet-search .search-icon{flex:none;width:16px;height:16px}
        .sheet-search input{flex:1;min-width:0;height:100%;padding:0;border:0;background:transparent;color:#2b2117;font:16px Figtree,system-ui,sans-serif}
        .sheet-search input:focus-visible{outline:none}
        .sheet-search input::placeholder{color:#5c4a33;opacity:1}
        input[type=search]::-webkit-search-cancel-button{-webkit-appearance:none;appearance:none}
        .sheet-clear{flex:none;display:grid;place-items:center;width:32px;height:32px;border-radius:999px;color:#5c4a33}
        .sheet-clear:hover{background:#e0d0a8;color:#2b2117}
        .month-rubric{display:flex;align-items:center;gap:16px;margin:24px 0 4px;font-family:Cinzel,serif;font-weight:700;font-size:10px;letter-spacing:.22em;text-transform:uppercase;color:#8f2a20}
        .month-rubric::before,.month-rubric::after{content:'';flex:1;height:1px;background:rgba(143,42,32,.3)}
        .month-rubric>span::before,.month-rubric>span::after{content:'';display:inline-block;width:5px;height:5px;margin:0 12px;background:#8f2a20;transform:rotate(45deg);vertical-align:middle}
        .rows{list-style:none;margin:0;padding:0}
        .row{position:relative;display:flex;flex-direction:column;gap:2px;padding:12px 6px;border-radius:8px}
        .row:hover{background:#e0d0a8}
        .sheet .row:has(.row-title:focus-visible){outline:2px solid #8f2a20;outline-offset:2px}
        .row-meta{display:flex;align-items:baseline;gap:10px}
        .row-date{font-family:'JetBrains Mono',monospace;font-weight:500;font-size:12px;color:#5c4a33}
        .row-number{font-family:'JetBrains Mono',monospace;font-weight:500;font-size:12px;letter-spacing:.06em;color:#5c4a33}
        .kind{font-family:Cinzel,serif;font-weight:700;font-size:13px;letter-spacing:.16em;color:#c9a25a}
        .row .kind{font-size:10px;letter-spacing:.2em;color:#8f2a20}
        .row-main{min-width:0;display:flex;flex-direction:column;gap:2px}
        .row-title{font-family:Cinzel,serif;font-weight:700;font-size:17px;line-height:1.2;color:#8f2a20;text-decoration:none}
        .row-title:hover{color:#8f2a20}
        .row-title:focus-visible{outline:none}
        .row-title::after{content:'';position:absolute;inset:0;border-radius:8px}
        .row-line{font-family:'EB Garamond',serif;font-size:16px;line-height:1.45;color:#2b2117;text-wrap:pretty}
        .row-truth{font-style:italic;color:#5c4a33}
        .row-counts{display:flex;gap:16px;margin-top:4px;font-weight:600;font-size:13px;color:#5c4a33}
        .row-counts span{display:inline-flex;align-items:center;gap:6px}
        .row-counts svg{width:16px;height:16px}
        .row-tags{display:flex;flex-wrap:wrap;gap:6px;margin-top:6px}
        .row-tags .tag{position:relative;z-index:1;min-height:28px;padding:2px 12px;border-color:rgba(143,42,32,.45);color:#8f2a20;font-size:13px}
        .row-tags .tag:hover{background:#d8c59a;color:#8f2a20}
        mark{border-radius:2px;background:#f0d59a;color:#2b2117}
        .sheet-none{display:flex;flex-direction:column;align-items:center;gap:16px;padding:24px 0 8px;text-align:center}
        .sheet-none p{margin:0;font-family:'EB Garamond',serif;font-size:19px;line-height:1.5}
        .sheet-button{display:inline-flex;align-items:center;min-height:44px;padding:8px 20px;border:1px solid #8f2a20;border-radius:999px;font-weight:600;font-size:15px;color:#8f2a20;text-decoration:none}
        .sheet-button:hover{background:#e0d0a8;color:#8f2a20}
        .archive-hero .marginalia,.month-index{display:none}
        .side{display:contents}
        .archive-loading{width:100%;display:flex;flex-direction:column;gap:12px;margin-top:12px}
        .skeleton-row{height:118px;border-radius:20px;background:#1a1612}
        .archive-more{display:flex;justify-content:center;padding-top:28px}
        .archive-more:has(.archive-seal){flex-direction:column;align-items:center;padding-top:0}
        .archive-seal{display:flex;flex-direction:column;align-items:center;margin-bottom:24px}
        .closing-seal{display:block;margin-top:-30px;line-height:0}
        .archive-seal .purity-strip{margin:-18px 0 0;border-radius:4px;transform:rotate(-3deg)}
        .tags{display:flex;flex-wrap:wrap;justify-content:center;align-items:center;gap:8px 10px;margin:18px 0 0;font-size:15px;color:#bfb39a}
        .tag{display:inline-flex;align-items:center;min-height:32px;padding:4px 14px;border-radius:999px;border:1px solid rgba(201,162,90,.45);font-size:14px;font-weight:600;color:#d9b874;text-decoration:none;white-space:nowrap}
        .tag:hover{background:rgba(201,162,90,.12);color:#f0d59a}
        .other-subjects{width:100%;margin-top:56px;display:flex;flex-direction:column;align-items:center;gap:12px}
        .other-subjects h2{margin:0;font-family:Cinzel,serif;font-weight:700;font-size:22px}
        nav .nav-search{display:grid;place-items:center;width:44px;height:44px;padding:0;color:#d9b874}
        .nav-find{position:relative;display:flex;align-items:center;width:44px;height:44px;border:1px solid transparent;border-radius:999px;transition:width 250ms ease}
        .nav-find.open{width:320px;border-color:#84662c}
        .nav-find:has(.nav-find-field:focus-visible){outline:2px solid #f0b45a;outline-offset:4px}
        .nav-find-open,.nav-find-close{flex:none;display:grid;place-items:center;width:42px;height:42px;padding:0;border:0;border-radius:999px;background:transparent;cursor:pointer}
        .nav-find-open{color:#d9b874}
        .nav-find-close{display:none;color:#b6a98f}
        .nav-find.open .nav-find-close{display:grid}
        .nav-find-field{flex:1;min-width:0;width:0;height:42px;padding:0;border:0;background:transparent;color:#ede3cf;font:16px Figtree,system-ui,sans-serif}
        .nav-find:not(.open) .nav-find-field{visibility:hidden}
        .nav-find-field:focus-visible{outline:none}
        .nav-find-field::placeholder{color:#8f8068;opacity:1}
        .nav-find-drop{position:absolute;top:calc(100% + 8px);right:0;z-index:30;width:400px;max-width:calc(100vw - 32px);max-height:calc(100dvh - 96px);overflow-y:auto;overscroll-behavior:contain;padding:8px;border-radius:14px;background:#ebdfc2;color:#2b2117;text-align:left;box-shadow:0 14px 30px rgba(0,0,0,.55),inset 0 0 0 4px #ebdfc2,inset 0 0 0 5px #8f2a20}
        .nav-find-drop[hidden]{display:none}
        .nav-find-drop :focus-visible{outline-color:#8f2a20;outline-offset:2px}
        .nav-find-label{margin:0;padding:8px 12px 4px;font-family:'EB Garamond',serif;font-style:italic;font-size:15px;color:#5c4a33}
        .nav-find-none{margin:0;padding:4px 12px 8px;font-family:'EB Garamond',serif;font-size:17px;line-height:1.4}
        .nav-find-drop [role=option]{position:relative;display:flex;flex-direction:column;justify-content:center;gap:2px;min-height:44px;padding:10px 12px;border-radius:6px;text-decoration:none}
        .nav-find-drop [role=option]:hover{background:#dfcda2}
        .nav-find-drop [aria-selected=true]{background:#dfcda2;outline:2px solid #8f2a20;outline-offset:-2px}
        .nav-find-title{font-family:Cinzel,serif;font-weight:700;font-size:16px;line-height:1.25;color:#8f2a20}
        .nav-find-meta{font-weight:600;font-size:13px;color:#5c4a33}
        .nav-find-all{position:relative;display:flex;align-items:center;min-height:44px;margin-top:4px;padding:8px 12px;border-top:1px solid rgba(143,42,32,.25);font-weight:600;font-size:14px;color:#8f2a20;text-decoration:none}
        .nav-find-all:hover{color:#8f2a20;text-decoration:underline}
        @media (max-width:699px){
        nav:has(.nav-find.open)>a{display:none}
        .nav-find.open{width:min(358px,calc(100vw - 32px))}
        .nav-find-drop{left:0;width:auto;max-width:none}
        }
        .archive-end{margin:0;font-family:'EB Garamond',serif;font-style:italic;font-size:19px;color:#b6a98f}
        .state{display:flex;flex-direction:column;align-items:center;gap:16px;max-width:580px;padding:24px 0}
        .state h1,.state h2{margin:0;font-family:Cinzel,serif;font-size:clamp(26px,3.6vw,36px);line-height:1.15}
        .state h1{font-size:clamp(28px,4vw,42px);line-height:1.12}
        .state-body{margin:0;font-family:'EB Garamond',serif;font-size:21px;line-height:1.5;color:#d7c9ad;text-wrap:balance;overflow-wrap:anywhere}
        .state-eyebrow{margin:0;font-family:'JetBrains Mono',monospace;font-size:14px;letter-spacing:.1em;color:#8cc4ae}
        .state-code{max-width:100%;overflow-wrap:anywhere;font-family:'JetBrains Mono',monospace;font-size:14px;padding:8px 14px;border-radius:12px;background:#141c1f;border:1px solid #2e4248;color:#dbe6e8}
        .state-actions{display:flex;flex-wrap:wrap;justify-content:center;gap:12px;margin-top:6px}
        .site-footer{margin-top:72px;padding-bottom:48px;display:flex;flex-direction:column;align-items:center;gap:12px;max-width:600px}
        .motto{margin:0;font-family:Cinzel,serif;font-weight:700;font-size:16px;letter-spacing:.08em;color:#c9a25a}
        .disclaimer{margin:0;font-size:14px;line-height:1.6;color:#b6a98f}
        .toast-region{position:fixed;left:50%;bottom:24px;transform:translateX(-50%);z-index:40;pointer-events:none;width:max-content;max-width:calc(100vw - 32px)}
        .toast{padding:12px 22px;border-radius:24px;background:#ede3cf;color:#100e0b;font-size:15px;font-weight:600;line-height:1.4;text-align:center;box-shadow:0 8px 28px rgba(0,0,0,.5)}
        @media (min-width:700px){
        .page:has(.masthead) .site-header{margin-bottom:28px}
        .masthead{gap:12px;margin-bottom:28px}
        .masthead-rule{width:224px}
        .masthead .site-name{font-size:44px}
        .tagline{font-size:18px}
        .sheet-inner{padding:56px 64px 72px}
        .sheet .corner{display:block}
        .sheet-title{font-size:34px}
        .sheet-sub{font-size:16px}
        .sheet-search{height:40px}
        .month-rubric{font-size:12px}
        .row{display:grid;grid-template-columns:80px minmax(0,1fr);gap:20px;padding:14px 8px}
        .row-meta{flex-direction:column;gap:4px;padding-top:3px}
        .row-date{font-size:14px}
        .row-title{font-size:19px}
        .row-line{font-size:17px}
        }
        @media (min-width:1280px){
        .hero{--gap:clamp(32px,3vw,60px);display:grid;grid-template-columns:minmax(0,1fr) minmax(0,740px) minmax(0,1fr);column-gap:var(--gap);align-items:start;width:min(100vw - 48px,1220px + 2*var(--gap))}
        .hero>.rite{grid-column:2;grid-row:1}
        .hero .parchment{padding:56px 92px 44px}
        .hero>.truth{grid-column:2;grid-row:2;justify-self:center}
        .actions{display:contents}
        .faithful,.marginalia{grid-row:1;display:flex;flex-direction:column;gap:28px;margin-top:80px}
        .faithful{grid-column:3;gap:10px;align-items:flex-start;padding-left:28px;border-left:1px solid #2f2820;text-align:left}
        .marginalia{grid-column:1;align-items:flex-end;padding-right:28px;border-right:1px solid #2f2820;text-align:right}
        .faithful>.margin-label,.margin-countdown{display:block}
        .margin-label{margin:0 0 10px;font-family:Cinzel,serif;font-weight:700;font-size:12px;letter-spacing:.2em;text-transform:uppercase;color:#c9a25a}
        .faithful>.margin-label{margin:0}
        .margin-value{margin:0;font-family:'EB Garamond',serif;font-size:24px;color:#ede3cf;font-variant-numeric:tabular-nums}
        .faithful .reactions{justify-content:flex-start}
        .faithful .vote-status{margin-top:18px;text-align:left}
        .sharing{grid-column:2;grid-row:3;display:flex;flex-wrap:wrap;justify-content:center;gap:12px;margin-top:28px}
        .marginalia .tags{justify-content:flex-end;margin:0}
        .tags .margin-label{flex-basis:100%;margin-bottom:0}
        .hero>.sheet{grid-column:2;grid-row:1}
        .hero>.archive-more{grid-column:2;grid-row:2}
        .hero:has(.archive-seal)>.sheet{align-self:stretch}
        .archive-hero .marginalia{display:flex}
        .side{grid-row:1;align-self:start;display:flex;flex-direction:column;gap:56px;margin-top:80px}
        .side-left{grid-column:1}
        .side-right{grid-column:3}
        .side>.marginalia,.side>.month-index,.side>.other-subjects{margin-top:0}
        .side>.altar{align-self:center}
        .sub-wide{display:inline}
        .sub-narrow{display:none}
        .margin-number{margin:0;font-family:Cinzel,serif;font-weight:700;font-size:32px;line-height:1.1;color:#ede3cf}
        .margin-since{margin:0;font-family:'EB Garamond',serif;font-style:italic;font-size:20px;color:#cdbd9e}
        .margin-sought{margin:0;font-family:'EB Garamond',serif;font-style:italic;font-size:22px;color:#ede3cf;overflow-wrap:anywhere}
        .margin-clear{font-weight:600;text-decoration:none}
        .month-index,.other-subjects{grid-column:3;grid-row:1;align-self:start;display:flex;flex-direction:column;align-items:stretch;gap:12px;width:auto;margin-top:80px;padding-left:28px;border-left:1px solid #2f2820;text-align:left}
        .month-index .margin-label{margin:0}
        .month-index ul{list-style:none;margin:0;padding:0;display:flex;flex-direction:column;gap:12px}
        .month-index a{display:flex;justify-content:space-between;gap:12px;font-family:'EB Garamond',serif;font-size:20px;color:#ede3cf;text-decoration:none}
        .month-index a:hover{color:#f0d59a}
        .month-count{padding-top:4px;font-family:'JetBrains Mono',monospace;font-weight:500;font-size:14px;color:#b6a98f}
        .other-subjects h2{font-size:12px;letter-spacing:.2em;text-transform:uppercase;color:#c9a25a}
        .other-subjects .tags{justify-content:flex-start;margin:0}
        .countdown{margin-top:28px}
        .countdown-line{display:none}
        }
        """ + Altar + """
        @media (prefers-reduced-motion:reduce){*,*::before,*::after{animation:none!important;transition:none!important}}
        """;

    /// <summary>
    /// The wide screen's decoration, which the Scriptorium shares (its stylesheet is its own): a candle in each margin of
    /// a three-column grid (Today's rite, or an altar heading), from 1280px, and circuit traces in the space outside it,
    /// from 1440px, where that space is wide enough to see them. An altar heading has the glow behind it at every width.
    /// The candles' glow reaches past the screen's edge, so the page's frame clips it (<c>overflow-x: clip</c>).
    /// </summary>
    public const string Altar = """
        .altar,.traces{display:none}
        .altar-head{position:relative;width:100%}
        .altar-head::before{content:'';position:absolute;left:50%;top:100%;z-index:-1;width:600px;height:920px;transform:translate(-50%,-50%);background:radial-gradient(ellipse 300px 460px,#2b2117 0%,#18140f 55%,rgba(16,14,11,0) 100%);pointer-events:none}
        @media (min-width:700px){.altar-head::before{width:1520px;height:1280px;background:radial-gradient(ellipse 760px 640px,#2b2117 0%,#18140f 55%,rgba(16,14,11,0) 100%)}}
        @media (min-width:1280px){
        .altar-head{--gap:clamp(32px,3vw,60px);--width:min(100vw - 48px,1220px + 2*var(--gap));display:grid;grid-template-columns:minmax(0,1fr) minmax(0,740px) minmax(0,1fr);column-gap:var(--gap);width:var(--width);margin-inline:calc((100% - var(--width))/2)}
        .altar-head>:first-child{grid-column:2;grid-row:1}
        .altar{display:block;position:relative;grid-row:1;align-self:end;justify-self:center;line-height:0}
        .altar-left{grid-column:1}
        .altar-right{grid-column:3}
        .altar::before{content:'';position:absolute;left:50%;top:30%;z-index:-1;width:600px;height:600px;transform:translate(-50%,-50%);background:radial-gradient(circle 300px,rgba(240,180,90,.16),transparent 70%);pointer-events:none}
        .altar-flame{transform-box:fill-box;transform-origin:50% 100%;animation:altar-flicker 3.6s ease-in-out infinite}
        .altar-right .altar-flame{animation-duration:4.3s;animation-delay:1.1s}
        }
        @keyframes altar-flicker{0%,100%{transform:scaleY(1)}40%{transform:scaleY(.93) skewX(1.5deg)}70%{transform:scaleY(1.025) skewX(-1deg)}}
        @media (min-width:1440px){
        .traces{display:block;position:absolute;top:-240px;z-index:-1;width:calc(50vw - 50%);height:950px;overflow:hidden;pointer-events:none}
        .altar-head .traces{top:-120px}
        .traces-left{right:100%}
        .traces-right{left:100%}
        .traces svg{position:absolute;top:0}
        .traces-left svg{left:0}
        .traces-right svg{right:0;transform:scaleX(-1)}
        }
        """;
}
