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
