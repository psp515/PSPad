import { expect, Page } from '@playwright/test';
import { goTo, navLink } from './nav';
import { awaitNoDrawer, chooseProperty, panel } from './panels';
import { shortDate, utcToday } from './dates';
import { escapeRegExp } from './text';

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
  const arrows = dialog.locator('.mud-picker-calendar-header-switch .mud-icon-button');
  for (let step = 0; step < 3 && (await shown.innerText()).trim() !== wanted; step++) {
    const showing = new Date(`1 ${(await shown.innerText()).trim()} UTC`);
    await (showing.getTime() > day.getTime() ? arrows.first() : arrows.last()).click();
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

export function weekDay(page: Page, day: Date) {
  return page.getByRole('button', { name: new RegExp(`^${escapeRegExp(shortDate(day))}(,|$)`) });
}

export async function pickWeekDay(page: Page, day: Date) {
  await weekDay(page, day).click();
}

export function myDayLink(page: Page) {
  return navLink(page, '/');
}

export async function openNewTaskFromFab(page: Page) {
  await awaitNoDrawer(page);
  await page.getByRole('button', { name: 'Add task', exact: true }).click();
  await expect(panel(page).getByText('New task', { exact: true })).toBeVisible();
}

export type CustomRepeat = { every?: number; unit?: string; days?: string[]; start?: Date };

export async function setCustomRepeat(page: Page, repeat: CustomRepeat) {
  await chooseProperty(page, 'Repeat', 'Custom…');
  const dialog = page.getByRole('dialog').filter({ hasText: 'Custom repeat' });
  await expect(dialog).toBeVisible();
  if (repeat.every) {
    const every = dialog.getByRole('spinbutton').first();
    await every.fill(String(repeat.every), { timeout: 10_000 });
    await every.press('Tab', { timeout: 10_000 });
  }
  if (repeat.unit) {
    await dialog.getByRole('combobox').first().click({ timeout: 10_000 });
    await page.getByRole('option', { name: repeat.unit, exact: true }).click({ timeout: 10_000 });
    await expect(page.getByRole('option')).toHaveCount(0);
  }
  if (repeat.start) {
    await dialog.getByLabel('Starts').click({ timeout: 10_000 });
    await pickCalendarDay(page.locator('.mud-popover-open'), repeat.start);
    await expect(page.locator('.mud-popover-open .mud-picker-calendar-header-transition')).toHaveCount(0);
  }
  for (const day of repeat.days ?? []) {
    await dialog.getByText(day, { exact: true }).click({ timeout: 10_000 });
  }
  await dialog.getByRole('button', { name: 'Save' }).click({ timeout: 10_000 });
  await expect(dialog).toBeHidden();
}

export async function addTaskAtFirstFreeSlot(page: Page): Promise<string> {
  const slots = page.getByRole('button', { name: /^Add a task at \d\d:\d\d$/ });
  const band = page.getByRole('button', { name: /^Show \d\d:\d\d/ }).first();
  await expect(slots.or(band).first()).toBeVisible();
  if ((await slots.count()) === 0) {
    await band.click();
  }
  const slot = slots.first();
  const time = (await slot.getAttribute('aria-label'))!.slice(-5);
  await slot.click();
  return time;
}

export async function setCustomLeadTime(page: Page, amount: number, unit: string) {
  await chooseProperty(page, 'Remind me', 'Custom…');
  const dialog = page.getByRole('dialog').filter({ hasText: 'Remind me' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText(/^How early the task shows in Coming up/)).toBeVisible();
  const field = dialog.getByRole('spinbutton').first();
  await field.fill(String(amount), { timeout: 10_000 });
  await field.press('Tab', { timeout: 10_000 });
  await dialog.getByRole('combobox').first().click({ timeout: 10_000 });
  await page.getByRole('option', { name: unit, exact: true }).click({ timeout: 10_000 });
  await expect(page.getByRole('option')).toHaveCount(0);
  await dialog.getByRole('button', { name: 'Save' }).click({ timeout: 10_000 });
  await expect(dialog).toBeHidden();
}

export async function setOneTime(page: Page) {
  await panel(page).getByText('One-time', { exact: true }).click();
  await expect(panel(page).getByRole('button', { name: /^Repeat\b/ })).toHaveCount(0);
}

export async function backToToday(page: Page) {
  await page.getByRole('button', { name: 'Today', exact: true }).click();
  await expect(dayLabel(page)).toContainText('Today');
}

export async function passUtcMidnight(page: Page) {
  const nextMidnight = utcToday().getTime() + 86_400_000;
  await page.clock.fastForward(nextMidnight - Date.now() + 90_000);
  await page.clock.fastForward(61_000);
}
