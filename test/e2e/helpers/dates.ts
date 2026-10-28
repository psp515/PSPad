import type { Locator, Page } from '@playwright/test';
import { escapeRegExp } from './text';

const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const weekdays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

export function utcToday(): Date {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
}

export function addDays(day: Date, days: number): Date {
  return new Date(day.getTime() + days * 86_400_000);
}

export function shortDate(day: Date): string {
  return `${weekdays[day.getUTCDay()]}, ${day.getUTCDate()} ${months[day.getUTCMonth()]}`;
}

export function isoDate(day: Date): string {
  return day.toISOString().slice(0, 10);
}

export function isoMonth(day: Date): string {
  return isoDate(day).slice(0, 7);
}

export function shiftedZone(): { id: string; localDate: Date } {
  const today = utcToday();
  return new Date().getUTCHours() >= 10
    ? { id: 'Pacific/Kiritimati', localDate: addDays(today, 1) }
    : { id: 'Pacific/Pago_Pago', localDate: addDays(today, -1) };
}

export function dayMonth(day: Date): string {
  return `${day.getUTCDate()} ${months[day.getUTCMonth()]}`;
}

export function utcTime(): string {
  return new Date().toISOString().slice(11, 16);
}

export function weekdayShort(day: Date): string {
  return weekdays[day.getUTCDay()];
}

export function weekdaysInWeekOrder(days: string[]): string[] {
  const order = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  return [...new Set(days)].sort((a, b) => order.indexOf(a) - order.indexOf(b));
}

export function shiftTime(time: string, minutes: number): string {
  const [hours, mins] = time.split(':').map(Number);
  const total = (((hours * 60 + mins + minutes) % 1440) + 1440) % 1440;
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;
}

export function monthTitle(day: Date): string {
  return day.toLocaleString('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' });
}

export function monthButton(scope: Page | Locator, day: Date) {
  return scope.getByRole('button', { name: new RegExp(`^${escapeRegExp(monthTitle(day))}(,|$)`) });
}
