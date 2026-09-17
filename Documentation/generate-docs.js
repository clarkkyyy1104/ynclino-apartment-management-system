// Regenerate the review copies from the application's current SQL schema and
// the verified controller workflows. Run with: node Documentation/generate-docs.js
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const sql = fs.readFileSync(path.join(root, 'Database', 'ynclino_schema.sql'), 'utf8');
const out = __dirname;
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

const tables = [...sql.matchAll(/CREATE TABLE (\w+) \(([\s\S]*?)\n\);/g)].map(m => {
  const name = m[1], body = m[2];
  const columns = [...body.matchAll(/^    (\w+)\s+((?:TINYINT|BIGINT|INT|VARCHAR|DECIMAL|DATETIME|DATE|BOOLEAN)\b[^\n]*|GENERATED ALWAYS AS \()/gm)]
    .map(x => ({ name: x[1], type: x[2].trim() }));
  const primary = body.match(/PRIMARY KEY \((\w+)\)/)?.[1];
  const foreign = [...body.matchAll(/FOREIGN KEY \((\w+)\)\s*REFERENCES (\w+)\((\w+)\)/g)]
    .map(x => ({ from: x[1], to: x[2], target: x[3] }));
  return { name, columns, primary, foreign };
});

const entityId = name => `entity_${name}`;
const locations = new Map(tables.map((t, i) => [t.name, { x: 40 + (i % 4) * 470, y: 40 + Math.floor(i / 4) * 630 }]));
let erdCells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>'];
for (const t of tables) {
  const { x, y } = locations.get(t.name);
  const fk = new Set(t.foreign.map(f => f.from));
  const lines = t.columns.map(c => `${c.name === t.primary ? 'PK ' : fk.has(c.name) ? 'FK ' : '    '}${c.name} : ${c.type.replace(/\s+(NOT NULL|NULL|DEFAULT|AUTO_INCREMENT).*/, '')}`);
  const label = `<b>${t.name}</b><br/><font face="Courier New" size="10">${lines.map(esc).join('<br/>')}</font>`;
  const height = 42 + lines.length * 19;
  erdCells.push(`<mxCell id="${entityId(t.name)}" value="${esc(label)}" style="rounded=1;whiteSpace=wrap;html=1;align=left;verticalAlign=top;spacing=12;fillColor=#F7F9FC;strokeColor=#4D6075;fontSize=14;" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="440" height="${height}" as="geometry"/></mxCell>`);
}
let edgeNo = 0;
for (const t of tables) for (const f of t.foreign) {
  erdCells.push(`<mxCell id="fk_${++edgeNo}" value="${esc(f.from)} → ${esc(f.target)}" style="edgeStyle=orthogonalEdgeStyle;rounded=1;html=1;endArrow=ERone;startArrow=ERmany;strokeColor=#54677C;fontSize=10;labelBackgroundColor=#FFFFFF;" edge="1" parent="1" source="${entityId(t.name)}" target="${entityId(f.to)}"><mxGeometry relative="1" as="geometry"/></mxCell>`);
}
const diagram = (name, id, cells, w = 1900, h = 1900) => `<diagram name="${esc(name)}" id="${id}"><mxGraphModel dx="1400" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="${w}" pageHeight="${h}" math="0" shadow="0"><root>${cells.join('')}</root></mxGraphModel></diagram>`;
fs.writeFileSync(path.join(out, 'System_ERD_Corrected.drawio'), `<?xml version="1.0" encoding="UTF-8"?><mxfile host="app.diagrams.net">${diagram('Current YAMSDB schema', 'current-yamsdb', erdCells)}</mxfile>`);

const cases = [
  { module: 'Units', entries: [
    ['View and search units', 'Admin, Tenant', 'Show unit number, type, rent, capacity and status; search by unit number or type and filter by status.'],
    ['Add, update or delete a unit', 'Admin', 'Validate unit details. Deletion is allowed only when references and occupancy permit it.']
  ]},
  { module: 'Tenants', entries: [
    ['Register tenant', 'Admin', 'Create a tenant account and profile with contact and optional emergency contact, photo and unit information.'],
    ['View and search tenants', 'Admin', 'View tenant details and search by first or last name; filter by active or inactive status.'],
    ['View own tenant details', 'Tenant', 'Open only the tenant profile linked to the signed-in account.'],
    ['Edit, archive or reactivate tenant', 'Admin', 'Maintain tenant details and status; synchronize account and occupancy information.']
  ]},
  { module: 'User Accounts', entries: [
    ['View user accounts', 'Admin', 'Show admin, maintenance and tenant accounts.'],
    ['Create staff account', 'Admin', 'Create an Admin or Maintenance account. Tenant accounts are created through tenant registration.'],
    ['Deactivate or reactivate account', 'Admin', 'Change active status, with safeguards for the main admin and signed-in account.'],
    ['Reset non-admin password', 'Admin', 'Issue a temporary password for a tenant or maintenance account and require a password change at next login.']
  ]},
  { module: 'Unit Transfers', entries: [
    ['Request a unit or transfer', 'Tenant', 'Submit a request for an available unit; the current unit is retained on the request.'],
    ['View and review request', 'Admin', 'View requests, approve an eligible unit change or reject with optional notes.'],
    ['Cancel pending request', 'Tenant', 'Cancel the tenant’s own pending request.'],
    ['Archive or restore closed request', 'Admin, Tenant', 'File an approved, rejected or cancelled request from the viewer’s list, or restore it.']
  ]},
  { module: 'Billing and Payments', entries: [
    ['View and filter bills', 'Admin, Tenant', 'Admins see managed bills; tenants see only their own. Filter by status and search by tenant name.'],
    ['Issue or update bill', 'Admin', 'Create or edit billing details for a tenant.'],
    ['Record payment', 'Admin', 'Record the payment in Payments and update the bill’s amount-paid cache and status.'],
    ['Archive or restore settled bill', 'Admin', 'Archive only a fully settled bill from the admin working list; tenant history remains visible.'],
    ['View payment history', 'Admin, Tenant', 'Admins see history by tenant and can search tenant names; tenants see only their own payment history.']
  ]},
  { module: 'Maintenance', entries: [
    ['Submit request', 'Admin, Tenant', 'Create a request with category, description and unit; tenants submit for their own active tenancy.'],
    ['View and filter requests', 'Admin, Tenant, Maintenance Staff', 'Tenants see their own requests; staff see assigned work; admins see managed requests.'],
    ['Update or assign request', 'Admin, Maintenance Staff', 'Admins manage and assign requests; maintenance staff update work assigned to them.'],
    ['Archive or restore closed request', 'Admin, Tenant, Maintenance Staff', 'Archive a resolved or cancelled request for the viewer’s side, or restore it.']
  ]},
  { module: 'Lost and Found', entries: [
    ['Report item', 'Admin, Tenant', 'Create a lost or found report with item name, type, location, description and optional photo.'],
    ['View and search items', 'Admin, Tenant', 'Search by item name or location and filter by type or status; tenant visibility follows the application rules.'],
    ['Submit ownership claim', 'Tenant', 'Submit verification details and optional proof image for a found item.'],
    ['Review ownership claim', 'Admin', 'Approve or reject a pending claim; approval marks the item claimed and records the claimant.'],
    ['Update open record', 'Admin', 'Edit an item while it remains open.'],
    ['Archive or restore closed item', 'Admin', 'Archive a closed item with no pending claims, or restore it; claim history remains available.']
  ]}
];

const actorColors = { Admin: '#FFE9D9', Tenant: '#E4F4E9', 'Maintenance Staff': '#E5ECFF' };
const useCasePages = cases.map((group, pi) => {
  const actors = [...new Set(group.entries.flatMap(e => e[1].split(', ')))];
  const cells = ['<mxCell id="0"/>', '<mxCell id="1" parent="0"/>'];
  actors.forEach((actor, ai) => cells.push(`<mxCell id="actor_${pi}_${ai}" value="${esc(actor)}" style="shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;fillColor=${actorColors[actor]};" vertex="1" parent="1"><mxGeometry x="40" y="${80 + ai * 180}" width="90" height="100" as="geometry"/></mxCell>`));
  group.entries.forEach((entry, ci) => {
    cells.push(`<mxCell id="case_${pi}_${ci}" value="${esc(entry[0])}" style="ellipse;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=#4D6075;fontSize=14;" vertex="1" parent="1"><mxGeometry x="360" y="${50 + ci * 85}" width="320" height="60" as="geometry"/></mxCell>`);
    entry[1].split(', ').forEach(actor => {
      const ai = actors.indexOf(actor);
      cells.push(`<mxCell id="association_${pi}_${ci}_${ai}" style="endArrow=none;startArrow=none;html=1;strokeColor=#4D6075;" edge="1" parent="1" source="actor_${pi}_${ai}" target="case_${pi}_${ci}"><mxGeometry relative="1" as="geometry"/></mxCell>`);
    });
  });
  return diagram(group.module, `usecase-${pi}`, cells, 750, Math.max(700, 120 + group.entries.length * 85));
});
fs.writeFileSync(path.join(out, 'System_Use_Cases_Corrected.drawio'), `<?xml version="1.0" encoding="UTF-8"?><mxfile host="app.diagrams.net">${useCasePages.join('')}</mxfile>`);

const wordP = (text, style) => `<w:p>${style ? `<w:pPr><w:pStyle w:val="${style}"/></w:pPr>` : ''}<w:r><w:t xml:space="preserve">${esc(text)}</w:t></w:r></w:p>`;
const paragraphs = [
  wordP('YNCLINO Apartment Management System', 'Title'),
  wordP('Corrected use case descriptions', 'Subtitle'),
  wordP('Aligned with Database/ynclino_schema.sql and current ASP.NET controllers. Diagrams are in System_Use_Cases_Corrected.drawio; the database diagram is in System_ERD_Corrected.drawio.', null),
  wordP('General rules', 'Heading1'),
  wordP('Access requires an authenticated account. Tenant views are limited to their own records where stated. An archive hides a record from a working list and retains its history. The system does not provide tenant ID, payment reference, or payment date search in the described lists.', null)
];
for (const group of cases) {
  paragraphs.push(wordP(group.module, 'Heading1'));
  for (const [name, actors, behavior] of group.entries) {
    paragraphs.push(wordP(name, 'Heading2'));
    paragraphs.push(wordP(`Actors: ${actors}.`));
    paragraphs.push(wordP(`Successful scenario: ${behavior}`));
    paragraphs.push(wordP('Exception: Missing, invalid or unauthorized records are rejected; validation errors leave the existing record unchanged.'));
  }
}
paragraphs.push(wordP('Database notes', 'Heading1'));
paragraphs.push(wordP('Users stores account identity and RoleID references Roles. TenantProfiles stores current tenant details and UnitID; TenantUnitAssignments preserves assignment history. UnitTransferRequests references the tenant and requested/current units. Payments is the payment ledger; Billings.AmountPaid is an application-maintained compatibility cache. MaintenanceRequests.AssignedStaffUserID references a maintenance user. LostFoundItems and ClaimRequests preserve the item and ownership claim workflow.'));
const documentXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>${paragraphs.join('')}<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1080" w:right="1080" w:bottom="1080" w:left="1080"/></w:sectPr></w:body></w:document>`;
const docxFiles = {
  '[Content_Types].xml': `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/></Types>`,
  '_rels/.rels': `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>`,
  'word/document.xml': documentXml,
  'word/_rels/document.xml.rels': `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>`,
  'word/styles.xml': `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:rPr><w:sz w:val="22"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:rPr><w:b/><w:sz w:val="36"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Subtitle"><w:name w:val="Subtitle"/><w:rPr><w:sz w:val="26"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:rPr><w:b/><w:sz w:val="28"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:rPr><w:b/><w:sz w:val="24"/></w:rPr></w:style></w:styles>`
};
function crc32(data) {
  let crc = -1;
  for (const byte of data) {
    crc ^= byte;
    for (let i = 0; i < 8; i++) crc = (crc >>> 1) ^ (crc & 1 ? 0xEDB88320 : 0);
  }
  return (crc ^ -1) >>> 0;
}
function makeZip(files) {
  const locals = [], central = [];
  let offset = 0;
  for (const [name, content] of Object.entries(files)) {
    const filename = Buffer.from(name), data = Buffer.from(content, 'utf8'), checksum = crc32(data);
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034B50, 0); local.writeUInt16LE(20, 4); local.writeUInt32LE(checksum, 14);
    local.writeUInt32LE(data.length, 18); local.writeUInt32LE(data.length, 22); local.writeUInt16LE(filename.length, 26);
    locals.push(local, filename, data);
    const directory = Buffer.alloc(46);
    directory.writeUInt32LE(0x02014B50, 0); directory.writeUInt16LE(20, 4); directory.writeUInt16LE(20, 6);
    directory.writeUInt32LE(checksum, 16); directory.writeUInt32LE(data.length, 20); directory.writeUInt32LE(data.length, 24);
    directory.writeUInt16LE(filename.length, 28); directory.writeUInt32LE(offset, 42);
    central.push(directory, filename);
    offset += local.length + filename.length + data.length;
  }
  const centralSize = central.reduce((n, b) => n + b.length, 0), count = Object.keys(files).length;
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054B50, 0); end.writeUInt16LE(count, 8); end.writeUInt16LE(count, 10);
  end.writeUInt32LE(centralSize, 12); end.writeUInt32LE(offset, 16);
  return Buffer.concat([...locals, ...central, end]);
}
fs.writeFileSync(path.join(out, 'Use_Case_Descriptions_Corrected.docx'), makeZip(docxFiles));

