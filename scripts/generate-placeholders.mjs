// Generates rights-clean SVG product illustrations until real photography exists: one "studio shot" per product,
// each on its own soft background colour so the catalogue doesn't read as one repeated pattern.
// Usage: node scripts/generate-placeholders.mjs   (writes web/public/placeholders/*.svg)
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const out = join(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "web",
  "public",
  "placeholders",
);
mkdirSync(out, { recursive: true });

const W = 900;
const H = 1125;

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
const stitch = (color, w = 3, dash = "10 8") =>
  `fill="none" stroke="${color}" stroke-width="${w}" stroke-dasharray="${dash}" stroke-linecap="round"`;

// Blends two #rrggbb colours; t = 0 gives a, t = 1 gives b.
function mix(a, b, t) {
  const pa = [1, 3, 5].map((i) => parseInt(a.slice(i, i + 2), 16));
  const pb = [1, 3, 5].map((i) => parseInt(b.slice(i, i + 2), 16));
  return `#${pa
    .map((v, i) =>
      Math.round(v + (pb[i] - v) * t)
        .toString(16)
        .padStart(2, "0"),
    )
    .join("")}`;
}
const lighten = (c, t) => mix(c, "#ffffff", t);
const darken = (c, t) => mix(c, "#000000", t);

// Shared defs: soft studio backdrop, blur for contact shadows, light/shade for volume, and two textures
// (kantha stitch rows, jute weave) drawn as overlays so they work on any fill colour.
function defs(bg, ink) {
  return `<defs>
<linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${lighten(bg, 0.35)}"/><stop offset="1" stop-color="${bg}"/></linearGradient>
<filter id="soft" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="18"/></filter>
<radialGradient id="vol" cx=".38" cy=".32" r=".85"><stop offset="0" stop-color="#fff" stop-opacity=".22"/><stop offset=".55" stop-color="#fff" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity=".18"/></radialGradient>
<pattern id="rows" width="16" height="14" patternUnits="userSpaceOnUse"><path d="M0 7H16" stroke="${ink}" stroke-opacity=".2" stroke-width="1.5" stroke-dasharray="5 3"/></pattern>
<pattern id="weave" width="10" height="10" patternUnits="userSpaceOnUse"><path d="M0 2.5H10M0 7.5H10" stroke="#000" stroke-opacity=".13" stroke-width="2.4"/><path d="M2.5 0V10M7.5 0V10" stroke="#fff" stroke-opacity=".12" stroke-width="2"/></pattern>
</defs>`;
}

// A filled shape with a texture overlay and soft volume shading, all from the same path.
const surface = (d, fill, texture) =>
  `<path d="${d}" fill="${fill}"/><path d="${d}" fill="url(#${texture})"/><path d="${d}" fill="url(#vol)"/>`;
const shadow = (cx, cy, rx, ry, bg, strength = 0.28) =>
  `<ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="${darken(bg, strength)}" opacity=".55" filter="url(#soft)"/>`;
const rect = (x, y, w, h, r = 0) =>
  `M${x + r} ${y}H${x + w - r}Q${x + w} ${y} ${x + w} ${y + r}V${y + h - r}Q${x + w} ${y + h} ${x + w - r} ${y + h}H${x + r}Q${x} ${y + h} ${x} ${y + h - r}V${y + r}Q${x} ${y} ${x + r} ${y}Z`;
const wave = (x, y, len, amp, step, vertical = false) => {
  let d = vertical
    ? `M${x} ${y}q${amp} ${step / 2} 0 ${step}`
    : `M${x} ${y}q${step / 2} ${-amp} ${step} 0`;
  for (let i = step; i < len; i += step)
    d += vertical ? `t0 ${step}` : `t${step} 0`;
  return d;
};

