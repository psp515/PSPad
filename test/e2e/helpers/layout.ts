import { expect, Locator } from '@playwright/test';

export async function visibleWidth(locator: Locator): Promise<number> {
  const box = await locator.boundingBox();
  expect(box, 'element is not rendered').not.toBeNull();
  const clip = await locator.evaluate(element => {
    let left = -Infinity;
    let right = Infinity;
    for (let parent = element.parentElement; parent; parent = parent.parentElement) {
      if (getComputedStyle(parent).overflowX !== 'visible') {
        const rect = parent.getBoundingClientRect();
        left = Math.max(left, rect.left);
        right = Math.min(right, rect.right);
      }
    }
    return { left, right };
  });
  return Math.max(0, Math.min(box!.x + box!.width, clip.right) - Math.max(box!.x, clip.left));
}
