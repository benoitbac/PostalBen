'use strict';

// Rendu de la page marché. Source unique dans 0-AllMyPersoRepo/cockpit/.

const $ = (id) => document.getElementById(id);
const esc = (s) =>
  String(s ?? '').replace(
    /[&<>"']/g,
    (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c],
  );
// Le gras vient de la prose écrite à la main : on échappe d'abord, on rend
// l'emphase ensuite — dans cet ordre, aucune balise ne peut être ouverte.
const rich = (s) => esc(s).replace(/\*\*(.+?)\*\*/g, '<b>$1</b>');
const grab = (n) => fetch(n, { cache: 'no-store' }).then((r) => (r.ok ? r.json() : null)).catch(() => null);

const RAIL = [
  { url: 'index.html', label: 'Le cockpit', note: 'la feuille de route, le journal, la carte système' },
  { url: 'etat.html', label: 'Où on en est', note: 'les faits mesurés du dépôt' },
];

async function load() {
  const [m, roadmap] = await Promise.all([grab('marche.json'), grab('roadmap.json')]);
  const name = roadmap?.epic ? String(roadmap.epic).split('—')[0].trim() : '';

  $('links').innerHTML = RAIL.map(
    (l) => `<a href="${esc(l.url)}" data-in title="${esc(l.note)}">${esc(l.label)}</a>`,
  ).join('');

  if (!m) {
    $('title').textContent = name ? `${name} — le marché` : 'Le marché';
    document.title = 'le marché — à écrire';
    $('empty').innerHTML = `<div class="empty">
      <b>Cette page n'existe pas encore pour ce projet.</b><br><br>
      Il manque <code>dashboard/marche.json</code>. Elle ne peut pas être générée :
      un paysage concurrentiel ne se déduit d'aucune commande, il se lit et il s'écrit.
      Le modèle est <code>NextTatoo/docs/MARCHE.md</code> — six familles d'outils, ce que
      chacune ne fait pas, la case vide nommée, et jusqu'où elle va.<br><br>
      Tant qu'elle n'est pas écrite, ce projet ne sait pas dire par écrit qui existe en face.
    </div>`;
    return;
  }

  $('title').textContent = name ? `${name} — le marché` : (m.title ?? 'Le marché');
  document.title = `${name || 'projet'} — le marché`;
  $('updated').textContent = m.updated ? `mis à jour ${m.updated}` : '—';
  $('claim').innerHTML = rich(m.claim ?? '');

  // Un chiffre non mesuré ne prend pas la couleur d'un chiffre mesuré. C'est la
  // seule chose qui empêche une estimation de se faire passer pour un relevé.
  $('size').innerHTML = (m.size ?? [])
    .map(
      (s) => `<div class="kpi">
        <div class="kpi__v" data-s="${s.measured ? 'good' : 'none'}">${esc(s.value)}</div>
        <div class="kpi__l">${esc(s.label)}</div>
        <div class="kpi__n">${s.measured ? esc(s.note ?? 'mesuré') : `estimé — ${esc(s.note ?? 'aucune source')}`}</div>
      </div>`,
    )
    .join('');

  const fam = m.families ?? [];
  if (fam.length) {
    $('families-section').hidden = false;
    $('families').innerHTML =
      '<thead><tr><th>Famille</th><th>Acteurs</th><th>Ce qu\'ils font</th><th>Ce qu\'ils ne font pas</th></tr></thead>'
      + `<tbody>${fam
        .map(
          (f) => `<tr>
            <td style="color:var(--zinc-100);font-weight:700">${esc(f.family)}</td>
            <td style="white-space:normal">${rich(f.actors)}</td>
            <td style="white-space:normal">${rich(f.do)}</td>
            <td style="white-space:normal;color:var(--amber)">${rich(f.dont)}</td>
          </tr>`,
        )
        .join('')}</tbody>`;
  }

  if (m.gap || (m.sections ?? []).length) {
    $('gap-section').hidden = false;
    $('gap').innerHTML = rich(m.gap ?? '');
    $('sections').innerHTML = (m.sections ?? [])
      .map(
        (s) => `<h3>${esc(s.h)}</h3>${(s.p ?? []).map((p) => `<p>${rich(p)}</p>`).join('')}`,
      )
      .join('');
  }

  if ((m.reserve ?? []).length) {
    $('reserve-section').hidden = false;
    $('reserve').innerHTML = m.reserve.map((r) => `<li>${rich(r)}</li>`).join('');
  }
}

load();
