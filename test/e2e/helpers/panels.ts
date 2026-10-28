import { expect, Page } from '@playwright/test';

export function panel(page: Page) {
  return page.locator('.pspad-detail-panel');
}

export async function savePanel(page: Page) {
  await panel(page).locator('.pspad-panel-save').click();
}

export async function closePanel(page: Page) {
  const close = panel(page).locator('.pspad-panel-close');
  await expect(async () => {
    if (await close.isVisible()) {
      await close.click({ timeout: 2_000 });
    }
    await expect(panel(page).locator('.pspad-panel-title')).toBeHidden({ timeout: 3_000 });
    await expect(page.locator('.mud-drawer-overlay--open')).toHaveCount(0, { timeout: 3_000 });
  }).toPass({ timeout: 30_000 });
}

export async function deleteFromPanel(page: Page) {
  await panel(page).locator('.pspad-panel-delete').click();
}

export async function confirm(page: Page, text?: string) {
  const buttons = page.getByRole('dialog').getByRole('button');
  await (text ? buttons.filter({ hasText: text }) : buttons.last()).click();
  await expect(page.getByRole('dialog')).toBeHidden();
}

export async function awaitNoDrawer(page: Page) {
  await expect(page.locator('.pspad-detail-panel.mud-drawer--open')).toHaveCount(0);
  await expect(page.locator('.mud-drawer-overlay--open')).toHaveCount(0);
}

export async function openFabAction(page: Page, name: string) {
  await awaitNoDrawer(page);
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

export function propertyRow(page: Page, label: string) {
  return panel(page).getByRole('button', { name: new RegExp(`^${label}\\b`) }).last();
}

export async function chooseProperty(page: Page, label: string, option: string | RegExp) {
  const item = typeof option === 'string'
    ? openMenuItems(page).getByText(option, { exact: true })
    : openMenuItems(page).filter({ hasText: option });
  await expect(async () => {
    if (!(await item.first().isVisible())) {
      await propertyRow(page, label).click({ timeout: 2_000 });
    }
    await item.first().click({ timeout: 2_000 });
    await expect(openMenuItems(page)).toHaveCount(0, { timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
}

export async function menuOptions(page: Page, label: string) {
  await expect(async () => {
    await propertyRow(page, label).click({ timeout: 2_000 });
    await expect(openMenuItems(page).first()).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
  return openMenuItems(page);
}

export function openMenuItems(page: Page) {
  return page.locator('.mud-popover-open').getByRole('menuitem');
}

export async function dismissMenu(page: Page) {
  await expect(async () => {
    if ((await openMenuItems(page).count()) > 0) {
      await page.keyboard.press('Escape');
    }
    await expect(openMenuItems(page)).toHaveCount(0, { timeout: 1_000 });
  }).toPass({ timeout: 10_000 });
}

export async function clearProperty(page: Page, label: string) {
  const clear = panel(page).getByRole('button', { name: `Clear ${label.toLowerCase()}`, exact: true });
  await expect(async () => {
    if (await clear.isVisible()) {
      await clear.click({ timeout: 2_000 });
    }
    await expect(clear).toHaveCount(0, { timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
}
