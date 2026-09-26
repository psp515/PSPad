const hourly = 60 * 60 * 1000;

export async function subscribe(dotnetRef) {
  if (!('serviceWorker' in navigator)) {
    return;
  }

  const registration = await navigator.serviceWorker.ready;
  const announce = () => dotnetRef.invokeMethodAsync('OnUpdateAvailable');
  const watch = worker => worker?.addEventListener('statechange', () => {
    if (worker.state === 'installed' && navigator.serviceWorker.controller) {
      announce();
    }
  });

  if (registration.waiting && navigator.serviceWorker.controller) {
    announce();
  }

  watch(registration.installing);
  navigator.serviceWorker.addEventListener('controllerchange', announce);
  registration.addEventListener('updatefound', () => watch(registration.installing));

  const check = () => registration.update().catch(() => { });
  setInterval(check, hourly);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      check();
    }
  });
}

export async function activate() {
  const registration = await navigator.serviceWorker.getRegistration();

  if (!registration?.waiting) {
    location.reload();
    return;
  }

  navigator.serviceWorker.addEventListener('controllerchange', () => location.reload(), { once: true });
  registration.waiting.postMessage({ type: 'SKIP_WAITING' });
}
