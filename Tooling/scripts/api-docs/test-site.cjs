// Exercise real generated output, served at the GitHub project base path.
const {chromium} = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const assert = require('node:assert/strict');
const site = path.resolve(process.argv[2]);
const manifest = JSON.parse(fs.readFileSync(path.join(site, 'api/versions.json')));
const versions = [...manifest.releases, 'latest'];
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (!url.pathname.startsWith('/Gondwana/')) {res.writeHead(404).end(); return;}
  let name = path.resolve(site, '.' + url.pathname.slice('/Gondwana'.length));
  if (name !== site && !name.startsWith(site + path.sep)) {res.writeHead(404).end(); return;}
  if (fs.existsSync(name) && fs.statSync(name).isDirectory()) name = path.join(name, 'index.html');
  if (!fs.existsSync(name)) {res.writeHead(404).end(); return;}
  res.setHeader('Content-Type', ({'.js':'text/javascript', '.css':'text/css', '.html':'text/html', '.json':'application/json'})[path.extname(name)] || 'application/octet-stream');
  res.end(req.method === 'HEAD' ? undefined : fs.readFileSync(name));
});
(async () => {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}/Gondwana/`;
  const browser = await chromium.launch({headless:true, executablePath:process.env.CHROMIUM_EXECUTABLE});
  try {
    const page = await browser.newPage();
    await page.goto(base);
    await page.waitForURL(base + 'api/v2.5.2/');
    const nojs = await browser.newContext({javaScriptEnabled:false});
    const fallback = await nojs.newPage();
    await fallback.goto(base);
    await fallback.waitForURL(base + 'api/v2.5.2/');
    await nojs.close();
    const pages = new Map();
    for (const version of versions) {
      const folder = path.join(site, 'api', version);
      const files = fs.readdirSync(folder).filter(n => /^class(?:_|[A-Z]).*\.html$/.test(n) && !n.endsWith('-members.html'));
      const file = files.find(n => /(?:_scene|_1Scene)\.html$/.test(n)) || files[0];
      const html = fs.readFileSync(path.join(folder, file), 'utf8');
      const anchor = (html.match(/id="(a[0-9a-f]{32})"/) || [,''])[1];
      assert.ok(anchor, `${version} has a representative member anchor`);
      pages.set(version, {file, anchor, files});
      for (const suffix of ['', file + '#' + anchor]) {
        await page.goto(base + 'api/' + version + '/' + suffix);
        await page.locator('select').waitFor();
        assert.equal(await page.locator('select').inputValue(), version);
        assert.deepEqual(await page.locator('select option').evaluateAll(ns=>ns.map(n=>n.value)), versions);
        assert.match(await page.locator('select option').first().innerText(), /latest stable/);
        if (version === 'latest') {
          assert.match(await page.locator('#api-versions').innerText(), /unreleased/);
          assert.equal(await page.getByRole('link', {name:'Switch to latest stable: 2.5.2'}).count(), 1);
        }
      }
    }
    // Switch in both directions through the requested oldest/middle/newest/dev.
    const route = ['v2.1.0', 'v2.3.0', 'v2.5.2', 'latest', 'v2.1.0'];
    for (let i=0; i<route.length-1; ++i) {
      const from=route[i], to=route[i+1];
      const common = pages.get(from).files.find(n=>pages.get(to).files.includes(n) && /(?:_scene|_1Scene)\.html$/.test(n));
      assert.ok(common, `${from} and ${to} have a common class page`);
      const anchor = (fs.readFileSync(path.join(site,'api',from,common),'utf8').match(/id="(a[0-9a-f]{32})"/) || [,''])[1];
      assert.ok(anchor);
      await page.goto(base + `api/${from}/${common}#${anchor}`);
      await page.selectOption('select', to);
      await page.waitForURL(base + `api/${to}/${common}#${anchor}`);
      const missing = pages.get(from).files.find(n=>!pages.get(to).files.includes(n));
      // Where the target is a superset, simulate an unavailable class through a
      // failed HEAD request while keeping the real source HTML and selector.
      const file = missing || common;
      if (!missing) await page.route('**/api/' + to + '/' + file, r => r.request().method()==='HEAD' ? r.fulfill({status:404,body:''}) : r.continue());
      await page.goto(base + `api/${from}/${file}#${anchor}`);
      await page.selectOption('select', to);
      await page.waitForURL(base + `api/${to}/`);
      await page.unrouteAll();
    }
    console.log(`Real-site browser checks passed for ${versions.length} versions, redirects with/without JS, member anchors, and missing-page fallback.`);
  } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>server.close());
