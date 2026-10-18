import { expect, Page } from '@playwright/test';
import { goTo } from './nav';
import { panel } from './panels';
import { shortDate } from './dates';

export async function goToMyDay(page: Page) {
  await goTo(page, 'My Day');
}

export function daySection(page: Page, section: 'overdue' | 'anytime' | 'starred' | 'comingup' | 'completed') {
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
    .locator('.mud-picker-calendar-day:not(.mud-hidden) button, button.mud-picker-calendar-day')
    .filter({ hasText: new RegExp(`^${day.getUTCDate()}$`) })
    .first()
    .click();
}

export async function setRepeating(page: Page) {
  await panel(page).locator('.pspad-task-kind').getByText('Repeating', { exact: true }).click();
  await expect(panel(page).locator('.pspad-task-repeat')).toBeVisible();
}

export async function pickWeekDay(page: Page, day: Date) {
  await page.getByRole('button', { name: shortDate(day), exact: true }).click();
}
