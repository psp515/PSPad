export function attach(dotnetRef) {
  const threshold = 80;
  const indicator = document.querySelector('.pspad-pull-indicator');
  let startY = null;
  let distance = 0;

  function blocked() {
    return window.scrollY > 0
      || document.querySelector('.mud-overlay, .mud-drawer--open.mud-drawer-temporary, .mud-dialog');
  }

  function show(offset, settled) {
    if (!indicator) {
      return;
    }
    indicator.style.transition = settled ? 'transform 150ms, opacity 150ms' : 'none';
    indicator.style.transform = `translate(-50%, ${offset}px)`;
    indicator.style.opacity = offset > 0 ? '1' : '0';
  }

  function onStart(event) {
    startY = blocked() ? null : event.touches[0].clientY;
    distance = 0;
  }

  function onMove(event) {
    if (startY === null) {
      return;
    }
    distance = event.touches[0].clientY - startY;
    if (distance <= 0) {
      show(0, true);
      return;
    }
    show(Math.min(distance / 2, threshold), false);
  }

  function onEnd() {
    const pulled = startY !== null && distance >= threshold;
    startY = null;
    show(0, true);
    if (pulled) {
      dotnetRef.invokeMethodAsync('OnPulled');
    }
  }

  window.addEventListener('touchstart', onStart, { passive: true });
  window.addEventListener('touchmove', onMove, { passive: true });
  window.addEventListener('touchend', onEnd, { passive: true });
  window.addEventListener('touchcancel', onEnd, { passive: true });

  return {
    dispose() {
      window.removeEventListener('touchstart', onStart);
      window.removeEventListener('touchmove', onMove);
      window.removeEventListener('touchend', onEnd);
      window.removeEventListener('touchcancel', onEnd);
    }
  };
}
