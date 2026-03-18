// Minimal service worker for PWA installability.
// Blazor WASM handles its own caching, so this just satisfies the PWA install criteria.

self.addEventListener('install', event => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

self.addEventListener('fetch', event => {
    // Let the browser handle all fetches normally — Blazor manages its own cache.
    // This is intentionally a no-op pass-through.
});
