import { expect, Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { apiFor, bearerToken, Device, me, sendCommand } from './api';
import { isoDate, utcToday } from './dates';
import { awaitNoDrawer, closePanel, openFabAction, panel, savePanel } from './panels';

export function taskRow(page: Page, name: string) {
  return page.locator('.pspad-row').filter({ has: page.locator('.pspad-row-name', { hasText: name }) });
}

export async function openNewTask(page: Page) {
  await awaitNoDrawer(page);
  if (await page.getByRole('button', { name: 'Add task' }).isVisible()) {
    await page.getByRole('button', { name: 'Add task' }).click();
  } else {
    await openFabAction(page, 'Add task');
  }
  await expect(panel(page).getByPlaceholder('Task name')).toBeVisible();
}

export async function addTask(page: Page, name: string) {
  await openNewTask(page);
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
  const steps = panel(page).locator('.pspad-steps .pspad-step-name');
  const input = panel(page).locator('.pspad-step-add input');
  const before = await steps.count();
  // a sync pull can undo a just-made edit (reported in PR #225)
  let accepted = false;
  await expect(async () => {
    if (!accepted) {
      await input.fill(name, { timeout: 2_000 });
      await expect(input).toHaveValue(name, { timeout: 2_000 });
      await input.press('Enter', { timeout: 2_000 });
      await expect(input).toHaveValue('', { timeout: 10_000 });
      accepted = true;
    }
    await expect(steps).toHaveCount(before + 1, { timeout: 10_000 });
    await expect(stepNameField(page, name)).toHaveValue(name, { timeout: 5_000 });
  }).toPass({ timeout: 60_000 });
}

export async function renameStep(page: Page, from: string, to: string) {
  // a sync pull can undo a just-made edit (reported in PR #225)
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

export function stepRow(page: Page, name: string) {
  return panel(page)
    .locator('.pspad-step')
    .filter({ has: page.getByRole('textbox', { name: `Rename ${name}`, exact: true }) });
}

export async function setStepDeadline(page: Page, step: string, day: 'Today' | 'Tomorrow') {
  const due = stepRow(page, step).locator('.pspad-step-due');
  await expect(async () => {
    if (!(await due.innerText({ timeout: 2_000 })).includes(day)) {
      await stepRow(page, step).hover({ timeout: 2_000 });
      await due.click({ timeout: 2_000 });
      await page.getByRole('menuitem', { name: day }).first().click({ timeout: 2_000 });
    }
    await expect(due).toContainText(day, { timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
}

export async function createTaskDueTodayFromAnotherDevice(page: Page, listId: string, name: string) {
  const api = await apiFor(await bearerToken(page));
  const { userId } = await me(api);
  const taskId = randomUUID();
  for (const [type, payload] of [
    ['CreateTask', { userId, taskId, listId, name }],
    ['SetTaskDueDate', { userId, taskId, dueOn: isoDate(utcToday()) }],
  ] as const) {
    const result = await sendCommand(api, type, payload);
    expect(result.accepted, result.rejection ?? '').toBe(true);
  }
  await api.dispose();
}

export async function renameTask(page: Page, to: string) {
  const name = panel(page).getByPlaceholder('Task name');
  await name.fill(to);
  await name.press('Enter');
  await name.press('Tab');
  await expect(taskRow(page, to)).toBeVisible();
}

export type TaskSetup = {
  name: string;
  repeatsFrom?: Date;
  every?: number;
  until?: Date;
  completed?: boolean;
  deleted?: boolean;
  tickedToday?: boolean;
};

export async function createTasksOn(device: Device, listId: string, tasks: TaskSetup[]): Promise<Record<string, string>> {
  const ids: Record<string, string> = {};
  for (const task of tasks) {
    const taskId = randomUUID();
    ids[task.name] = taskId;
    await device.send('CreateTask', { taskId, listId, name: task.name });
    if (task.repeatsFrom) {
      const rule = { kind: 0, startsOn: isoDate(task.repeatsFrom), days: [], dayOfMonth: 0, interval: task.every ?? 1 };
      await device.send('SetTaskRecurrence', { taskId, rule });
    }
    if (task.until) {
      await device.send('SetTaskDueDate', { taskId, dueOn: isoDate(task.until) });
    }
    if (task.tickedToday) {
      await device.send('CompleteOccurrence', { taskId, day: isoDate(utcToday()), completed: true });
    }
    if (task.completed) {
      await device.send('CompleteTask', { taskId });
    }
    if (task.deleted) {
      await device.send('DeleteTask', { taskId });
    }
  }
  return ids;
}
