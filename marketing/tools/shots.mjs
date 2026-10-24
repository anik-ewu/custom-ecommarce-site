// Captures desktop (2x) and mobile (3x) screenshots of the running app (web on :4200). Usage: node shots.mjs <outDir>
import { chromium } from 'playwright';
const BASE = 'http://localhost:4200';
const OUT = process.argv[2];
const b = await chromium.launch();
const settle = (p, ms = 900) => p.waitForTimeout(ms);

async function desktop() {
  const ctx = await b.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2, colorScheme: 'light' });
  const p = await ctx.newPage();
  const shot = async (name, opts = {}) => { await settle(p); await p.screenshot({ path: `${OUT}/${name}.png`, ...opts }); console.log(name); };
  await p.goto(BASE); await p.waitForLoadState('networkidle'); await shot('desktop-home');
  await shot('desktop-home-full', { fullPage: true });
  await p.goto(`${BASE}/shop`); await p.waitForLoadState('networkidle'); await shot('desktop-shop');
  await p.goto(`${BASE}/product/lotus-pond-cushion`); await p.waitForLoadState('networkidle'); await shot('desktop-product');
  await p.getByRole('button', { name: /add to cart/i }).first().click(); await settle(p, 1200); await shot('desktop-cart-drawer');
  await p.goto(`${BASE}/login`); await p.waitForLoadState('networkidle');
  await p.getByRole('button', { name: /demo as a shopper/i }).click(); await settle(p, 1500);
  await p.goto(`${BASE}/checkout`); await p.waitForLoadState('networkidle'); await shot('desktop-checkout');
  await ctx.clearCookies();
  const a = await b.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2 });
  const ap = await a.newPage();
  await ap.goto(`${BASE}/login`); await ap.waitForLoadState('networkidle');
  await ap.getByRole('button', { name: /demo as an admin/i }).click(); await ap.waitForTimeout(1500);
  for (const [path, name] of [['/admin', 'desktop-admin-dashboard'], ['/admin/orders', 'desktop-admin-orders'], ['/admin/products', 'desktop-admin-products']]) {
    await ap.goto(BASE + path); await ap.waitForLoadState('networkidle'); await ap.waitForTimeout(900);
    await ap.screenshot({ path: `${OUT}/${name}.png` }); console.log(name);
  }
  await ctx.close(); await a.close();
}

async function mobile() {
  const ctx = await b.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 3, isMobile: true, hasTouch: true });
  const p = await ctx.newPage();
  for (const [path, name] of [['/', 'mobile-home'], ['/shop', 'mobile-shop'], ['/product/tree-of-life-hanging', 'mobile-product']]) {
    await p.goto(BASE + path); await p.waitForLoadState('networkidle'); await settle(p);
    await p.screenshot({ path: `${OUT}/${name}.png` }); console.log(name);
  }
  await ctx.close();
}

await desktop(); await mobile(); await b.close();
