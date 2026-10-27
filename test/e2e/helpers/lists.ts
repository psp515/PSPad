import { expect, Page } from '@playwright/test';
import { awaitNoDrawer, confirm, openFabAction, panel, savePanel } from './panels';

export type ListKind = 'Tasks' | 'Reference';

export async function createList(page: Page, name: string, kind: ListKind = 'Tasks') {
  await awaitNoDrawer(page);
  if (await page.getByRole('button', { name: 'New list' }).isVisible()) {
    await page.getByRole('button', { name: 'New list' }).click();
  } else {
    await openFabAction(page, 'New list');
  }
  await panel(page).getByPlaceholder('List name').fill(name);
  await panel(page).locator('.pspad-list-kind').getByText(kind, { exact: true }).click();
  await savePanel(page);
  await expect(page.getByRole('link', { name, exact: true })).toBeVisible();
}

export async function openList(page: Page, name: string) {
  await page.getByRole('link', { name, exact: true }).first().click();
  await expect(page.getByRole('heading', { level: 1 })).toContainText(name);
  await expect(page).toHaveURL(/\/lists\//);
}

export function listIdFromUrl(page: Page): string {
  return new URL(page.url()).pathname.split('/').pop()!;
}

export async function deleteCurrentList(page: Page) {
  await openFabAction(page, 'Delete list');
  await confirm(page, 'Delete');
}

export function listRowNames(page: Page) {
  return page.locator('.pspad-row-group .pspad-row-name');
}

export async function pickListFilter(page: Page, label: string) {
  await page.locator('.pspad-list-filter').click();
  await page.locator('.mud-popover-open .pspad-list-filter-chip', { hasText: label }).first().click();
  await expect(page.locator('.pspad-list-filter-badge')).toBeVisible();
  await page.locator('.pspad-list-filter').click();
  await expect(page.locator('.mud-popover-open .pspad-list-filter-chip')).toHaveCount(0);
}

export async function sortListBy(page: Page, order: string) {
  await page.locator('.pspad-list-sort').click();
  await page.locator('.mud-popover-open .pspad-list-sort-option', { hasText: order }).click();
  await expect(page.locator('.pspad-list-sort')).toContainText(order);
}

export async function showListAs(page: Page, view: 'List' | 'Calendar') {
  await page.locator('.pspad-list-views .mud-toggle-item', { hasText: view }).click();
}

export function monthDay(page: Page, isoDay: string) {
  return page.locator(`.pspad-month-day[data-day="${isoDay}"]`);
}
