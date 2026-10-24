// Renders 1920x1080 scene cards for the promo video. Usage: node scenes.mjs <screenshotsDir> <outDir> <scratch dir>
import { chromium } from 'playwright';
import { writeFileSync } from 'node:fs';
const [SHOTS, OUT, PAGES] = process.argv.slice(2);
const img = (n) => `file://${SHOTS}/${n}.png`;
const css = `
@import url('https://fonts.googleapis.com/css2?family=Fraunces:opsz,wght@9..144,500;9..144,600&family=Inter:wght@400;500;600;700&display=swap');
:root{--bg:#faf6f0;--ink:#1f2a44;--muted:#5b6475;--accent:#c2603a;--accent-strong:#a64e2c;--soft:#e8dcc4;--green:#2f6b4f}
*{box-sizing:border-box;margin:0;padding:0}
body{width:1920px;height:1080px;overflow:hidden;background:var(--bg);font-family:Inter,sans-serif;color:var(--ink);position:relative}
body.dark{background:var(--ink);color:#fdfbf7}
body.clear{background:transparent}
.display{font-family:Fraunces,Georgia,serif;font-weight:600;letter-spacing:-.01em;line-height:1.05}
.eyebrow{font-weight:600;text-transform:uppercase;letter-spacing:.14em;color:var(--accent-strong);font-size:24px}
.dark .eyebrow{color:#e8a184}
.stitch{position:absolute;left:72px;right:72px;border-top:4px dashed var(--accent);opacity:.55}
.browser{background:#fff;border-radius:18px;box-shadow:0 40px 90px rgb(31 42 68/.22),0 0 0 1px rgb(31 42 68/.08);overflow:hidden;position:absolute}
.browser .bar{height:44px;background:#f1ebe1;display:flex;align-items:center;gap:9px;padding-left:18px}
.browser .bar i{width:14px;height:14px;border-radius:50%;background:#d9cfbf;display:block}
.browser img{display:block;width:100%}
.phone{position:absolute;background:var(--ink);border-radius:48px;padding:12px;box-shadow:0 40px 80px rgb(31 42 68/.3)}
.phone img{display:block;width:100%;border-radius:37px}
.cap{position:absolute;left:96px;top:90px;width:560px}
.cap h2{font-size:64px;margin-top:20px}
.cap p{font-size:28px;color:var(--muted);margin-top:22px;line-height:1.45}
ul.ticks{list-style:none;display:grid;gap:26px;font-size:34px;font-weight:500}
ul.ticks li{display:flex;gap:18px;align-items:center}
ul.ticks li::before{content:'';flex:none;width:34px;height:34px;border-radius:50%;background:var(--green) url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='white' stroke-width='3.5' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpath d='M5 12l5 5 9-10'/%3E%3C/svg%3E") center/20px no-repeat}
.chip{display:inline-block;border:2px solid rgb(253 251 247/.35);border-radius:999px;padding:12px 26px;font-weight:600;font-size:26px;margin:0 8px}
.cta{display:inline-block;background:var(--accent-strong);color:#fff;font-weight:700;border-radius:999px;font-size:32px;padding:22px 44px}
.bar-cap{position:absolute;left:96px;bottom:80px;background:rgb(31 42 68/.92);color:#fdfbf7;border-radius:20px;padding:26px 38px;max-width:1100px}
.bar-cap .eyebrow{color:#e8a184;font-size:20px}
.bar-cap h2{font-size:48px;margin-top:10px}
`;
const browser = (src, s) => `<div class="browser" style="${s}"><div class="bar"><i></i><i></i><i></i></div><img src="${src}"></div>`;
const phone = (src, s) => `<div class="phone" style="${s}"><img src="${src}"></div>`;
const shotScene = (eyebrow, title, sub, shot) => ({
  body: `<div class="cap"><div class="eyebrow">${eyebrow}</div><h2 class="display">${title}</h2><p>${sub}</p></div>
  ${browser(img(shot), 'width:1160px;right:-40px;top:150px')}`,
});

