import { expect, Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { Device } from './api';
import { isoDate } from './dates';
import { goTo } from './nav';
import { awaitNoDrawer, closePanel, confirm, panel, pickFromPropertyRow, savePanel } from './panels';

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
  return page.locator('.pspad-goal-list-row').filter({ has: page.locator('.pspad-goal-name', { hasText: name }) });
}

export async function openGoal(page: Page, name: string) {
  await page.locator('.pspad-goal-name', { hasText: name }).click();
  await expect(page.getByRole('heading', { level: 1 })).toContainText(name);
}

export async function linkTaskToGoal(page: Page, goalName: string) {
  await pickFromPropertyRow(page, 'pspad-task-goal', goalName);
  await expect(panel(page).locator('.pspad-task-goal .pspad-property-value')).toHaveText(goalName);
}

export async function deleteGoal(page: Page, name: string) {
  await goToGoals(page);
  await goalCard(page, name).getByRole('button', { name: 'Actions' }).click();
  await page.getByRole('menuitem', { name: 'Delete', exact: true }).click();
  await confirm(page, 'Delete');
  await expect(goalCard(page, name)).toHaveCount(0);
}

export function goalRow(page: Page, name: string) {
  return page.getByRole('link', { name, exact: true });
}

export async function openGoalMenu(page: Page, name: string) {
  await goalRow(page, name).getByRole('button', { name: 'Actions' }).click();
}

export async function createGoalOn(device: Device, name: string, dueOn?: Date): Promise<string> {
  const goalId = randomUUID();
  await device.send('CreateGoal', { goalId, name });
  if (dueOn) {
    await device.send('SetGoalDueDate', { goalId, dueOn: isoDate(dueOn) });
  }
  return goalId;
}

export async function linkTasksOn(device: Device, goalId: string, taskIds: string[]) {
  for (const taskId of taskIds) {
    await device.send('AddTaskToGoal', { goalId, taskId });
  }
}

export async function editGoal(page: Page, name: string) {
  if (await panel(page).getByPlaceholder('Goal name').isVisible()) {
    await closePanel(page);
  }
  await awaitNoDrawer(page);
  await openGoalMenu(page, name);
  await page.getByRole('menuitem', { name: 'Edit', exact: true }).click();
  await expect(panel(page).getByPlaceholder('Goal name')).toHaveValue(name);
}
