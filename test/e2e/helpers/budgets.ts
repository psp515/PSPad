import { expect, Locator, Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { Device } from './api';
import { addDays, isoDate, isoMonth, utcToday } from './dates';
import { goTo } from './nav';
import { escapeRegExp } from './text';
import { confirm, openFabAction, panel, savePanel } from './panels';

export type BudgetView = 'Overview' | 'Entries' | 'Balance' | 'Stats';

export async function createBudget(page: Page, name: string) {
  await goTo(page, 'Budgets');
  await page.getByRole('button', { name: 'New budget' }).first().click();
  await panel(page).getByPlaceholder('Budget name').fill(name);
  await savePanel(page);
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
}

export function budgetIdFromUrl(page: Page): string {
  return new URL(page.url()).pathname.split('/')[2];
}

export async function showView(page: Page, view: BudgetView) {
  await viewToggle(page, view).click();
}

export async function openBudgetSettings(page: Page) {
  await page.getByRole('button', { name: 'Budget settings' }).click();
  await expect(region(page, 'Budget')).toBeVisible();
}

export async function pickOption(page: Page, trigger: Locator, option: string) {
  await expect(page.getByRole('option')).toHaveCount(0);
  await trigger.click();
  await page.getByRole('option', { name: option, exact: true }).click();
  await expect(page.getByRole('option')).toHaveCount(0);
}

export async function addEntry(
  page: Page,
  kind: 'Expense' | 'Income',
  entry: { name: string; category: string; amount: string; currency?: string; rate?: string },
) {
  await page.getByRole('button', { name: kind, exact: true }).click();
  const form = panel(page);
  await form.getByRole('textbox', { name: 'Name', exact: true }).fill(entry.name);
  const category = form.getByRole('combobox', { name: 'Category', exact: true });
  await category.fill(entry.category);
  await page.getByRole('option').filter({ hasText: entry.category }).first().click();
  await expect(category).toHaveValue(entry.category);
  await form.getByRole('spinbutton', { name: 'Amount', exact: true }).fill(entry.amount);
  if (entry.currency) {
    await pickOption(page, form.getByRole('combobox', { name: 'Currency', exact: true }), entry.currency);
  }
  if (entry.rate) {
    await form.getByRole('spinbutton', { name: 'Rate to PLN', exact: true }).fill(entry.rate);
  }
  await savePanel(page);
  await expect(form).toHaveCount(0);
}

export async function addHolding(
  page: Page,
  holding: { name: string; type: 'Cash' | 'Savings' | 'Investment'; amount: string; currency?: string; rate?: string },
) {
  await openFabAction(page, 'Add holding');
  const form = panel(page);
  await form.getByRole('textbox', { name: 'Name', exact: true }).fill(holding.name);
  await form.getByRole('checkbox', { name: holding.type, exact: true }).click();
  await form.getByRole('spinbutton', { name: 'Amount', exact: true }).fill(holding.amount);
  if (holding.currency) {
    await pickOption(page, form.getByRole('combobox', { name: 'Currency', exact: true }), holding.currency);
  }
  if (holding.rate) {
    await form.getByRole('spinbutton', { name: 'Rate to PLN', exact: true }).fill(holding.rate);
  }
  await savePanel(page);
  await expect(form).toHaveCount(0);
}

export async function confirmIfAsked(page: Page) {
  const dialog = page.getByRole('dialog');
  if (await dialog.isVisible()) {
    await confirm(page);
  }
}

export function digitsOf(text: string): string {
  return text.replace(/\D/g, '');
}

export type EntrySetup = {
  kind: 'expense' | 'income';
  name: string;
  category: string;
  amount: number;
  date: Date;
  currency?: string;
  rate?: number;
};

export type HoldingSetup = {
  name: string;
  type: 'Cash' | 'Savings' | 'Investment';
  amount: number;
  currency?: string;
  rate?: number;
};

const holdingTypes = ['Cash', 'Savings', 'Investment'];

export function monthOf(day: Date, offset = 0): Date {
  return new Date(Date.UTC(day.getUTCFullYear(), day.getUTCMonth() + offset, 1));
}

export function lastDayOf(month: Date): Date {
  return new Date(Date.UTC(month.getUTCFullYear(), month.getUTCMonth() + 1, 0));
}

export async function createBudgetOn(device: Device, name: string): Promise<string> {
  const budgetId = randomUUID();
  await device.send('CreateBudget', { budgetId, name });
  return budgetId;
}

export async function recordOn(device: Device, budgetId: string, entries: EntrySetup[]) {
  for (const entry of entries) {
    await device.send(entry.kind === 'expense' ? 'RecordExpense' : 'RecordIncome', {
      entryId: randomUUID(),
      budgetId,
      name: entry.name,
      category: entry.category,
      money: { amount: entry.amount, currency: entry.currency ?? 'PLN', rateToPln: entry.rate ?? 1, rateDate: isoDate(entry.date) },
      date: isoDate(entry.date),
      note: null,
    });
  }
}

export async function startBalanceOn(device: Device, budgetId: string, month: Date, holdings: HoldingSetup[]) {
  const today = utcToday();
  const lastDay = lastDayOf(month);
  const rateDate = isoDate(lastDay < today ? lastDay : today);
  await device.send('StartBalanceMonth', { budgetId, month: isoMonth(month) });
  for (const holding of holdings) {
    await device.send('AddHolding', {
      budgetId,
      month: isoMonth(month),
      holdingId: randomUUID(),
      name: holding.name,
      type: holdingTypes.indexOf(holding.type),
      money: { amount: holding.amount, currency: holding.currency ?? 'PLN', rateToPln: holding.rate ?? 1, rateDate },
    });
  }
}

export async function stubNbp(page: Page, rates: Record<string, number> | 'down'): Promise<string[]> {
  const asked: string[] = [];
  await page.context().unroute('**/api/money/nbp-rate**');
  await page.context().route('**/api/money/nbp-rate**', route => {
    const currency = new URL(route.request().url()).searchParams.get('currency') ?? '';
    asked.push(currency);
    const asOf = new Date(`${new URL(route.request().url()).searchParams.get('date')}T00:00:00Z`);
    if (rates === 'down') {
      return route.fulfill({ status: 502 });
    }
    return currency in rates
      ? route.fulfill({ json: { currency, rate: rates[currency], effectiveDate: isoDate(nbpDayBefore(asOf)) } })
      : route.fulfill({ status: 404 });
  });
  return asked;
}

export function nbpDayBefore(day: Date): Date {
  let previous = addDays(day, -1);
  while (previous.getUTCDay() === 0 || previous.getUTCDay() === 6) {
    previous = addDays(previous, -1);
  }
  return previous;
}

export function region(page: Page, name: string): Locator {
  return page.getByRole('region', { name, exact: true });
}

export function figure(scope: Page | Locator, label: string): Locator {
  return scope.getByRole('group', { name: label, exact: true });
}

export function longDay(day: Date): string {
  const part = (options: Intl.DateTimeFormatOptions) => day.toLocaleString('en-US', { ...options, timeZone: 'UTC' });
  return `${part({ weekday: 'long' })}, ${day.getUTCDate()} ${part({ month: 'long' })}`;
}

export function ordinal(day: number): string {
  const suffix = day % 10 === 1 && day !== 11 ? 'st' : day % 10 === 2 && day !== 12 ? 'nd' : day % 10 === 3 && day !== 13 ? 'rd' : 'th';
  return `${day}${suffix}`;
}

export function shortRange(from: Date, to: Date): string {
  const format = (day: Date) => `${day.getUTCDate()} ${day.toLocaleString('en-GB', { month: 'short', timeZone: 'UTC' })} ${day.getUTCFullYear()}`;
  return `${format(from)} – ${format(to)}`;
}

export function entryRow(scope: Page | Locator, name: string): Locator {
  return scope.getByRole('option', { name: new RegExp(`^${escapeRegExp(name)} `) });
}

export function viewToggle(page: Page, view: BudgetView): Locator {
  return page.getByRole('checkbox', { name: view, exact: true });
}
