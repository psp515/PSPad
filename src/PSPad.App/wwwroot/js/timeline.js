export function reveal(hours) {
  const pane = hours.closest('.pspad-timeline');
  const scroll = () => {
    const anchor = hours.querySelector('.pspad-timeline-anchor');
    if (anchor) {
      pane.scrollTop = anchor.offsetTop;
    }
  };
  if (pane.clientHeight > 0) {
    scroll();
    return;
  }
  const observer = new ResizeObserver(() => {
    if (pane.clientHeight > 0) {
      observer.disconnect();
      scroll();
    }
  });
  observer.observe(pane);
}