// --- Kantha motifs, drawn around (0, 0) ---------------------------------------------------
const motifs = {
  lotus(ink, accent) {
    let s = "";
    for (const a of [-64, -32, 0, 32, 64]) {
      const outer = Math.abs(a) === 64;
      s += `<g transform="translate(0 70) rotate(${a})"><path d="M0 0C-34 -55 -26 -120 0 -160C26 -120 34 -55 0 0Z" ${outer ? stitch(ink, 4, "9 6") : `fill="${ink}" opacity="${a ? 0.82 : 1}"`}/>${outer ? "" : `<path d="M0 -14V-128" ${stitch("#ffffff", 2, "6 6")} opacity=".7"/>`}</g>`;
    }
    s += `<ellipse cx="-145" cy="78" rx="58" ry="17" fill="${accent}" transform="rotate(-12 -145 78)"/>`;
    s += `<ellipse cx="145" cy="78" rx="58" ry="17" fill="${accent}" transform="rotate(12 145 78)"/>`;
    for (const y of [112, 138, 164])
      s += `<path d="${wave(-168, y, 336, 10, 42)}" ${stitch(accent, 3, "8 6")}/>`;
    return s;
  },
  fish(ink, accent) {
    const fish = `<path d="M-120 0C-66 -60 52 -60 92 0C52 60 -66 60 -120 0Z" fill="${ink}"/><path d="M90 0L140 -40L132 0L140 40Z" fill="${ink}"/>
      <path d="M-40 -26q16 26 0 52M-8 -32q16 32 0 64M24 -30q16 30 0 60" ${stitch(accent, 3, "6 5")}/><circle cx="-84" cy="-10" r="7" fill="${accent}"/>`;
    return `<circle r="205" ${stitch(accent, 3, "10 8")}/><circle r="222" ${stitch(ink, 2, "4 8")} opacity=".6"/>
      <g transform="translate(0 -78)">${fish}</g><g transform="translate(0 78) rotate(180)">${fish}</g>`;
  },
  wheel(ink, accent) {
    let s = `<circle r="200" ${stitch(ink, 3, "10 7")}/><circle r="128" fill="${accent}"/><circle r="96" fill="none" stroke="${ink}" stroke-width="4"/>`;
    for (let i = 0; i < 16; i++) {
      const a = (i / 16) * Math.PI * 2;
      const [c, sn] = [Math.cos(a), Math.sin(a)];
      s += `<path d="M${(c * 30).toFixed(1)} ${(sn * 30).toFixed(1)}L${(c * 92).toFixed(1)} ${(sn * 92).toFixed(1)}" stroke="${ink}" stroke-width="5" stroke-linecap="round"/>`;
      s += `<ellipse cx="${(c * 164).toFixed(1)}" cy="${(sn * 164).toFixed(1)}" rx="10" ry="24" fill="${ink}" transform="rotate(${((a * 180) / Math.PI + 90).toFixed(1)} ${(c * 164).toFixed(1)} ${(sn * 164).toFixed(1)})"/>`;
    }
    return `${s}<circle r="22" fill="${ink}"/>`;
  },
  fair(ink, accent) {
    let s = "";
    for (const [i, y] of [-130, 0, 130].entries()) {
      for (const [j, x] of [-130, 0, 130].entries()) {
        s +=
          (i + j) % 2
            ? `<g transform="translate(${x} ${y})"><circle r="26" fill="${accent}"/>${Array.from(
                { length: 12 },
                (_, k) => {
                  const a = (k / 12) * Math.PI * 2;
                  return `<path d="M${(Math.cos(a) * 36).toFixed(1)} ${(Math.sin(a) * 36).toFixed(1)}L${(Math.cos(a) * 50).toFixed(1)} ${(Math.sin(a) * 50).toFixed(1)}" stroke="${accent}" stroke-width="5" stroke-linecap="round"/>`;
                },
              ).join("")}</g>`
            : `<g transform="translate(${x} ${y - 10})"><path d="M0 -48L36 0L0 48L-36 0Z" fill="${ink}"/><path d="M0 -40V40M-28 0H28" ${stitch("#ffffff", 2, "5 5")} opacity=".75"/><path d="M0 48q10 12 0 24" ${stitch(ink, 3, "5 4")}/></g>`;
      }
    }
    return s;
  },
  vine(ink, accent) {
    const xAt = (y) => 40 * Math.sin(y / 70);
    let d = `M${xAt(-430).toFixed(1)} -430`;
    for (let y = -420; y <= 430; y += 10) d += `L${xAt(y).toFixed(1)} ${y}`;
    let s = `<path d="${d}" ${stitch(ink, 4, "10 6")}/>`;
    for (let y = -400, side = 1; y <= 400; y += 70, side = -side) {
      const x = xAt(y);
      s += `<ellipse cx="${(x + side * 30).toFixed(1)}" cy="${y}" rx="13" ry="34" fill="${ink}" transform="rotate(${side * 50} ${(x + side * 30).toFixed(1)} ${y})"/>`;
      s += `<circle cx="${(x - side * 26).toFixed(1)}" cy="${y + 30}" r="9" fill="${accent}"/>`;
    }
    return s;
  },
  river(ink, accent) {
    let s = `<rect x="-80" y="-470" width="160" height="940" fill="${ink}" opacity=".16"/>`;
    for (const x of [-50, 0, 50])
      s += `<path d="${wave(x, -460, 920, 10, 40, true)}" ${stitch(ink, 3, "8 6")}/>`;
    for (const y of [-300, 0, 300]) {
      s += `<g transform="translate(0 ${y})"><path d="M-46 0Q0 34 46 0Z" fill="${accent}"/><path d="M2 -6V-70L38 -12Z" fill="${ink}"/></g>`;
    }
    for (const y of [-420, -160, 150, 410]) {
      for (const x of [-122, 112])
        s += `<path d="M${x} ${y}v-40M${x + 10} ${y}v-56M${x + 20} ${y}v-36" stroke="${ink}" stroke-width="3" stroke-linecap="round"/>`;
    }
    return s;
  },
  tree(ink, accent, r) {
    const bark = "#7a5a3a";
    let s = `<path d="M-130 -110L0 0M130 -110L0 0M-160 -20L0 50M160 -20L0 50M0 -150V0" stroke="${bark}" stroke-width="10" stroke-linecap="round"/>`;
    for (let i = 0; i < 46; i++) {
      const a = Math.PI + r() * Math.PI;
      const d = Math.sqrt(r());
      const x = Math.cos(a) * 205 * d;
      const y = -110 + Math.sin(a) * 140 * d + 30;
      s += `<ellipse cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" rx="12" ry="24" fill="${ink}" opacity="${(0.7 + r() * 0.3).toFixed(2)}" transform="rotate(${Math.round(r() * 180)} ${x.toFixed(1)} ${y.toFixed(1)})"/>`;
    }
    s += `<path d="M-22 255C-10 150 -28 60 0 -10C28 60 10 150 22 255Z" fill="${bark}"/>`;
    for (const [x, y] of [
      [-150, -40],
      [120, -150],
      [60, -40],
      [-90, -190],
    ]) {
      s += `<g transform="translate(${x} ${y})"><path d="M-16 0q16 -16 32 0q-16 8 -32 0Z" fill="${accent}"/><circle cx="14" cy="-6" r="6" fill="${accent}"/></g>`;
    }
    return `${s}<path d="M-230 262H230" ${stitch(ink, 3, "10 7")}/>`;
  },
  monsoon(ink, accent, r) {
    let s = "";
    for (let i = 0; i < 38; i++) {
      const x = -230 + r() * 440;
      const y = -290 + r() * 480;
      s += `<path d="M${x.toFixed(1)} ${y.toFixed(1)}l-14 42" ${stitch(ink, 3, "7 6")} opacity=".75"/>`;
    }
    for (const [x, y] of [
      [-120, -150],
      [110, -60],
      [-70, 110],
      [140, 170],
      [10, -250],
    ]) {
      s += `<g transform="translate(${x} ${y})">${Array.from(
        { length: 18 },
        (_, k) => {
          const a = (k / 18) * Math.PI * 2;
          return `<circle cx="${(Math.cos(a) * 38).toFixed(1)}" cy="${(Math.sin(a) * 38).toFixed(1)}" r="4.5" fill="${accent}"/>`;
        },
      ).join(
        "",
      )}<circle r="28" fill="${accent}"/><circle r="11" fill="${darken(accent, 0.25)}"/></g>`;
    }
    for (const y of [240, 270])
      s += `<path d="${wave(-230, y, 460, 9, 46)}" ${stitch(ink, 3, "9 6")}/>`;
    return s;
  },
};

