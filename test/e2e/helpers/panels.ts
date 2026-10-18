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

export async function confirm(page: Page, text?: string) {
  const buttons = page.getByRole('dialog').getByRole('button');
  await (text ? buttons.filter({ hasText: text }) : buttons.last()).click();
  await expect(page.getByRole('dialog')).toBeHidden();
}

export async function openFabAction(page: Page, name: string) {
  await expect(page.locator('.pspad-detail-panel.mud-drawer--open')).toHaveCount(0);
  await expect(page.locator('.mud-drawer-overlay--open')).toHaveCount(0);
  const menu = page.locator('.pspad-fab .mud-fab-menu');
  const item = page.getByRole('menuitem', { name, exact: true });
  await expect(async () => {
    if (!(await menu.evaluate(element => element.classList.contains('mud-fab-menu-open')))) {
      await page.locator('.pspad-fab').getByRole('button').click();
      await expect(menu).toHaveClass(/mud-fab-menu-open/, { timeout: 2_000 });
    }
    await item.click({ timeout: 3_000 });
  }).toPass({ timeout: 30_000 });
  await expect(menu).not.toHaveClass(/mud-fab-menu-open/);
}

export async function pickFromPropertyRow(page: Page, rowClass: string, option: string) {
  await panel(page).locator(`.${rowClass} .pspad-property-activator`).click();
  await page.getByRole('menuitem', { name: option, exact: true }).click();
}
