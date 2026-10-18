import { expect, Page } from '@playwright/test';

export function panel(page: Page) {
  return page.locator('.pspad-detail-panel');
}

export async function savePanel(page: Page) {
  await panel(page).locator('.pspad-panel-save').click();
}

export async function closePanel(page: Page) {
  const close = panel(page).locator('.pspad-panel-close');
  if (await close.isVisible()) {
    await close.click();
  }
  await expect(panel(page).locator('.pspad-panel-title')).toBeHidden();
}

export async function deleteFromPanel(page: Page) {
  await panel(page).locator('.pspad-panel-delete').click();
}

export async function confirm(page: Page, text: string) {
  await page.getByRole('dialog').getByRole('button', { name: text }).click();
}

export async function openFabAction(page: Page, name: string) {
  await page.locator('.pspad-fab').getByRole('button').click();
  await page.getByRole('menuitem', { name, exact: true }).click();
}

export async function pickFromPropertyRow(page: Page, rowClass: string, option: string) {
  await panel(page).locator(`.${rowClass} .pspad-property-activator`).click();
  await page.getByRole('menuitem', { name: option, exact: true }).click();
}