// --- Kantha products ------------------------------------------------------------------------
function cushion(p) {
  const d =
    "M190 300Q450 324 710 300Q686 560 710 820Q450 796 190 820Q214 560 190 300Z";
  const inner =
    "M226 336Q450 356 674 336Q654 560 674 784Q450 764 226 784Q246 560 226 336Z";
  return `${shadow(450, 850, 280, 26, p.bg)}<g transform="rotate(-3 450 560)">${surface(d, p.fabric, "rows")}
    <path d="${inner}" ${stitch(p.accent, 3, "9 7")}/><g transform="translate(450 560)">${motifs[p.motif](p.ink, p.accent)}</g>
    <path d="${d}" fill="url(#vol)"/></g>`;
}

function runner(p) {
  const d = rect(-150, -490, 300, 980, 4);
  let fringe = "";
  for (let x = -140; x <= 140; x += 10) fringe += `M${x} -490v-26M${x} 490v26`;
  return `<g transform="translate(450 562) rotate(-12)">
    <rect x="-140" y="-470" width="300" height="980" fill="${darken(p.bg, 0.3)}" opacity=".45" filter="url(#soft)"/>
    <path d="${fringe}" stroke="${darken(p.fabric, 0.12)}" stroke-width="3" stroke-linecap="round"/>
    ${surface(d, p.fabric, "rows")}<path d="${rect(-132, -472, 264, 944)}" ${stitch(p.accent, 3, "9 7")}/>
    ${motifs[p.motif](p.ink, p.accent)}</g>`;
}

