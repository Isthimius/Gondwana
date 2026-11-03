/* Each immutable release keeps this small UI; the shared manifest stays live. */
(() => {
  'use strict';
  const script = document.currentScript;
  // Asset URL works for home pages, deep pages, and CREATE_SUBDIRS output.
  const root = new URL('.', script.src);
  const api = new URL('../', root);
  const current = root.pathname.split('/').filter(Boolean).pop();
  const controls = document.getElementById('api-version-controls');
  if (!controls) return;

  async function destination(version) {
    const home = new URL(version + '/', api);
    const page = location.pathname.startsWith(root.pathname)
      ? location.pathname.slice(root.pathname.length) : '';
    if (!page || page === 'index.html') return home.href;
    const target = new URL(page, home);
    try {
      const response = await fetch(target, { method: 'HEAD' });
      if (response.ok && !response.redirected) {
        target.search = location.search;
        target.hash = location.hash;
        return target.href;
      }
    } catch (_) { /* A missing page or network error falls back to home. */ }
    return home.href;
  }

  fetch(new URL('versions.json', api), { cache: 'no-store' })
    .then(response => {
      if (!response.ok) throw new Error('Manifest unavailable');
      return response.json();
    })
    .then(manifest => {
      const releases = manifest.releases.filter(v => /^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(v));
      controls.replaceChildren();
      const label = document.createElement('label');
      label.textContent = ' API version: ';
      const select = document.createElement('select');
      label.append(select);
      controls.append(label);
      for (const version of [...releases, ...(manifest.development ? ['latest'] : [])]) {
        const option = document.createElement('option');
        option.value = version;
        option.textContent = version === 'latest' ? 'Development (master) — unreleased'
          : `Version ${version.slice(1)}${version === manifest.stable ? ' (latest stable)' : ''}`;
        option.selected = version === current;
        select.append(option);
      }
      select.addEventListener('change', async () => {
        select.disabled = true;
        location.assign(await destination(select.value));
      });
      if (current === 'latest') {
        const notice = document.createElement('span');
        notice.textContent = ' Development / unreleased documentation. ';
        controls.append(notice);
        if (releases.includes(manifest.stable)) {
          const stable = document.createElement('a');
          stable.textContent = `Switch to latest stable: ${manifest.stable.slice(1)}`;
          stable.href = new URL(manifest.stable + '/', api).href;
          stable.addEventListener('click', async event => {
            if (event.button || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            location.assign(await destination(manifest.stable));
          });
          controls.append(stable);
        } else {
          controls.append('No stable API documentation has been published yet.');
        }
      }
      // Doxygen's tree/content offsets depend on the asynchronously sized header.
      window.dispatchEvent(new Event('resize'));
    })
    .catch(() => {
      controls.textContent = ' Version list unavailable. ';
      const stable = document.createElement('a');
      stable.href = api.href;
      stable.textContent = 'Stable API home';
      controls.append(stable);
      window.dispatchEvent(new Event('resize'));
    });
})();
