import { expect, Locator, Page } from '@playwright/test';
import { awaitNoDrawer, confirm, openFabAction, openMenu, openMenuItems, panel, savePanel } from './panels';
import { escapeRegExp } from './text';

export type ListKind = 'Tasks' | 'Reference';

export async function createList(page: Page, name: string, kind: ListKind = 'Tasks') {
  await awaitNoDrawer(page);
  if (await page.getByRole('button', { name: 'New list' }).isVisible()) {
    await page.getByRole('button', { name: 'New list' }).click();
  } else {
    await openFabAction(page, 'New list');
  }
  await panel(page).getByPlaceholder('List name').fill(name);
  await panel(page).getByRole('checkbox', { name: kind, exact: true }).click();
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

export function listRows(page: Page) {
  return page.getByRole('article');
}

export function listRowNames(rows: Locator): Promise<string[]> {
  return rows.evaluateAll(elements => elements.map(element => element.getAttribute('aria-label') ?? ''));
}

export function filterButton(page: Page) {
  return page.getByRole('button', { name: /^Filter/ }).last();
}

export async function pickListFilter(page: Page, label: string) {
  const filter = filterButton(page);
  const clear = openMenu(page).getByRole('button', { name: 'Clear', exact: true });
  await filter.click();
  await openMenu(page).getByRole('button', { name: label, exact: true }).first().click();
  await expect(filter).toHaveText(/^Filter\s*\d+$/);
  await filter.click();
  await expect(clear).toHaveCount(0);
}

const orders = /^(Date|Priority|Name|Recently added)$/;

export function sortButton(page: Page) {
  return page.getByRole('button', { name: orders }).last();
}

export async function sortListBy(page: Page, order: string) {
  await sortButton(page).click();
  await openMenuItems(page).filter({ hasText: new RegExp(`^${escapeRegExp(order)}`) }).click();
  await expect(sortButton(page)).toHaveText(order);
}

export async function showListAs(page: Page, view: 'List' | 'Calendar') {
  await page.getByRole('checkbox', { name: view, exact: true }).click();
}

export function monthStrip(page: Page) {
  return page.getByRole('group', { name: 'Month', exact: true });
}

export async function showCalendarMonth(page: Page, day: Date) {
  await page.getByRole('button', { name: 'Pick another month' }).click();
  const sheet = page.getByRole('dialog');
  const year = String(day.getUTCFullYear());
  if ((await sheet.getByText(year, { exact: true }).count()) === 0) {
    await sheet.getByRole('button', { name: 'Next year' }).click();
  }
  await expect(sheet.getByText(year, { exact: true })).toBeVisible();
  await sheet.getByRole('button', { name: day.toLocaleString('en-US', { month: 'short', timeZone: 'UTC' }), exact: true }).click();
  await expect(sheet).toBeHidden();
}

export function calendarDay(page: Page, day: Date) {
  const name = `${day.getUTCDate()} ${day.toLocaleString('en-US', { month: 'long', timeZone: 'UTC' })}`;
  return page.getByRole('button', { name: new RegExp(`^${name}, \\d+ tasks?$`) });
}

export async function closeFilterChip(page: Page, text: string) {
  await page.getByText(text, { exact: true }).getByRole('button', { name: 'Close' }).click();
}
