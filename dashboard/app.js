'use strict';

const $ = (sel) => document.querySelector(sel);
const el = (tag, cls, text) => {
  const n = document.createElement(tag);
  if (cls) n.className = cls;
  if (text != null) n.textContent = text;
  return n;
};

const DAY = 86400000;
const fmtDate = (d) =>
  d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' });

// ---------------------------------------------------------------- navigation

document.querySelectorAll('nav button').forEach((btn) => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('nav button').forEach((b) => b.classList.remove('on'));
    document.querySelectorAll('main section').forEach((s) => s.classList.remove('on'));
    btn.classList.add('on');
    $('#view-' + btn.dataset.view).classList.add('on');
  });
});

// ---------------------------------------------------------------- roadmap

function renderHeader(data, sprints) {
  $('#tagline').textContent = data.tagline;

  const totals = sprints.reduce(
    (acc, s) => {
      s.items.forEach((i) => {
        acc.all++;
        if (i.state === 'done') acc.done++;
      });
      return acc;
    },
    { all: 0, done: 0 }
  );

  const current = sprints.find((s) => s.id === data.currentSprint);
  const pct = totals.all ? Math.round((100 * totals.done) / totals.all) : 0;

  const meta = $('#meta');
  meta.innerHTML = '';
  const chips = [
    ['Current', current ? `${current.id} — ${current.name}` : '—'],
    ['Overall', `${totals.done}/${totals.all} tasks (${pct}%)`],
    ['Sprints', String(sprints.length)],
    ['Updated', data.updated],
  ];
  chips.forEach(([k, v]) => {
    const c = el('span', 'chip');
    c.append(document.createTextNode(k + ' '), el('b', null, v));
    meta.append(c);
  });
}

function renderGantt(sprints) {
  const host = $('#gantt');
  host.innerHTML = '';

  const starts = sprints.map((s) => new Date(s.start).getTime());
  const ends = sprints.map((s) => new Date(s.end).getTime());
  const min = Math.min(...starts);
  const max = Math.max(...ends);
  const span = Math.max(1, max - min);
  const now = Date.now();

  const table = el('table');
  const thead = el('thead');
  const hr = el('tr');
  ['Sprint', 'Window', 'Progress', ''].forEach((h) => hr.append(el('th', null, h)));
  thead.append(hr);
  table.append(thead);

  const tbody = el('tbody');
  sprints.forEach((s) => {
    const from = new Date(s.start);
    const to = new Date(s.end);
    const done = s.items.filter((i) => i.state === 'done').length;
    const pct = s.items.length ? Math.round((100 * done) / s.items.length) : 0;

    const tr = el('tr');

    const nameCell = el('td', 'sname');
    nameCell.append(el('span', 'sid', s.id), document.createTextNode(s.name));
    tr.append(nameCell);

    tr.append(el('td', null, `${fmtDate(from)} → ${fmtDate(to)}`));
    tr.append(el('td', null, `${done}/${s.items.length}`));

    const trackCell = el('td');
    const track = el('div', 'track');
    const bar = el('div', 'bar ' + s.status, pct > 0 ? pct + '%' : '');
    bar.style.left = (100 * (from.getTime() - min)) / span + '%';
    bar.style.width =
      Math.max(3, (100 * (to.getTime() - from.getTime())) / span) + '%';
    bar.title = `${s.id} — ${s.goal}`;
    track.append(bar);

    // "Today" marker, only when the window actually contains today.
    if (now >= min && now <= max) {
      const marker = el('div', 'today');
      marker.style.left = (100 * (now - min)) / span + '%';
      marker.title = 'Today';
      track.append(marker);
    }

    trackCell.append(track);
    tr.append(trackCell);
    tbody.append(tr);
  });

  table.append(tbody);
  host.append(table);
}