const htmlTables = tables.map(t => `<section><h2>${esc(t.name)}</h2><table><thead><tr><th>Key</th><th>Column</th><th>Type</th></tr></thead><tbody>${t.columns.map(c => `<tr><td>${c.name === t.primary ? 'PK' : t.foreign.some(f => f.from === c.name) ? 'FK' : ''}</td><td>${esc(c.name)}</td><td>${esc(c.type)}</td></tr>`).join('')}</tbody></table><p>${t.foreign.map(f => `${esc(f.from)} → ${esc(f.to)}.${esc(f.target)}`).join('<br/>')}</p></section>`).join('');
fs.writeFileSync(path.join(out, 'System_ERD_Corrected.html'), `<!doctype html><html><head><meta charset="utf-8"><title>YAMSDB ERD — Current Schema</title><style>@page{size:A3 landscape;margin:12mm}body{font:12px Arial;color:#233143}h1{font-size:24px}main{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}section{break-inside:avoid;border:1px solid #91a0b0;border-radius:6px;padding:8px}h2{font-size:15px;margin:0 0 7px;background:#e8edf3;padding:6px}table{width:100%;border-collapse:collapse}td,th{border-bottom:1px solid #ddd;padding:2px 4px;text-align:left;font-size:10px}p{font-size:10px;line-height:1.4}</style></head><body><h1>YNCLINO — Current YAMSDB entities and foreign keys</h1><main>${htmlTables}</main></body></html>`);

