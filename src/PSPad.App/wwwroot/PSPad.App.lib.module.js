export function beforeStart() {
  try {
    Blazor.registerCustomEventType('markdownviewclick', {
      browserEventName: 'click',
      createEventArgs: event => {
        const selection = window.getSelection();
        return {
          onLink: event.target instanceof Element && event.target.closest('a') !== null,
          hasSelection: selection !== null && !selection.isCollapsed
        };
      }
    });
  } catch (e) {
    console.warn('markdownviewclick unavailable', e);
  }
}