const scenes = {
  '01-intro': { cls: 'dark', body: `<div style="position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;text-align:center">
    <div class="eyebrow">Full-stack e-commerce development</div>
    <h1 class="display" style="font-size:120px;margin:28px 0 30px;max-width:1500px">Your custom online store, built from scratch</h1>
    <div><span class="chip">Angular</span><span class="chip">ASP.NET Core</span><span class="chip">PostgreSQL</span></div></div>` },
  '02-home-cap': { cls: 'clear', body: `<div class="bar-cap"><div class="eyebrow">Storefront</div><h2 class="display">A branded shop that feels like your business</h2></div>` },
  '03-shop': shotScene('Catalog', 'Browse, filter and search', 'Categories, sorting, instant search and shareable filter links.', 'desktop-shop'),
  '04-product': shotScene('Product pages', 'Every product tells its story', 'Photos, price, stock status, materials and the maker behind it.', 'desktop-product'),
  '05-cart': shotScene('Cart', 'Stock is held while you shop', 'One-of-a-kind items are reserved for 15 minutes, so nobody double-sells.', 'desktop-cart-drawer'),
  '06-checkout': shotScene('Checkout', 'A short, friendly checkout', 'Validated delivery details, prices re-checked on the server, order tracking after.', 'desktop-checkout'),
  '07-mobile': { body: `<div class="cap" style="width:600px;top:260px"><div class="eyebrow">Mobile-first</div><h2 class="display">Built for the phone in your customer's hand</h2><p>Fast, accessible (WCAG AA) and keyboard friendly.</p></div>
    ${phone(img('mobile-home'), 'width:330px;left:760px;top:120px')}${phone(img('mobile-shop'), 'width:330px;left:1130px;top:200px')}${phone(img('mobile-product'), 'width:330px;left:1500px;top:120px')}` },
  '08-admin': shotScene('Admin panel', 'See the business at a glance', 'Sales, orders awaiting confirmation and low-stock alerts.', 'desktop-admin-dashboard'),
  '09-admin-products': shotScene('Admin panel', 'Manage products and stock', 'Add products with photos, update prices and stock, ship orders.', 'desktop-admin-products'),
  '10-quality': { body: `<div class="stitch" style="top:70px"></div><div class="stitch" style="bottom:70px"></div><div style="position:absolute;left:160px;top:250px">
    <div class="eyebrow">Under the hood</div><h2 class="display" style="font-size:76px;margin:20px 0 56px">Production-grade, not a template</h2>
    <ul class="ticks"><li>Secure login: short-lived tokens, rotating refresh cookies</li><li>No overselling: stock updates are race-safe and tested</li><li>Integration tests on real PostgreSQL, CI on every push</li><li>Docker deploy, free-tier friendly hosting</li></ul></div>` },
  '11-outro': { cls: 'dark', body: `<div style="position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;text-align:center">
    <h1 class="display" style="font-size:120px;margin-bottom:26px">Let's build your store</h1>
    <p style="font-size:36px;opacity:.85;margin-bottom:56px">Storefront · Checkout · Admin panel · Deployment</p>
    <div class="cta">Message me to get started</div></div>` },
};

const b = await chromium.launch();
for (const [name, s] of Object.entries(scenes)) {
  const p = await b.newPage({ viewport: { width: 1920, height: 1080 } });
  const file = `${PAGES}/scene-${name}.html`;
  writeFileSync(file, `<!doctype html><html><head><meta charset="utf-8"><style>${css}</style></head><body class="${s.cls ?? ''}">${s.body}</body></html>`);
  await p.goto(`file://${file}`, { waitUntil: 'networkidle' });
  await p.evaluate(() => document.fonts.ready);
  await p.screenshot({ path: `${OUT}/${name}.png`, omitBackground: s.cls === 'clear' });
  await p.close();
  console.log(name);
}
await b.close();
