import { APIRequestContext, expect, Locator, Page } from '@playwright/test';
import { listCard } from './areas';
import { openFabAction, panel } from './panels';

export type Invite = { link: string; code: string; token: string };

export async function openMembersView(page: Page) {
  await panel(page).getByRole('option', { name: /^Members/ }).click();
  await expect(panel(page).getByText(/^People · \d+$/)).toBeVisible();
}

export async function openListMembers(page: Page) {
  await openFabAction(page, 'Edit list');
  await openMembersView(page);
}

export async function openSharedWithMe(page: Page) {
  await page.getByRole('navigation').getByRole('link', { name: 'Shared with me', exact: true }).first().click();
  await expect(page.getByRole('heading', { name: 'Shared with me', exact: true })).toBeVisible();
}

export async function openCardMembers(page: Page, listName: string) {
  await listCard(page, listName).getByRole('button', { name: 'Actions' }).click();
  await page.getByRole('menuitem', { name: 'Edit', exact: true }).click();
  await openMembersView(page);
}

export async function openSharedListMembersFromBoard(page: Page, listName: string) {
  await openSharedWithMe(page);
  await openCardMembers(page, listName);
}

export async function openListSnapshots(page: Page) {
  await openFabAction(page, 'Edit list');
  await panel(page).getByRole('option', { name: /^Public snapshots/ }).click();
  await expect(panel(page).getByText('Link works for')).toBeVisible();
}

export async function createInvite(page: Page, button: 'Create invite link' | 'Create link · 30 min' = 'Create invite link'): Promise<Invite> {
  await panel(page).getByRole('button', { name: button }).click();
  return readInvite(page);
}

export async function readInvite(page: Page): Promise<Invite> {
  const shown = panel(page).getByText(/^[A-Z0-9]{3}-[A-Z0-9]{3}$/);
  await expect(shown).toBeVisible();
  const link = await panel(page).getByRole('textbox').first().inputValue();
  const code = (await shown.innerText()).trim().replace('-', '');
  const token = new URL(link).pathname.split('/').pop()!;
  return { link, code, token };
}

export function sharedMarker(card: Locator) {
  return card.getByRole('img', { name: /^Shared · / });
}

export function joinButton(page: Page) {
  return page.getByRole('button', { name: /^Join (list|budget)$/ });
}

export async function joinWithCode(page: Page, link: string, code: string, beforeTyping?: () => Promise<void>) {
  const openingSync = page.waitForResponse(response => response.url().includes('/api/sync') && response.ok());
  await page.goto(new URL(link).pathname);
  await openingSync;
  await expect(page.getByText('Enter the invite code')).toBeVisible();
  await beforeTyping?.();
  await page.getByLabel('Code').fill(code);
  await joinButton(page).click();
}

export async function tryJoin(api: APIRequestContext, kind: 'lists' | 'budgets', token: string, code: string) {
  const response = await api.post(`/api/${kind}/join`, { data: { token, code } });
  return response.status();
}

export function wrongCode(code: string) {
  return code === 'ZZZ999' ? 'ZZZ998' : 'ZZZ999';
}
