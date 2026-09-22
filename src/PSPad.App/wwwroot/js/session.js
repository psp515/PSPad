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
