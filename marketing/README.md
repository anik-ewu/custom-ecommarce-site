# Marketing assets (Upwork and Fiverr)

Images and a promo video of the Nokshi Kathar Math demo shop, for an Upwork project/portfolio and a Fiverr gig.

## What to upload where

| File | Size | Use it for |
| --- | --- | --- |
| `thumbnails/fiverr-gig-1-main.png` | 1280×769 | Fiverr gig image 1 (the cover in search results) |
| `thumbnails/fiverr-gig-2-features.png` | 1280×769 | Fiverr gig image 2 |
| `thumbnails/fiverr-gig-3-admin.png` | 1280×769 | Fiverr gig image 3 |
| `thumbnails/upwork-project-1-main.png` | 1600×1200 (4:3) | Upwork Project Catalog cover / portfolio thumbnail |
| `thumbnails/upwork-project-2-features.png` | 1600×1200 | Upwork project gallery |
| `thumbnails/upwork-project-3-admin.png` | 1600×1200 | Upwork project gallery |
| `video/ecommerce-promo-1080p.mp4` | 1920×1080, 54 s, 15 MB, H.264 | Fiverr gig video (limit 75 s / 50 MB), Upwork project video |
| `video/ecommerce-promo-fiverr-1080p.mp4` | 1920×1080, 59 s, 15 MB, H.264 | Fiverr gig video: adds a "how we work" scene and ends on "Message me on Fiverr" |
| `pdf/ecommerce-case-study.pdf` | A4, 6 pages, 2.4 MB | Fiverr gig PDF (limit 10 MB), Upwork portfolio file |
| `thumbnails/video-thumbnail.png` | 1280×720 | Cover frame for the video (YouTube, LinkedIn, Upwork) |
| `screenshots/*.png` | desktop 2880×1800, mobile 1170×2532 | Extra gallery images, portfolio case study |

The video has a silent audio track so it uploads everywhere. Fiverr and Upwork let you add music in their editors.

## Suggested gig copy

- **Fiverr title:** I will build a custom ecommerce website with Angular and ASP.NET Core
- **Upwork project title:** Custom e-commerce website: storefront, checkout and admin panel
- **Tags:** ecommerce website, online store, Angular, ASP.NET Core, full stack web development
- **Short pitch:** A complete online store built from scratch for your brand: product catalog with search and
  filters, cart and checkout, order tracking, and an admin panel for orders, stock and products. Mobile-first,
  accessible, secure login, tested on real PostgreSQL with CI on every push.

Only claim what the demo shows: card payments (Stripe) are not built yet, checkout is cash on delivery.

## Regenerating

Needs the app running locally (web on `:4200`, API on `:5277`, see the main README), Node, `ffmpeg`
(`brew install ffmpeg`) and Playwright in a scratch folder (`npm i playwright && npx playwright install chromium`;
run the scripts from that folder or symlink `node_modules`).

```bash
node marketing/tools/shots.mjs marketing/screenshots
```

```bash
TMPDIR_PAGES=/tmp/pages node marketing/tools/thumbs.mjs "$PWD/marketing/screenshots" marketing/thumbnails
```

```bash
node marketing/tools/scenes.mjs "$PWD/marketing/screenshots" /tmp/scenes /tmp/pages && bash marketing/tools/video.sh /tmp/scenes marketing/screenshots marketing/video/ecommerce-promo-1080p.mp4 /tmp/clips
```

Fiverr video cut (same scenes plus the process and Fiverr outro): pass `fiverr` as the fifth argument.

```bash
bash marketing/tools/video.sh /tmp/scenes marketing/screenshots marketing/video/ecommerce-promo-fiverr-1080p.mp4 /tmp/clips fiverr
```

The PDF uses smaller JPEG copies of the screenshots so it stays well under Fiverr's 10 MB limit.

```bash
mkdir -p /tmp/jpg && for f in marketing/screenshots/*.png; do sips -Z 1600 -s format jpeg -s formatOptions 82 "$f" --out "/tmp/jpg/$(basename "${f%.png}").jpg"; done
```

```bash
node marketing/tools/pdf.mjs /tmp/jpg "$PWD/marketing/pdf/ecommerce-case-study.pdf" /tmp/pages
```

Screenshots use the seeded demo data, so the admin numbers change with whatever orders exist locally.
