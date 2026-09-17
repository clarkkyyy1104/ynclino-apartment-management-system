// Rendered UML class diagrams for the current C# domain models.
// Run: node Documentation/generate-class-diagrams.js
const fs = require('fs');
const path = require('path');

const out = __dirname;
const xml = value => String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const slug = value => value.toLowerCase().replace(/[^a-z0-9]+/g, '_');

const classes = {
  User: { model: 'tblUser', tone: 'blue', fields: [
    'UserID : int {PK}', 'Username : string', 'Password : string',
    'Role : string {RoleID}', 'FirstName : string', 'LastName : string',
    'ContactNumber : string?', 'IsActive : bool',
    'MustChangePassword : bool', 'IsMainAdmin : bool', 'LastLoginAt : DateTime?',
    'DisplayName : string {derived}'
  ]},
  TenantProfile: { model: 'tblTenant', tone: 'teal', fields: [
    'TenantID : int {PK}', 'UserID : int? {FK}', 'UnitID : int? {FK}',
    'FirstName : string', 'LastName : string', 'ContactNumber : string?',
    'EmergencyContactName : string?', 'EmergencyContactNumber : string?',
    'MoveInDate : DateTime?', 'MoveOutDate : DateTime?',
    'LeaseStart : DateTime?', 'LeaseEnd : DateTime?',
    'AdvanceCredit : decimal', 'Status : string', 'PhotoPath : string?',
    'FullName : string {derived}'
  ]},
  Unit: { model: 'tblUnit', tone: 'teal', fields: [
    'UnitID : int {PK}', 'UnitNumber : string', 'UnitType : string',
    'RentPrice : decimal', 'Deposit : decimal', 'AdvancePayment : decimal',
    'Capacity : int', 'Status : string'
  ]},
  TenantUnitAssignment: { model: 'TenantUnitAssignment', tone: 'teal', fields: [
    'AssignmentID : long {PK}', 'TenantID : int {FK}', 'UnitID : int {FK}',
    'MoveInDate : DateTime?', 'MoveOutDate : DateTime?',
    'LeaseStart : DateTime?', 'LeaseEnd : DateTime?', 'Status : string'
  ]},
  UnitTransferRequest: { model: 'tblUnitTransferRequest', tone: 'amber', fields: [
    'TransferID : int {PK}', 'TenantID : int {FK}', 'CurrentUnitID : int? {FK}',
    'RequestedUnitID : int {FK}', 'Reason : string', 'Status : string',
    'DateRequested : DateTime', 'DateReviewed : DateTime?',
    'TenantArchivedAt : DateTime?', 'StaffArchivedAt : DateTime?'
  ]},
  Billing: { model: 'tblBilling', tone: 'violet', fields: [
    'BillingID : int {PK}', 'TenantID : int {FK}', 'BillingPeriod : DateTime',
    'AmountDue : decimal', 'AmountPaid : decimal? {cache}',
    'Deposit : decimal', 'Advance : decimal', 'DueDate : DateTime',
    'AdvanceFromOverpayment : decimal', 'DatePaid : DateTime? {cache}',
    'Status : string', 'ArchivedAt : DateTime?'
  ]},
  Payment: { model: 'tblPayment', tone: 'violet', fields: [
    'PaymentID : int {PK}', 'BillingID : int {FK}', 'Amount : decimal',
    'DatePaid : DateTime', 'Method : string?', 'Remarks : string?',
    'RecordedAt : DateTime'
  ]},
  MaintenanceRequest: { model: 'tblMaintenanceRequest', tone: 'amber', fields: [
    'RequestID : int {PK}', 'TenantID : int {FK}', 'UnitID : int? {FK}',
    'AssignedStaffID : int? {FK}', 'Category : string',
    'Description : string', 'Priority : string', 'Status : string',
    'DateSubmitted : DateTime', 'DateResolved : DateTime?',
    'TenantArchivedAt : DateTime?', 'StaffArchivedAt : DateTime?'
  ]},
  LostFoundItem: { model: 'tblLostFoundItem', tone: 'rose', fields: [
    'ItemID : int {PK}', 'ReportedByUserID : int {FK}',
    'ClaimedByUserID : int? {FK}', 'ItemName : string',
    'ItemType : string', 'Location : string?', 'Status : string',
    'DateReported : DateTime', 'DateClaimed : DateTime?',
    'ArchivedAt : DateTime?', 'ImagePath : string?'
  ]},
  ClaimRequest: { model: 'tblClaimRequest', tone: 'rose', fields: [
    'ClaimID : int {PK}', 'ItemID : int {FK}', 'ClaimantUserID : int {FK}',
    'VerificationDetails : string', 'SubmittedAt : DateTime',
    'Status : string', 'AdminNotes : string?', 'ImagePath : string?'
  ]}
};

