self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const excluded = [/^service-worker\.js$/, /(^|\/)\./];
const assetUrls = new Set(self.assetsManifest.assets.map(asset => new URL(asset.url, self.registration.scope).href));

self.addEventListener('install', event => event.waitUntil(install()));
self.addEventListener('activate', event => event.waitUntil(activate()));
self.addEventListener('fetch', event => {
    const request = event.request;
    if (request.method === 'GET' && (request.mode === 'navigate' || assetUrls.has(request.url))) {
        event.respondWith(respond(request));
    }
});

async function install() {
    const requests = self.assetsManifest.assets
        .filter(asset => !excluded.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    await cache.addAll(requests);
}

async function activate() {
    const keys = await caches.keys();
    await Promise.all(keys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function respond(request) {
    const page = request.mode === 'navigate' && !assetUrls.has(request.url);
    const cached = await (await caches.open(cacheName)).match(page ? 'index.html' : request);
    return cached ?? await fetch(request);
}
