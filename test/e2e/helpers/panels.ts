import { expect, Page } from '@playwright/test';
import { escapeRegExp } from './text';

const closeName = /^(Close|Zamknij)$/;

export function panel(page: Page) {
  return page.getByRole('complementary').filter({ has: page.getByRole('button', { name: closeName }) });
}

const saveName = /^(Save|Add|Add (task|item|goal|list|area|budget)|Convert to task)$/;

export function saveButton(page: Page) {
  return panel(page).getByRole('button', { name: saveName }).last();
}

export async function savePanel(page: Page) {
  await saveButton(page).click();
}

export async function closePanel(page: Page) {
  const close = panel(page).getByRole('button', { name: closeName });
  await expect(async () => {
    if (await close.isVisible()) {
      await close.click({ timeout: 2_000 });
    }
    await expect(panel(page)).toHaveCount(0, { timeout: 3_000 });
  }).toPass({ timeout: 30_000 });
}

export async function deleteFromPanel(page: Page, label: 'Delete' | 'Discard' | 'Remove' = 'Delete') {
  await panel(page).getByRole('button', { name: label, exact: true }).click();
}

export async function confirm(page: Page, text?: string) {
  const buttons = page.getByRole('dialog').getByRole('button');
  await (text ? buttons.filter({ hasText: text }) : buttons.last()).click();
  await expect(page.getByRole('dialog')).toBeHidden();
}

export async function awaitNoDrawer(page: Page) {
  await expect(panel(page)).toHaveCount(0);
}

function fabMenu(page: Page, item?: string) {
  const menus = page.getByRole('menu', { name: /./ });
  return item ? menus.filter({ has: page.getByRole('menuitem', { name: item, exact: true }) }) : menus;
}

function clickable(item: import('@playwright/test').Locator) {
  return item.click({ trial: true, timeout: 1_000 }).then(() => true, () => false);
}

export async function openFabAction(page: Page, name: string) {
  await openFab(page, name);
  const item = fabMenu(page, name).getByRole('menuitem', { name, exact: true });
  await item.click();
  await expect.poll(() => clickable(item)).toBe(false);
}

export async function openFab(page: Page, item?: string) {
  await awaitNoDrawer(page);
  const fab = fabMenu(page, item);
  const items = fab.getByRole('menuitem');
  await expect(async () => {
    if (!(await clickable(items.first()))) {
      await fab.getByRole('button').click({ timeout: 2_000 });
    }
    expect(await clickable(items.first())).toBe(true);
  }).toPass({ timeout: 30_000 });
  return items;
}

export function propertyRow(page: Page, label: string) {
  return panel(page).getByRole('button', { name: new RegExp(`^${escapeRegExp(label)}\\b`) }).last();
}

export async function chooseProperty(page: Page, label: string, option: string | RegExp) {
  const item = typeof option === 'string'
    ? openMenuItems(page).getByText(option, { exact: true })
    : openMenuItems(page).filter({ hasText: option });
  await expect(async () => {
    if (!(await item.first().isVisible())) {
      await propertyRow(page, label).click({ timeout: 2_000 });
    }
    await expect(item.first()).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
  await item.first().click();
  await expect(openMenuItems(page)).toHaveCount(0);
}

export async function menuOptions(page: Page, label: string) {
  await expect(async () => {
    if (!(await openMenuItems(page).first().isVisible())) {
      await propertyRow(page, label).click({ timeout: 2_000 });
    }
    await expect(openMenuItems(page).first()).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
  return openMenuItems(page);
}

export function openMenu(page: Page) {
  return page.getByRole('menu', { name: /^$/ });
}

export function openMenuItems(page: Page) {
  return openMenu(page).getByRole('menuitem');
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
  await clear.click();
  await expect(clear).toHaveCount(0);
}