const pages = [
  {
    id: '01_accounts_tenancy', title: 'Accounts, tenancy & unit transfers',
    subtitle: 'Identity, current occupancy, assignment history and transfer requests',
    nodes: {
      User: [45, 154, 315], TenantProfile: [480, 113, 390], Unit: [1020, 154, 330],
      TenantUnitAssignment: [480, 623, 390], UnitTransferRequest: [1020, 608, 390]
    },
    links: [
      { a: 'User', b: 'TenantProfile', label: 'account', mult: ['1', '0..1'], points: [[360, 280], [480, 280]], at: [420, 258] },
      { a: 'TenantProfile', b: 'Unit', label: 'current unit', mult: ['0..*', '0..1'], points: [[870, 280], [1020, 280]], at: [945, 258] },
      { a: 'TenantProfile', b: 'TenantUnitAssignment', label: 'history', mult: ['1', '0..*'], points: [[665, 535], [665, 623]], at: [725, 579] },
      { a: 'Unit', b: 'TenantUnitAssignment', label: 'assigned unit', mult: ['1', '0..*'], points: [[1185, 400], [1185, 554], [900, 554], [900, 746], [870, 746]], at: [1040, 536] },
      { a: 'TenantProfile', b: 'UnitTransferRequest', label: 'requests', mult: ['1', '0..*'], points: [[870, 467], [960, 467], [960, 702], [1020, 702]], at: [910, 447] },
      { a: 'Unit', b: 'UnitTransferRequest', label: 'requested unit', mult: ['1', '0..*'], points: [[1250, 400], [1250, 608]], at: [1320, 520] },
      { a: 'Unit', b: 'UnitTransferRequest', label: 'previous unit', mult: ['0..1', '0..*'], points: [[1340, 400], [1410, 400], [1410, 778]], at: [1390, 585] }
    ],
    note: 'User.Password maps to SQL PasswordHash. TenantProfile.UnitID is current; assignments retain history. Transfer.CurrentUnitID is a snapshot.'
  },
  {
    id: '02_billing_maintenance', title: 'Billing, payments & maintenance',
    subtitle: 'Money received is recorded per payment; maintenance stays linked to its original unit',
    nodes: {
      TenantProfile: [45, 155, 365], Billing: [535, 113, 390], Payment: [1050, 155, 340],
      User: [45, 668, 365], MaintenanceRequest: [535, 605, 390], Unit: [1050, 668, 340]
    },
    concise: {
      TenantProfile: ['TenantID : int {PK}', 'UserID : int? {FK}', 'UnitID : int? {FK}', 'FullName : string {derived}', 'Status : string', 'AdvanceCredit : decimal'],
      User: ['UserID : int {PK}', 'Username : string', 'Role : string {RoleID}', 'IsActive : bool'],
      Unit: ['UnitID : int {PK}', 'UnitNumber : string', 'UnitType : string', 'Status : string']
    },
    links: [
      { a: 'TenantProfile', b: 'Billing', label: 'bills', mult: ['1', '0..*'], points: [[410, 270], [535, 270]], at: [473, 248] },
      { a: 'Billing', b: 'Payment', label: 'payments', mult: ['1', '0..*'], points: [[925, 270], [1050, 270]], at: [988, 248] },
      { a: 'TenantProfile', b: 'MaintenanceRequest', label: 'submits', mult: ['1', '0..*'], points: [[228, 357], [228, 550], [535, 550], [535, 710]], at: [380, 532] },
      { a: 'User', b: 'MaintenanceRequest', label: 'assigned staff', mult: ['0..1', '0..*'], points: [[410, 755], [535, 755]], at: [473, 733] },
      { a: 'Unit', b: 'MaintenanceRequest', label: 'repair unit', mult: ['0..1', '0..*'], points: [[1050, 755], [925, 755]], at: [988, 733] }
    ],
    note: 'Payments is the ledger. Billing.AmountPaid and Billing.DatePaid are synchronized summaries. AssignedStaffID maps to AssignedStaffUserID in SQL.'
  },
  {
    id: '03_lost_found', title: 'Lost & Found and ownership claims',
    subtitle: 'Admins and tenants report items; tenant ownership claims are reviewed by an admin',
    nodes: { User: [45, 215, 365], LostFoundItem: [555, 164, 400], ClaimRequest: [1105, 215, 350] },
    concise: { User: ['UserID : int {PK}', 'Username : string', 'Role : string {RoleID}', 'FirstName : string', 'LastName : string', 'ContactNumber : string?', 'IsActive : bool', 'DisplayName : string {derived}'] },
    links: [
      { a: 'User', b: 'LostFoundItem', label: 'reported by', mult: ['1', '0..*'], points: [[410, 290], [555, 290]], at: [482, 268] },
      { a: 'User', b: 'LostFoundItem', label: 'claimed by', mult: ['0..1', '0..*'], points: [[410, 402], [555, 402]], at: [482, 380] },
      { a: 'LostFoundItem', b: 'ClaimRequest', label: 'claims', mult: ['1', '0..*'], points: [[955, 300], [1105, 300]], at: [1030, 278] },
      { a: 'User', b: 'ClaimRequest', label: 'claimant', mult: ['1', '0..*'], points: [[228, 483], [228, 718], [1280, 718], [1280, 461]], at: [755, 697] }
    ],
    note: 'ClaimedByUserID is optional until handover. An approved claim closes an item; admins can also record a direct handover without an online claim.'
  }
];

