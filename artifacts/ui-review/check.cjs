const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const os = require('node:os');
const { spawn } = require('node:child_process');
const root = process.cwd();
const edge = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const read = file => fs.readFileSync(path.join(root, file), 'utf8');

// Preview the actual form markup with sample values, without starting the app or touching its database.
function form(file) {
    let source = read(file).match(/<form\b[\s\S]*?<\/form>/)[0];
    source = source.replace(/@\*[\s\S]*?\*@/g, '')
        .replace(/@\{[\s\S]*?^\s*\}/gm, '')
        .replace(/^\s*@(if|foreach|await|Html\.)[^\n]*$/gm, '')
        .replace(/^\s*(?:else|[{}])\s*$/gm, '')
        .replace(/@Model\.\w+\.ToString\("[^"]+"\)/g, '1,900')
        .replace(/@Model\.\w+|@st/g, 'Sample')
        .replace(/asp-items="[^"]*"/g, '')
        .replace(/<label asp-for="([^"]+)"([^>]*)>([^<]*)<\/label>/g,
            (_, name, attrs, label) => `<label for="${name}"${attrs}>${label || name.replace(/([a-z])([A-Z])/g, '$1 $2')}</label>`)
        .replace(/<(input|select|textarea)\b([^>]*?)asp-for="([^"]+)"([^>]*)>/g,
            (_, tag, before, name, after) => `<${tag}${before} name="${name}"${/\bid=/.test(before + after) ? '' : ` id="${name}"`}${tag === 'input' && !/\btype=/.test(before + after) ? ' type="text"' : ''}${after}>`);
    return `<div data-form-panel><div data-form-body>${source}</div></div>`;
}
const tenant = form('Views/Tenants/Edit.cshtml');
const maintenance = form('Views/Maintenance/Edit.cshtml');
const billing = form('Views/Billing/Edit.cshtml');
const table = id => `<div data-report-page><div data-table-wrap><table id="${id}" data-page-size="2"><thead><tr><th>Record</th></tr></thead><tbody>${[1,2,3,4,5].map(n=>`<tr><td>${n}</td></tr>`).join('')}</tbody></table></div></div>`;
const verify = `
window.addEventListener('DOMContentLoaded', () => {
    const results = [];
    const check = (name, condition) => results.push({ name, passed: !!condition });
    const q = s => document.querySelector(s);
    const css = (s, pseudo) => getComputedStyle(typeof s === 'string' ? q(s) : s, pseudo);
    const box = s => (typeof s === 'string' ? q(s) : s).getBoundingClientRect();
    const orange = 'rgb(249, 76, 1)', red = 'rgb(176, 42, 55)';
    ['Add Unit','Request Transfer','Issue Bill','Register Tenant','Report Item','New Request'].forEach(label => {
        const button = [...document.querySelectorAll('[data-page-actions] a')].find(a=>a.textContent.trim()===label);
        check(label + ' has plus', button.querySelector('use')?.getAttribute('href').endsWith('#add'));
        check(label + ' orange primary', css(button).backgroundColor === orange);
    });
    check('Archive first', q('[data-page-actions]').firstElementChild.textContent.trim() === 'Archive');
    check('Single bottom mark-all button', document.querySelectorAll('[data-mark-all-read]').length === 1 && !q('[data-panel-head] button') && !q('[data-mark-read]'));
    check('Mark-all red outline', css('[data-mark-all-read]').color === red && css('[data-mark-all-read]').borderTopColor === red && css('[data-mark-all-read]').backgroundColor === 'rgba(0, 0, 0, 0)');
    check('Lost and Found light orange', css('[data-note-module="LostFound"] [data-module]').backgroundColor === 'rgb(255, 243, 236)');
    check('Logout text alignment', Math.abs(box('[data-account-text]').x - box('[data-logout] [data-nav-label]').x) < 1);
    check('One paginator per table', document.querySelectorAll('[data-pagination]').length === 2 && !q('[data-report-pagination]'));
    check('Paginator follows its table', q('#report1').parentElement.nextElementSibling.hasAttribute('data-pagination'));
    q('#report1').parentElement.nextElementSibling.querySelector('[data-pagination-controls]').lastElementChild.click();
    check('Tables paginate independently', q('#report1 tbody tr').hidden && !q('#report2 tbody tr').hidden);
    q('[data-mark-all-read]').click();
    check('Mark-all reads all rows', !q('[data-note][data-unread]') && q('[data-mark-all-read]').hidden);
    const dialog = q('#appModal');
    dialog.showModal();
    const mobile = window.innerWidth <= 560;
    const first = box('#tenant [name="FirstName"]'), last = box('#tenant [name="LastName"]');
    check('Tenant names align', mobile ? Math.abs(first.x-last.x)<1 && last.y>first.y : Math.abs(first.y-last.y)<1 && last.x>first.x);
    const password = box('#tenant [name="Password"]'), confirm = box('#tenant [name="ConfirmPassword"]');
    check('Password pair aligns', mobile ? confirm.y>password.y : Math.abs(password.y-confirm.y)<1);
    const staff = box('#maintenance [name="AssignedStaffID"]'), grid = box('#maintenance form');
    check('Assigned Staff full width', Math.abs(staff.width-grid.width)<1);
    check('File button navy', css('#maintenance input[type="file"]','::file-selector-button').backgroundColor === 'rgb(43, 54, 72)');
    check('File text unchanged', css('#maintenance input[type="file"]').backgroundColor !== 'rgb(43, 54, 72)');
    check('Save solid orange', [...document.querySelectorAll('#appModal button[type="submit"]')].every(b=>css(b).backgroundColor===orange));
    check('Cancel white and orange', [...document.querySelectorAll('#appModal [data-actions-row] a')].every(b=>css(b).backgroundColor==='rgb(255, 255, 255)' && css(b).color===orange && css(b).borderTopColor===orange));
    check('Bill uses dashboard cards', ['borderLeftColor','borderLeftWidth','borderRadius','backgroundColor','boxShadow','padding'].every(p=>css('#dashboard-stat')[p]===css('[data-billing-summary] [data-stat]')[p]));
    check('Dialog fits viewport', dialog.scrollWidth <= dialog.clientWidth && box(dialog).width <= window.innerWidth);
    document.querySelector('#results').textContent = JSON.stringify(results);
});`;
const html = `<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><link rel="stylesheet" href="/css/ynclino.css"></head><body>
<div><aside id="appSidebar"><nav><a href="#"><span data-nav-icon="dashboard"></span><span data-nav-label>Dashboard</span></a></nav><div data-account><a data-account-card><span data-nav-icon="profile"></span><span data-account-text>Admin</span></a><form data-logout><button><span data-nav-icon="logout"></span><span data-nav-label>Logout</span></button></form></div></aside><div><main>
<div data-module-shell><div data-page-head><div><h3>UI Review</h3></div></div><div data-page-actions><a href="#" data-action-tone="amber">Archive</a>${['Add Unit','Request Transfer','Issue Bill','Register Tenant','Report Item','New Request'].map(t=>`<a href="#" data-primary>${t}</a>`).join('')}</div></div>
<div data-stat id="dashboard-stat"><div><div>Total Paid</div><div>₱1,900</div></div></div>
<div data-panel data-notification-feed data-notification-user="ui-review"><div data-panel-head>Notifications</div><div data-panel-body>${['LostFound','Maintenance'].map(m=>`<div data-note data-note-module="${m}" data-notification-key="${m}" data-unread="true"><a data-note-link href="#"><span data-module>${m}</span><span data-message>Pending records</span></a></div>`).join('')}</div><div data-notification-actions><button data-mark-all-read data-action-tone="red">Mark all as read</button></div></div>
${table('report1')}${table('report2')}</main></div></div>
<dialog data-modal id="appModal" data-kind="form"><div data-modal-panel><div data-modal-head><span data-modal-title>Update forms</span></div><div data-modal-body><section id="tenant">${tenant}</section><section id="maintenance">${maintenance}</section><section id="billing">${billing}</section></div></div></dialog>
<pre id="results"></pre><script src="/js/ynclino.js"></script><script>${verify}</script></body></html>`;
const server = http.createServer((req,res) => {
    if (req.url === '/') { res.setHeader('Content-Type','text/html; charset=utf-8'); return res.end(html); }
    const target = path.resolve(root,'wwwroot','.' + decodeURI(req.url.split('?')[0]));
    if (!target.startsWith(path.join(root,'wwwroot') + path.sep) || !fs.existsSync(target)) { res.writeHead(404); return res.end(); }
    res.setHeader('Content-Type', target.endsWith('.css') ? 'text/css' : target.endsWith('.svg') ? 'image/svg+xml' : 'application/javascript');
    fs.createReadStream(target).pipe(res);
});
server.listen(0,'127.0.0.1', async () => {
    try {
        for (const width of [1440,390]) {
            const profile = fs.mkdtempSync(path.join(os.tmpdir(),'ynclino-ui-review-'));
            const args = ['--headless=new','--disable-gpu','--no-first-run', '--no-default-browser-check','--disable-extensions', '--user-data-dir='+profile,'--window-size='+width+',1000','--virtual-time-budget=3000','--dump-dom','http://127.0.0.1:'+server.address().port+'/'];
            const output = await new Promise((resolve,reject)=>{
                const child = spawn(edge,args,{windowsHide:true});
                let stdout='', stderr='';
                child.stdout.on('data',b=>stdout+=b); child.stderr.on('data',b=>stderr+=b);
                child.on('error',reject); child.on('exit',()=>resolve({stdout,stderr}));
                const timer = setTimeout(()=>{child.kill(); reject(new Error('Browser preview timed out'));},45000);
                child.on('exit',()=>clearTimeout(timer));
            });
            const match = output.stdout.match(/<pre id="results">([\s\S]*?)<\/pre>/);
            if (!match || !match[1]) throw new Error('No browser results: '+output.stderr.slice(-1200));
            const results = JSON.parse(match[1].replace(/&quot;/g,'"').replace(/&amp;/g,'&'));
            console.log(JSON.stringify({width,results}));
            if (results.some(r=>!r.passed)) process.exitCode = 1;
        }
    } catch(err) { console.error(err); process.exitCode=1; }
    finally { server.close(); }
});
