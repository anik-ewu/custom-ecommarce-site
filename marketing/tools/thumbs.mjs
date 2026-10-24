// Composes Fiverr (1280x769), Upwork (1600x1200) and video thumbnails from the screenshots.
// Usage: TMPDIR_PAGES=<scratch dir> node thumbs.mjs <screenshotsDir> <outDir>
import { writeFileSync } from 'node:fs';
import { chromium } from 'playwright';
const [SHOTS, OUT] = process.argv.slice(2);
const img = (n) => `file://${SHOTS}/${n}.png`;

const css = `
@import url('https://fonts.googleapis.com/css2?family=Fraunces:opsz,wght@9..144,500;9..144,600&family=Inter:wght@400;500;600;700&display=swap');
:root{--bg:#faf6f0;--ink:#1f2a44;--muted:#5b6475;--accent:#c2603a;--accent-strong:#a64e2c;--red:#9e2b25;--soft:#e8dcc4;--green:#2f6b4f}
*{box-sizing:border-box;margin:0;padding:0}
body{width:var(--w);height:var(--h);overflow:hidden;background:var(--bg);font-family:Inter,system-ui,sans-serif;color:var(--ink);position:relative}
.stitch{position:absolute;border-top:3px dashed var(--accent);opacity:.55}
.display{font-family:Fraunces,Georgia,serif;font-weight:600;letter-spacing:-.01em;line-height:1.05}
.eyebrow{font-weight:600;text-transform:uppercase;letter-spacing:.14em;color:var(--accent-strong)}
.browser{background:#fff;border-radius:14px;box-shadow:0 30px 70px rgb(31 42 68/.22),0 0 0 1px rgb(31 42 68/.08);overflow:hidden;position:absolute}
.browser .bar{height:34px;background:#f1ebe1;display:flex;align-items:center;gap:7px;padding-left:14px}
.browser .bar i{width:11px;height:11px;border-radius:50%;background:#d9cfbf;display:block}
.browser img{display:block;width:100%}
.phone{position:absolute;background:#1f2a44;border-radius:38px;padding:10px;box-shadow:0 30px 60px rgb(31 42 68/.3)}
.phone img{display:block;width:100%;border-radius:29px}
.chips{display:flex;flex-wrap:wrap;gap:10px}
.chip{background:#fff;border:1.5px solid var(--soft);border-radius:999px;padding:8px 16px;font-weight:600;color:var(--ink)}
.cta{display:inline-block;background:var(--accent-strong);color:#fff;font-weight:700;border-radius:999px}
.badge{position:absolute;background:var(--ink);color:#fff;border-radius:16px;font-weight:600}
ul.ticks{list-style:none}
ul.ticks li{display:flex;gap:12px;align-items:flex-start;font-weight:500}
ul.ticks li::before{content:'';flex:none;width:22px;height:22px;margin-top:3px;border-radius:50%;background:var(--green) url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='white' stroke-width='3.5' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpath d='M5 12l5 5 9-10'/%3E%3C/svg%3E") center/14px no-repeat}
`;

const browser = (src, s) => `<div class="browser" style="${s}"><div class="bar"><i></i><i></i><i></i></div><img src="${src}"></div>`;
const phone = (src, s) => `<div class="phone" style="${s}"><img src="${src}"></div>`;

// Main hero: one headline, desktop + phone mockup. Scales with width.
const hero = (w, h, { headline, sub, chips }) => {
  const u = w / 1280;
  return `<div class="stitch" style="left:${48 * u}px;right:${48 * u}px;top:${36 * u}px"></div>
  <div class="stitch" style="left:${48 * u}px;right:${48 * u}px;bottom:${36 * u}px"></div>
  <div style="position:absolute;left:${64 * u}px;top:${h * 0.15}px;width:${500 * u}px">
    <div class="eyebrow" style="font-size:${17 * u}px;margin-bottom:${18 * u}px">Angular · ASP.NET Core · PostgreSQL</div>
    <h1 class="display" style="font-size:${62 * u}px">${headline}</h1>
    <p style="font-size:${21 * u}px;color:var(--muted);margin:${20 * u}px 0 ${26 * u}px;line-height:1.45">${sub}</p>
    <div class="chips" style="font-size:${16 * u}px">${chips.map((c) => `<span class="chip">${c}</span>`).join('')}</div>
  </div>
  ${browser(img('desktop-home'), `width:${640 * u}px;right:${-40 * u}px;top:${h * 0.17}px`)}
  ${phone(img('mobile-product'), `width:${190 * u}px;right:${470 * u}px;top:${h * 0.38}px`)}`;
};

