import { run } from './replica.js';

export function load() {
  return run('session', 'readonly', session => session.get('current'));
}

export function save(value) {
  return run('session', 'readwrite', session => session.put({ key: 'current', value }));
}

export function clear() {
  clearOidcCredentials();
  return run('session', 'readwrite', session => session.clear());
}

function clearOidcCredentials() {
  const stale = [];

  for (let index = 0; index < sessionStorage.length; index += 1) {
    const key = sessionStorage.key(index);
    if (key && key.startsWith('oidc.')) {
      stale.push(key);
    }
  }

  stale.forEach(key => sessionStorage.removeItem(key));
}

export function captureOidcRefreshToken(authority, clientId) {
  const raw = sessionStorage.getItem(`oidc.user:${authority}:${clientId}`);
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw).refresh_token ?? null;
  } catch {
    return null;
  }
}
