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