const palette = {
  blue: ['#233B62', '#E8F0FC'], teal: ['#176A6C', '#E4F5F2'],
  violet: ['#6947A1', '#F0EAFB'], amber: ['#9B5B13', '#FFF2DD'], rose: ['#A33F5A', '#FCEBF0']
};
function fieldsFor(page, name) { return page.concise?.[name] || classes[name].fields; }
function boxHeight(fields) { return 70 + fields.length * 22; }
function drawBox(page, name, [x, y, w]) {
  const meta = classes[name], fields = fieldsFor(page, name), h = boxHeight(fields);
  const [strong, soft] = palette[meta.tone];
  const lines = fields.map((field, i) => {
    const key = field.includes('{PK}') || field.includes('{FK}');
    return `<text x="${x + 20}" y="${y + 86 + i * 22}" class="field${key ? ' key' : ''}">${xml(field)}</text>`;
  }).join('');
  return `<g class="uml-class"><rect x="${x}" y="${y}" width="${w}" height="${h}" rx="10" fill="#fff" stroke="#AAB8C8" stroke-width="1.5"/><path d="M ${x + 10} ${y} H ${x + w - 10} Q ${x + w} ${y} ${x + w} ${y + 10} V ${y + 56} H ${x} V ${y + 10} Q ${x} ${y} ${x + 10} ${y}" fill="${strong}"/><text x="${x + 20}" y="${y + 28}" class="class-title">${xml(name)}</text><text x="${x + 20}" y="${y + 47}" class="model-name">C# ${xml(meta.model)}</text><line x1="${x}" x2="${x + w}" y1="${y + 58}" y2="${y + 58}" stroke="${soft}" stroke-width="4"/>${lines}</g>`;
}
function drawLink(link) {
  const points = link.points.map(p => p.join(',')).join(' ');
  const [first, last] = [link.points[0], link.points[link.points.length - 1]];
  const next = link.points[1], previous = link.points[link.points.length - 2];
  const [lx, ly] = link.at;
  const pillW = Math.max(82, link.label.length * 8.1 + 22);
  const startDx = next[0] - first[0], startDy = next[1] - first[1];
  const endDx = last[0] - previous[0], endDy = last[1] - previous[1];
  const startHorizontal = Math.abs(startDx) > Math.abs(startDy);
  const endHorizontal = Math.abs(endDx) > Math.abs(endDy);
  const sx = first[0] + (startHorizontal ? Math.sign(startDx) * 9 : 12);
  const sy = first[1] + (startHorizontal ? 21 : startDy > 0 ? 21 : -8);
  const ex = last[0] - (endHorizontal ? Math.sign(endDx) * 9 : -12);
  const ey = last[1] + (endHorizontal ? 21 : endDy > 0 ? -8 : 21);
  const m1 = `<text x="${sx}" y="${sy}" text-anchor="${startHorizontal && startDx < 0 ? 'end' : 'start'}" class="multiplicity">${xml(link.mult[0])}</text>`;
  const m2 = `<text x="${ex}" y="${ey}" text-anchor="${endHorizontal && endDx > 0 ? 'end' : 'start'}" class="multiplicity">${xml(link.mult[1])}</text>`;
  return `<g class="uml-link"><polyline points="${points}" fill="none" stroke="#65778F" stroke-width="2.2" stroke-linejoin="round" stroke-linecap="round"/><rect x="${lx - pillW / 2}" y="${ly - 16}" width="${pillW}" height="27" rx="13.5" fill="#fff" stroke="#D4DDE8"/><text x="${lx}" y="${ly + 3}" text-anchor="middle" class="link-label">${xml(link.label)}</text>${m1}${m2}</g>`;
}
function svgPage(page, number) {
  const W = 1510, H = 1010;
  return `<?xml version="1.0" encoding="UTF-8"?><svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}" role="img" aria-labelledby="title desc"><title id="title">${xml(page.title)}</title><desc id="desc">UML class diagram of ${xml(page.subtitle)}. Class cards list selected public properties. Solid lines are associations with multiplicities.</desc><style>
  .heading{font:700 31px Arial,sans-serif;fill:#152439}.subheading{font:17px Arial,sans-serif;fill:#52647A}.class-title{font:700 22px Arial,sans-serif;fill:#fff}.model-name{font:13px Arial,sans-serif;fill:#DDE9F5}.field{font:15px Consolas,'Courier New',monospace;fill:#283648}.field.key{font-weight:700}.link-label{font:700 13px Arial,sans-serif;fill:#43566E}.multiplicity{font:700 14px Arial,sans-serif;fill:#334A66}.foot{font:15px Arial,sans-serif;fill:#43566E}.small{font:13px Arial,sans-serif;fill:#77879A}
  </style><rect width="${W}" height="${H}" fill="#F6F8FB"/><rect x="0" y="0" width="${W}" height="8" fill="#284566"/><text x="45" y="55" class="heading">${xml(page.title)}</text><text x="45" y="84" class="subheading">${xml(page.subtitle)}</text><text x="1455" y="55" text-anchor="end" class="small">SYSTEM CLASS DIAGRAM · ${number} / ${pages.length}</text>${page.links.map(drawLink).join('')}${Object.entries(page.nodes).map(([name, coords]) => drawBox(page, name, coords)).join('')}<rect x="45" y="951" width="1420" height="38" rx="8" fill="#E8EDF4"/><text x="62" y="976" class="foot">${xml(page.note)}</text></svg>`;
}

