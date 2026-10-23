namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Precepts' script, on top of their forms and links: a precept's words open to edit in place (its pencil, or a
/// click on the words), and Add the Precept waits for words. Without it, the pencil is a link to the page with those
/// words open (<c>?edit=</c>).
/// </summary>
public static class PreceptsScript
{
    public const string Js = """
        (() => {
          const main = document.getElementById('main');
          main.setAttribute('data-edits-in-place', '');

          // One precept open to edit at a time; closing one gives focus back to its pencil, and leaves its words as they
          // were.
          const edit = (precept, open) => {
            main.querySelectorAll('.precept.editing').forEach((other) => {
              if (other !== precept) close(other, false);
            });
            if (!open) {
              close(precept, true);
              return;
            }
            precept.classList.add('editing');
            const field = precept.querySelector('.precept-edit textarea');
            field.focus();
            field.setSelectionRange(field.value.length, field.value.length);
          };
          function close(precept, refocus) {
            const field = precept.querySelector('.precept-edit textarea');
            if (field) field.value = field.defaultValue;
            precept.classList.remove('editing');
            if (refocus) precept.querySelector('.icon-button.edit')?.focus();
          }

          main.addEventListener('click', (event) => {
            const target = event.target;
            if (!(target instanceof Element)) return;
            const precept = target.closest('.precept:not(.own)');
            if (!precept) return;
            // The pencil, or the words themselves, unless they're being selected.
            if (target.closest('.icon-button.edit') || (target.closest('.precept-text') && !getSelection()?.toString())) {
              event.preventDefault();
              edit(precept, true);
            } else if (target.closest('.precept-edit .cancel')) {
              event.preventDefault();
              edit(precept, false);
            }
          });
          main.addEventListener('keydown', (event) => {
            const precept = event.target instanceof Element ? event.target.closest('.precept.editing') : null;
            if (event.key === 'Escape' && precept) edit(precept, false);
          });

          const own = main.querySelector('.precept-add');
          if (own) {
            const field = own.querySelector('textarea');
            const add = own.querySelector('[data-needs-words]');
            const wait = () => { add.disabled = field.value.trim() === ''; };
            field.addEventListener('input', wait);
            wait();
          }
        })();
        """;
}
