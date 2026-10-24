// Renders the A4 case-study PDF for the Fiverr gig gallery (Fiverr allows PDFs up to 10 MB).
// Usage: node pdf.mjs <imagesDir> <out.pdf> <scratch dir>
// imagesDir holds JPEG copies of the screenshots (full-size PNGs make the PDF too big), see marketing/README.md.
import { writeFileSync } from 'node:fs';
import { chromium } from 'playwright';
const [IMGS, OUT, PAGES] = process.argv.slice(2);
const img = (n) => `file://${IMGS}/${n}.jpg`;

const css = `
@import url('https://fonts.googleapis.com/css2?family=Fraunces:opsz,wght@9..144,500;9..144,600&family=Inter:wght@400;500;600;700&display=swap');
@page{size:A4;margin:0}
:root{--bg:#faf6f0;--ink:#1f2a44;--muted:#5b6475;--accent:#c2603a;--accent-strong:#a64e2c;--soft:#e8dcc4;--green:#2f6b4f}
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:Inter,sans-serif;color:var(--ink);-webkit-print-color-adjust:exact;print-color-adjust:exact}
.page{width:210mm;height:297mm;padding:18mm 17mm;position:relative;overflow:hidden;background:var(--bg);page-break-after:always}
.page.dark{background:var(--ink);color:#fdfbf7}
.display{font-family:Fraunces,Georgia,serif;font-weight:600;letter-spacing:-.01em;line-height:1.08}
.eyebrow{font-weight:600;text-transform:uppercase;letter-spacing:.14em;color:var(--accent-strong);font-size:9pt}
.dark .eyebrow{color:#e8a184}
h1{font-size:38pt}
h2{font-size:24pt;margin:3mm 0 4mm}
h3{font-size:12pt;margin-bottom:1.5mm}
p,li{font-size:10.5pt;line-height:1.55}
.muted{color:var(--muted)}
.dark .muted{color:#c9cdd6}
.stitch{border-top:2px dashed var(--accent);opacity:.55;margin:6mm 0}
.browser{background:#fff;border-radius:3mm;box-shadow:0 6mm 14mm rgb(31 42 68/.18),0 0 0 .3mm rgb(31 42 68/.08);overflow:hidden}
.browser .bar{height:5mm;background:#f1ebe1;display:flex;align-items:center;gap:1.2mm;padding-left:2.5mm}
.browser .bar i{width:1.6mm;height:1.6mm;border-radius:50%;background:#d9cfbf;display:block}
.browser img,.phone img{display:block;width:100%}
.phone{background:var(--ink);border-radius:6mm;padding:1.5mm;box-shadow:0 6mm 14mm rgb(31 42 68/.25)}
.phone img{border-radius:4.6mm}
.grid2{display:grid;grid-template-columns:1fr 1fr;gap:7mm}
.grid3{display:grid;grid-template-columns:repeat(3,1fr);gap:5mm}
.card{background:#fff;border:.3mm solid var(--soft);border-radius:3mm;padding:5mm}
.dark .card{background:rgb(253 251 247/.06);border-color:rgb(253 251 247/.18)}
.chip{display:inline-block;border:.3mm solid var(--soft);background:#fff;border-radius:999px;padding:1.5mm 3.5mm;font-weight:600;font-size:9pt;margin:0 1.5mm 1.5mm 0}
.dark .chip{background:transparent;border-color:rgb(253 251 247/.35);color:#fdfbf7}
ul.ticks{list-style:none;display:grid;gap:2.2mm}
ul.ticks li{display:flex;gap:2.5mm;align-items:flex-start}
ul.ticks li::before{content:'';flex:none;width:4.2mm;height:4.2mm;margin-top:.7mm;border-radius:50%;background:var(--green) url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='white' stroke-width='3.5' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpath d='M5 12l5 5 9-10'/%3E%3C/svg%3E") center/2.8mm no-repeat}
figure figcaption{font-size:9pt;color:var(--muted);margin-top:2.5mm}
.num{font-family:Fraunces,Georgia,serif;font-weight:600;font-size:20pt;color:var(--accent-strong)}
.cta{display:inline-block;background:var(--accent-strong);color:#fff;font-weight:700;border-radius:999px;font-size:12pt;padding:3.5mm 8mm}
.foot{position:absolute;left:17mm;right:17mm;bottom:10mm;display:flex;justify-content:space-between;font-size:8pt;color:var(--muted)}
.dark .foot{color:#c9cdd6}
`;

