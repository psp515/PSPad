import { expect, Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { Device } from './api';
import { isoDate } from './dates';
import { goTo } from './nav';
import { awaitNoDrawer, chooseProperty, closePanel, confirm, panel, propertyRow, savePanel } from './panels';

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
  return goalRow(page, name);
}

export async function openGoal(page: Page, name: string) {
  await goalCard(page, name).getByText(name, { exact: true }).click();
  await expect(page.getByRole('heading', { level: 1 })).toContainText(name);
}

export async function linkTaskToGoal(page: Page, goalName: string) {
  await chooseProperty(page, 'Goal', goalName);
  await expect(propertyRow(page, 'Goal')).toHaveAccessibleName(`Goal ${goalName}`);
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
