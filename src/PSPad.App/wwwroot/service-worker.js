// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

self.addEventListener('push', event => {
  const data = event.data ? event.data.json() : {};
  event.waitUntil(self.registration.showNotification(data.title ?? 'PSPad', {
    body: data.body ?? '', tag: data.tag, data: { url: data.url ?? '/' }, icon: 'icon-192.png',
  }));
});
self.addEventListener('notificationclick', event => {
  event.notification.close();
  let target;
  try { target = new URL(event.notification.data?.url ?? '/', self.location.origin); } catch { target = null; }
  const url = target && target.origin === self.location.origin ? target.href : self.location.origin + '/';
  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const open = windows.find(client => new URL(client.url).origin === self.location.origin);
    if (open) {
      try { await open.focus(); return await open.navigate(url); } catch { }
    }
    return self.clients.openWindow(url);
  })());
});
