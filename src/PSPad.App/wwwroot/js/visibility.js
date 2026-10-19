export function subscribe(dotnetRef) {
  document.addEventListener('visibilitychange', () =>
    dotnetRef.invokeMethodAsync('OnVisibilityChanged', document.visibilityState === 'visible'));
  return document.visibilityState === 'visible';
}
