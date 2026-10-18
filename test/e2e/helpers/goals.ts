import { expect, Page } from '@playwright/test';
import { goTo } from './nav';
import { panel, pickFromPropertyRow, savePanel } from './panels';

export async function goToGoals(page: Page) {
  await goTo(page, 'Goals');
}

export async function createGoal(page: Page, name: string) {
  await goToGoals(page);
  await page.getByRole('button', { name: 'Add goal' }).first().click();
  await panel(page).getByPlaceholder('Goal name').fill(name);
  await savePanel(page);
  await expect(goalCard(page, name)).toBeVisible();
}

export function goalCard(page: Page, name: string) {
  return page.locator('.mud-paper').filter({ has: page.locator('.pspad-goal-name', { hasText: name }) }).first();
}

export async function openGoal(page: Page, name: string) {
  await page.locator('.pspad-goal-name', { hasText: name }).click();
  await expect(page.getByRole('heading', { level: 1 })).toContainText(name);
}

export async function linkTaskToGoal(page: Page, goalName: string) {
  await pickFromPropertyRow(page, 'pspad-task-goal', goalName);
  await expect(panel(page).locator('.pspad-task-goal .pspad-property-value')).toHaveText(goalName);
}