function hanging(p, r) {
  const d = "M200 270H700V890Q450 910 200 890Z";
  return `<rect x="216" y="292" width="500" height="630" fill="${darken(p.bg, 0.3)}" opacity=".5" filter="url(#soft)"/>
    <circle cx="450" cy="150" r="7" fill="${darken(p.bg, 0.5)}"/><path d="M450 150L190 262M450 150L710 262" stroke="${darken(p.bg, 0.45)}" stroke-width="2.5"/>
    ${surface(d, p.fabric, "rows")}<path d="M226 296H674V868Q450 886 226 868Z" ${stitch(p.accent, 3, "9 7")}/>
    <g transform="translate(450 585)">${motifs[p.motif](p.ink, p.accent, r)}</g>
    <rect x="168" y="256" width="564" height="18" rx="9" fill="#8a6a4a"/><rect x="168" y="256" width="564" height="7" rx="3.5" fill="#fff" opacity=".18"/>`;
}

// --- Jute products --------------------------------------------------------------------------
function tote(p) {
  const body = "M262 430H638L668 870Q668 900 638 900H262Q232 900 232 870Z";
  return `${shadow(450, 910, 250, 24, p.bg)}
    <path d="M330 432C330 220 570 220 570 432" fill="none" stroke="${darken(p.handle, 0.18)}" stroke-width="22" stroke-linecap="round" transform="translate(12 -6)"/>
    ${surface(body, p.fiber, "weave")}
    <path d="M300 432C300 210 600 210 600 432" fill="none" stroke="${p.handle}" stroke-width="24" stroke-linecap="round"/>
    <path d="M246 650H654L657 712H243Z" fill="${p.accent}"/><path d="M250 681H650" ${stitch(p.handle, 3, "10 7")}/>
    <path d="M262 452H638" ${stitch(darken(p.fiber, 0.35), 2.5, "8 6")}/>`;
}

