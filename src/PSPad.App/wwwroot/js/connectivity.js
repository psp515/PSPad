export function subscribe(dotnetRef) {
  window.addEventListener('online', () => dotnetRef.invokeMethodAsync('OnOnline'));
  window.addEventListener('offline', () => dotnetRef.invokeMethodAsync('OnBrowserOffline'));
  return navigator.onLine;
}

export function probe(url) {
  return fetch(url, { mode: 'no-cors', cache: 'no-store', signal: AbortSignal.timeout(5000) })
    .then(() => true, () => false);
}
