export function observe(element, dotnetRef) {
  const report = () => dotnetRef.invokeMethodAsync('Resized', element.clientWidth);
  const observer = new ResizeObserver(report);
  observer.observe(element);
  report();
  return {
    dispose() {
      observer.disconnect();
    }
  };
}
