import { expect, Page } from '@playwright/test';
import { chooseProperty } from './panels';
import { clickClockNumber } from './today';

export async function setCustomReminder(page: Page, days: number, hour: number, minute: number) {
  await chooseProperty(page, 'Remind me', 'Custom…');
  const dialog = page.getByRole('dialog').filter({ hasText: 'Remind me' });
  await expect(dialog).toBeVisible();
  const field = dialog.getByRole('spinbutton', { name: 'Days before' });
  await field.fill(String(days));
  await field.press('Tab');
  await dialog.getByRole('textbox', { name: 'Time' }).click();
  const clock = page.getByRole('dialog', { name: 'Time', exact: true });
  await expect(clock).toBeVisible();
  await clickClockNumber(page, clock.locator('.mud-time-picker-hour'), hour);
  await expect(clock.locator('.mud-time-picker-minute')).not.toHaveClass(/mud-time-picker-dial-hidden/);
  await clickClockNumber(page, clock.locator('.mud-time-picker-minute'), minute);
  await expect(clock).toBeHidden();
  await dialog.getByRole('button', { name: 'Save' }).click();
  await expect(dialog).toBeHidden();
}
