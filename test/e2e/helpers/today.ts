import { expect, Page } from '@playwright/test';
import { goTo } from './nav';
import { panel } from './panels';
import { shortDate } from './dates';

export async function goToMyDay(page: Page) {
  await goTo(page, 'My Day');
}

export function daySection(page: Page, section: 'overdue' | 'scheduled' | 'anytime' | 'starred' | 'completed') {
  return page.locator(`.pspad-day-${section}`);
}

export function dayLabel(page: Page) {
  return page.locator('.pspad-week-label');
}

export async function setDueDate(page: Page, day: 'Today' | 'Tomorrow' | Date) {
  await panel(page).locator('.pspad-task-due .pspad-property-activator').click();
  if (day instanceof Date) {
    await page.getByRole('menuitem', { name: 'Pick a date…' }).click();
    await pickCalendarDay(page.locator('.pspad-due-dialog'), day);
    await expect(page.locator('.pspad-due-dialog')).toBeHidden();
  } else {
    await page.getByRole('menuitem', { name: day }).click();
  }
}

export async function pickCalendarDay(dialog: import('@playwright/test').Locator, day: Date) {
  const wanted = day.toLocaleString('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' });
  const shown = dialog.locator('.mud-picker-calendar-header-transition');
  for (let step = 0; step < 3 && (await shown.innerText()).trim() !== wanted; step++) {
    await dialog.locator('.mud-picker-calendar-header-switch .mud-icon-button').first().click();
  }
  await expect(shown).toHaveText(wanted);
  await dialog
    .locator('.mud-picker-calendar-day:not(.mud-hidden) button, button.mud-picker-calendar-day:not(.mud-hidden)')
    .filter({ hasText: new RegExp(`^${day.getUTCDate()}$`), visible: true })
    .first()
    .click();
}

export async function setTaskTime(page: Page, start: string, end?: string) {
  const row = panel(page).locator('.pspad-task-time');
  const startInput = row.locator('.pspad-time-start input');
  await startInput.fill(start);
  await startInput.press('Tab');
  await expect(startInput).toHaveValue(start);
  await expect(row.locator('.pspad-time-clear')).toBeVisible();
  if (end) {
    const endInput = row.locator('.pspad-time-end input');
    await endInput.fill(end);
    await endInput.press('Tab');
    await expect(endInput).toHaveValue(end);
  }
}

export function timelineBlock(page: Page, name: string) {
  return page.locator('.pspad-timeline-task').filter({ has: page.locator('.pspad-timeline-task-name', { hasText: name }) });
}

export async function setRepeating(page: Page) {
  await panel(page).locator('.pspad-task-kind').getByText('Repeating', { exact: true }).click();
  await expect(panel(page).locator('.pspad-task-repeat')).toBeVisible();
}

export async function showDay(page: Page, day: Date) {
  await page.locator('.pspad-week-pick').click();
  await pickCalendarDay(page.locator('.pspad-day-dialog'), day);
  await expect(dayLabel(page)).toContainText(shortDate(day));
}

export async function pickWeekDay(page: Page, day: Date) {
  await page.getByRole('button', { name: new RegExp(`^${escapeRegExp(shortDate(day))}(,|$)`) }).click();
}

function escapeRegExp(text: string) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
