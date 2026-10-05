import fs from "node:fs";
import path from "node:path";

const LOCAL_EXTENSIONS = ["", ".js", ".jsx", ".mjs", ".css", ".json", ".png", ".svg", ".webp", ".jpg", ".jpeg", ".gif"];

function resolveLocalSpecifier(importer, specifier) {
  if (!specifier.startsWith(".")) return null;
  const clean = specifier.split(/[?#]/, 1)[0];
  const base = path.resolve(path.dirname(importer), clean);
  for (const extension of LOCAL_EXTENSIONS) {
    const candidate = `${base}${extension}`;
    if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) return candidate;
  }
  for (const extension of [".js", ".jsx", ".mjs", ".css", ".json"]) {
    const candidate = path.join(base, `index${extension}`);
    if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) return candidate;
  }
  throw new Error(`unresolved local dependency ${specifier} from ${importer}`);
}

function moduleSpecifiers(source) {
  const found = new Set();
  for (const pattern of [
    /\b(?:import|export)\s+[\s\S]*?\bfrom\s*["']([^"']+)["']/g,
    /\bimport\s*["']([^"']+)["']/g,
    /\bimport\s*\(\s*["']([^"']+)["']\s*\)/g,
  ]) {
    let match;
    while ((match = pattern.exec(source))) found.add(match[1]);
  }
  return [...found];
}

function cssSpecifiers(source) {
  const found = [];
  const pattern = /url\(\s*["']?([^"')]+)["']?\s*\)/g;
  let match;
  while ((match = pattern.exec(source))) {
    const specifier = match[1].trim();
    if (!specifier || specifier.startsWith("data:") || specifier.startsWith("http:") || specifier.startsWith("https:") || specifier.startsWith("#") || specifier.startsWith("/")) continue;
    found.push(specifier);
  }
  return found;
}

export function collectAppDependencyClosure(repo) {
  const uiRoot = path.join(repo, "src/LWBridge.UI-0.3.17");
  const entry = path.join(uiRoot, "src/main.jsx");
  const indexHtml = path.join(uiRoot, "index.html");
  const visited = new Set([indexHtml]);
  const queue = [entry];

  while (queue.length) {
    const filePath = queue.shift();
    if (visited.has(filePath)) continue;
    visited.add(filePath);
    const extension = path.extname(filePath).toLowerCase();
    if (![".js", ".jsx", ".mjs", ".css"].includes(extension)) continue;
    const source = fs.readFileSync(filePath, "utf8");
    const specifiers = extension === ".css" ? cssSpecifiers(source) : moduleSpecifiers(source);
    for (const specifier of specifiers) {
      const resolved = resolveLocalSpecifier(filePath, specifier);
      if (resolved && !visited.has(resolved)) queue.push(resolved);
    }
  }

  return [...visited]
    .map((filePath) => path.relative(repo, filePath).replaceAll("\\", "/"))
    .sort();
}
