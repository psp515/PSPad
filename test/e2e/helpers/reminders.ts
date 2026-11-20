import { expect, Page } from '@playwright/test';
import { panel } from './panels';
import { pickClockTime } from './today';

export function reminderChoice(page: Page, label: string) {
  return panel(page).getByRole('checkbox', { name: label, exact: true });
}

export function reminderDays(page: Page) {
  return panel(page).getByRole('textbox', { name: 'Days before', exact: true });
}

export function reminderTime(page: Page) {
  return panel(page).getByRole('textbox', { name: 'Reminder time', exact: true });
}

export async function chooseReminder(page: Page, label: string) {
  await expect(async () => {
    await reminderChoice(page, label).click({ timeout: 2_000 });
    await expect(reminderChoice(page, label)).toBeChecked({ timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
}

export async function typeReminder(page: Page, days: string, time: string) {
  await expect(async () => {
    await reminderDays(page).fill(days, { timeout: 2_000 });
    await reminderDays(page).press('Enter', { timeout: 2_000 });
    await reminderTime(page).fill(time, { timeout: 2_000 });
    await reminderTime(page).press('Enter', { timeout: 2_000 });
    await expect(reminderDays(page)).toHaveValue(days, { timeout: 2_000 });
    await expect(reminderTime(page)).toHaveValue(time, { timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
}

export async function pickReminderTime(page: Page, hour: number, minute: number) {
  await pickClockTime(page, 'Reminder', hour, minute);
}

export async function expectReminder(page: Page, days: string, time: string) {
  await expect(reminderDays(page)).toHaveValue(days);
  await expect(reminderTime(page)).toHaveValue(time);
}
