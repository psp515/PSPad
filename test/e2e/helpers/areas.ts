import { expect, Page } from '@playwright/test';
import { confirm, openFabAction, panel, savePanel } from './panels';
import { shellMenu } from './shell';

export async function openNewArea(page: Page) {
  await shellMenu(page).getByText('New area', { exact: true }).click();
}

export async function createArea(page: Page, name: string) {
  await openNewArea(page);
  await panel(page).getByPlaceholder('Area name').fill(name);
  await savePanel(page);
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
}

export async function openArea(page: Page, name: string) {
  await shellMenu(page).getByRole('link', { name, exact: true }).click();
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
}

export function pageHeading(page: Page) {
  return page.getByRole('heading', { level: 1 }).or(page.getByRole('banner').getByRole('heading')).filter({ visible: true });
}

export async function deleteCurrentArea(page: Page) {
  await openFabAction(page, 'Delete area');
  await confirm(page, 'Delete');
}

export function listCard(page: Page, name: string) {
  return page.getByRole('group', { name, exact: true });
}

export function listCardNames(page: Page): Promise<string[]> {
  return page
    .getByRole('group')
    .filter({ has: page.getByRole('link') })
    .evaluateAll(cards => cards.map(card => card.getAttribute('aria-label') ?? '').filter(name => name !== ''));
}

export async function openCardMenu(page: Page, list: string, action: 'Edit' | 'Delete') {
  await listCard(page, list).getByRole('button', { name: 'Actions' }).click();
  await page.getByRole('menuitem', { name: action, exact: true }).click();
}

export function areaChips(page: Page) {
  return page.getByRole('navigation', { name: 'Areas' });
}
