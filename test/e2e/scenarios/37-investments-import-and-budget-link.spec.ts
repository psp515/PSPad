import { expect, test } from '../fixtures/accounts';
import { createBudget, region, showView } from '../helpers/budgets';
import { openInvestments, statementErrorRow, stubQuotes, xtbAccount, xtbStatement } from '../helpers/investments';
import { panel } from '../helpers/panels';
import { desktopViewport, phoneViewport, shellMenu } from '../helpers/shell';
import { signIn } from '../helpers/sign-in';

test.use({ serviceWorkers: 'block' });

test('an XTB export imports into a new account, lists its wrong row, and the account joins a budget\'s balance', async ({ page, account }) => {
  const accountName = `IKE ${xtbAccount}`;
  const budget = `Net worth ${account.suffix}`;

  await signIn(page, account);
  await stubQuotes(page);

  await test.step('the import reads the file on the device and lists the row it cannot import, with its number and reason', async () => {
    await openInvestments(page);
    await shellMenu(page).getByRole('link', { name: 'Import from XTB' }).click();
    await expect(page.getByRole('heading', { name: 'Import from XTB', exact: true })).toBeVisible();
    await page.locator('input[type=file]').setInputFiles(xtbStatement());
    await expect(page.getByText(`Account ${xtbAccount} · 5 rows`)).toBeVisible();
    await expect(page.getByText('Rows with errors (1)')).toBeVisible();
    const errorRow = page.getByRole('row', { name: new RegExp(`^Row ${statementErrorRow} Stock split `) });
    await expect(errorRow).toContainText('Unknown type \'Stock split\'.');
    await page.setViewportSize(phoneViewport);
    await expect(page.getByRole('cell', { name: 'Type Stock split', exact: true }).first()).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Ticker BLT.PL', exact: true }).first()).toBeVisible();
    await page.setViewportSize(desktopViewport);
    await expect(page.getByText('To import · 4')).toBeVisible();
    await expect(page.getByText('Errors · 1')).toBeVisible();
  });

  await test.step('the new account takes a name and PLN, and the import opens it with the open position valued at cost', async () => {
    await expect(page.getByText(`No account has number ${xtbAccount} yet.`)).toBeVisible();
    await page.getByRole('textbox', { name: 'Name' }).fill(accountName);
    await expect(page.getByText('PLN', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Create account and import 4 operations' }).click();
    await expect(page.getByRole('heading', { name: 'Investments', exact: true })).toBeVisible();
    const position = page.getByRole('button', { name: /^ACM\.PL / });
    await expect(position).toBeVisible();
    await expect(position).toContainText('1,000.00');
    await expect(page.getByRole('button', { name: /^BLT\.PL / })).toHaveCount(0);
    await expect(page.getByText('No price', { exact: true })).toBeVisible();
  });

  await test.step('Add from investments copies the account into this month\'s balance as a linked Investment holding', async () => {
    await createBudget(page, budget);
    await showView(page, 'Balance');
    await page.getByRole('button', { name: 'Add from investments' }).click();
    const link = panel(page);
    await expect(link.getByRole('checkbox', { name: accountName })).toBeChecked();
    await expect(link).toContainText('No price');
    await expect(link).toContainText('10,050.00 PLN');
    await link.getByRole('button', { name: 'Add holdings' }).click();
    await expect(panel(page)).toHaveCount(0);
    const holding = region(page, 'Investment').getByRole('button', { name: new RegExp(`^${accountName} `) });
    await expect(holding).toBeVisible();
    await expect(holding).toContainText('10,050.00 PLN');
    await expect(region(page, 'Investment').getByRole('button', { name: 'Linked account options' })).toBeVisible();
    await page.setViewportSize(phoneViewport);
    await expect(holding).toBeVisible();
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(phoneViewport.width);
  });
});