function shoulderBag(p) {
  let braid = "";
  for (let y = 540; y < 870; y += 22) {
    braid += `<path d="${Array.from({ length: 22 }, (_, i) => `${i ? "L" : "M"}${240 + i * 20} ${y + (i % 2 ? 10 : 0)}`).join("")}" fill="none" stroke="${darken(p.fiber, 0.28)}" stroke-width="3" opacity=".6"/>`;
  }
  return `${shadow(450, 880, 240, 24, p.bg)}
    <path d="M262 560C262 120 638 120 638 560" fill="none" stroke="${p.accent}" stroke-width="16" stroke-linecap="round"/>
    ${surface(rect(240, 520, 420, 350, 70), p.fiber, "weave")}${braid}
    ${surface("M240 590Q240 520 310 520H590Q660 520 660 590V640Q450 760 240 640Z", darken(p.fiber, 0.08), "weave")}
    <path d="M256 640Q450 744 644 640" ${stitch(lighten(p.fiber, 0.5), 3, "9 7")}/>
    <path d="M450 690v40" stroke="${p.accent}" stroke-width="6"/><rect x="418" y="722" width="64" height="22" rx="11" fill="#a0764e"/>`;
}

function sleeve(p) {
  let rows = "";
  for (let y = 360; y < 480; y += 18)
    rows += `<path d="M222 ${y}H678" ${stitch(lighten(p.accent, 0.6), 2, "6 6")}/>`;
  return `${shadow(450, 890, 270, 26, p.bg)}<g transform="rotate(-6 450 600)">
    ${surface(rect(200, 330, 500, 550, 22), p.fiber, "weave")}
    <path d="M200 352Q200 330 222 330H678Q700 330 700 352V480Q450 590 200 480Z" fill="${p.accent}"/>${rows}
    <path d="M214 482Q450 586 686 482" ${stitch(lighten(p.accent, 0.6), 3, "9 6")}/>
    <circle cx="450" cy="532" r="15" fill="${lighten(p.fiber, 0.4)}"/><circle cx="450" cy="532" r="6" fill="${darken(p.fiber, 0.3)}"/></g>`;
}

function pouches(p) {
  const pouch = (x, y, w, h, color) =>
    `${surface(rect(x, y, w, h, 26), color, "weave")}<path d="M${x + 18} ${y + 30}H${x + w - 18}" stroke="#8c8c8c" stroke-width="7" stroke-dasharray="3 3"/>
    <rect x="${x + w - 52}" y="${y + 22}" width="14" height="44" rx="5" fill="#b9b2a6"/>`;
  return `${shadow(450, 930, 300, 26, p.bg)}${pouch(170, 400, 430, 420, p.colors[0])}${pouch(430, 560, 320, 330, p.colors[1])}${pouch(250, 700, 240, 240, p.colors[2])}`;
}