function renderCards(sprints, currentId) {
  const host = $('#cards');
  host.innerHTML = '';

  sprints.forEach((s) => {
    const done = s.items.filter((i) => i.state === 'done').length;
    const pct = s.items.length ? (100 * done) / s.items.length : 0;

    const card = el('div', 'card' + (s.id === currentId ? ' is-active' : ''));

    const h3 = el('h3');
    h3.append(el('em', null, s.id), document.createTextNode(s.name));
    card.append(h3);
    card.append(el('div', 'goal', s.goal));

    const prog = el('div', 'prog');
    const fill = el('i');
    fill.style.width = pct + '%';
    prog.append(fill);
    card.append(prog);

    const ul = el('ul', 'items');
    s.items.forEach((item) => {
      const li = el('li');
      li.append(el('span', 'dot ' + item.state));
      const body = el('div');
      body.append(
        el('div', item.state === 'done' ? 'strike' : null, item.title)
      );
      if (item.note) body.append(el('div', 'it-note', item.note));
      li.append(body);
      ul.append(li);
    });
    card.append(ul);
    host.append(card);
  });
}

// ---------------------------------------------------------------- voice-over

function renderVo(cov) {
  const host = $('#vo');
  host.innerHTML = '';

  if (!cov) {
    host.append(
      el(
        'p',
        'empty',
        'No coverage data yet. Run: pwsh tools/Get-VoCoverage.ps1'
      )
    );
    return;
  }

  const names = { en: 'English', fr: 'Français' };
  const total = cov.totalLines;

  Object.entries(cov.locales).forEach(([code, loc]) => {
    const card = el('div', 'vo-card');

    const h3 = el('h3');
    h3.append(
      document.createTextNode(names[code] || code),
      el('span', 'vo-pct', loc.percent + '%')
    );
    card.append(h3);

    // Recorded and placeholder are disjoint: a placeholder is by definition not
    // recorded, so the two widths never overlap.
    const bar = el('div', 'vo-bar');
    const rec = el('i', 'rec');
    rec.style.width = (100 * loc.recorded) / total + '%';
    const stub = el('i', 'stub');
    stub.style.width = (100 * (loc.placeholders || 0)) / total + '%';
    bar.append(rec, stub);
    card.append(bar);

    const legend = el('div', 'vo-legend');
    legend.append(
      el('span', 'l-rec', `${loc.recorded} recorded`),
      el('span', 'l-stub', `${loc.placeholders || 0} placeholder`),
      el('span', 'l-miss', `${loc.missing - (loc.placeholders || 0)} not started`)
    );
    card.append(legend);

    if (loc.bySpeaker) {
      const table = el('table', 'spk');
      const thead = el('thead');
      const hr = el('tr');
      ['Speaker', 'Recorded'].forEach((h) => hr.append(el('th', null, h)));
      thead.append(hr);
      table.append(thead);

      const tbody = el('tbody');
      Object.entries(loc.bySpeaker).forEach(([speaker, s]) => {
        const tr = el('tr');
        tr.append(el('td', null, speaker));
        tr.append(el('td', null, `${s.recorded}/${s.total}`));
        tbody.append(tr);
      });
      table.append(tbody);
      card.append(table);
    }

    if (loc.orphans && loc.orphans.length) {
      card.append(
        el(
          'div',
          'warn',
          `${loc.orphans.length} orphan file(s) with no matching key — likely a filename typo: ${loc.orphans.join(', ')}`
        )
      );
    }

    host.append(card);
  });
}

// ---------------------------------------------------------------- bootstrap

async function fetchJson(path) {
  try {
    const res = await fetch(path, { cache: 'no-store' });
    return res.ok ? await res.json() : null;
  } catch {
    return null;
  }
}

(async function init() {
  const roadmap = await fetchJson('roadmap.json');
  if (!roadmap) {
    $('#tagline').textContent = 'Could not load roadmap.json';
    return;
  }

  renderHeader(roadmap, roadmap.sprints);
  renderGantt(roadmap.sprints);
  renderCards(roadmap.sprints, roadmap.currentSprint);

  renderVo(await fetchJson('vo-coverage.json'));
})();
