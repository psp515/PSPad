import { expect, Locator, Page } from '@playwright/test';
import { goTo, navLink } from './nav';
import { awaitNoDrawer, chooseProperty, panel, propertyRow } from './panels';
import { shortDate, utcToday } from './dates';
import { escapeRegExp } from './text';

export async function goToMyDay(page: Page) {
  await goTo(page, 'My Day');
}

const sections = {
  overdue: 'Overdue',
  scheduled: 'Scheduled',
  anytime: /, any time$/,
  starred: 'Starred',
  completed: 'Completed',
} as const;

export function daySection(page: Page, section: keyof typeof sections) {
  const name = sections[section];
  return page.getByRole('region', typeof name === 'string' ? { name, exact: true } : { name });
}

export function dayLabel(page: Page, text: string | RegExp) {
  return page.getByText(text, { exact: true });
}

export function dayShown(page: Page, day: Date) {
  return dayLabel(page, new RegExp(`^(\\w+ · )?${escapeRegExp(shortDate(day))}$`));
}

export async function setDueDate(page: Page, day: 'Today' | 'Tomorrow' | Date) {
  if (day instanceof Date) {
    await chooseProperty(page, 'Due', 'Pick a date…');
    const dialog = page.getByRole('dialog');
    await pickCalendarDay(dialog, day);
    await expect(dialog).toBeHidden();
  } else {
    await chooseProperty(page, 'Due', day);
  }
}

export async function pickCalendarDay(calendar: Locator | Page, day: Date) {
  const wanted = day.toLocaleString('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' });
  const shown = calendar.getByRole('button', { name: /^[A-Z][a-z]+ \d{4}$/ });
  const previous = calendar.getByRole('button', { name: /^Previous month \(/ });
  const next = calendar.getByRole('button', { name: /^Next month \(/ });
  for (let step = 0; step < 3 && (await shown.innerText()).trim() !== wanted; step++) {
    const showing = new Date(`1 ${(await shown.innerText()).trim()} UTC`);
    await (showing.getTime() > day.getTime() ? previous : next).click();
  }
  await expect(shown).toHaveText(wanted);
  const date = day.getUTCDate();
  const matching = calendar.getByRole('button', { name: String(date), exact: true });
  await (date < 15 ? matching.first() : matching.last()).click();
}

export function calendarOpen(page: Page) {
  return page.getByRole('button', { name: /^Next month \(/ });
}

export async function setTaskTime(page: Page, start: string, end?: string) {
  const startInput = timeField(page, 'Start');
  await startInput.fill(start);
  await startInput.press('Tab');
  await expect(startInput).toHaveValue(start);
  await expect(panel(page).getByRole('button', { name: 'Clear time', exact: true })).toBeVisible();
  if (end) {
    const endInput = timeField(page, 'End');
    await endInput.fill(end);
    await endInput.press('Tab');
    await expect(endInput).toHaveValue(end);
  }
}

export function timeField(page: Page, which: 'Start' | 'End') {
  return panel(page).getByRole('textbox', { name: which, exact: true });
}

export async function typeTaskTime(page: Page, which: 'Start' | 'End', text: string) {
  const field = timeField(page, which);
  await field.fill(text);
  await field.press('Enter');
}

async function openClock(page: Page, which: 'Start' | 'End') {
  await panel(page).getByRole('button', { name: `Pick ${which.toLowerCase()} time` }).click();
  const clock = page.locator('.mud-picker .mud-overlay .mud-picker-paper');
  await expect(clock).toBeVisible();
  return clock;
}

export async function leaveClockUntouched(page: Page, which: 'Start' | 'End') {
  const clock = await openClock(page, which);
  await page.mouse.click(5, 5);
  await expect(clock).toBeHidden();
}

export async function pickClockHourAndLeave(page: Page, which: 'Start' | 'End', hour: number) {
  const clock = await openClock(page, which);
  await clickClockNumber(page, clock.locator('.mud-time-picker-hour'), hour);
  await expect(clock.locator('.mud-time-picker-minute')).not.toHaveClass(/mud-time-picker-dial-hidden/);
  await page.mouse.click(5, 5);
  await expect(clock).toBeHidden();
}

export async function pickTaskTimeFromClock(page: Page, which: 'start' | 'end', hour: number, minute: number) {
  const clock = await openClock(page, which === 'start' ? 'Start' : 'End');
  await clickClockNumber(page, clock.locator('.mud-time-picker-hour'), hour);
  await expect(clock.locator('.mud-time-picker-minute')).not.toHaveClass(/mud-time-picker-dial-hidden/);
  await clickClockNumber(page, clock.locator('.mud-time-picker-minute'), minute);
  await expect(clock).toBeHidden();
  const text = `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`;
  await expect(timeField(page, which === 'start' ? 'Start' : 'End')).toHaveValue(text);
}

// The outer-ring hour sticks are zero-width, so Playwright never sees them as visible; a user clicks the number instead.
async function clickClockNumber(page: Page, dial: import('@playwright/test').Locator, value: number) {
  const label = dial.locator('.mud-clock-number', { hasText: new RegExp(`^${String(value).padStart(2, '0')}$`) });
  await expect(label).toBeVisible();
  const box = (await label.boundingBox())!;
  await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
}

export function schedule(page: Page) {
  return page.getByRole('region', { name: 'Schedule', exact: true });
}

export function timelineBlock(page: Page, name: string) {
  return schedule(page).getByRole('article', { name });
}

export async function setRepeating(page: Page) {
  await panel(page).getByRole('checkbox', { name: 'Repeating', exact: true }).click();
  await expect(propertyRow(page, 'Repeat')).toBeVisible();
}

export async function showDay(page: Page, day: Date) {
  await page.getByRole('button', { name: 'Pick a date', exact: true }).click();
  await pickCalendarDay(page.getByRole('dialog'), day);
  await expect(dayShown(page, day)).toBeVisible();
}

export function weekDay(page: Page, day: Date) {
  return page.getByRole('button', { name: new RegExp(`^${escapeRegExp(shortDate(day))}(,|$)`) });
}

export async function swipeWeek(page: Page, towards: 'left' | 'right') {
  const box = (await weekDay(page, utcToday()).or(page.getByRole('button', { pressed: true })).first().boundingBox())!;
  const y = box.y + box.height / 2;
  const from = box.x + box.width / 2;
  const to = towards === 'left' ? from - 120 : from + 120;
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 1 });
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: from, y }] });
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: (from + to) / 2, y }] });
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: to, y }] });
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  await cdp.send('Emulation.setTouchEmulationEnabled', { enabled: false });
  await cdp.detach();
}

export async function pickWeekDay(page: Page, day: Date) {
  await weekDay(page, day).click();
}

export function myDayLink(page: Page) {
  return navLink(page, 'My Day');
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
    await pickCalendarDay(page, repeat.start);
    await expect(calendarOpen(page)).toHaveCount(0);
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
  await expect(dayLabel(page, /^Today · /)).toBeVisible();
}

export async function passUtcMidnight(page: Page) {
  const nextMidnight = utcToday().getTime() + 86_400_000;
  await page.clock.fastForward(nextMidnight - Date.now() + 90_000);
  await page.clock.fastForward(61_000);
}