// One-page A2 PDF of the same ERD. The lines show FK connections; each box
// lists its exact columns and points out the referenced primary keys.
const pdfW = 1684, pdfH = 1191;
const pdfText = s => String(s).replace(/[^\x20-\x7E]/g, '-').replace(/([\\()])/g, '\\$1');
const boxes = tables.map((t, i) => ({ t, x: 36 + (i % 4) * 412, top: 55 + Math.floor(i / 4) * 373, w: 390, h: 350 }));
const pdf = [];
pdf.push('0.55 G 0.5 w');
for (const box of boxes) for (const f of box.t.foreign) {
  const target = boxes.find(b => b.t.name === f.to);
  if (!target) continue;
  pdf.push(`${box.x + box.w / 2} ${pdfH - box.top - box.h / 2} m ${target.x + target.w / 2} ${pdfH - target.top - target.h / 2} l S`);
}
pdf.push('0 G 0 g');
pdf.push(`BT /F1 20 Tf 36 ${pdfH - 30} Td (YNCLINO - Current YAMSDB ERD) Tj ET`);
for (const { t, x, top, w, h } of boxes) {
  const y = pdfH - top - h;
  pdf.push('1 1 1 rg', `${x} ${y} ${w} ${h} re f`, '0.25 0.35 0.48 RG', `${x} ${y} ${w} ${h} re S`);
  pdf.push('0.89 0.93 0.97 rg', `${x + 1} ${y + h - 29} ${w - 2} 28 re f`, '0 g');
  pdf.push(`BT /F1 13 Tf ${x + 9} ${y + h - 20} Td (${pdfText(t.name)}) Tj ET`);
  let textY = y + h - 44;
  for (const c of t.columns) {
    const mark = c.name === t.primary ? 'PK ' : t.foreign.some(f => f.from === c.name) ? 'FK ' : '   ';
    pdf.push(`BT /F1 9 Tf ${x + 9} ${textY} Td (${pdfText(mark + c.name + ' : ' + c.type.replace(/\s+(NOT NULL|NULL|DEFAULT|AUTO_INCREMENT).*/, ''))}) Tj ET`);
    textY -= 13;
  }
  if (t.foreign.length) {
    textY -= 3;
    pdf.push(`BT /F1 8 Tf ${x + 9} ${textY} Td (References:) Tj ET`);
    textY -= 11;
    for (const f of t.foreign) {
      pdf.push(`BT /F1 8 Tf ${x + 9} ${textY} Td (${pdfText(f.from + ' -> ' + f.to + '.' + f.target)}) Tj ET`);
      textY -= 11;
    }
  }
}
const stream = pdf.join('\n') + '\n';
const objects = [
  '<< /Type /Catalog /Pages 2 0 R >>',
  '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
  `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${pdfW} ${pdfH}] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>`,
  `<< /Length ${Buffer.byteLength(stream, 'ascii')} >>\nstream\n${stream}endstream`,
  '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>'
];
let pdfFile = '%PDF-1.4\n', offsets = [0];
for (let i = 0; i < objects.length; i++) {
  offsets.push(Buffer.byteLength(pdfFile, 'ascii'));
  pdfFile += `${i + 1} 0 obj\n${objects[i]}\nendobj\n`;
}
const xrefAt = Buffer.byteLength(pdfFile, 'ascii');
pdfFile += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
for (const offset of offsets.slice(1)) pdfFile += `${String(offset).padStart(10, '0')} 00000 n \n`;
pdfFile += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xrefAt}\n%%EOF`;
fs.writeFileSync(path.join(out, 'System_ERD_Corrected.pdf'), pdfFile, 'ascii');
console.log(`Generated ${tables.length} entities, ${edgeNo} foreign keys, ${cases.length} use case pages, PDF and DOCX.`);
