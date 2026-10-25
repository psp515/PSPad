import { expect, Page } from '@playwright/test';
import { awaitNoDrawer, closePanel, openFabAction, panel, savePanel } from './panels';

export function taskRow(page: Page, name: string) {
  return page.locator('.pspad-row').filter({ has: page.locator('.pspad-row-name', { hasText: name }) });
}

export async function addTask(page: Page, name: string) {
  await awaitNoDrawer(page);
  if (await page.getByRole('button', { name: 'Add task' }).isVisible()) {
    await page.getByRole('button', { name: 'Add task' }).click();
  } else {
    await openFabAction(page, 'Add task');
  }
  await panel(page).getByPlaceholder('Task name').fill(name);
  await savePanel(page);
  await expect(taskRow(page, name)).toBeVisible();
}

export async function openTask(page: Page, name: string) {
  await taskRow(page, name).locator('.pspad-task-name').click();
  await expect(panel(page).getByPlaceholder('Task name')).toHaveValue(name);
}

export async function closeTask(page: Page) {
  await closePanel(page);
}

export async function completeTaskRow(page: Page, name: string) {
  const visible = taskRow(page, name).filter({ visible: true });
  await visible.first().getByRole('checkbox').first().click();
  await expect(async () => {
    if ((await visible.count()) === 0) {
      return;
    }
    await expect(visible.first().getByRole('checkbox').first()).toBeChecked({ timeout: 500 });
  }).toPass();
}

export function stepNameField(page: Page, name: string) {
  return panel(page).getByRole('textbox', { name: `Rename ${name}`, exact: true });
}

export async function addStep(page: Page, name: string) {
  await expect(async () => {
    if (!(await stepNameField(page, name).isVisible())) {
      const input = panel(page).locator('.pspad-step-add input');
      await input.fill(name, { timeout: 2_000 });
      await input.press('Enter', { timeout: 2_000 });
    }
    await expect(stepNameField(page, name)).toHaveValue(name, { timeout: 5_000 });
  }).toPass({ timeout: 30_000 });
}

export async function renameStep(page: Page, from: string, to: string) {
  await expect(async () => {
    const field = stepNameField(page, from);
    if (await field.isVisible()) {
      await field.fill(to, { timeout: 2_000 });
      await field.press('Enter', { timeout: 2_000 });
    }
    await expect(stepNameField(page, to)).toHaveValue(to, { timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
}

export async function checkStep(page: Page, name: string) {
  const box = panel(page).getByRole('checkbox', { name, exact: true });
  await expect(async () => {
    if (!(await box.isChecked())) {
      await box.click({ timeout: 2_000 });
    }
    await expect(box).toBeChecked({ timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
}
