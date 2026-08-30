# MessageFlow site

The sources of the site published to GitHub Pages at
<https://charles2ke.github.io/Message-Flow/>: an overview of the four language ports and a Swagger
UI playground for the MessageFlow API.

## How the playground works

`public/openapi.yaml` describes a small API on top of the library — listing the supported languages,
describing the support ticket triage chain, executing it, and composing an ad-hoc chain from a JSON
description. The API has no server: `public/app.js` wraps `fetch`, and every request aimed at the
playground server is routed through a real chain built with the **JavaScript port** of the library,
which is compiled from `node/` and copied into the site as `assets/messageflow`. The router that
dispatches the operations is itself a `Chain`, with the 404 response as its fallback.

Because the same API shape can be implemented by every port, the description doubles as a
specification that the C#, Java, Python and Node implementations can be checked against.

## Build and preview locally

```bash
cd node && npm ci && npm run build && cd ..
cd docs && npm ci && npm run build
python -m http.server --directory _site 8080
```

Then open <http://localhost:8080/>.

## Layout

| Path | Description |
| --- | --- |
| `public/index.html` | The landing page and the Swagger UI container. |
| `public/openapi.yaml` | The OpenAPI 3 description of the playground API. |
| `public/app.js` | The in-browser implementation of the API, built on the JavaScript port. |
| `public/styles.css` | The styles of the landing page. |
| `build.mjs` | Copies `public`, Swagger UI and the compiled JavaScript port into `_site`. |

`.github/workflows/pages.yml` runs the same build and deploys `_site` on every push to `main`. The
deployment step runs `actions/configure-pages` with `enablement: true`, so it creates the Pages
site with **GitHub Actions** as the source when the repository does not have Pages enabled yet.
