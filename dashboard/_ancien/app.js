'use strict';

// La logique du cockpit. Identique dans les 18 dépôts — source unique dans
// 0-AllMyPersoRepo/cockpit/. NE PAS ÉDITER dans un dépôt.

const $ = (id) => document.getElementById(id);
const esc = (s) =>
  String(s ?? '').replace(
    /[&<>"']/g,
    (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c],
  );

const MONTHS = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.'];
const dayLabel = (ms) => {
  const d = new Date(ms);
  return `${d.getDate()} ${MONTHS[d.getMonth()]}`;
};

const grab = (name) =>
  fetch(name, { cache: 'no-store' }).then((r) => (r.ok ? r.json() : null)).catch(() => null);

// ---------------------------------------------------------------------
// Gantt SVG fait main — pas de librairie de graphes dans ce dépôt.
//
// Une ligne PAR SPRINT, pas par tâche. Quarante-cinq barres de six pixels avec
// une graduation quotidienne, c'est une image qu'on ne lit pas. Chaque barre
// porte donc deux informations : sa POSITION dit quand, son REMPLISSAGE dit où
// on en est. Le compte est écrit en clair au bout, parce qu'une proportion se
// compare mal à l'œil d'une ligne à l'autre.
// ---------------------------------------------------------------------
function renderGantt(roadmap) {
  const sprints = (roadmap.sprints ?? [])
    .map((s) => ({ ...s, a: Date.parse(s.start), b: Date.parse(s.end) }))
    .filter((s) => !Number.isNaN(s.a) && !Number.isNaN(s.b));

  if (!sprints.length) return '<p class="lede">Aucun sprint daté.</p>';

  const day = 86400000;
  const tMin = Math.min(...sprints.map((s) => s.a)) - day * 2;
  const tMax = Math.max(...sprints.map((s) => s.b)) + day * 2;

  const W = 1000;
  const leftLabel = 268;
  const rightPad = 56;
  const topAxis = 72;
  const rowH = 26;
  const rowGap = 14;
  const innerW = W - leftLabel - rightPad;
  const totalMs = Math.max(tMax - tMin, 1);
  const H = topAxis + sprints.length * (rowH + rowGap) + 18;
  const x = (ms) => leftLabel + ((ms - tMin) / totalMs) * innerW;

  let g = `<svg viewBox="0 0 ${W} ${H}" width="${W}" height="${H}" role="img" aria-label="Feuille de route : un sprint par ligne, la position donne les dates et le remplissage l'avancement">`;

  // Bandes mensuelles alternées : elles donnent l'échelle sans ajouter un seul
  // trait de plus, et c'est ce qui permet d'espacer les dates à deux semaines.
  const first = new Date(tMin);
  let band = new Date(first.getFullYear(), first.getMonth(), 1).getTime();
  let odd = 0;
  while (band <= tMax) {
    const next = new Date(new Date(band).getFullYear(), new Date(band).getMonth() + 1, 1).getTime();
    const bx = Math.max(x(band), leftLabel);
    const bw = Math.min(x(next), W - rightPad) - bx;
    if (bw > 0) {
      if (odd % 2 === 1) {
        g += `<rect x="${bx}" y="${topAxis - 22}" width="${bw}" height="${H - topAxis + 16}" fill="#ffffff" fill-opacity="0.022"/>`;
      }
      if (bw > 46) {
        g += `<text x="${bx + 6}" y="${topAxis - 50}" fill="#71717a" style="font-size:11px;letter-spacing:.1em;text-transform:uppercase">${MONTHS[new Date(band).getMonth()].replace('.', '')}</text>`;
      }
    }
    band = next;
    odd++;
  }

  // Graduation tous les 14 jours. Le quotidien produisait 90 étiquettes de 9 px
  // qui se chevauchaient — donc une frise qui ne datait plus rien.
  const step = day * 14;
  const firstTick = Math.ceil(tMin / step) * step;
  for (let t = firstTick; t <= tMax; t += step) {
    const gx = x(t);
    g += `<line x1="${gx}" y1="${topAxis - 16}" x2="${gx}" y2="${H - 8}" stroke="#27272a" stroke-dasharray="2 5"/>`;
    g += `<text x="${gx}" y="${topAxis - 4}" text-anchor="middle" fill="#a1a1aa" style="font-size:11px">${dayLabel(t)}</text>`;
  }

  const now = Date.now();
  if (now >= tMin && now <= tMax) {
    const tx = x(now);
    g += `<line x1="${tx}" y1="${topAxis - 16}" x2="${tx}" y2="${H - 8}" stroke="#fbbf24" stroke-width="1.5"/>`;
    g += `<circle cx="${tx}" cy="${topAxis - 16}" r="3.5" fill="#fbbf24"/>`;
    g += `<text x="${tx}" y="${topAxis - 28}" text-anchor="middle" fill="#fbbf24" style="font-size:10px;font-weight:700;letter-spacing:.14em">AUJOURD’HUI</text>`;
  }

  sprints.forEach((sprint, i) => {
    const y = topAxis + i * (rowH + rowGap);
    const x1 = x(sprint.a);
    const x2 = Math.max(x(sprint.b), x1 + 14);
    const tasks = sprint.tasks ?? [];
    const done = tasks.filter((t) => t.status === 'done').length;
    const doing = tasks.filter((t) => t.status === 'doing').length;
    const blocked = tasks.filter((t) => t.status === 'blocked').length;
    const ratio = tasks.length ? done / tasks.length : 0;
    const live = now >= sprint.a && now <= sprint.b;

    g += `<text x="0" y="${y + 12}" fill="#f4f4f5" style="font-size:12.5px;font-weight:700">${esc(sprint.id)} · ${esc(sprint.title)}</text>`;
    g += `<text x="0" y="${y + 26}" fill="#71717a" style="font-size:10.5px">${dayLabel(sprint.a)} → ${dayLabel(sprint.b)}</text>`;

    g += `<rect x="${x1}" y="${y}" width="${x2 - x1}" height="${rowH}" rx="5" fill="#27272a" stroke="${live ? '#fbbf24' : '#3f3f46'}" stroke-width="${live ? 1.4 : 1}">`;
    g += `<title>${esc(sprint.goal ?? sprint.title)}</title></rect>`;

    if (ratio > 0) {
      const fw = Math.max((x2 - x1) * ratio, 5);
      g += `<rect x="${x1}" y="${y}" width="${fw}" height="${rowH}" rx="5" fill="#34d399" fill-opacity="0.55"/>`;
    }
    if (doing > 0) {
      const dx = x1 + (x2 - x1) * ratio;
      g += `<rect x="${dx}" y="${y}" width="4" height="${rowH}" fill="#fbbf24"/>`;
    }
    if (blocked > 0) {
      g += `<circle cx="${x2 - 9}" cy="${y + 9}" r="4" fill="#fb7185"><title>${blocked} tâche(s) bloquée(s)</title></circle>`;
    }

    g += `<text x="${x2 + 10}" y="${y + 17}" fill="${ratio === 1 ? '#34d399' : '#a1a1aa'}" style="font-size:12px;font-weight:700">${done}/${tasks.length}</text>`;
  });

  return `${g}</svg>`;
}

// ---------------------------------------------------------------------
// Burn-up SVG fait main.
// ---------------------------------------------------------------------
function renderBurnup(changelog) {
  const history = changelog.history ?? [];
  if (history.length < 2) return '';
  const goal = changelog.goal ?? Math.max(...history.map((h) => h.done));

  const W = 340;
  const H = 110;
  const pad = { l: 26, r: 12, t: 12, b: 22 };
  const iw = W - pad.l - pad.r;
  const ih = H - pad.t - pad.b;
  const max = Math.max(goal, ...history.map((h) => h.done)) || 1;
  const px = (i) => pad.l + (i / (history.length - 1)) * iw;
  const py = (v) => pad.t + ih - (v / max) * ih;

  const line = history.map((h, i) => `${px(i)},${py(h.done)}`).join(' ');
  const area = `${pad.l},${py(0)} ${line} ${px(history.length - 1)},${py(0)}`;

  let g = `<svg viewBox="0 0 ${W} ${H}" width="${W}" height="${H}" role="img" aria-label="Burn-up">`;
  g += `<polygon points="${area}" fill="rgba(52,211,153,0.14)"/>`;
  g += `<polyline points="${line}" fill="none" stroke="#34d399" stroke-width="2"/>`;
  g += `<line x1="${pad.l}" y1="${py(goal)}" x2="${W - pad.r}" y2="${py(goal)}" stroke="#fbbf24" stroke-dasharray="4 4"/>`;
  g += `<text x="${W - pad.r}" y="${py(goal) - 4}" text-anchor="end" fill="#fbbf24" style="font-size:9px">objectif ${goal}</text>`;

  history.forEach((h, i) => {
    g += `<circle cx="${px(i)}" cy="${py(h.done)}" r="3" fill="#34d399"><title>${esc(h.label)} — ${h.done} tâches</title></circle>`;
    if (i === 0 || i === history.length - 1) {
      g += `<text x="${px(i)}" y="${H - 6}" text-anchor="${i === 0 ? 'start' : 'end'}" fill="#52525b" style="font-size:9px">${esc(h.label)}</text>`;
    }
  });
  return `${g}</svg>`;
}

// ---------------------------------------------------------------------
// Vision — où ça va, et pourquoi c'est gros. Optionnel : sans vision.json le
// panneau reste caché plutôt que de s'afficher vide.
//
// Chaque surface porte une réserve, et ce n'est pas de la modestie : une
// ambition dont on ne sait pas nommer le point faible est une ambition que
// personne n'a encore attaquée.
// ---------------------------------------------------------------------
function renderVision(vision) {
  if (!vision) return;
  $('vision-section').hidden = false;
  $('vision-thesis').innerHTML = String(vision.thesis ?? '')
    .replace(/\*\*(.+?)\*\*/g, (_, m) => `<em>${esc(m)}</em>`);
  $('vision-wedge').textContent = vision.wedge ?? '';

  $('vision-surfaces').innerHTML = (vision.surfaces ?? [])
    .map(
      (s) => `<article class="surf">
        <div class="surf__top">
          <span class="surf__name">${esc(s.name)}</span>
          <span class="surf__state" data-s="${esc(s.state ?? 'later')}">${esc(s.stateLabel ?? s.state ?? '')}</span>
        </div>
        <div class="surf__claim">${esc(s.claim ?? '')}</div>
        ${s.evidence ? `<div class="surf__evidence">${esc(s.evidence)}</div>` : ''}
        ${s.reserve ? `<div class="surf__reserve">réserve — ${esc(s.reserve)}</div>` : ''}
      </article>`,
    )
    .join('');

  $('vision-flywheel').innerHTML = (vision.flywheel ?? [])
    .map(
      (f, i) => `<div class="fly">
        <div class="fly__n">${String(i + 1).padStart(2, '0')} ${esc(f.step ?? '')}</div>
        <div class="fly__t">${esc(f.text ?? '')}</div>
      </div>`,
    )
    .join('');

  $('vision-moat').innerHTML = vision.moat
    ? `<b>Ce qu'on ne peut pas nous reprendre —</b> ${esc(vision.moat)}`
    : '';

  renderProof(vision.proof);
}

// Trois états, et le mot compte : `directional` n'est pas un `proven` timide.
// C'est « on sait de quel côté ça penche, pas de combien » — la seule case qui
// autorise à décider sans mesure, et seulement parce que le sens suffit.
const PROOF_STATES = {
  proven: 'prouvé',
  directional: 'le sens, pas l’ampleur',
  unproven: 'pas prouvé',
};

function renderProof(proof) {
  if (!Array.isArray(proof) || proof.length === 0) return;
  $('proof-section').hidden = false;

  const tally = { proven: 0, directional: 0, unproven: 0 };
  for (const p of proof) {
    if (tally[p.state] !== undefined) tally[p.state] += 1;
  }
  $('proof-tally').innerHTML = Object.entries(tally)
    .map(
      ([k, n]) =>
        `<span><span class="proof__state" data-s="${k}">${esc(PROOF_STATES[k])}</span> <b>${n}</b></span>`,
    )
    .join('');

  $('proof-list').innerHTML = proof
    .map(
      (p) => `<div class="proof">
        <span class="proof__state" data-s="${esc(p.state ?? 'unproven')}">${esc(PROOF_STATES[p.state] ?? p.state ?? '')}</span>
        <div>
          <div class="proof__claim">${esc(p.claim ?? '')}</div>
          ${p.note ? `<div class="proof__note">${esc(p.note)}</div>` : ''}
        </div>
      </div>`,
    )
    .join('');
}

// ---------------------------------------------------------------------
// Mesures — le panneau venu de NextQR, rendu générique.
//
// Générique veut dire : le JSON décrit ses propres colonnes. Un banc de
// décodage QR et un relevé d'allocations n'ont pas les mêmes, et coder l'une
// des deux formes en dur revenait à interdire l'autre.
// ---------------------------------------------------------------------
function renderBench(bench) {
  if (!bench) return;
  $('bench-section').hidden = false;
  $('bench-note').textContent = bench.note ?? '';
  $('bench-updated').textContent = bench.updated ? `relevé du ${bench.updated}` : '';

  $('bench-summary').innerHTML = (bench.summary ?? [])
    .map(
      (s) => `<div class="kpi">
        <div class="kpi__v" data-s="${esc(s.state ?? 'good')}">${esc(s.value)}</div>
        <div class="kpi__l">${esc(s.label)}</div>
        ${s.note ? `<div class="kpi__n">${esc(s.note)}</div>` : ''}
      </div>`,
    )
    .join('');

  const cols = bench.columns ?? [];
  const rows = bench.rows ?? [];
  $('bench-table').innerHTML = !cols.length
    ? ''
    : `<thead><tr>${cols.map((c) => `<th>${esc(c.t)}</th>`).join('')}</tr></thead>`
      + `<tbody>${rows
        .map(
          (r) =>
            `<tr>${cols
              .map((c) => {
                const v = r[c.k];
                const cls = c.num ? ' class="num"' : '';
                const st = r[`${c.k}_s`] ? ` data-s="${esc(r[`${c.k}_s`])}"` : '';
                return `<td${cls}${st}>${esc(v ?? '')}</td>`;
              })
              .join('')}</tr>`,
        )
        .join('')}</tbody>`;
}

// ---------------------------------------------------------------------
// Comment tester — repris de NextQR, où il était écrit en dur dans le HTML.
// En données, il devient le même panneau pour tous les projets.
// ---------------------------------------------------------------------
function renderTest(test) {
  if (!test || !(test.steps ?? []).length) return;
  $('test-section').hidden = false;
  $('test-steps').innerHTML = test.steps
    .map(
      (s, i) => `<div>
        <span class="step">${esc(s.step ?? String(i + 1))}</span>
        <h3>${esc(s.title ?? '')}</h3>
        <p>${esc(s.text ?? '')}</p>
        ${(s.commands ?? []).length ? `<code>${(s.commands ?? []).map(esc).join('\n')}</code>` : ''}
      </div>`,
    )
    .join('');
}

// ---------------------------------------------------------------------
// Le rail de liens. Les deux pages sœurs y entrent toutes seules quand leur
// fichier existe : les câbler à la main dans dix-huit roadmap.json, c'est
// dix-huit occasions d'en oublier une.
// ---------------------------------------------------------------------
function renderLinks(roadmap, has) {
  const auto = [];
  if (has.marche) {
    auto.push({ url: 'marche.html', label: 'Le marché', inside: true,
                note: 'qui existe déjà en face, et quelle case reste vide' });
  }
  auto.push({ url: 'etat.html', label: 'Où on en est', inside: true,
              note: 'les faits mesurés du dépôt — page générée, jamais écrite à la main' });

  const given = (roadmap?.links ?? []).filter((l) => !/^(marche|etat)\.html$/.test(l.url));
  $('links').innerHTML = [...auto, ...given]
    .map(
      (l) =>
        `<a href="${esc(l.url)}"${l.inside ? ' data-in' : ' target="_blank" rel="noopener noreferrer"'} title="${esc(l.note ?? l.url)}">${esc(l.label)}</a>`,
    )
    .join('');
}

async function load() {
  const [roadmap, changelog, system, vision, bench, test, marche] = await Promise.all(
    ['roadmap.json', 'changelog.json', 'system.json', 'vision.json',
     'bench.json', 'test.json', 'marche.json'].map(grab),
  );

  renderVision(vision);
  renderBench(bench);
  renderTest(test);
  renderLinks(roadmap, { marche: !!marche });

  if (roadmap) {
    $('epic').textContent = roadmap.epic ?? '';
    document.title = `${roadmap.epic ?? 'projet'} — cockpit`;
    $('goal').textContent = roadmap.goal ?? '';
    $('updated').textContent = `mis à jour ${roadmap.updated ?? '—'}`;

    $('gantt').innerHTML = renderGantt(roadmap);
    // La légende décrit ce que la figure encode, pas la liste des statuts
    // possibles : les tâches ne sont plus des barres, donc annoncer quatre
    // couleurs de tâche serait décrire une image qui n'existe plus.
    $('gantt-legend').innerHTML =
      [
        ['#34d399', 0.55, 'part des tâches faites'],
        ['#fbbf24', 1, 'sprint en cours, et la tâche en vol'],
        ['#fb7185', 1, 'au moins une tâche bloquée'],
      ]
        .map(
          ([color, opacity, label]) =>
            `<span class="chip"><span class="swatch" style="background:${color};opacity:${opacity}"></span>${label}</span>`,
        )
        .join('') +
      '<span class="chip" style="color:var(--zinc-500)">le détail des tâches est dans le journal</span>';

    const all = (roadmap.sprints ?? []).flatMap((s) => s.tasks ?? []);
    const done = all.filter((t) => t.status === 'done').length;
    $('kpis').innerHTML = [
      ['tâches faites', `${done}/${all.length}`],
      ['sprints', String((roadmap.sprints ?? []).length)],
      ['réserve', String((roadmap.backlog ?? []).length)],
      ['mis à jour', roadmap.updated ?? '—'],
    ]
      .map(
        ([l, v]) =>
          `<div class="kpi"><div class="kpi__v">${esc(v)}</div><div class="kpi__l">${esc(l)}</div></div>`,
      )
      .join('');

    $('backlog').innerHTML = (roadmap.backlog ?? [])
      .map(
        (b) =>
          `<li><b>${esc(b.title)}</b><br><span style="color:var(--zinc-400)">${esc(b.note ?? '')}</span></li>`,
      )
      .join('');
  }

  if (changelog) {
    $('narrative').textContent = changelog.narrative ?? '';
    $('focus').textContent = changelog.focus ?? '';
    $('burnup').innerHTML = renderBurnup(changelog);
    $('journal').innerHTML = (changelog.sessions ?? [])
      .map(
        (s) => `<article class="entry">
          <div class="entry__head">
            <span class="entry__date">${esc(s.date)}</span>
            <span class="entry__title">${esc(s.title)}</span>
          </div>
          ${(s.items ?? [])
            .map(
              (i) =>
                `<div class="item ${esc(i.status)}"><span class="tag">${esc(i.tag ?? '')}</span><span>${esc(i.text)}</span></div>`,
            )
            .join('')}
        </article>`,
      )
      .join('');
    $('next').innerHTML = (changelog.next ?? [])
      .map(
        (n) =>
          `<li>${esc(n.text)}${n.blocked ? `<span class="blocked">⛔ bloqué par : ${esc(n.blocked)}</span>` : ''}</li>`,
      )
      .join('');
  }

  if (system) {
    $('stack').textContent = system.stack ?? '—';
    $('system-summary').textContent = system.summary ?? '';
    $('system').innerHTML = (system.layers ?? [])
      .map(
        (layer) => `<div class="layer">
          <div class="layer__name">${esc(layer.name)} <span>— ${esc(layer.desc ?? '')}</span></div>
          <div class="grid">
            ${(layer.bricks ?? [])
              .map(
                (b) => `<div class="card">
                  <div class="card__title">${esc(b.title ?? b.name)}</div>
                  <div class="card__path">${esc(b.path ?? '')}</div>
                  <div class="card__role">${esc(b.role ?? '')}</div>
                  <div class="card__ports">${(b.ports ?? []).map((p) => `<span class="port">${esc(p)}</span>`).join('')}</div>
                </div>`,
              )
              .join('')}
          </div>
        </div>`,
      )
      .join('');

    $('edges').innerHTML = (system.edges ?? [])
      .map(
        (e) =>
          `<div><b>${esc(e.from)}</b> <span class="arrow">→</span> <b>${esc(e.to)}</b> <span style="color:var(--zinc-400)">${esc(e.label ?? '')}</span></div>`,
      )
      .join('');
  }
}

load();
