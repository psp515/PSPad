import { expect, test } from '../fixtures/accounts';
import {
  dollarAccount, openInvestments, plansAccount, stubNbpSeries, stubQuotes, tradesAccount, xtbDollarStatement, xtbProductStatement,
} from '../helpers/investments';
import { pickOption } from '../helpers/budgets';
import { shellMenu } from '../helpers/shell';
import { signIn } from '../helpers/sign-in';

test.use({ serviceWorkers: 'block' });

test('an XTB file with a second product feeds two new accounts, and the account stats show money put in, moved between own accounts, and the realized result; a USD account adds its value in PLN', async ({ page, account }) => {
  const tradesName = `My Trades ${tradesAccount}`;
  const plansName = `Investment Plans ${plansAccount}`;

  await signIn(page, account);
  await stubQuotes(page);

  await test.step('the preview groups the rows per target account and proposes an account for each product', async () => {
    await openInvestments(page);
    await shellMenu(page).getByRole('link', { name: 'Import from XTB' }).click();
    await expect(page.getByRole('heading', { name: 'Import from XTB', exact: true })).toBeVisible();
    await page.locator('input[type=file]').setInputFiles(xtbProductStatement());
    await expect(page.getByText(`Account ${tradesAccount} · 7 rows`)).toBeVisible();
    await expect(page.getByText('Errors · 0')).toBeVisible();
    await expect(page.getByText(`No account has number ${tradesAccount} yet.`)).toBeVisible();
    await expect(page.getByText(`No account has number ${plansAccount} yet.`)).toBeVisible();
    const names = page.getByRole('textbox', { name: 'Name' });
    await expect(names).toHaveCount(2);
    await expect(names.nth(0)).toHaveValue(tradesName);
    await expect(names.nth(1)).toHaveValue(plansName);
    await expect(page.getByText('Rows: 5', { exact: true })).toBeVisible();
    await expect(page.getByText('Rows: 2', { exact: true })).toBeVisible();
  });

  await test.step('the import creates both accounts, and the Investment Plans account holds its own position', async () => {
    await page.getByRole('button', { name: 'Create account and import 7 operations' }).click();
    await expect(page.getByRole('heading', { name: 'Investments', exact: true })).toBeVisible();
    const accounts = page.getByRole('navigation', { name: 'Accounts' });
    await expect(accounts.getByRole('link', { name: new RegExp(`^${tradesName}`) })).toBeVisible();
    await accounts.getByRole('link', { name: new RegExp(`^${plansName}`) }).click();
    await expect(page.getByRole('button', { name: /^ZTA\.PL / })).toBeVisible();
    await expect(page.getByRole('button', { name: /^KPA\.PL / })).toHaveCount(0);
    await expect(page.getByText('Moved in', { exact: true }).locator('..')).toContainText('1,000.00 PLN');
    await expect(page.getByText('Put in', { exact: true }).locator('..')).toContainText(/(^|\s)0\.00 PLN/);
  });

  await test.step('the trading account counts the conversion as taken out, the subaccount transfer as moved out, and nets its sale', async () => {
    await page.getByRole('navigation', { name: 'Accounts' }).getByRole('link', { name: new RegExp(`^${tradesName}`) }).click();
    await expect(page.getByText('Put in', { exact: true }).locator('..')).toContainText('4,000.00 PLN');
    await expect(page.getByText('Taken out', { exact: true }).locator('..')).toContainText(/(^|\s)400\.00 PLN/);
    await expect(page.getByText('Moved out', { exact: true }).locator('..')).toContainText('1,000.00 PLN');
    await expect(page.getByText('Realized result', { exact: true }).locator('..')).toContainText('100.00 PLN');
  });

  await test.step('a USD account shows a second line in the default currency under its value, and a PLN account shows none', async () => {
    await stubNbpSeries(page, { USD: 4 });
    await openInvestments(page);
    await shellMenu(page).getByRole('link', { name: 'Import from XTB' }).click();
    await expect(page.getByRole('heading', { name: 'Import from XTB', exact: true })).toBeVisible();
    await page.locator('input[type=file]').setInputFiles(xtbDollarStatement());
    await expect(page.getByText(`Account ${dollarAccount} · 1 rows`)).toBeVisible();
    await pickOption(page, page.getByRole('combobox', { name: 'Currency' }), 'USD');
    await expect(page.getByText('Errors · 0')).toBeVisible();
    await page.getByRole('button', { name: 'Create account and import 1 operation' }).click();
    await expect(page.getByRole('heading', { name: 'Investments', exact: true })).toBeVisible();

    await page.getByRole('navigation', { name: 'Accounts' }).getByRole('link', { name: new RegExp(`^My Trades ${dollarAccount}`) }).click();
    const dollarValue = page.getByRole('group', { name: /^Value · / });
    await expect(dollarValue).toContainText('100.00 USD');
    await expect(dollarValue.getByText('≈ 400.00 PLN', { exact: true })).toBeVisible();

    await page.getByRole('navigation', { name: 'Accounts' }).getByRole('link', { name: new RegExp(`^${plansName}`) }).click();
    const plnValue = page.getByRole('group', { name: /^Value · / });
    await expect(plnValue).toContainText('PLN');
    await expect(plnValue.getByText(/^≈ /)).toHaveCount(0);
  });
});
