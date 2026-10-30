import { expect, Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { anotherDevice, apiFor, bearerToken, Device, me, sendCommand } from './api';
import { isoDate, utcToday } from './dates';
import { menuTrigger } from './lists';
import { awaitNoDrawer, closePanel, openFabAction, panel, savePanel } from './panels';
import { escapeRegExp } from './text';

export function taskRow(page: Page, name: string) {
  return page.getByRole('article', { name });
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
  await taskRow(page, name).getByText(name, { exact: true }).click();
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

export function newStepField(page: Page) {
  return panel(page).getByRole('textbox', { name: /^(Add step|Next step)$/ });
}

export function stepNameField(page: Page, name: string) {
  return panel(page).getByRole('textbox', { name: `Rename ${name}`, exact: true });
}

export async function addStep(page: Page, name: string) {
  const steps = panel(page).getByRole('textbox', { name: /^Rename / });
  const input = newStepField(page);
  const before = await steps.count();
  await input.fill(name);
  await input.press('Enter');
  await expect(input).toHaveValue('');
  await expect(steps).toHaveCount(before + 1);
  await expect(stepNameField(page, name)).toHaveValue(name);
}

export async function renameStep(page: Page, from: string, to: string) {
  const field = stepNameField(page, from);
  await field.fill(to);
  await field.press('Enter');
  await expect(stepNameField(page, to)).toHaveValue(to);
}

export async function removeStep(page: Page, name: string) {
  await stepRow(page, name).hover();
  await panel(page).getByRole('button', { name: `Remove ${name}`, exact: true }).click();
  await expect(stepNameField(page, name)).toHaveCount(0);
}

export async function checkStep(page: Page, name: string) {
  const box = panel(page).getByRole('checkbox', { name, exact: true });
  await box.click();
  await expect(box).toBeChecked();
}

export function stepRow(page: Page, name: string) {
  return panel(page).getByRole('group', { name, exact: true });
}

export async function setStepDeadline(page: Page, step: string, day: 'Today' | 'Tomorrow') {
  await stepRow(page, step).hover();
  await menuTrigger(stepRow(page, step), new RegExp(`^Set a deadline for ${escapeRegExp(step)}$`)).click();
  await page.getByRole('menuitem', { name: day }).first().click();
  await expect(menuTrigger(stepRow(page, step), new RegExp(`^${day}$`))).toBeVisible();
}

export async function stepDeadlineOnAnotherDevice(page: Page, task: string, step: string): Promise<string | null> {
  const device = await anotherDevice(page);
  try {
    const held = (await device.documents('todotasks')).find(doc => doc['name'] === task);
    const steps = (held?.['steps'] ?? []) as Array<Record<string, unknown>>;
    return (steps.find(candidate => candidate['name'] === step)?.['dueOn'] as string | undefined) ?? null;
  } finally {
    await device.dispose();
  }
}

export async function clearStepDeadline(page: Page, step: string, shown: string) {
  await stepRow(page, step).hover();
  await stepRow(page, step).getByRole('button', { name: shown, exact: true }).first().click();
  await page.getByRole('menuitem', { name: 'Clear date' }).click();
  await expect(panel(page).getByLabel(`Set a deadline for ${step}`, { exact: true })).toBeVisible();
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
