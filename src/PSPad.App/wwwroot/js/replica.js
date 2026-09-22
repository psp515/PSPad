const DB_NAME = 'pspad';
const VERSION = 2;

export function open() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, VERSION);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains('documents')) {
        const documents = db.createObjectStore('documents', { keyPath: 'id' });
        documents.createIndex('type_user', ['type', 'userId']);
      }
      if (!db.objectStoreNames.contains('meta')) {
        db.createObjectStore('meta', { keyPath: 'key' });
      }
      if (!db.objectStoreNames.contains('outbox')) {
        // autoIncrement gives strict append order for free, matching the outbox's ordering guarantee.
        db.createObjectStore('outbox', { keyPath: 'position', autoIncrement: true });
      }
      if (!db.objectStoreNames.contains('session')) {
        db.createObjectStore('session', { keyPath: 'key' });
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export function run(store, mode, work) {
  return open().then(db => new Promise((resolve, reject) => {
    const transaction = db.transaction(store, mode);
    const request = work(transaction.objectStore(store));
    transaction.oncomplete = () => resolve(request ? request.result : undefined);
    transaction.onerror = () => reject(transaction.error);
  }));
}

export function get(id) {
  return run('documents', 'readonly', documents => documents.get(id));
}

export function getAll(type, userId) {
  return run('documents', 'readonly', documents =>
    documents.index('type_user').getAll([type, userId]));
}

export function put(document) {
  return run('documents', 'readwrite', documents => documents.put(document));
}

export function putMany(items) {
  return run('documents', 'readwrite', documents => {
    items.forEach(item => documents.put(item));
    return null;
  });
}

export function getMeta(key) {
  return run('meta', 'readonly', meta => meta.get(key));
}

export function setMeta(key, value) {
  return run('meta', 'readwrite', meta => meta.put({ key, value }));
}

export function append(entry) {
  return run('outbox', 'readwrite', outbox => outbox.add(entry));
}

export function peek(limit) {
  return open().then(db => new Promise((resolve, reject) => {
    const transaction = db.transaction('outbox', 'readonly');
    const request = transaction.objectStore('outbox').getAll(null, limit);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  }));
}

export function removeThrough(position) {
  return run('outbox', 'readwrite', outbox =>
    outbox.delete(IDBKeyRange.upperBound(position)));
}

export function count() {
  return run('outbox', 'readonly', outbox => outbox.count());
}

export function clearOutbox() {
  return run('outbox', 'readwrite', outbox => outbox.clear());
}

export function clearReplica() {
  return open().then(db => new Promise((resolve, reject) => {
    const transaction = db.transaction(['documents', 'meta'], 'readwrite');
    transaction.objectStore('documents').clear();
    transaction.objectStore('meta').clear();
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  }));
}
