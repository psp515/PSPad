import { expect, test } from '../fixtures/accounts';
import { addEntry, confirmIfAsked, createBudget, digitsOf, openBudgetSettings, showView } from '../helpers/budgets';
import { addDays, utcToday } from '../helpers/dates';
import { signIn } from '../helpers/sign-in';

function monthTitle(day: Date): string {
  return day.toLocaleString('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' });
}

test('a budget sums a month in PLN and archives read-only', async ({ page, account }) => {
  const budget = `Household ${account.suffix}`;
  const today = utcToday();
  const previousMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth() - 1, 1));

  await signIn(page, account);
  await createBudget(page, budget);

  await addEntry(page, 'Expense', { name: 'Groceries', category: 'Food', amount: '84.20' });
  await addEntry(page, 'Income', { name: 'Salary', category: 'Salary', amount: '100', currency: 'EUR', rate: '4.25' });

  await showView(page, 'Entries');
  const rows = page.locator('.pspad-entry-row');
  await expect(rows.filter({ hasText: 'Groceries' })).toBeVisible();
  await expect(rows.filter({ hasText: 'Salary' })).toContainText('EUR');

  await showView(page, 'Overview');
  const hero = page.locator('.pspad-overview-hero');
  await expect.poll(async () => digitsOf(await hero.locator('.pspad-overview-net').innerText())).toBe('34080');
  await expect(hero).toContainText('Income');
  await expect(hero).toContainText('Expenses');
  await expect(hero).toContainText('425.00 PLN');
  await expect(hero).toContainText('84.20 PLN');

  await showView(page, 'Balance');
  await expect(page.locator('.pspad-balance-missing')).toContainText(monthTitle(today));
  await page.locator('.pspad-month-more').click();
  const sheet = page.locator('.pspad-month-sheet');
  if ((await sheet.locator('.pspad-month-sheet-year').innerText()).trim() !== String(previousMonth.getUTCFullYear())) {
    await sheet.locator('.pspad-month-sheet-previous').click();
  }
  await sheet.locator('.pspad-month-sheet-month').nth(previousMonth.getUTCMonth()).click();
  await expect(page.locator('.pspad-balance-missing')).toContainText(monthTitle(previousMonth));
  await page.locator('.pspad-month-today').click();
  await expect(page.locator('.pspad-balance-missing')).toContainText(monthTitle(today));
  await showView(page, 'Overview');
  await expect.poll(async () => digitsOf(await hero.locator('.pspad-overview-net').innerText())).toBe('34080');

  await openBudgetSettings(page);
  await page.locator('.pspad-archive-budget').click();
  await confirmIfAsked(page);
  await expect(page.locator('.pspad-archived-banner')).toBeVisible();
  await showView(page, 'Overview');
  await expect(page.locator('.pspad-budget-quick-expense')).toHaveCount(0);
  await expect(page.locator('.pspad-budget-quick-income')).toHaveCount(0);
  await expect(page.locator('.pspad-archived-banner')).toBeVisible();
  void addDays;
});
