# Project website

The landing page promotes the downloader library and `hfdownload` CLI. It is a
single static HTML file in `docs/site/index.html`, with inline styles and a small
system-theme detection script. There is no build step, package installation,
analytics, third-party asset request, or application backend.

## Publish to GitHub Pages

The intended public URL is:

https://elbruno.github.io/ElBruno.HuggingFace.Downloader/

1. In the repository's **Settings > Pages**, select **GitHub Actions** as the
   build and deployment source.
2. Commit and push the website changes to `main`, or run the **Website** workflow
   from the Actions tab after the workflow has been committed.
3. Wait for the deployment to finish before using the URL in promotional material.

The URL is not live merely because these files exist locally. Publishing requires
the repository setting and a successful workflow run.

The workflow validates changes in pull requests but deploys only from `main`.
Only `docs/site` is uploaded, not the repository's source code, tests, or other
documentation. Existing documentation links point to Markdown files on GitHub.

## Preview locally

From the repository root:

```powershell
python -m http.server 8080 --bind 127.0.0.1 --directory docs\site
```

Open `http://localhost:8080/`. Stop the preview with Ctrl+C.
The page also works directly from its HTML file. It follows your system's theme;
use `?scoutTheme=light` or `?scoutTheme=dark` to preview a specific theme.

## Update the page

Edit `docs/site/index.html` to add features, change the installation commands,
or update the examples. Keep the layout responsive, preserve accessible headings
and navigation, and use the existing theme variables rather than external assets.
Check package support against the production `.csproj` files.

Keep claims grounded in the implementation: the project is free and MIT-licensed,
but downloads contact the configured Hub, and gated/private repositories can
require a Hugging Face account and token. Do not claim that all network activity
is private or that the project is a standalone mobile app.

Run the dependency-free website checks from the repository root:

```powershell
python -m unittest discover -s tests\site -v
```
