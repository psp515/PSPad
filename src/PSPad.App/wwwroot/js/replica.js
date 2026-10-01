const DB_NAME = 'pspad';
const VERSION = 3;

let connection;

export function open() {
  connection ??= openAt(VERSION)
    .catch(error =>
      // A script cached from an older build meets a database a newer build already upgraded; stores only ever get added.
      error?.name === 'VersionError' ? openAt() : Promise.reject(error))
    .then(db => {
      db.onversionchange = () => forget(db);
      db.onclose = () => forget(db);
      return db;
    }, error => {
      connection = undefined;
      return Promise.reject(error);
    });
  return connection;
}

function forget(db) {
  db.close();
  connection = undefined;
}

function openAt(version) {
  return new Promise((resolve, reject) => {
    const request = version === undefined ? indexedDB.open(DB_NAME) : indexedDB.open(DB_NAME, version);
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
      if (!db.objectStoreNames.contains('snapshots')) {
        db.createObjectStore('snapshots', { keyPath: 'token' });
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
    // A version upgrade blocked by a connection in another tab settles neither onsuccess nor
    // onerror, so without this the promise never resolves and never rejects.
    request.onblocked = () => reject(new Error('IndexedDB upgrade blocked by another open tab'));
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

export function getAll(type) {
  // Arrays sort after every string in IndexedDB keys, so [type, []] bounds every [type, userId].
  return run('documents', 'readonly', documents =>
    documents.index('type_user').getAll(IDBKeyRange.bound([type], [type, []])));
}

export function remove(id) {
  return run('documents', 'readwrite', documents => documents.delete(id));
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
    request.onsuccess = () => {
      const db = request.result;
      db.onversionchange = () => db.close();
      resolve(db);
    };
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

export function putSnapshot(entry) {
  return run('snapshots', 'readwrite', snapshots => snapshots.put(entry));
}

export function getSnapshot(token) {
  return run('snapshots', 'readonly', snapshots => snapshots.get(token));
}

export function allSnapshots() {
  return run('snapshots', 'readonly', snapshots => snapshots.getAll());
}

export function deleteSnapshot(token) {
  return run('snapshots', 'readwrite', snapshots => snapshots.delete(token));
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