function mermaid(page) {
  let lines = [`%% ${page.title}`, 'classDiagram', 'direction LR'];
  for (const name of Object.keys(page.nodes)) {
    lines.push(`class ${name} {`);
    for (const field of fieldsFor(page, name)) {
      const [property, type] = field.replace(/\s*\{[^}]+\}/g, '').split(' : ');
      lines.push(`  +${type} ${property}`);
    }
    lines.push('}');
  }
  for (const link of page.links) lines.push(`${link.a} "${link.mult[0]}" -- "${link.mult[1]}" ${link.b} : ${link.label}`);
  return lines.join('\n') + '\n';
}

for (const [i, page] of pages.entries()) {
  fs.writeFileSync(path.join(out, `Class_Diagram_${page.id}.svg`), svgPage(page, i + 1));
  fs.writeFileSync(path.join(out, `Class_Diagram_${page.id}.mmd`), mermaid(page));
}

function pdfEscape(value) { return String(value).replace(/[^\x20-\x7E]/g, '-').replace(/([\\()])/g, '\\$1'); }
function pdfRgb(hex) { return [1, 3, 5].map(i => (parseInt(hex.slice(i, i + 2), 16) / 255).toFixed(3)).join(' '); }
function pdfPage(page, number) {
  const H = 1010, commands = [];
  const fill = hex => commands.push(`${pdfRgb(hex)} rg`);
  const stroke = hex => commands.push(`${pdfRgb(hex)} RG`);
  const rect = (x, y, w, h, color) => { fill(color); commands.push(`${x} ${H - y - h} ${w} ${h} re f`); };
  const text = (x, y, size, value, font = 'F1', color = '#283648') => {
    fill(color); commands.push(`BT /${font} ${size} Tf ${x} ${H - y} Td (${pdfEscape(value)}) Tj ET`);
  };
  rect(0, 0, 1510, 1010, '#F6F8FB'); rect(0, 0, 1510, 8, '#284566');
  text(45, 55, 31, page.title, 'F2', '#152439');
  text(45, 84, 17, page.subtitle, 'F1', '#52647A');
  text(1280, 55, 13, `SYSTEM CLASS DIAGRAM - ${number} / ${pages.length}`, 'F1', '#77879A');
  for (const link of page.links) {
    stroke('#65778F'); commands.push('2.2 w');
    const [first, ...rest] = link.points;
    commands.push(`${first[0]} ${H - first[1]} m ${rest.map(p => `${p[0]} ${H - p[1]} l`).join(' ')} S`);
    const labelW = Math.max(82, link.label.length * 8.1 + 22);
    rect(link.at[0] - labelW / 2, link.at[1] - 16, labelW, 27, '#FFFFFF');
    text(link.at[0] - link.label.length * 3.2, link.at[1] + 3, 13, link.label, 'F2', '#43566E');
    const next = link.points[1], last = link.points[link.points.length - 1], prev = link.points[link.points.length - 2];
    const dx1 = next[0] - first[0], dy1 = next[1] - first[1], dx2 = last[0] - prev[0], dy2 = last[1] - prev[1];
    text(first[0] + (Math.abs(dx1) > Math.abs(dy1) ? Math.sign(dx1) * 9 : 12), first[1] + (Math.abs(dx1) > Math.abs(dy1) ? 21 : dy1 > 0 ? 21 : -8), 14, link.mult[0], 'F2', '#334A66');
    text(last[0] - (Math.abs(dx2) > Math.abs(dy2) ? Math.sign(dx2) * 9 + link.mult[1].length * 7 : -12), last[1] + (Math.abs(dx2) > Math.abs(dy2) ? 21 : dy2 > 0 ? -8 : 21), 14, link.mult[1], 'F2', '#334A66');
  }
  for (const [name, [x, y, w]] of Object.entries(page.nodes)) {
    const meta = classes[name], fields = fieldsFor(page, name), h = boxHeight(fields), [strong] = palette[meta.tone];
    rect(x, y, w, h, '#FFFFFF'); stroke('#AAB8C8'); commands.push('1.5 w', `${x} ${H - y - h} ${w} ${h} re S`);
    rect(x, y, w, 58, strong);
    text(x + 20, y + 28, 22, name, 'F2', '#FFFFFF');
    text(x + 20, y + 47, 13, `C# ${meta.model}`, 'F1', '#DDE9F5');
    fields.forEach((field, i) => text(x + 20, y + 86 + i * 22, 14, field, field.includes('{PK}') || field.includes('{FK}') ? 'F4' : 'F3', '#283648'));
  }
  rect(45, 951, 1420, 38, '#E8EDF4');
  text(62, 976, 15, page.note, 'F1', '#43566E');
  return commands.join('\n') + '\n';
}
function makePdf(contents) {
  const count = contents.length, objects = [];
  objects.push('<< /Type /Catalog /Pages 2 0 R >>');
  objects.push(`<< /Type /Pages /Kids [${contents.map((_, i) => `${3 + i} 0 R`).join(' ')}] /Count ${count} >>`);
  const streamStart = 3 + count;
  for (let i = 0; i < count; i++) objects.push(`<< /Type /Page /Parent 2 0 R /MediaBox [0 0 1510 1010] /Resources << /Font << /F1 ${streamStart + count} 0 R /F2 ${streamStart + count + 1} 0 R /F3 ${streamStart + count + 2} 0 R /F4 ${streamStart + count + 3} 0 R >> >> /Contents ${streamStart + i} 0 R >>`);
  for (const stream of contents) objects.push(`<< /Length ${Buffer.byteLength(stream, 'ascii')} >>\nstream\n${stream}endstream`);
  objects.push('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>');
  objects.push('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>');
  objects.push('<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>');
  objects.push('<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold >>');
  let result = '%PDF-1.4\n'; const offsets = [0];
  for (let i = 0; i < objects.length; i++) { offsets.push(Buffer.byteLength(result, 'ascii')); result += `${i + 1} 0 obj\n${objects[i]}\nendobj\n`; }
  const xrefAt = Buffer.byteLength(result, 'ascii');
  result += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  for (const offset of offsets.slice(1)) result += `${String(offset).padStart(10, '0')} 00000 n \n`;
  result += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xrefAt}\n%%EOF`;
  return Buffer.from(result, 'ascii');
}
fs.writeFileSync(path.join(out, 'System_Class_Diagram.pdf'), makePdf(pages.map((page, i) => pdfPage(page, i + 1))));
const section = page => `<section id="${page.id}"><h2>${xml(page.title)}</h2><p>${xml(page.subtitle)}</p><img src="Class_Diagram_${page.id}.svg" alt="${xml(page.title)}"/><p><a href="Class_Diagram_${page.id}.svg">Open full-size SVG</a> · <a href="Class_Diagram_${page.id}.mmd">Mermaid source</a></p></section>`;
fs.writeFileSync(path.join(out, 'System_Class_Diagram.html'), `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>YNCLINO System Class Diagram</title><style>body{margin:0;background:#f3f6fa;color:#17263a;font:16px Arial,sans-serif}header{padding:28px max(24px,calc((100vw - 1200px)/2));background:#233b62;color:#fff}h1{margin:0 0 10px}header p{margin:0;color:#d9e5f4}nav{display:flex;gap:10px;flex-wrap:wrap;margin-top:18px}nav a{color:#fff;border:1px solid #95a9c3;border-radius:6px;padding:9px 13px;text-decoration:none}main{max-width:1400px;margin:auto;padding:25px}section{background:#fff;border:1px solid #d7dfe8;border-radius:12px;margin:0 0 28px;padding:18px;box-shadow:0 4px 18px #19304f10}h2{margin:0 0 5px}section p{color:#52647a}img{display:block;width:100%;height:auto;border:1px solid #d7dfe8;border-radius:6px}a{color:#205887}</style></head><body><header><h1>YNCLINO System Class Diagram</h1><p>Actual C# model classes and EF/SQL associations, separated into readable views.</p><nav>${pages.map(p => `<a href="#${p.id}">${xml(p.title)}</a>`).join('')}</nav></header><main>${pages.map(section).join('')}<p>PK = primary key · FK = foreign key · 0..1 = optional one · 0..* = any number. Properties marked “cache” are synchronized summaries; “derived” properties are not stored. The Role property on User maps to the SQL Roles table; there is no C# Role class.</p></main></body></html>`);
console.log(`Generated ${pages.length} UML class diagram views (SVG, Mermaid, PDF, and HTML index).`);
