"""Dependency-free checks for the GitHub Pages landing page."""

from html.parser import HTMLParser
from pathlib import Path
import unittest
from urllib.parse import urlparse
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
SITE = ROOT / "docs" / "site"


class PageParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.elements = []
        self.text = []

    def handle_starttag(self, tag, attrs):
        self.elements.append((tag, dict(attrs)))

    def handle_data(self, data):
        self.text.append(data)


class WebsiteTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.html = (SITE / "index.html").read_text(encoding="utf-8")
        cls.page = PageParser()
        cls.page.feed(cls.html)

    def test_accessible_document_structure(self):
        elements = self.page.elements
        self.assertIn(("html", {"lang": "en"}), elements)
        self.assertEqual(sum(tag == "h1" for tag, _ in elements), 1)
        self.assertEqual(sum(tag == "main" for tag, _ in elements), 1)
        self.assertTrue(any(
            tag == "meta" and attrs.get("name") == "viewport"
            for tag, attrs in elements
        ))
        ids = [attrs["id"] for _, attrs in elements if "id" in attrs]
        self.assertEqual(len(ids), len(set(ids)), "Duplicate HTML IDs")
        for tag, attrs in elements:
            if tag == "section":
                self.assertIn(attrs.get("aria-labelledby"), ids)

    def test_links_and_documentation_targets(self):
        ids = {attrs["id"] for _, attrs in self.page.elements if "id" in attrs}
        for tag, attrs in self.page.elements:
            if tag != "a":
                continue
            href = attrs["href"]
            if href.startswith("#"):
                self.assertIn(href[1:], ids)
            elif href == "./":
                self.assertTrue((SITE / "index.html").is_file())
            else:
                self.assertEqual(urlparse(href).scheme, "https")
                prefix = "https://github.com/elbruno/ElBruno.HuggingFace.Downloader/blob/main/"
                if href.startswith(prefix):
                    self.assertTrue((ROOT / href[len(prefix):]).is_file(), href)

    def test_no_external_assets_or_tracking(self):
        for tag, attrs in self.page.elements:
            self.assertNotIn(tag, {"iframe", "object", "embed", "form"})
            if tag in {"script", "img", "audio", "video", "source"}:
                self.assertNotIn("src", attrs)
            if tag == "link":
                self.assertEqual(attrs.get("rel"), "canonical")
        for api in ("fetch(", "XMLHttpRequest", "sendBeacon", "document.cookie"):
            self.assertNotIn(api, self.html)
        self.assertIn('prefers-reduced-motion: reduce', self.html)

    def test_packages_and_frameworks_match_projects(self):
        for name in ("ElBruno.HuggingFace.Downloader", "ElBruno.HuggingFace.Downloader.Cli"):
            project = ET.parse(ROOT / "src" / name / f"{name}.csproj")
            package_id = project.findtext(".//PackageId")
            self.assertIn(f"https://www.nuget.org/packages/{package_id}", self.html)
            self.assertIn(package_id, "".join(self.page.text))
            for framework in project.findtext(".//TargetFrameworks").split(";"):
                major = framework.removeprefix("net").split(".")[0]
                self.assertIn(f".NET {major}", self.html)
        self.assertIn("dotnet add package ElBruno.HuggingFace.Downloader", self.html)
        self.assertIn("dotnet tool install -g ElBruno.HuggingFace.Downloader.Cli", self.html)

    def test_canonical_url_and_honest_privacy_copy(self):
        canonical = [
            attrs["href"] for tag, attrs in self.page.elements
            if tag == "link" and attrs.get("rel") == "canonical"
        ]
        self.assertEqual(canonical, [
            "https://elbruno.github.io/ElBruno.HuggingFace.Downloader/"
        ])
        text = "".join(self.page.text)
        self.assertIn("Downloads contact Hugging Face", text)
        self.assertIn("may require a Hugging Face account", text)
        self.assertNotIn("100% Private", text)


if __name__ == "__main__":
    unittest.main()
