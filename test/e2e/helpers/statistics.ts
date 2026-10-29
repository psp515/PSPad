import { Page } from '@playwright/test';
import { isoDate } from './dates';

export function statTile(page: Page, label: string) {
  return page.getByRole('group', { name: label, exact: true }).getByRole('heading', { level: 5 });
}

export function statRange(page: Page, days: 30 | 90 | 365) {
  return page.getByRole('checkbox', { name: `${days} days`, exact: true });
}

export function heatmapDays(page: Page) {
  return page.getByRole('button', { name: /^\d{4}-\d{2}-\d{2}: \d+ completed$/ });
}

export function heatmapDay(page: Page, day: Date) {
  return page.getByRole('button', { name: new RegExp(`^${isoDate(day)}: \\d+ completed$`) });
}

export function heatmapMonth(day: Date) {
  return day.toLocaleString('en-US', { month: 'short', year: 'numeric', timeZone: 'UTC' });
}

export function heatmapMonthGroup(page: Page, day: Date) {
  return page.getByRole('group', { name: heatmapMonth(day), exact: true });
}

export function readoutDay(day: Date) {
  return `${day.getUTCDate()} ${heatmapMonth(day).split(' ')[0]} ${day.getUTCFullYear()}`;
}

export function capturesTable(page: Page) {
  return page.getByRole('table', { name: 'Captured into the Inbox each week', exact: true });
}

export function recordFeed(page: Page) {
  return page.getByText('Everything that happened', { exact: true });
}
