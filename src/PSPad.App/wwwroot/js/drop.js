export function attach(element, dotnetRef) {
  function onDragOver(event) {
    event.preventDefault();
  }

  function onDrop(event) {
    event.preventDefault();
    const dataTransfer = event.dataTransfer;
    if (!dataTransfer) {
      return;
    }
    const uriList = dataTransfer.getData('text/uri-list');
    const text = dataTransfer.getData('text/plain');
    const hadFiles = dataTransfer.files && dataTransfer.files.length > 0;
    dotnetRef.invokeMethodAsync('OnDropped', uriList, text, hadFiles);
  }

  element.addEventListener('dragover', onDragOver);
  element.addEventListener('drop', onDrop);

  return {
    dispose() {
      element.removeEventListener('dragover', onDragOver);
      element.removeEventListener('drop', onDrop);
    }
  };
}
