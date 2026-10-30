import { expect, test } from '../fixtures/accounts';
import { anotherDevice } from '../helpers/api';
import {
  addHolding, budgetIdFromUrl, createBudget, figure, openBudgetSettings, pickOption, region, showView, startBalanceOn, stubNbp,
} from '../helpers/budgets';
import { monthButton, monthTitle, utcToday } from '../helpers/dates';
import { closePanel, confirm, deleteFromPanel, openFabAction, panel, savePanel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { goOffline, goOnline } from '../helpers/sync';

test.use({ serviceWorkers: 'block' });

test('a balance month is started by hand, valued at NBP\'s rate, deleted, and started again from the month before', async ({ page, account }) => {
  const budget = `Savings ${account.suffix}`;
  const today = utcToday();
  const previousMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth() - 1, 1));
  const history = page.getByRole('table', { name: 'Net worth by holding type, last 12 months' });
  const holding = (name: string) => page.getByRole('button', { name: new RegExp(`^${name} `) });

  await signIn(page, account);
  let asked = await stubNbp(page, 'down');
  await createBudget(page, budget);
  const budgetId = budgetIdFromUrl(page);

  await test.step('a first month offers to start it, and a started month waits for holdings', async () => {
    await showView(page, 'Balance');
    await expect(monthButton(page, today)).toHaveAttribute('aria-pressed', 'true');
    await expect(monthButton(page, today)).toHaveAttribute('aria-current', 'date');
    await expect(page.getByRole('button', { name: 'This month' })).toBeDisabled();
    await expect(monthButton(page, new Date(Date.UTC(today.getUTCFullYear(), 0, 1)))).toContainText(String(today.getUTCFullYear()));
    await expect(page.getByText(`${monthTitle(today)} has no balance yet`)).toBeVisible();
    await expect(page.getByText('Start it and add your holdings.')).toBeVisible();
    await expect(page.getByText(`No holdings in ${monthTitle(today)}.`)).toBeVisible();
    await expect(page.getByRole('menu', { name: 'Month actions' })).toHaveCount(0);
    await expect(history).toHaveCount(0);
    await page.getByRole('button', { name: `Start ${monthTitle(today)}` }).click();
    await expect(figure(page, 'Net worth')).toContainText('0.00 PLN');
    await expect(figure(page, 'vs previous month')).toContainText('—');
    await expect(page.getByText(`No holdings in ${monthTitle(today)}.`)).toBeVisible();
    await expect(page.getByRole('menu', { name: 'Month actions' })).toBeVisible();
  });

  await addHolding(page, { name: 'Wallet', type: 'Cash', amount: '350' });
  await addHolding(page, { name: 'ETF', type: 'Investment', amount: '2500', currency: 'EUR', rate: '4.265' });
  await expect(figure(page, 'Net worth')).toContainText('11,012.50 PLN');
  await expect(page.getByRole('region', { name: /^(Cash|Savings|Investment)$/ })).toHaveCount(2);

  await test.step('holdings group by type with their amount, rate and PLN value, and a failed NBP lookup is not retried', async () => {
    await expect(region(page, 'Cash').getByRole('button')).toHaveAccessibleName(/^Wallet PLN 350\.00 PLN 1\.0000 350\.00 PLN —$/);
    await expect(region(page, 'Investment').getByRole('button'))
      .toHaveAccessibleName(/^ETF 2,500\.00 EUR @ 4\.2650 2,500\.00 EUR 4\.2650 10,662\.50 PLN —$/);
    await expect(region(page, 'Investment')).toContainText('10,662.50 PLN');
    await expect(history.getByRole('row', { name: new RegExp(`^${monthTitle(today)} `) }))
      .toHaveAccessibleName(`${monthTitle(today)} 350.00 PLN 0.00 PLN 10,662.50 PLN 11,012.50 PLN`);
    await expect(figure(page, 'At NBP today')).toHaveCount(0);
    await monthButton(page, previousMonth).click();
    await expect(page.getByText(`${monthTitle(previousMonth)} has no balance yet`)).toBeVisible();
    await page.getByRole('button', { name: 'This month' }).click();
    await expect(figure(page, 'Net worth')).toContainText('11,012.50 PLN');
    expect(asked).toEqual(['EUR']);
  });

  await test.step('a holding opens in its panel, renames and retypes; a new one suggests the latest rate and takes NBP\'s; Remove takes it off', async () => {
    asked = await stubNbp(page, { EUR: 4.4 });
    await holding('Wallet').click();
    await expect(panel(page).getByText('Holding', { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Remove' })).toBeVisible();
    await panel(page).getByRole('textbox', { name: 'Name' }).fill('Cash box');
    await panel(page).getByText('Savings', { exact: true }).click();
    await savePanel(page);
    await expect(region(page, 'Savings').getByRole('button')).toHaveAccessibleName(/^Cash box /);
    await expect(region(page, 'Cash')).toHaveCount(0);

    await openFabAction(page, 'Add holding');
    await expect(panel(page).getByText('New holding', { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Save' })).toBeDisabled();
    await expect(panel(page).getByRole('button', { name: 'Remove' })).toHaveCount(0);
    await panel(page).getByRole('textbox', { name: 'Name' }).fill('Broker');
    await panel(page).getByRole('spinbutton', { name: 'Amount' }).fill('10');
    await pickOption(page, panel(page).getByRole('combobox', { name: 'Currency' }), 'EUR');
    await expect(panel(page).getByRole('textbox', { name: 'In PLN' })).toHaveValue('42.65 PLN');
    await panel(page).getByRole('button', { name: 'NBP' }).click();
    await expect(panel(page).getByRole('textbox', { name: 'In PLN' })).toHaveValue('44.00 PLN');
    await savePanel(page);
    await expect(holding('Broker')).toHaveAccessibleName(/^Broker 10\.00 EUR @ 4\.4000 /);

    await holding('Broker').click();
    await deleteFromPanel(page, 'Remove');
    await expect(holding('Broker')).toHaveCount(0);
  });

  await test.step('NBP values the holdings at today\'s rate, asking once per currency', async () => {
    asked = await stubNbp(page, { EUR: 4.4 });
    await openBudgetSettings(page);
    await showView(page, 'Balance');
    await expect(figure(page, 'At NBP today')).toContainText('11,350.00 PLN');
    await expect(figure(page, 'Currency difference')).toContainText('+337.50 PLN');
    await expect(holding('ETF')).toHaveAccessibleName(/ \+337\.50 PLN$/);
    expect(asked).toEqual(['EUR']);
  });

  await test.step('offline the NBP figures hide and the NBP button is off, and coming online asks again', async () => {
    asked = await stubNbp(page, { EUR: 4.4 });
    await openBudgetSettings(page);
    await goOffline(page);
    await showView(page, 'Balance');
    await expect(figure(page, 'Net worth')).toContainText('11,012.50 PLN');
    await expect(figure(page, 'At NBP today')).toHaveCount(0);
    await expect(holding('ETF')).toHaveAccessibleName(/ —$/);
    await holding('ETF').click();
    await expect(panel(page).getByRole('button', { name: 'NBP' })).toBeDisabled();
    await closePanel(page);
    expect(asked).toEqual([]);
    await goOnline(page);
    await expect(figure(page, 'At NBP today')).toContainText('11,350.00 PLN');
    expect(asked).toEqual(['EUR']);
  });

  await test.step('deleting the month asks first, and Cancel keeps it', async () => {
    await openFabAction(page, 'Delete month');
    await expect(page.getByRole('dialog')).toContainText(`Delete the ${monthTitle(today)} balance and its holdings? This can’t be undone.`);
    await confirm(page, 'Cancel');
    await expect(holding('ETF')).toBeVisible();
    await openFabAction(page, 'Delete month');
    await confirm(page, 'Delete month');
    await expect(page.getByText(`${monthTitle(today)} has no balance yet`)).toBeVisible();
    await expect(holding('ETF')).toHaveCount(0);
  });

  const device = await anotherDevice(page);
  await startBalanceOn(device, budgetId, previousMonth, [
    { name: 'Wallet', type: 'Cash', amount: 300 },
    { name: 'ETF', type: 'Investment', amount: 2000, currency: 'EUR', rate: 4.2 },
  ]);

  await showView(page, 'Balance');
  await expect(monthButton(page, previousMonth)).toHaveAccessibleName(`${monthTitle(previousMonth)}, has entries`);
  await monthButton(page, previousMonth).click();
  await expect(figure(page, 'Net worth')).toContainText('8,700.00 PLN');
  await expect(monthButton(page, previousMonth)).toHaveAttribute('aria-pressed', 'true');
  await expect(monthButton(page, today)).toHaveAttribute('aria-pressed', 'false');

  await test.step('the month sheet opens on the shown year, picks a month of another year, and This month jumps back', async () => {
    const year = previousMonth.getUTCFullYear();
    await page.getByRole('button', { name: 'Pick another month' }).click();
    const sheet = page.getByRole('dialog');
    await expect(sheet.getByText(String(year), { exact: true })).toBeVisible();
    await sheet.getByRole('button', { name: 'Previous year' }).click();
    await expect(sheet.getByText(String(year - 1), { exact: true })).toBeVisible();
    await sheet.getByRole('button', { name: 'Jan', exact: true }).click();
    await expect(sheet).toBeHidden();
    await expect(monthButton(page, new Date(Date.UTC(year - 1, 0, 1)))).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByText(`${monthTitle(new Date(Date.UTC(year - 1, 0, 1)))} has no balance yet`)).toBeVisible();
    await page.getByRole('button', { name: 'Pick another month' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'This month' }).click();
    await expect(monthButton(page, today)).toHaveAttribute('aria-pressed', 'true');
    await monthButton(page, previousMonth).click();
    await expect(figure(page, 'Net worth')).toContainText('8,700.00 PLN');
  });

  await test.step('starting this month copies the latest earlier balance, and the overview shows it', async () => {
    await page.getByRole('button', { name: 'This month' }).click();
    await expect(page.getByText(`Starting it copies the holdings from ${monthTitle(previousMonth)}.`)).toBeVisible();
    await page.getByRole('button', { name: `Start ${monthTitle(today)}` }).click();
    await expect(holding('Wallet')).toBeVisible();
    await expect(holding('ETF')).toBeVisible();
    await expect(figure(page, 'Net worth')).toContainText('8,700.00 PLN');
    await expect(figure(page, `vs ${monthTitle(previousMonth)}`)).toContainText('0.00 PLN');
    await expect(history.getByRole('row', { name: new RegExp(`^${monthTitle(previousMonth)} `) }))
      .toHaveAccessibleName(`${monthTitle(previousMonth)} 300.00 PLN 0.00 PLN 8,400.00 PLN 8,700.00 PLN`);
    await showView(page, 'Overview');
    const worth = region(page, 'Net worth');
    await expect(worth).toContainText(`Net worth · ${monthTitle(today)}`);
    await expect(worth).toContainText('8,700.00 PLN');
    await expect(worth).toContainText(`0.00 PLN vs ${monthTitle(previousMonth)}`);
  });

  await device.dispose();
});