const features = (w, h) => {
  const u = w / 1280;
  const items = ['Product catalog, filters & search', 'Cart, checkout & order tracking', 'Admin dashboard: orders, stock, products', 'Secure login with refresh-token rotation', 'Mobile-first, accessible (WCAG AA)', 'Tested on real Postgres + CI'];
  return `<div class="stitch" style="left:${48 * u}px;right:${48 * u}px;top:${36 * u}px"></div>
  <div style="position:absolute;left:${64 * u}px;top:${80 * u}px;width:${540 * u}px">
    <div class="eyebrow" style="font-size:${17 * u}px;margin-bottom:${16 * u}px">What you get</div>
    <h2 class="display" style="font-size:${52 * u}px;margin-bottom:${30 * u}px">A complete online store, not a template</h2>
    <ul class="ticks" style="font-size:${21 * u}px;display:grid;gap:${16 * u}px">${items.map((i) => `<li>${i}</li>`).join('')}</ul>
  </div>
  ${browser(img('desktop-cart-drawer'), `width:${560 * u}px;right:${-30 * u}px;top:${70 * u}px`)}
  ${browser(img('desktop-checkout'), `width:${480 * u}px;right:${170 * u}px;top:${h * 0.5}px`)}`;
};

const admin = (w, h) => {
  const u = w / 1280;
  return `<div class="stitch" style="left:${48 * u}px;right:${48 * u}px;bottom:${36 * u}px"></div>
  <div style="position:absolute;left:0;right:0;top:${50 * u}px;text-align:center">
    <div class="eyebrow" style="font-size:${17 * u}px;margin-bottom:${12 * u}px">Built-in admin panel</div>
    <h2 class="display" style="font-size:${50 * u}px">Run the shop without touching code</h2>
  </div>
  ${browser(img('desktop-admin-dashboard'), `width:${760 * u}px;left:${70 * u}px;top:${200 * u}px`)}
  ${browser(img('desktop-admin-products'), `width:${560 * u}px;right:${50 * u}px;top:${300 * u}px`)}
  <div class="badge" style="font-size:${19 * u}px;padding:${14 * u}px ${22 * u}px;left:${110 * u}px;top:${h - 150 * u}px">Orders · Stock · Products · Artisans</div>`;
};

const video = (w, h) => {
  const u = w / 1280;
  return `<div style="position:absolute;inset:0;background:var(--ink)"></div>
  ${browser(img('desktop-home'), `width:${760 * u}px;right:${-60 * u}px;top:${110 * u}px;opacity:.95`)}
  ${phone(img('mobile-home'), `width:${200 * u}px;right:${560 * u}px;top:${250 * u}px`)}
  <div style="position:absolute;left:${64 * u}px;top:${150 * u}px;width:${520 * u}px;color:#fdfbf7">
    <div class="eyebrow" style="font-size:${18 * u}px;color:#e8a184;margin-bottom:${18 * u}px">Live demo walkthrough</div>
    <h1 class="display" style="font-size:${72 * u}px">Custom e‑commerce, start to finish</h1>
    <div class="cta" style="font-size:${22 * u}px;padding:${16 * u}px ${30 * u}px;margin-top:${34 * u}px">▶ Watch the 60‑second tour</div>
  </div>`;
};

// Upwork wants 4:3: render the 1280x769 composition in a wider frame, then a stack strip below.
const fourByThree = (fn) => (w, h) => {
  const ih = Math.round((w * 769) / 1280);
  const stack = ['Angular 22', 'ASP.NET Core · .NET 10', 'PostgreSQL', 'Docker', 'GitHub Actions CI'];
  return `<div style="position:absolute;left:0;top:${Math.round((h - ih) * 0.25)}px;width:${w}px;height:${ih}px">${fn(w, ih)}</div>
  <div class="chips" style="position:absolute;left:0;right:0;bottom:${(h - ih) * 0.32}px;justify-content:center;font-size:24px;gap:14px">${stack.map((c) => `<span class="chip" style="padding:12px 24px">${c}</span>`).join('')}</div>`;
};

const jobs = [
  ['fiverr-gig-1-main', 1280, 769, (w, h) => hero(w, h, { headline: 'I will build your custom e‑commerce website', sub: 'Fast, secure online store with an admin panel, built from scratch for your brand.', chips: ['Mobile-first', 'Admin panel', 'Secure checkout'] })],
  ['fiverr-gig-2-features', 1280, 769, features],
  ['fiverr-gig-3-admin', 1280, 769, admin],
  ['upwork-project-1-main', 1600, 1200, fourByThree((w, h) => hero(w, h, { headline: 'Custom e‑commerce website, built to sell', sub: 'Full-stack online store: storefront, checkout and admin panel, designed around your brand.', chips: ['Angular', '.NET 10 API', 'PostgreSQL', 'Mobile-first'] }))],
  ['upwork-project-2-features', 1600, 1200, fourByThree(features)],
  ['upwork-project-3-admin', 1600, 1200, fourByThree(admin)],
  ['video-thumbnail', 1280, 720, video],
];

const b = await chromium.launch();
for (const [name, w, h, fn] of jobs) {
  const p = await b.newPage({ viewport: { width: w, height: h } });
  const file = `${process.env.TMPDIR_PAGES}/${name}.html`;
  writeFileSync(file, `<!doctype html><html><head><meta charset="utf-8"><style>${css}</style></head><body style="--w:${w}px;--h:${h}px">${fn(w, h)}</body></html>`);
  await p.goto(`file://${file}`, { waitUntil: 'networkidle' });
  await p.evaluate(() => document.fonts.ready);
  await p.screenshot({ path: `${OUT}/${name}.png` });
  await p.close();
  console.log(name);
}
await b.close();
