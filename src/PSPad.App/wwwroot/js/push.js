function base64UrlToBytes(text) {
  const padded = (text + '='.repeat((4 - text.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/');
  return Uint8Array.from(atob(padded), c => c.charCodeAt(0));
}
function bytesToBase64Url(buffer) {
  return btoa(String.fromCharCode(...new Uint8Array(buffer))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
function isIos() { return /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1); }
function isStandalone() { return window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true; }

export async function support() {
  const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
  const needsHomeScreen = isIos() && !isStandalone();
  const label = navigator.userAgentData?.platform || navigator.platform || 'Browser';
  if (!supported) { return { supported: false, needsHomeScreen, permission: 'default', endpoint: null, label }; }
  const registration = await navigator.serviceWorker.getRegistration();
  const subscription = registration ? await registration.pushManager.getSubscription() : null;
  return { supported: true, needsHomeScreen, permission: Notification.permission, endpoint: subscription?.endpoint ?? null, label };
}

export async function subscribe(publicKey) {
  if (await Notification.requestPermission() !== 'granted') { return null; }
  if (!await navigator.serviceWorker.getRegistration()) { return null; }
  const registration = await navigator.serviceWorker.ready;
  const subscription = await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: base64UrlToBytes(publicKey) });
  return { endpoint: subscription.endpoint, p256dh: bytesToBase64Url(subscription.getKey('p256dh')), auth: bytesToBase64Url(subscription.getKey('auth')) };
}

export async function unsubscribe() {
  if (!('serviceWorker' in navigator)) { return null; }
  const registration = await navigator.serviceWorker.getRegistration();
  const subscription = registration ? await registration.pushManager.getSubscription() : null;
  if (!subscription) { return null; }
  const endpoint = subscription.endpoint;
  await subscription.unsubscribe();
  return endpoint;
}
