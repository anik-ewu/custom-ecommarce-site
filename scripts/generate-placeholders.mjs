// Generates rights-clean SVG placeholder images in the brand palette until real photography exists.
// Usage: node scripts/generate-placeholders.mjs   (writes web/public/placeholders/*.svg)
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const out = join(dirname(fileURLToPath(import.meta.url)), '..', 'web', 'public', 'placeholders');
mkdirSync(out, { recursive: true });

const palette = {
  indigo: '#1f2a44',
  cream: '#f3ead9',
  terracotta: '#c2603a',
  red: '#9e2b25',
  jute: '#d8c39b',
  juteDark: '#a88b5c',
  ochre: '#c9973f',
  green: '#4f6b4a',
};

// Small deterministic PRNG so each slug always gets the same picture.
function rng(seed) {
  let h = 2166136261;
  for (const c of seed) h = Math.imul(h ^ c.charCodeAt(0), 16777619);
  return () => {
    h = Math.imul(h ^ (h >>> 15), 2246822507);
    h = Math.imul(h ^ (h >>> 13), 3266489909);
    return ((h ^= h >>> 16) >>> 0) / 4294967296;
  };
}

const pick = (r, list) => list[Math.floor(r() * list.length)];
const stitch = (color, w = 3, dash = '10 8') =>
  `fill="none" stroke="${color}" stroke-width="${w}" stroke-dasharray="${dash}" stroke-linecap="round"`;

function kantha(seed, w = 900, h = 1125) {
  const r = rng(seed);
  const [bg, ink, accent] = pick(r, [
    [palette.cream, palette.red, palette.indigo],
    [palette.indigo, palette.cream, palette.terracotta],
    [palette.red, palette.cream, palette.ochre],
    [palette.cream, palette.indigo, palette.terracotta],
  ]);
  const cx = w / 2;
  const cy = h / 2;
  let body = '';
  // Background running-stitch rows: the texture of layered, quilted saris.
  for (let y = 24; y < h; y += 22) body += `<path d="M0 ${y}H${w}" ${stitch(ink, 1.5, '6 10')} opacity="0.35"/>`;
  // Concentric rings with petals: a lotus medallion, the classic kantha centre.
  const rings = 4 + Math.floor(r() * 3);
  for (let i = 1; i <= rings; i++) body += `<circle cx="${cx}" cy="${cy}" r="${i * 48}" ${stitch(i % 2 ? accent : ink, 3)}/>`;
  const petals = 8 + Math.floor(r() * 6);
  for (let i = 0; i < petals; i++) {
    const a = (i / petals) * Math.PI * 2;
    const x = cx + Math.cos(a) * (rings * 48 + 60);
    const y = cy + Math.sin(a) * (rings * 48 + 60);
    body += `<ellipse cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" rx="22" ry="48" transform="rotate(${((a * 180) / Math.PI + 90).toFixed(1)} ${x.toFixed(1)} ${y.toFixed(1)})" ${stitch(accent, 3, '8 6')}/>`;
  }
  // Border of small triangles, as kantha edges often have.
  for (let x = 30; x < w - 30; x += 40) {
    body += `<path d="M${x} 40l20 24l20-24" ${stitch(ink, 2, '5 5')}/>`;
    body += `<path d="M${x} ${h - 40}l20-24l20 24" ${stitch(ink, 2, '5 5')}/>`;
  }
  return svg(w, h, bg, body);
}

function jute(seed, w = 900, h = 1125) {
  const r = rng(seed);
  const bg = pick(r, [palette.jute, '#cdb486', '#e0cfa9']);
  let body = '';
  // Woven texture: alternating over-under bands.
  for (let y = 0; y < h; y += 28) {
    const shade = y % 56 ? palette.juteDark : '#8f7447';
    body += `<path d="M0 ${y + 14}H${w}" stroke="${shade}" stroke-width="10" opacity="0.28"/>`;
  }
  for (let x = 0; x < w; x += 28) body += `<path d="M${x + 14} 0V${h}" stroke="#8f7447" stroke-width="6" opacity="0.18"/>`;
  // A single coloured stitched band, the only decoration on most jute goods.
  const band = h * (0.35 + r() * 0.3);
  const color = pick(r, [palette.terracotta, palette.indigo, palette.green, palette.red]);
  body += `<rect x="0" y="${band}" width="${w}" height="70" fill="${color}" opacity="0.85"/>`;
  body += `<path d="M0 ${band + 35}H${w}" ${stitch(palette.cream, 3)}/>`;
  return svg(w, h, bg, body);
}

function artisan(name, w = 600, h = 600) {
  const initials = name.split(' ').map((p) => p[0]).join('').slice(0, 2);
  const body = `
    <circle cx="${w / 2}" cy="${h / 2}" r="${w / 2 - 40}" ${stitch(palette.terracotta, 4, '12 9')}/>
    <text x="50%" y="54%" text-anchor="middle" dominant-baseline="middle" font-family="Georgia, serif" font-size="180" fill="${palette.cream}">${initials}</text>`;
  return svg(w, h, palette.indigo, body);
}

function svg(w, h, bg, body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}"><rect width="100%" height="100%" fill="${bg}"/>${body}</svg>\n`;
}

const kanthaSlugs = [
  'lotus-pond-cushion', 'indigo-fish-cushion', 'wheel-of-life-cushion', 'village-fair-cushion',
  'kantha-vine-runner', 'river-bank-runner', 'tree-of-life-hanging', 'monsoon-hanging',
];
const juteSlugs = [
  'everyday-jute-tote', 'braided-jute-shoulder-bag', 'jute-laptop-sleeve', 'mini-jute-pouch-set',
  'round-storage-basket', 'nesting-baskets', 'braided-jute-floor-mat', 'jute-table-mats',
];

for (const slug of kanthaSlugs) {
  writeFileSync(join(out, `${slug}.svg`), kantha(slug));
  writeFileSync(join(out, `${slug}-detail.svg`), kantha(`${slug}-detail`));
}
for (const slug of juteSlugs) {
  writeFileSync(join(out, `${slug}.svg`), jute(slug));
  writeFileSync(join(out, `${slug}-detail.svg`), jute(`${slug}-detail`));
}
for (const [slug, name] of [['rahima', 'Rahima Begum'], ['shefali', 'Shefali Khatun'], ['karim', 'Abdul Karim']]) {
  writeFileSync(join(out, `artisan-${slug}.svg`), artisan(name));
}
writeFileSync(join(out, 'hero.svg'), kantha('kinbo-hero', 1800, 1100));
console.log(`wrote placeholders to ${out}`);
