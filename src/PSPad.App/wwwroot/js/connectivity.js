export function subscribe(dotnetRef) {
  window.addEventListener('online', () => dotnetRef.invokeMethodAsync('OnOnline'));
  window.addEventListener('offline', () => dotnetRef.invokeMethodAsync('OnOffline'));
  return navigator.onLine;
}