function basket(cx, top, rx, h, fiber, band) {
  const ry = rx * 0.26;
  const brx = rx * 0.88;
  const bot = top + h;
  let coils = "";
  for (let y = top + 22; y < bot; y += 20) {
    const t = (y - top) / h;
    const r = rx + (brx - rx) * t;
    coils += `<path d="M${cx - r} ${y}A${r} ${ry} 0 0 0 ${cx + r} ${y}" fill="none" stroke="${darken(fiber, 0.25)}" stroke-width="3" opacity=".55"/>`;
  }
  const bandY = top + h * 0.38;
  const body = `M${cx - rx} ${top}L${cx - brx} ${bot}A${brx} ${ry} 0 0 0 ${cx + brx} ${bot}L${cx + rx} ${top}Z`;
  return `${shadow(cx, bot + ry * 0.6, brx * 1.05, ry * 0.7, fiber, 0.5)}${surface(body, fiber, "weave")}
    <path d="M${cx - rx * 0.97} ${bandY}A${rx} ${ry} 0 0 0 ${cx + rx * 0.97} ${bandY}V${bandY + h * 0.14}A${rx} ${ry} 0 0 1 ${cx - rx * 0.97} ${bandY + h * 0.14}Z" fill="${band}"/>
    ${coils}<ellipse cx="${cx}" cy="${top}" rx="${rx}" ry="${ry}" fill="${darken(fiber, 0.3)}"/>
    <ellipse cx="${cx}" cy="${top}" rx="${rx}" ry="${ry}" fill="none" stroke="${lighten(fiber, 0.2)}" stroke-width="12"/>`;
}

function roundBasket(p) {
  return `${basket(450, 470, 230, 400, p.fiber, p.accent)}
    <ellipse cx="236" cy="530" rx="10" ry="26" fill="${darken(p.fiber, 0.45)}"/><ellipse cx="664" cy="530" rx="10" ry="26" fill="${darken(p.fiber, 0.45)}"/>`;
}

function nesting(p) {
  return `${basket(390, 420, 210, 340, p.fiber, p.bands[0])}${basket(590, 640, 150, 250, darken(p.fiber, 0.05), p.bands[1])}${basket(330, 780, 110, 190, lighten(p.fiber, 0.06), p.bands[2])}`;
}

function floorMat(p) {
  let rings = "";
  for (let rx = 360, i = 0; rx > 10; rx -= 24, i++) {
    const color = i === 3 ? p.accent : i % 2 ? darken(p.fiber, 0.12) : p.fiber;
    rings += `<ellipse cx="450" cy="640" rx="${rx}" ry="${(rx * 0.42).toFixed(1)}" fill="none" stroke="${color}" stroke-width="20" stroke-dasharray="11 4"/>`;
  }
  return `${shadow(450, 670, 380, 150, p.bg)}<ellipse cx="450" cy="640" rx="372" ry="157" fill="${darken(p.fiber, 0.1)}"/>${rings}
    <ellipse cx="450" cy="640" rx="372" ry="157" fill="url(#vol)"/>`;
}

function tableMats(p) {
  let mats = "";
  for (const [i, a] of [-14, -5, 4].entries()) {
    mats += `<g transform="rotate(${a} 450 590) translate(${(i - 1) * 16} ${(i - 1) * 14})">${surface(rect(190, 420, 520, 340, 10), p.fiber, "weave")}
      <path d="${rect(206, 436, 488, 308, 4)}" ${stitch(p.accent, 4, "10 6")}/></g>`;
  }
  return `${shadow(450, 780, 320, 40, p.bg)}${mats}
    <circle cx="470" cy="600" r="128" fill="${darken(p.bg, 0.15)}" opacity=".5" filter="url(#soft)"/>
    <circle cx="462" cy="590" r="130" fill="#fbf8f2"/><circle cx="462" cy="590" r="92" fill="none" stroke="#e6dfd2" stroke-width="3"/>
    <path d="M430 572q30 -40 64 -10M452 560l-8 -26M470 556l6 -24" stroke="${p.accent}" stroke-width="5" stroke-linecap="round" fill="none"/>`;
}

