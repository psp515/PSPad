import { run } from './replica.js';

export function load() {
  return run('session', 'readonly', session => session.get('current'));
}

export function save(value) {
  return run('session', 'readwrite', session => session.put({ key: 'current', value }));
}

export function clear() {
  return run('session', 'readwrite', session => session.clear());
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