const browser = (n, s = '') => `<div class="browser" style="${s}"><div class="bar"><i></i><i></i><i></i></div><img src="${img(n)}" alt=""></div>`;
const phone = (n, s = '') => `<div class="phone" style="${s}"><img src="${img(n)}" alt=""></div>`;
const foot = (n) => `<div class="foot"><span>Custom e-commerce development · Case study</span><span>${n} / 6</span></div>`;

const pages = [
  // 1. Cover
  `<section class="page dark">
    <div class="eyebrow">Case study · Full-stack e-commerce</div>
    <h1 class="display" style="margin:6mm 0 5mm;font-size:42pt">Your custom online store, built from scratch</h1>
    <p class="muted" style="font-size:13pt;max-width:150mm">A walkthrough of Nokshi Kathar Math, a complete shop I designed and built: storefront, checkout,
      customer accounts and an admin panel.</p>
    <div style="margin-top:7mm"><span class="chip">Angular</span><span class="chip">ASP.NET Core · .NET 10</span><span class="chip">PostgreSQL</span><span class="chip">Docker</span><span class="chip">CI on every push</span></div>
    <div style="position:relative;height:135mm;margin-top:12mm">
      ${browser('desktop-home', 'position:absolute;right:-45mm;top:0;width:170mm')}
      ${phone('mobile-product', 'position:absolute;left:0;top:22mm;width:52mm')}
    </div>
    ${foot(1)}
  </section>`,

  // 2. The project
  `<section class="page">
    <div class="eyebrow">The project</div>
    <h2 class="display">A shop for one-of-a-kind handmade goods</h2>
    <div class="grid2">
      <div>
        <h3>The goal</h3>
        <p class="muted">Sell handmade nakshi kantha and jute goods online. Many pieces exist only once, so the shop must never sell
          the same item twice, even when two shoppers check out at the same moment.</p>
      </div>
      <div>
        <h3>My role</h3>
        <p class="muted">Everything: UX and visual design, Angular frontend, ASP.NET Core API, PostgreSQL database, tests, CI and
          deployment.</p>
      </div>
    </div>
    <div class="stitch"></div>
    <h3>What the shop does</h3>
    <ul class="ticks" style="margin:3mm 0 7mm">
      <li>Catalog with categories, sorting, search and shareable filter links</li>
      <li>One-of-a-kind items are held for 15 minutes when added to a cart</li>
      <li>Cash-on-delivery checkout with prices and stock re-checked on the server</li>
      <li>Customer accounts: the cart built before signing in comes along</li>
      <li>Admin panel for orders, products, stock, photos and artisans</li>
    </ul>
    <figure>${browser('desktop-shop')}<figcaption>Shop page: filters, sorting and search.</figcaption></figure>
    ${foot(2)}
  </section>`,

  // 3. Storefront
  `<section class="page">
    <div class="eyebrow">Storefront</div>
    <h2 class="display">From browsing to checkout</h2>
    <figure>${browser('desktop-product')}<figcaption>Product page: photos, price, stock status, materials and the maker behind it.</figcaption></figure>
    <div class="grid2" style="margin-top:7mm">
      <figure>${browser('desktop-cart-drawer')}<figcaption>Cart drawer: unique pieces are reserved while the shopper decides.</figcaption></figure>
      <figure>${browser('desktop-checkout')}<figcaption>Checkout: validated delivery details, a short friendly form.</figcaption></figure>
    </div>
    ${foot(3)}
  </section>`,

  // 4. Mobile + admin
  `<section class="page">
    <div class="eyebrow">Mobile-first</div>
    <h2 class="display">Built for the phone in your customer's hand</h2>
    <div class="grid3" style="padding:0 8mm">
      ${phone('mobile-home')}${phone('mobile-shop')}${phone('mobile-product')}
    </div>
    <p class="muted" style="margin-top:5mm">Fast, accessible (WCAG AA contrast, keyboard friendly) with light and dark themes.</p>
    <div class="stitch"></div>
    <div class="eyebrow">Admin panel</div>
    <h2 class="display" style="font-size:20pt">Run the shop without touching code</h2>
    <div class="grid2">
      <figure>${browser('desktop-admin-dashboard')}<figcaption>Dashboard: sales, orders waiting and low-stock alerts.</figcaption></figure>
      <figure>${browser('desktop-admin-orders')}<figcaption>Orders: status workflow; cancelling restores stock.</figcaption></figure>
    </div>
    ${foot(4)}
  </section>`,

  // 5. Under the hood
  `<section class="page">
    <div class="eyebrow">Under the hood</div>
    <h2 class="display">Production-grade, not a template</h2>
    <div class="grid2" style="margin-top:4mm">
      <div class="card"><h3>No overselling</h3><p class="muted">Holds are a single conditional database update and stock uses optimistic
        concurrency, so two shoppers can't buy the last item. Both cases have automated tests.</p></div>
      <div class="card"><h3>Secure accounts</h3><p class="muted">Short-lived access tokens kept in memory, refresh tokens in httpOnly cookies,
        hashed and rotated on every use, with reuse detection.</p></div>
      <div class="card"><h3>Never trust the client</h3><p class="muted">Prices, holds and stock are re-checked on the server at checkout. Errors
        come back in one consistent format the UI can explain.</p></div>
      <div class="card"><h3>Tested and automated</h3><p class="muted">Integration tests run against a real PostgreSQL database, and the full
        build, test and lint suite runs on every push.</p></div>
    </div>
    <div class="stitch" style="margin-top:8mm"></div>
    <h3>Architecture</h3>
    <p class="muted" style="margin-bottom:4mm">Clean, layered and easy for another developer to pick up. Free-tier friendly hosting.</p>
    <div class="grid3" style="grid-template-columns:repeat(4,1fr);gap:3mm;text-align:center">
      <div class="card"><b>Angular</b><p class="muted" style="font-size:9pt">Standalone components, signals, lazy routes</p></div>
      <div class="card"><b>ASP.NET Core API</b><p class="muted" style="font-size:9pt">.NET 10, Docker image</p></div>
      <div class="card"><b>PostgreSQL</b><p class="muted" style="font-size:9pt">EF Core, migrations, seed data</p></div>
      <div class="card"><b>Image storage</b><p class="muted" style="font-size:9pt">Signed photo uploads</p></div>
    </div>
    <p class="muted" style="font-size:9pt;margin-top:4mm">Multi-store ready: every query is scoped to its shop, so the same code can serve more than one brand.</p>
    ${foot(5)}
  </section>`,

  // 6. Working together
  `<section class="page dark">
    <div class="eyebrow">Working together</div>
    <h2 class="display" style="font-size:28pt">How we'll build your store</h2>
    <div class="grid2" style="margin:6mm 0 9mm;gap:5mm">
      <div class="card"><div class="num">01</div><h3>Discuss</h3><p class="muted">We agree on pages, products, payment method and timeline before you order.</p></div>
      <div class="card"><div class="num">02</div><h3>Design</h3><p class="muted">A look built around your brand, logo and photos, mobile-first.</p></div>
      <div class="card"><div class="num">03</div><h3>Build</h3><p class="muted">Storefront, checkout and admin panel, with updates at each milestone.</p></div>
      <div class="card"><div class="num">04</div><h3>Launch</h3><p class="muted">Deployed to your hosting, full source code and setup notes handed over.</p></div>
    </div>
    <h3>Packages</h3>
    <div class="grid3" style="margin-top:3mm">
      <div class="card"><b>Starter Store</b><p class="muted" style="font-size:9.5pt">Catalog, product pages, cart, mobile-ready</p></div>
      <div class="card"><b>Complete Store</b><p class="muted" style="font-size:9.5pt">Adds search, checkout, customer accounts and hosting setup</p></div>
      <div class="card"><b>Store + Admin</b><p class="muted" style="font-size:9.5pt">Adds the admin panel for orders, stock and photos, deployed live</p></div>
    </div>
    <div style="text-align:center;margin-top:16mm">
      <h1 class="display" style="font-size:32pt;margin-bottom:6mm">Let's build your store</h1>
      <div class="cta">Message me on Fiverr to get started</div>
    </div>
    ${foot(6)}
  </section>`,
];

const b = await chromium.launch();
const p = await b.newPage();
const file = `${PAGES}/case-study.html`;
writeFileSync(file, `<!doctype html><html lang="en"><head><meta charset="utf-8"><style>${css}</style></head><body>${pages.join('')}</body></html>`);
await p.goto(`file://${file}`, { waitUntil: 'networkidle' });
await p.evaluate(() => document.fonts.ready);
await p.pdf({ path: OUT, format: 'A4', printBackground: true, preferCSSPageSize: true });
await b.close();
console.log(OUT);
