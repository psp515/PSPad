import { expect, Page } from '@playwright/test';
import { goTo } from './nav';
import { confirm, panel, savePanel } from './panels';

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
  await page.locator('.pspad-budget-views').getByText(view, { exact: true }).click();
}

export async function openBudgetSettings(page: Page) {
  await page.getByRole('button', { name: 'Budget settings' }).click();
  await expect(page.locator('.pspad-budget-card-settings')).toBeVisible();
}

async function pickOption(page: Page, trigger: import('@playwright/test').Locator, option: string) {
  await expect(page.locator('.mud-popover-open')).toHaveCount(0);
  await trigger.click();
  await page.locator('.mud-popover-open').getByText(option, { exact: true }).click();
  await expect(page.locator('.mud-popover-open')).toHaveCount(0);
}

export async function addEntry(
  page: Page,
  kind: 'Expense' | 'Income',
  entry: { name: string; category: string; amount: string; currency?: string; rate?: string },
) {
  await page.locator(kind === 'Expense' ? '.pspad-budget-quick-expense' : '.pspad-budget-quick-income').click();
  const form = panel(page);
  await form.locator('.pspad-entry-name-field input').fill(entry.name);
  const category = form.locator('.pspad-entry-category-field input');
  await category.fill(entry.category);
  await page.locator('.mud-popover-open').getByText(entry.category, { exact: false }).first().click();
  await expect(category).toHaveValue(entry.category);
  await form.locator('.pspad-entry-amount-field input').fill(entry.amount);
  if (entry.currency) {
    await pickOption(page, form.locator('.pspad-entry-currency-field'), entry.currency);
  }
  if (entry.rate) {
    await form.locator('.pspad-entry-rate-field input').fill(entry.rate);
  }
  await savePanel(page);
  await expect(form.locator('.pspad-panel-title')).toBeHidden();
}

export async function addHolding(
  page: Page,
  holding: { name: string; type: 'Cash' | 'Savings' | 'Investment'; amount: string; currency?: string; rate?: string },
) {
  await page.locator('.pspad-fab').getByRole('button').click();
  await page.locator('.pspad-fab-add-holding').click();
  const form = panel(page);
  await form.locator('.pspad-holding-name-field input').fill(holding.name);
  await form.locator('.pspad-holding-type-field').getByText(holding.type, { exact: true }).click();
  await form.locator('.pspad-holding-panel-amount-field input').fill(holding.amount);
  if (holding.currency) {
    await pickOption(page, form.locator('.pspad-holding-panel-currency-field'), holding.currency);
  }
  if (holding.rate) {
    await form.locator('.pspad-holding-panel-rate-field input').fill(holding.rate);
  }
  await savePanel(page);
  await expect(form.locator('.pspad-panel-title')).toBeHidden();
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