// Each product: its drawing, a soft background of its own, how much to enlarge it in the main shot
// (long runners already fill the frame) and the point the detail shot zooms into.
const products = {
  "lotus-pond-cushion": {
    draw: cushion,
    motif: "lotus",
    bg: "#f1d9d3",
    fabric: "#fbf3e8",
    ink: "#c24d6b",
    accent: "#3f7a6a",
    focus: [450, 560],
  },
  "indigo-fish-cushion": {
    draw: cushion,
    motif: "fish",
    bg: "#d9e3ee",
    fabric: "#27375e",
    ink: "#f4ead8",
    accent: "#e0a04a",
    focus: [450, 500],
  },
  "wheel-of-life-cushion": {
    draw: cushion,
    motif: "wheel",
    bg: "#f3e4c4",
    fabric: "#a8322b",
    ink: "#f6e7c8",
    accent: "#e2a33b",
    focus: [450, 560],
  },
  "village-fair-cushion": {
    draw: cushion,
    motif: "fair",
    bg: "#d6e6da",
    fabric: "#f6efe2",
    ink: "#d0532f",
    accent: "#2f6f8f",
    focus: [450, 560],
  },
  "kantha-vine-runner": {
    draw: runner,
    scale: 1,
    motif: "vine",
    bg: "#e6ddd0",
    fabric: "#fbf5ea",
    ink: "#4f7a4a",
    accent: "#c2603a",
    focus: [450, 480],
  },
  "river-bank-runner": {
    draw: runner,
    scale: 1,
    motif: "river",
    bg: "#d3e4e3",
    fabric: "#f5efe3",
    ink: "#2e5f8a",
    accent: "#b8432f",
    focus: [450, 560],
  },
  "tree-of-life-hanging": {
    draw: hanging,
    scale: 1.06,
    motif: "tree",
    bg: "#e9e3f0",
    fabric: "#f7f0e2",
    ink: "#3d6b4f",
    accent: "#c4473a",
    focus: [450, 500],
  },
  "monsoon-hanging": {
    draw: hanging,
    scale: 1.06,
    motif: "monsoon",
    bg: "#e1e7dc",
    fabric: "#33463f",
    ink: "#cfd9d2",
    accent: "#f0b94a",
    focus: [450, 560],
  },
  "everyday-jute-tote": {
    draw: tote,
    bg: "#f2d8c3",
    fiber: "#d4b483",
    handle: "#efe6d6",
    accent: "#2f4f7a",
    focus: [450, 640],
  },
  "braided-jute-shoulder-bag": {
    draw: shoulderBag,
    bg: "#dce8de",
    fiber: "#c9a46b",
    accent: "#7a4a2c",
    focus: [450, 680],
  },
  "jute-laptop-sleeve": {
    draw: sleeve,
    bg: "#e0e3f0",
    fiber: "#cfb489",
    accent: "#b8432f",
    focus: [450, 470],
  },
  "mini-jute-pouch-set": {
    draw: pouches,
    bg: "#f5e6b8",
    colors: ["#d4b483", "#c46a4a", "#4f7a6a"],
    focus: [520, 660],
  },
  "round-storage-basket": {
    draw: roundBasket,
    bg: "#ebd7cf",
    fiber: "#cba36e",
    accent: "#2f4f4f",
    focus: [450, 640],
  },
  "nesting-baskets": {
    draw: nesting,
    bg: "#d6e2ec",
    fiber: "#cfa96f",
    bands: ["#c2603a", "#3f6e8c", "#e2a33b"],
    focus: [480, 700],
  },
  "braided-jute-floor-mat": {
    draw: floorMat,
    bg: "#e7e1d3",
    fiber: "#c8a56e",
    accent: "#8c3b2e",
    focus: [450, 640],
  },
  "jute-table-mats": {
    draw: tableMats,
    bg: "#d7e5d2",
    fiber: "#d2b689",
    accent: "#a8322b",
    focus: [380, 600],
  },
};

