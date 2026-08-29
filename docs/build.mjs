/**
 * Builds the static site published to GitHub Pages.
 *
 * The site is the content of `public`, plus two vendored pieces:
 *   - `assets/swagger-ui`: the Swagger UI distribution rendering `openapi.yaml`;
 *   - `assets/messageflow`: the compiled JavaScript port of the library, which answers the
 *     playground requests in the browser.
 */
import { cp, mkdir, rm, stat } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

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

console.log(`Site built in ${output}`);
