import { precacheAndRoute } from 'workbox-precaching';
import { registerRoute, setDefaultHandler } from 'workbox-routing';
import { StaleWhileRevalidate } from 'workbox-strategies';
import { BackgroundSyncPlugin } from 'workbox-background-sync';

// Precache build assets
precacheAndRoute(self.__WB_MANIFEST);

// Cache API requests
registerRoute(
  ({ url }) => url.pathname.startsWith('/api/'),
  new StaleWhileRevalidate({
    cacheName: 'api-cache',
    plugins: [
      new BackgroundSyncPlugin('api-queue', {
        maxRetentionTime: 24 * 60, // retry for 24h
      }),
    ],
  })
);

// Default: cache everything else (images, fonts, etc.)
setDefaultHandler(
  new StaleWhileRevalidate({
    cacheName: 'general-cache',
  })
);

self.addEventListener('install', () => {
  console.log('MKN-MS Service Worker installed');
  self.skipWaiting();
});

self.addEventListener('activate', () => {
  self.clients.claim();
});
