// Run with Playwright installed: node Tooling/scripts/api-docs/test-selector.cjs
const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
(async () => {
  const browser = await chromium.launch({headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE});
  try {
    const page = await browser.newPage();
    const js = fs.readFileSync(path.join(__dirname, '../../../docs/doxy/api-versions.js'), 'utf8');
    const manifest = {stable:'v2.10.0',development:true,releases:['v2.10.0','v2.9.0']};
    await page.route('https://example.test/**', async route => {
      const url = new URL(route.request().url());
      if (url.pathname === '/Gondwana/api/versions.json')
        return route.fulfill({json:manifest});
      if (url.pathname.endsWith('/api-versions.js'))
        return route.fulfill({contentType:'text/javascript',body:js});
      if (url.pathname.endsWith('/missing.html') && url.pathname.includes('/v2.10.0/'))
        return route.fulfill({status:404,body:'Missing'});
      const version = url.pathname.split('/')[3];
      const label = version === 'latest' ? 'Development (master) - 2.11.0' : `Version ${version.slice(1)}`;
      return route.fulfill({contentType:'text/html',body:`<!doctype html><html><head><script defer src="/Gondwana/api/${version}/api-versions.js"></script></head><body><div id="titlearea"><nav id="api-versions"><strong>${label}</strong><span id="api-version-controls"></span></nav></div></body></html>`});
    });
    for (const suffix of ['','index.html','classScene.html#member','d1/d2/classScene.html#member']) {
      await page.goto('https://example.test/Gondwana/api/v2.9.0/' + suffix);
      await page.locator('select').waitFor();
      assert.equal(await page.locator('select').inputValue(), 'v2.9.0');
      assert.deepEqual(await page.locator('option').evaluateAll(nodes=>nodes.map(n=>n.value)), ['v2.10.0','v2.9.0','latest']);
      await page.selectOption('select', 'v2.10.0');
      const expected = 'https://example.test/Gondwana/api/v2.10.0/' + (suffix === 'index.html' ? '' : suffix);
      await page.waitForURL(expected);
    }
    await page.goto('https://example.test/Gondwana/api/v2.9.0/missing.html#member');
    await page.selectOption('select', 'v2.10.0');
    await page.waitForURL('https://example.test/Gondwana/api/v2.10.0/');
    await page.goto('https://example.test/Gondwana/api/latest/classScene.html#member');
    await page.locator('select').waitFor();
    assert.match(await page.locator('#api-versions').innerText(), /Development \/ unreleased/);
    await page.getByRole('link', {name:'Switch to latest stable: 2.10.0'}).click();
    await page.waitForURL('https://example.test/Gondwana/api/v2.10.0/classScene.html#member');
    console.log('Selector browser tests passed: homes, historical deep links, nested pages, anchors, missing-page fallback, and development-to-stable link.');
  } finally { await browser.close(); }
})().catch(error => {console.error(error);process.exitCode=1;});
