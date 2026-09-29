let registered = false;

function registerMarkdownViewClick() {
  if (registered) {
    return;
  }
  registered = true;
  Blazor.registerCustomEventType('markdownviewclick', {
    browserEventName: 'click',
    createEventArgs: event => ({
      onLink: event.target instanceof Element && event.target.closest('a') !== null
    })
  });
}

export function beforeStart() {
  registerMarkdownViewClick();
}

export function beforeWebAssemblyStart() {
  registerMarkdownViewClick();
}
