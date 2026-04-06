import * as fs from "fs";
import * as path from "path";

const REPO_ROOT = path.resolve(__dirname, "../..");
const SOURCE_DIR = path.join(
  REPO_ROOT,
  "src/Courier.Frontend/public/guide/screenshots"
);
const OUTPUT_DIR = path.join(__dirname, "../public/screenshots");

console.log("sync-screenshots: copying screenshots...");

// Clean output dir
if (fs.existsSync(OUTPUT_DIR)) {
  fs.rmSync(OUTPUT_DIR, { recursive: true });
}
fs.mkdirSync(OUTPUT_DIR, { recursive: true });

// Copy all image files
const files = fs
  .readdirSync(SOURCE_DIR)
  .filter((f) => /\.(png|jpg|jpeg|gif|webp)$/i.test(f));

for (const file of files) {
  fs.copyFileSync(path.join(SOURCE_DIR, file), path.join(OUTPUT_DIR, file));
}

console.log(
  `sync-screenshots: copied ${files.length} screenshots to public/screenshots/`
);
