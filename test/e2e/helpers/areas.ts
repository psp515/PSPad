import { expect, Page } from '@playwright/test';
import { confirm, openFabAction, panel } from './panels';

export async function createArea(page: Page, name: string) {
  await page.locator('.pspad-nav .pspad-new-area').click();
  await panel(page).getByPlaceholder('Area name').fill(name);
  await panel(page).locator('.pspad-panel-save').click();
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
}

export async function openArea(page: Page, name: string) {
  await page.locator('.pspad-nav').getByRole('link', { name, exact: true }).click();
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
}

export function areaSubtitle(page: Page) {
  return page.locator('.pspad-page-subtitle:visible');
}

export async function deleteCurrentArea(page: Page) {
  await openFabAction(page, 'Delete area');
  await confirm(page, 'Delete');
}
