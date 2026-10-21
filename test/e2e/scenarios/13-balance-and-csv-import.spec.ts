import { expect, test } from '../fixtures/accounts';
import { addHolding, createBudget, digitsOf, openBudgetSettings, showView } from '../helpers/budgets';
import { isoDate, isoMonth, utcToday } from '../helpers/dates';
import { signIn } from '../helpers/sign-in';

function csv(name: string, lines: string[]) {
  return { name, mimeType: 'text/csv', buffer: Buffer.from(lines.join('\n') + '\n', 'utf8') };
}

test('a balance month is started by hand and history arrives from CSV', async ({ page, account }) => {
  const budget = `Savings ${account.suffix}`;
  const today = utcToday();
  const thisMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 3));
  const previousMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth() - 1, 1));

  await signIn(page, account);
  await createBudget(page, budget);

  await showView(page, 'Balance');
  await page.locator('.pspad-start-month').click();
  await expect(page.locator('.pspad-balance-net-worth')).toBeVisible();
  await addHolding(page, { name: 'Wallet', type: 'Cash', amount: '350' });
  await addHolding(page, { name: 'ETF', type: 'Investment', amount: '2500', currency: 'EUR', rate: '4.265' });
  await expect.poll(async () => digitsOf(await page.locator('.pspad-balance-net-worth').innerText())).toBe('1101250');
  await expect(page.locator('.pspad-holding-group-name')).toHaveCount(2);

  await openBudgetSettings(page);
  await page.getByRole('button', { name: 'Upload expenses and incomes' }).click();
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Import');
  await page.locator('input[type=file]').setInputFiles(csv('entries.csv', [
    'kind,date,name,category,amount,currency,rate,note',
    `expense,${isoDate(thisMonth)},Biedronka,Food,84.20,PLN,,`,
    `income,${isoDate(thisMonth)},Salary,Salary,9500.00,PLN,,September`,
  ]));
  await expect(page.locator('.pspad-import-chips')).toContainText('To import 2');
  await page.locator('.pspad-import-run').click();
  await expect(page.locator('.pspad-import-summary')).toContainText('Imported 2 entries and 0 holdings in 0 months.');

  await page.getByRole('link', { name: 'Back to settings' }).click();
  await page.getByRole('button', { name: 'Upload balances' }).click();
  await page.locator('input[type=file]').setInputFiles(csv('balances.csv', [
    'month,holding,type,amount,currency,rate',
    `${isoMonth(previousMonth)},Wallet,cash,300.00,PLN,`,
    `${isoMonth(previousMonth)},ETF,investment,2000.00,EUR,4.2000`,
  ]));
  await expect(page.locator('.pspad-import-chips')).toContainText('To import 2');
  await page.locator('.pspad-import-run').click();
  await expect(page.locator('.pspad-import-summary')).toContainText('Imported 0 entries and 2 holdings in 1 month');

  await page.getByRole('link', { name: 'Back to settings' }).click();
  await showView(page, 'Entries');
  await expect(page.locator('.pspad-entry-row').filter({ hasText: 'Biedronka' })).toBeVisible();
  await expect(page.locator('.pspad-entry-row').filter({ hasText: 'Salary' })).toBeVisible();

  await showView(page, 'Balance');
  await page.getByRole('button', {
    name: previousMonth.toLocaleString('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' }),
    exact: true,
  }).click();
  await expect.poll(async () => digitsOf(await page.locator('.pspad-balance-net-worth').innerText())).toBe('870000');
});
