'use strict';

// Rendu de la page « où on en est ». Source unique dans 0-AllMyPersoRepo/cockpit/.
// Les données viennent d'etat.json, généré — cette page ne calcule rien.

const $ = (id) => document.getElementById(id);
const esc = (s) =>
  String(s ?? '').replace(
    /[&<>"']/g,
    (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c],
  );
const grab = (n) => fetch(n, { cache: 'no-store' }).then((r) => (r.ok ? r.json() : null)).catch(() => null);

async function load() {
  const [e, roadmap, marche] = await Promise.all([
    grab('etat.json'), grab('roadmap.json'), grab('marche.json'),
  ]);

  const rail = [{ url: 'index.html', label: 'Le cockpit', note: 'feuille de route, journal, carte système' }];
  if (marche) rail.push({ url: 'marche.html', label: 'Le marché', note: 'qui existe déjà, et la case vide' });
  $('links').innerHTML = rail
    .map((l) => `<a href="${esc(l.url)}" data-in title="${esc(l.note)}">${esc(l.label)}</a>`)
    .join('');

  if (!e) {
    $('empty').innerHTML = `<div class="empty"><b>Aucun relevé.</b><br><br>
      Il manque <code>dashboard/etat.json</code>. Il se génère :
      <code style="display:block;margin-top:8px">python ops/cockpit.py etat</code></div>`;
    return;
  }

  const name = e.repo ?? (roadmap?.epic ? String(roadmap.epic).split('—')[0].trim() : '');
  $('title').textContent = name ? `${name} — où on en est` : 'Où on en est';
  document.title = `${name || 'projet'} — où on en est`;
  $('updated').textContent = e.generated ? `relevé du ${e.generated}` : '—';

  // Un relevé qui date n'est pas un relevé : on le dit avant de montrer un
  // seul chiffre, sinon la page ment pendant tout le temps qu'on la lit.
  if (e.staleDays > 1) {
    $('stale').hidden = false;
    $('stale').textContent =
      `Ce relevé a ${e.staleDays} jours et le dépôt a bougé depuis. Rejoue `
      + `python ops/cockpit.py etat avant de croire les chiffres ci-dessous.`;
  }

  $('facts').innerHTML = (e.facts ?? [])
    .map(
      (f) => `<div class="kpi" title="${esc(f.source ?? '')}">
        <div class="kpi__v" data-s="${esc(f.state ?? 'good')}">${esc(f.value)}</div>
        <div class="kpi__l">${esc(f.label)}</div>
        ${f.source ? `<div class="kpi__n">${esc(f.source)}</div>` : ''}
      </div>`,
    )
    .join('');

  $('groups').innerHTML = (e.groups ?? [])
    .map(
      (g) => `<section>
        <div class="shead">
          <h2>${esc(g.h)}</h2>
          ${g.note ? `<span class="note">${esc(g.note)}</span>` : ''}
        </div>
        <div class="scroller"><table class="data">
          <thead><tr><th>Ce qu'on regarde</th><th>Ce que ça donne</th><th>D'où ça vient</th></tr></thead>
          <tbody>${(g.rows ?? [])
            .map(
              (r) => `<tr>
                <td style="color:var(--zinc-100)">${esc(r.k)}</td>
                <td${r.s ? ` data-s="${esc(r.s)}"` : ''} style="white-space:normal">${esc(r.v)}</td>
                <td style="color:var(--zinc-500);white-space:normal"><code>${esc(r.src ?? '')}</code></td>
              </tr>`,
            )
            .join('')}</tbody>
        </table></div>
      </section>`,
    )
    .join('');

  if ((e.gaps ?? []).length) {
    $('gaps-section').hidden = false;
    // Le gras du générateur porte le mot qui compte dans la phrase ; l'échapper
    // puis le rendre, dans cet ordre, garde l'emphase sans ouvrir de balise.
    $('gaps').innerHTML = e.gaps
      .map((g) => `<li>${esc(g).replace(/\*\*(.+?)\*\*/g, '<b>$1</b>')}</li>`)
      .join('');
  }
}

load();
