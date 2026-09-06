/**
 * Builds the static site published to GitHub Pages.
 *
 * The site is the content of `public`, plus two vendored pieces:
 *   - `assets/swagger-ui`: the Swagger UI distribution rendering `openapi.yaml`;
 *   - `assets/messageflow`: the compiled JavaScript port of the library, which answers the
 *     playground requests in the browser.
 *
 * The language cards of the landing page are rendered from `public/languages.js`, the same
 * catalogue the playground serves, so the install commands are never duplicated.
 */
import { cp, mkdir, readFile, rm, stat, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const output = resolve(root, "_site");
const nodePackage = resolve(root, "..", "node");
const swaggerUi = resolve(root, "node_modules", "swagger-ui-dist");

const SWAGGER_UI_FILES = [
  "swagger-ui.css",
  "swagger-ui.css.map",
  "swagger-ui-bundle.js",
  "swagger-ui-bundle.js.map",
  "swagger-ui-standalone-preset.js",
  "swagger-ui-standalone-preset.js.map",
];

async function requireDirectory(path, hint) {
  try {
    const stats = await stat(path);
    if (stats.isDirectory()) {
      return;
    }
  } catch {
    // Falls through to the error below.
  }

  throw new Error(`${path} is missing. ${hint}`);
}

await requireDirectory(swaggerUi, "Run `npm ci` in the docs directory first.");
await requireDirectory(
  resolve(nodePackage, "dist"),
  "Run `npm ci && npm run build` in the node directory first.",
);

await rm(output, { recursive: true, force: true });
await mkdir(resolve(output, "assets", "swagger-ui"), { recursive: true });

await cp(resolve(root, "public"), output, { recursive: true });

for (const file of SWAGGER_UI_FILES) {
  await cp(resolve(swaggerUi, file), resolve(output, "assets", "swagger-ui", file));
}

await cp(resolve(nodePackage, "dist"), resolve(output, "assets", "messageflow"), {
  recursive: true,
});

await renderLanguageCards();

console.log(`Site built in ${output}`);

/** Escapes the characters that are significant in HTML text and attribute values. */
function escapeHtml(value) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

/** Replaces the language card placeholder of the landing page with the rendered cards. */
async function renderLanguageCards() {
  const { LANGUAGES } = await import(
    pathToFileURL(resolve(root, "public", "languages.js")).href
  );

  const cards = LANGUAGES.map(
    (entry) => `          <li class="card">
            <h3>${escapeHtml(entry.language)}</h3>
            <p>${escapeHtml(entry.runtime)}, <code>${escapeHtml(entry.style)}</code>-based.</p>
            <pre><code>${escapeHtml(entry.card)}</code></pre>
            <a href="${escapeHtml(entry.documentation)}">Documentation</a>
          </li>`,
  ).join("\n");

  const indexPath = resolve(output, "index.html");
  const html = await readFile(indexPath, "utf8");
  const placeholder = '<ul class="cards" data-language-cards></ul>';

  if (html.split(placeholder).length !== 2) {
    throw new Error(`${indexPath} must contain the language card placeholder exactly once.`);
  }

  await writeFile(
    indexPath,
    html.replace(placeholder, `<ul class="cards">\n${cards}\n        </ul>`),
  );
}
