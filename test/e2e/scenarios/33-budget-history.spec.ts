import { expect, test } from '../fixtures/accounts';
import { anotherDevice } from '../helpers/api';
import {
  createBudgetOn, entryRow, figure, monthOf, nbpDayBefore, openBudgetSettings, recordOn, region, shortRange, showView, startBalanceOn, stubNbp,
} from '../helpers/budgets';
import { addDays, isoDate, isoMonth, monthButton, monthTitle, utcToday } from '../helpers/dates';
import { confirm } from '../helpers/panels';
import { goTo } from '../helpers/nav';
import { signIn } from '../helpers/sign-in';

function csv(name: string, lines: string[]) {
  return { name, mimeType: 'text/csv', buffer: Buffer.from(lines.join('\n') + '\n', 'utf8') };
}

test('stats average the months with entries and chart them, and history arrives from CSV', async ({ page, account }) => {
  const budget = `Stats ${account.suffix}`;
  const today = utcToday();
  const lastMonth = monthOf(today, -1);
  const twoBack = monthOf(today, -2);
  const sixBack = monthOf(today, -6);
  const lastYear = today.getUTCFullYear() - 1;
  const thisMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 3));
  const byMonth = page.getByRole('table', { name: 'Income vs expenses by month' });
  const preset = (name: string) => page.getByRole('button', { name, exact: true });
  const range = (from: Date, to: Date, months: number) =>
    page.getByText(`${shortRange(from, to)} · ${months} month${months === 1 ? '' : 's'} with entries`, { exact: true });

  await signIn(page, account);
  const device = await anotherDevice(page);
  const budgetId = await createBudgetOn(device, budget);
  await goTo(page, 'Budgets');
  await page.getByRole('link', { name: budget, exact: true }).first().click();
  await showView(page, 'Stats');

  await test.step('an empty range reads as dashes, with no forecast, ranking or balances, and the monthly table at zero', async () => {
    await expect(preset('Last 6 months')).toHaveAttribute('aria-pressed', 'true');
    for (const other of ['This year', 'Last 12 months', 'Last year', 'Custom']) {
      await expect(preset(other)).toHaveAttribute('aria-pressed', 'false');
    }
    await expect(range(sixBack, addDays(monthOf(today), -1), 0)).toBeVisible();
    for (const tile of ['Avg income / month', 'Avg expenses / month', 'Avg saved / month', 'Savings rate']) {
      await expect(figure(page, tile)).toContainText('—');
    }
    await expect(region(page, `${monthTitle(monthOf(today, 1))} forecast`))
      .toContainText('A forecast needs at least one full month of entries.');
    await expect(page.getByText('No expenses in this range.')).toHaveCount(2);
    await expect(page.getByText('No balances in this range.')).toBeVisible();
    await expect(byMonth.getByRole('row')).toHaveCount(7);
    await expect(byMonth.getByRole('row', { name: `${monthTitle(lastMonth)} 0.00 PLN 0.00 PLN 0.00 PLN` })).toBeVisible();
  });

  await test.step('entries from another device average over the months that have them, forecast next month and rank where money goes', async () => {
    await recordOn(device, budgetId, [
      { kind: 'income', name: 'Pay', category: 'Salary', amount: 1000, date: twoBack },
      { kind: 'expense', name: 'Rent', category: 'Home', amount: 300, date: twoBack },
      { kind: 'expense', name: 'Bread', category: 'Food', amount: 50, date: twoBack },
      { kind: 'expense', name: 'Fuel', category: 'Car', amount: 20, date: twoBack },
      { kind: 'expense', name: 'Mouse', category: 'PC', amount: 15, date: twoBack },
      { kind: 'expense', name: 'Card', category: 'Gifts', amount: 10, date: twoBack },
      { kind: 'expense', name: 'Socks', category: 'Clothes', amount: 5, date: twoBack },
      { kind: 'income', name: 'Pay', category: 'Salary', amount: 3000, date: lastMonth },
      { kind: 'expense', name: 'Rent', category: 'Home', amount: 600, date: lastMonth },
    ]);
    await startBalanceOn(device, budgetId, twoBack, [{ name: 'Wallet', type: 'Cash', amount: 100 }]);

    await expect(range(sixBack, addDays(monthOf(today), -1), 2)).toBeVisible();
    await expect(figure(page, 'Avg income / month')).toContainText('2,000.00 PLN');
    await expect(figure(page, 'Avg income / month')).toContainText('lowest 1,000.00 · highest 3,000.00');
    await expect(figure(page, 'Avg expenses / month')).toContainText('500.00 PLN');
    await expect(figure(page, 'Avg saved / month')).toContainText('+1,500.00 PLN');
    await expect(figure(page, 'Avg saved / month')).toContainText('saved in 2 of 2 months');
    await expect(figure(page, 'Savings rate')).toContainText('75%');

    const forecast = region(page, `${monthTitle(monthOf(today, 1))} forecast`);
    await expect(forecast).toContainText('avg of last 2 months');
    await expect(figure(forecast, 'Expected expenses')).toContainText('≈ 500.00 PLN');
    await expect(figure(forecast, 'Expected income')).toContainText('≈ 2,000.00 PLN');
    await expect(figure(forecast, 'Expected saved')).toContainText('≈ +1,500.00 PLN');
    await expect(forecast.getByRole('button')).toHaveText(['Home', 'Food', 'Car', 'PC', 'Gifts', 'Clothes']);
    await expect(forecast).toContainText('450.00 PLN');

    const ranking = region(page, 'Where money goes');
    await expect(ranking.getByRole('button')).toHaveText(['Home', 'Food', 'Car', 'PC', 'Gifts', 'Clothes']);
    await expect(ranking).toContainText('90%');
    await expect(ranking).toContainText('900.00 PLN');

    await expect(byMonth.getByRole('row', { name: `${monthTitle(lastMonth)} 3,000.00 PLN 600.00 PLN +2,400.00 PLN` })).toBeVisible();
    const byCategory = page.getByRole('table', { name: 'Spend by category per month' });
    await expect(byCategory.getByRole('row').first()).toHaveAccessibleName('Month Home Food Car PC Other');
    await expect(byCategory.getByRole('row', { name: `${monthTitle(twoBack)} 300.00 PLN 50.00 PLN 20.00 PLN 15.00 PLN 15.00 PLN` }))
      .toBeVisible();
    const netWorth = page.getByRole('table', { name: 'Net worth by holding type' });
    await expect(netWorth.getByRole('row', { name: `${monthTitle(twoBack)} 100.00 PLN 0.00 PLN 0.00 PLN 100.00 PLN` })).toBeVisible();
    await expect(netWorth.getByRole('row', { name: `${monthTitle(lastMonth)} — — — —` })).toBeVisible();
  });

  await test.step('presets pick their range, picking one again keeps it, and Custom offers a range picker', async () => {
    const inLastYear = [twoBack, lastMonth].filter(month => month.getUTCFullYear() === lastYear).length;
    await preset('Last year').click();
    await expect(preset('Last year')).toHaveAttribute('aria-pressed', 'true');
    await expect(preset('Last 6 months')).toHaveAttribute('aria-pressed', 'false');
    await expect(range(new Date(Date.UTC(lastYear, 0, 1)), new Date(Date.UTC(lastYear, 11, 31)), inLastYear)).toBeVisible();
    await expect(byMonth.getByRole('row')).toHaveCount(13);
    await preset('Last year').click();
    await expect(preset('Last year')).toHaveAttribute('aria-pressed', 'true');
    await expect(range(new Date(Date.UTC(lastYear, 0, 1)), new Date(Date.UTC(lastYear, 11, 31)), inLastYear)).toBeVisible();

    const thisYear = [twoBack, lastMonth].filter(month => month.getUTCFullYear() === today.getUTCFullYear()).length;
    await preset('This year').click();
    await expect(range(new Date(Date.UTC(today.getUTCFullYear(), 0, 1)), today, thisYear)).toBeVisible();
    await preset('Last 12 months').click();
    await expect(range(monthOf(today, -11), today, 2)).toBeVisible();
    await preset('Custom').click();
    const picked = (day: Date) => day.toLocaleDateString('en-GB', { timeZone: 'UTC' });
    await expect(page.getByRole('textbox', { name: 'Start date' })).toHaveValue(picked(monthOf(today, -11)));
    await expect(page.getByRole('textbox', { name: 'End date' })).toHaveValue(picked(today));
    await expect(range(monthOf(today, -11), today, 2)).toBeVisible();
  });

  await test.step('a ranked or forecast category opens its entries', async () => {
    await region(page, 'Where money goes').getByRole('button', { name: 'Food' }).click();
    await expect(page).toHaveURL(new RegExp(`/budgets/${budgetId}/entries$`));
    await expect(page.getByText('Food', { exact: true }).getByRole('button', { name: 'Close' })).toBeVisible();
    await expect(page.getByText(`No entries in ${monthTitle(today)}.`)).toBeVisible();
    await showView(page, 'Stats');
    await region(page, `${monthTitle(monthOf(today, 1))} forecast`).getByRole('button', { name: 'Home' }).click();
    await expect(page.getByText('Home', { exact: true }).getByRole('button', { name: 'Close' })).toBeVisible();
  });

  await test.step('an entries file previews ready and error rows, new categories and rate sources, retries a missing rate, and leaving asks first', async () => {
    await stubNbp(page, 'down');
    await openBudgetSettings(page);
    await expect(page.getByRole('link', { name: 'Expenses and incomes template' })).toHaveAttribute('href', 'templates/pspad-entries.csv');
    await expect(page.getByRole('link', { name: 'Balances template' })).toHaveAttribute('href', 'templates/pspad-balances.csv');
    await page.getByRole('button', { name: 'Upload expenses and incomes' }).click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(`Import expenses and incomes into ${budget}`);
    await expect(page.getByRole('link', { name: 'Download the template' })).toHaveAttribute('href', 'templates/pspad-entries.csv');
    await page.locator('input[type=file]').setInputFiles(csv('entries.csv', [
      'kind,date,name,category,amount,currency,rate,note',
      `expense,${isoDate(thisMonth)},Biedronka,Food,84.20,PLN,,`,
      `income,${isoDate(thisMonth)},Salary,Salary,9500.00,PLN,,September`,
      `refund,${isoDate(thisMonth)},Oops,Food,1,PLN,,`,
      `expense,${isoDate(today)},Lego,Hobby,10,PLN,,`,
      `expense,${isoDate(today)},Steam,Games,19.99,EUR,,`,
      `expense,${isoDate(today)},Own,PC,5,EUR,4.5,`,
    ]));
    const steam = page.getByRole('row', { name: /Steam/ });
    await expect(steam).toContainText(`No NBP rate for EUR on ${today.toLocaleDateString('en-GB', { timeZone: 'UTC' })}.`);
    await stubNbp(page, { EUR: 4.4 });
    await page.getByRole('button', { name: 'Retry rates' }).click();
    await expect(steam).toContainText(`4.4 (NBP ${isoDate(nbpDayBefore(today))})`);
    for (const chip of ['To import 5', 'Errors 1', 'Already in budget 0', 'New categories 2', 'Rates from NBP 1']) {
      await expect(page.getByText(chip, { exact: true })).toBeVisible();
    }
    await expect(page.getByText('New expense categories')).toBeVisible();
    await expect(page.getByText('Hobby', { exact: true })).toHaveCount(2);
    await expect(page.getByRole('row', { name: '4 Kind must be expense or income.', exact: true })).toBeVisible();
    await expect(page.getByRole('row', { name: /Own/ })).toContainText('4.5 (file)');

    await page.getByRole('button', { name: 'Errors', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Errors', exact: true })).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('row')).toHaveCount(2);
    await page.getByRole('button', { name: 'All', exact: true }).click();
    await expect(page.getByRole('row')).toHaveCount(7);

    await page.getByRole('link', { name: 'Back to settings' }).click();
    await expect(page.getByRole('dialog')).toContainText('Leave the import? The file you picked won’t be imported.');
    await confirm(page, 'Cancel');
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(`Import expenses and incomes into ${budget}`);

    await page.getByRole('button', { name: 'Import 5 rows' }).click();
    await expect(page.getByText('Imported 5 entries and 0 holdings in 0 months.')).toBeVisible();
  });

  await page.getByRole('link', { name: 'Back to settings' }).click();
  await page.getByRole('button', { name: 'Upload balances' }).click();

  await test.step('leaving before a file is picked asks nothing; a balances file without an amount column says so, and a good one groups its rows under their month', async () => {
    await page.getByRole('link', { name: 'Back to settings' }).click();
    await page.getByRole('button', { name: 'Upload balances' }).click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(`Import balances into ${budget}`);
    await page.locator('input[type=file]').setInputFiles(csv('broken.csv', ['month,holding,type', `${isoMonth(lastMonth)},Wallet,cash`]));
    await expect(page.getByText('The file has no amount column.')).toBeVisible();
    await page.locator('input[type=file]').last().setInputFiles(csv('balances.csv', [
      'month,holding,type,amount,currency,rate',
      `${isoMonth(lastMonth)},Wallet,cash,300.00,PLN,`,
      `${isoMonth(lastMonth)},ETF,investment,2000.00,EUR,4.2000`,
    ]));
    await expect(page.getByText('To import 2', { exact: true })).toBeVisible();
    await expect(page.getByText(`${isoMonth(lastMonth)} · New month: 2 holdings`)).toBeVisible();
    await page.getByRole('button', { name: 'Import 2 rows' }).click();
    await expect(page.getByText(/^Imported 0 entries and 2 holdings in 1 month/)).toBeVisible();
  });

  await page.getByRole('link', { name: 'Back to settings' }).click();
  await showView(page, 'Entries');
  await expect(entryRow(page, 'Biedronka')).toBeVisible();
  await expect(entryRow(page, 'Salary')).toBeVisible();
  await showView(page, 'Balance');
  await monthButton(page, lastMonth).click();
  await expect(figure(page, 'Net worth')).toContainText('8,700.00 PLN');

  await test.step('an unknown import says so', async () => {
    await page.goto(`/budgets/${budgetId}/import/receipts`);
    await expect(page.getByText('That import does not exist.')).toBeVisible();
  });

  await device.dispose();
});
