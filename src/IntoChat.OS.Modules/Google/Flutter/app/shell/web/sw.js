'use strict';
// The shell's service worker: makes the web shell installable and lets it start offline. It caches the shell's own
// static files only. The OS is never cached: kernel calls go to a configured endpoint (often another origin), and
// when nginx proxies the kernel on the same origin its paths are not in the list below. config.json is never cached,
// so a changed endpoint takes effect on the next start.
const CACHE = 'intochat-shell-v1';
const SHELL_FILES = new Set([
  '', 'index.html', 'flutter.js', 'flutter_bootstrap.js', 'main.dart.js', 'main.dart.mjs', 'main.dart.wasm',
  'manifest.json', 'favicon.png', 'version.json',
]);
const SHELL_FOLDERS = ['assets/', 'canvaskit/', 'icons/'];

function shellPath(url) {
  const scope = new URL(self.registration.scope);
  if (url.origin !== scope.origin || !url.pathname.startsWith(scope.pathname)) return null;
  return url.pathname.slice(scope.pathname.length);
}

function isShellFile(path) {
  return path !== null && (SHELL_FILES.has(path) || SHELL_FOLDERS.some((folder) => path.startsWith(folder)));
}

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll(['./', 'flutter_bootstrap.js', 'manifest.json'])));
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    for (const name of await caches.keys()) {
      if (name !== CACHE) await caches.delete(name);
    }
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.method !== 'GET') return;
  const url = new URL(request.url);
  const path = shellPath(url);

  // Navigations: network first so a new build shows up, the cached page when offline.
  if (request.mode === 'navigate' && path !== null && (path === '' || path === 'index.html')) {
    event.respondWith((async () => {
      const cache = await caches.open(CACHE);
      try {
        const response = await fetch(request);
        if (response.ok) await cache.put('./', response.clone());
        return response;
      } catch (error) {
        return (await cache.match('./')) || Response.error();
      }
    })());
    return;
  }

  if (!isShellFile(path)) return;

  // Static shell files: serve the cached copy, refresh it in the background.
  event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    const cached = await cache.match(request);
    const refresh = fetch(request).then((response) => {
      if (response.ok) cache.put(request, response.clone());
      return response;
    });
    if (cached) {
      event.waitUntil(refresh.catch(() => undefined));
      return cached;
    }
    return refresh;
  })());
});
