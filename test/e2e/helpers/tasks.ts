import { expect, Page } from '@playwright/test';
import { closePanel, openFabAction, panel, savePanel } from './panels';

export function taskRow(page: Page, name: string) {
  return page.locator('.pspad-row').filter({ has: page.locator('.pspad-row-name', { hasText: name }) });
}

export async function addTask(page: Page, name: string) {
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

export async function addStep(page: Page, name: string) {
  const input = panel(page).locator('.pspad-step-add input');
  await input.fill(name);
  await input.press('Enter');
  await expect(panel(page).locator('.pspad-step-name', { hasText: name })).toBeVisible();
}

export async function checkStep(page: Page, name: string) {
  await panel(page).getByRole('checkbox', { name, exact: true }).check();
}