function productSvg(slug, p, detail) {
  const r = rng(slug);
  const ink = p.ink ?? darken(p.fiber ?? p.colors[0], 0.4);
  const art = p.draw(p, r);
  // The detail shot is a macro crop of the same drawing, on a slightly deeper backdrop.
  const bg = detail ? darken(p.bg, 0.06) : p.bg;
  const body = detail
    ? `<g transform="translate(${W / 2} ${H / 2}) scale(2.4) translate(${-p.focus[0]} ${-p.focus[1]})">${art}</g>`
    : `<g transform="translate(450 600) scale(${p.scale ?? 1.12}) translate(-450 -600)">${art}</g>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" width="${W}" height="${H}">${defs(bg, ink)}<rect width="100%" height="100%" fill="url(#bg)"/>${body}</svg>\n`;
}

function artisan(name, bg, ink, w = 600, h = 600) {
  const initials = name
    .split(" ")
    .map((p) => p[0])
    .join("")
    .slice(0, 2);
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}"><rect width="100%" height="100%" fill="${bg}"/>
    <circle cx="${w / 2}" cy="${h / 2}" r="${w / 2 - 60}" ${stitch(ink, 3, "10 9")} opacity=".55"/>
    <text x="50%" y="54%" text-anchor="middle" dominant-baseline="middle" font-family="Georgia, serif" font-size="150" fill="${ink}">${initials}</text></svg>\n`;
}

// The hero keeps the original kantha medallion (and its seed) so the landing cover doesn't change.
function kanthaHero(seed, w = 1800, h = 1100) {
  const r = rng(seed);
  const [bg, ink, accent] = pick(r, [
    ["#f3ead9", "#9e2b25", "#1f2a44"],
    ["#1f2a44", "#f3ead9", "#c2603a"],
    ["#9e2b25", "#f3ead9", "#c9973f"],
    ["#f3ead9", "#1f2a44", "#c2603a"],
  ]);
  const cx = w / 2;
  const cy = h / 2;
  let body = "";
  for (let y = 24; y < h; y += 22)
    body += `<path d="M0 ${y}H${w}" ${stitch(ink, 1.5, "6 10")} opacity="0.35"/>`;
  const rings = 4 + Math.floor(r() * 3);
  for (let i = 1; i <= rings; i++)
    body += `<circle cx="${cx}" cy="${cy}" r="${i * 48}" ${stitch(i % 2 ? accent : ink, 3)}/>`;
  const petals = 8 + Math.floor(r() * 6);
  for (let i = 0; i < petals; i++) {
    const a = (i / petals) * Math.PI * 2;
    const x = cx + Math.cos(a) * (rings * 48 + 60);
    const y = cy + Math.sin(a) * (rings * 48 + 60);
    body += `<ellipse cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" rx="22" ry="48" transform="rotate(${((a * 180) / Math.PI + 90).toFixed(1)} ${x.toFixed(1)} ${y.toFixed(1)})" ${stitch(accent, 3, "8 6")}/>`;
  }
  for (let x = 30; x < w - 30; x += 40) {
    body += `<path d="M${x} 40l20 24l20-24" ${stitch(ink, 2, "5 5")}/>`;
    body += `<path d="M${x} ${h - 40}l20-24l20 24" ${stitch(ink, 2, "5 5")}/>`;
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}"><rect width="100%" height="100%" fill="${bg}"/>${body}</svg>\n`;
}

for (const [slug, p] of Object.entries(products)) {
  writeFileSync(join(out, `${slug}.svg`), productSvg(slug, p, false));
  writeFileSync(join(out, `${slug}-detail.svg`), productSvg(slug, p, true));
}
for (const [slug, name, bg, ink] of [
  ["rahima", "Rahima Begum", "#f1d6cb", "#7a2f22"],
  ["shefali", "Shefali Khatun", "#d4e4d9", "#2f5a45"],
  ["karim", "Abdul Karim", "#dbe1f0", "#2b3a66"],
]) {
  writeFileSync(join(out, `artisan-${slug}.svg`), artisan(name, bg, ink));
}
writeFileSync(join(out, "hero.svg"), kanthaHero("kinbo-hero"));
console.log(`wrote placeholders to ${out}`);
