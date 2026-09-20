// Run with Node and Playwright available in NODE_PATH. No app/database required.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');
const root = path.resolve(__dirname, '../..');
const css = fs.readFileSync(path.join(root, 'wwwroot/css/ynclino.css'), 'utf8');
const script = fs.readFileSync(path.join(root, 'wwwroot/js/ynclino.js'), 'utf8');
const table = (count = 80, long = false) => `<div data-table-wrap><table>
  <thead><tr><th>Tenant</th><th>Unit</th><th>Description</th><th>Status</th><th>Actions</th></tr></thead>
  <tbody>${count ? Array.from({ length: count }, (_, i) => `<tr data-record="${i}"><td>Tenant ${i + 1}</td><td>${101 + i}</td><td>${long && i % 3 === 0 ? 'A longer request description that wraps over several lines and must remain fully readable.' : 'Rent payment'}</td><td>Pending</td><td><a href="#" data-modal-link>Update</a></td></tr>`).join('') : '<tr><td colspan="5">No records found.</td></tr>'}</tbody></table></div>`;
const heading = `<div data-module-shell><div data-page-head><div><h3>Billing &amp; Payment</h3></div></div><div data-page-actions><a href="#">Archive</a><a data-primary href="#">Issue Bill</a></div><form method="get"><div><input placeholder="Search tenant name..."></div><div><select><option>All Statuses</option></select></div><div><a href="#">Clear</a></div></form></div>`;
function html(content) {
  return `<!doctype html><html data-rail><head><meta name="viewport" content="width=device-width, initial-scale=1"><style>${css}</style></head><body><div><aside id="appSidebar"><nav><a href="#">Dashboard</a></nav></aside><div><header><button data-rail-toggle>Menu</button></header><main>${content}</main></div></div><dialog data-modal id="appModal"><div data-modal-panel><div data-modal-head><span data-modal-title>History</span></div><div data-modal-body></div></div></dialog></body></html>`;
}
async function inspect(page) {
  return page.evaluate(() => {
    const main = document.querySelector('main');
    return {
      height: main.clientHeight, scroll: main.scrollHeight,
      fallback: main.hasAttribute('data-pagination-overflow'),
      selects: document.querySelectorAll('[data-pagination] select').length,
      tables: Array.from(document.querySelectorAll('main table[data-paginated]')).filter(t => t.getClientRects().length).map(t => {
        const wrap = t.parentElement;
        const shown = Array.from(t.tBodies[0].rows).filter(r => !r.hidden);
        return { size: Number(t.dataset.fittedPageSize), shown: shown.length,
          lastBottom: shown.at(-1).getBoundingClientRect().bottom,
          tableBottom: t.getBoundingClientRect().bottom,
          wrapBottom: wrap.getBoundingClientRect().bottom,
          reservedHeight: wrap.style.height,
          pagerBottom: wrap.nextElementSibling.getBoundingClientRect().bottom };
      })
    };
  });
}
(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  async function load(content, width, height) {
    await page.setViewportSize({ width, height });
    await page.setContent(html(content));
    await page.addScriptTag({ content: script });
    await page.evaluate(() => window.dispatchEvent(new Event('DOMContentLoaded')));
    await page.waitForTimeout(350);
  }
  try {
    for (const [width, height] of [[1366, 768], [1920, 1080], [1024, 768], [390, 844]]) {
      await load(heading + table(), width, height);
      let state = await inspect(page);
      assert.equal(state.selects, 0);
      assert.equal(state.scroll <= state.height + 1, true, JSON.stringify({ width, height, state }));
      assert.ok(state.tables[0].size >= 1);
      assert.ok(state.tables[0].lastBottom <= state.tables[0].wrapBottom + 1, 'visible row was clipped');
      const initial = state.tables[0];
      // Every record remains reachable; no artificial space below the rows.
      const seen = new Set();
      for (let n = 0; n < 81; n++) {
        const currentState = await inspect(page);
        assert.ok(currentState.scroll <= currentState.height + 1);
        assert.equal(currentState.tables[0].reservedHeight, '');
        assert.ok(currentState.tables[0].wrapBottom - currentState.tables[0].tableBottom <= 2,
          'empty space was left inside the table border');
        (await page.locator('tr[data-record]:visible').evaluateAll(rows => rows.map(r => r.dataset.record))).forEach(id => seen.add(id));
        const next = page.locator('[data-pagination] button').last();
        if (await next.isDisabled()) break;
        await next.click();
      }
      state = await inspect(page);
      assert.equal(seen.size, 80);
      assert.equal(state.tables[0].size, initial.size);
      console.log(`PASS: ${width}x${height}, capacity ${initial.size}, no vertical scrolling, all 80 records reachable`);
    }

    await load(heading + table(80, true), 1366, 768);
    let state = await inspect(page);
    assert.ok(state.scroll <= state.height + 1);
    assert.ok(state.tables[0].lastBottom <= state.tables[0].wrapBottom + 1);
    await page.locator('[data-pagination] button').last().click();
    state = await inspect(page);
    assert.ok(state.tables[0].lastBottom <= state.tables[0].wrapBottom + 1);
    assert.ok(state.tables[0].wrapBottom - state.tables[0].tableBottom <= 2);
    console.log('PASS: wrapped rows remain readable and fit the viewport');

    // Reproduce a short list with a few taller request notes: those notes must
    // not inflate every row or add a blank panel beneath the final record.
    await load(heading + table(13, true), 1630, 1050);
    state = await inspect(page);
    assert.equal(state.tables[0].reservedHeight, '');
    assert.ok(state.tables[0].wrapBottom - state.tables[0].tableBottom <= 2);
    assert.ok(state.scroll <= state.height + 1);
    const withNotes = state.tables[0].size;
    await load(heading + table(13), 1630, 1050);
    const plain = (await inspect(page)).tables[0].size;
    assert.ok(withNotes >= plain - 2, 'one wrapped row unnecessarily reduced capacity for the entire table');
    console.log('PASS: short lists and mixed-height records have no reserved blank area');

    await load(heading + table(2), 1630, 1050);
    state = await inspect(page);
    assert.equal(state.tables[0].shown, 2);
    assert.ok(state.tables[0].wrapBottom - state.tables[0].tableBottom <= 2);
    console.log('PASS: sparse results end immediately after their records');

    await load(heading + table(), 1366, 768);
    const small = (await inspect(page)).tables[0].size;
    await page.setViewportSize({ width: 1366, height: 1080 });
    await page.waitForTimeout(350);
    assert.ok((await inspect(page)).tables[0].size > small);
    await page.setViewportSize({ width: 1366, height: 600 });
    await page.waitForTimeout(350);
    state = await inspect(page);
    assert.ok(state.tables[0].size < small);
    assert.ok(state.scroll <= state.height + 1);
    console.log('PASS: page capacity recalculates when screen height changes');

    for (const width of [1366, 1024]) {
      const section = () => `<div data-report-page data-report-group="Operations"><div data-panel><div data-panel-head>Summary</div><div data-panel-body>${table(20)}</div></div></div>`;
      await load(`<h3>Reports &amp; Records</h3><div>${section()}${section()}</div><nav data-report-pagination><button data-report-previous>Previous</button><span data-report-position></span><button data-report-next>Next</button></nav>`, width, 900);
      state = await inspect(page);
      assert.equal(state.tables.length, 2);
      assert.ok(state.scroll <= state.height + 1, JSON.stringify(state));
      console.log(`PASS: ${width}px report panels share the viewport`);
    }

    const sectionNav = '<nav data-report-pagination><button data-report-previous>Previous</button><span data-report-position></span><button data-report-next>Next</button></nav>';
    await load(`<h3>Reports</h3><div data-report-page data-report-group="First">${table(80)}</div><div data-report-page data-report-group="Second">${table(35, true)}</div>${sectionNav}`, 1366, 768);
    await page.locator('[data-report-next]').click();
    await page.waitForTimeout(250);
    state = await inspect(page);
    assert.equal(state.tables.length, 1);
    assert.ok(state.tables[0].size >= 1);
    assert.ok(state.scroll <= state.height + 1);
    console.log('PASS: previously hidden report sections are measured when opened');

    const wideTable = `<div data-table-wrap><table><thead><tr>${['Tenant', 'Unit', 'Billing Period', 'Due Date', 'Deposit', 'Advance Payment', 'Monthly Rent', 'Balance', 'Amount Paid', 'Status', 'Actions'].map(t => `<th>${t}</th>`).join('')}</tr></thead><tbody>${Array.from({ length: 50 }, (_, i) => `<tr><td>Joanna Lorenzo ${i}</td><td>115</td><td>September 2026</td><td>Sep 20, 2026</td>${'<td>₱1,900</td>'.repeat(5)}<td>Unpaid</td><td><a href="#">Update</a></td></tr>`).join('')}</tbody></table></div>`;
    await load(heading + wideTable, 1366, 768);
    state = await inspect(page);
    assert.ok(state.scroll <= state.height + 1);
    assert.ok(state.tables[0].lastBottom <= state.tables[0].wrapBottom + 1);
    console.log('PASS: eleven-column billing table fits without clipped rows');

    await page.route('https://pagination.test/history', route => route.fulfill({
      contentType: 'text/html', headers: { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Headers': '*' },
      body: `<div data-modal-source><div data-panel-head>History</div>${table(45)}<div data-actions-row><button data-modal-close>Back</button></div></div>`
    }));
    await load(heading + '<a data-modal-link href="https://pagination.test/history">Open history</a>' + table(), 1366, 768);
    await page.getByText('Open history', { exact: true }).click();
    await page.waitForSelector('#appModal[open] table[data-fitted-page-size]');
    await page.waitForTimeout(250);
    const modalState = await page.locator('[data-modal-body]').evaluate(body => ({
      height: body.clientHeight, scroll: body.scrollHeight,
      size: Number(body.querySelector('table').dataset.fittedPageSize),
      clipped: Array.from(body.querySelectorAll('tbody tr')).filter(r => !r.hidden)
        .some(r => r.getBoundingClientRect().bottom > body.querySelector('[data-table-wrap]').getBoundingClientRect().bottom + 1)
    }));
    assert.ok(modalState.height >= modalState.scroll - 1, JSON.stringify(modalState));
    assert.ok(modalState.size > 1);
    assert.equal(modalState.clipped, false);
    console.log('PASS: dynamically loaded modal tables fit below their header and above their actions');

    await load(heading + table(0), 1366, 768);
    assert.equal(await page.locator('[data-pagination]').count(), 0);
    assert.equal(await page.locator('text=No records found.').count(), 1);
    console.log('PASS: empty results do not create a fake record or pager');
    assert.deepEqual(errors, []);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
