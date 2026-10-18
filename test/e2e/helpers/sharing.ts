import { expect, Page } from '@playwright/test';
import { openFabAction, panel } from './panels';

export async function openListMembers(page: Page) {
  await openFabAction(page, 'Edit list');
  await panel(page).locator('.pspad-list-members-link').click();
  await expect(panel(page).locator('.pspad-list-members')).toBeVisible();
}

export async function openSharedListMembersFromBoard(page: Page, listName: string) {
  await page.locator('.pspad-nav').getByRole('link', { name: 'Shared with me' }).click();
  await expect(page.getByRole('heading', { name: 'Shared with me', exact: true })).toBeVisible();
  const card = page.locator('.mud-paper').filter({ has: page.locator('.pspad-card-header a', { hasText: listName }) }).first();
  await card.locator('.pspad-list-menu').getByRole('button').click();
  await page.getByRole('menuitem', { name: 'Edit', exact: true }).click();
  await panel(page).locator('.pspad-list-members-link').click();
  await expect(panel(page).locator('.pspad-list-members')).toBeVisible();
}

export async function openListSnapshots(page: Page) {
  await openFabAction(page, 'Edit list');
  await panel(page).locator('.pspad-list-snapshots-link').click();
  await expect(panel(page).locator('.pspad-snapshot-publisher')).toBeVisible();
}

export async function createInvite(page: Page): Promise<{ link: string; code: string }> {
  await panel(page).locator('.pspad-invite-create').click();
  const link = await panel(page).locator('.pspad-share-link input').inputValue();
  const code = (await panel(page).locator('.pspad-invite-code-value').innerText()).trim();
  return { link, code };
}

export async function joinWithCode(page: Page, link: string, code: string) {
  const url = new URL(link);
  await page.goto(url.pathname);
  await expect(page.getByText('Enter the invite code')).toBeVisible();
  await page.getByLabel('Code').fill(code);
  await page.locator('.pspad-join-submit').click();
}
