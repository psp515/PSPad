import { expect, test } from '../fixtures/accounts';
import { anotherDevice } from '../helpers/api';
import {
  addEntry, budgetIdFromUrl, digitsOf, entryRow, figure, lastDayOf, longDay, monthOf, ordinal, pickOption, recordOn, region,
  showView, stubNbp, viewToggle,
} from '../helpers/budgets';
import { monthButton, monthTitle, utcToday } from '../helpers/dates';
import { closeFilterChip } from '../helpers/lists';
import { goTo } from '../helpers/nav';
import { closePanel, deleteFromPanel, openFabAction, panel, savePanel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { syncNow } from '../helpers/sync';

test('a budget sums a month in PLN, reads it on the overview and lists its entries', async ({ page, account }) => {
  const budget = `Household ${account.suffix}`;
  const today = utcToday();
  const previous = monthOf(today, -1);
  const twoBack = monthOf(today, -2);
  const daysInMonth = lastDayOf(today).getUTCDate();

  await signIn(page, account);

  await test.step('Budgets starts empty, and a new budget waits for its name and opens on this month', async () => {
    await goTo(page, 'Budgets');
    await expect(page.getByText('0 budgets · 0 archived · totals in PLN').filter({ visible: true })).toBeVisible();
    await page.getByRole('button', { name: 'Create budget' }).click();
    const add = panel(page).getByRole('button', { name: 'Add budget' });
    const name = panel(page).getByPlaceholder('Budget name');
    await expect(add).toBeDisabled();
    await name.fill('   ');
    await expect(add).toBeDisabled();
    await name.fill(budget);
    await add.click();
    await expect(page.getByRole('heading', { name: budget, exact: true })).toBeVisible();
    await expect(page.getByText(`${monthTitle(today)} · day ${today.getUTCDate()} of ${daysInMonth}`)).toBeVisible();
    await expect(viewToggle(page, 'Overview')).toBeChecked();
    await expect(page.getByRole('group', { name: 'Month', exact: true })).toHaveCount(0);
    await expect(page.getByText('Averages appear after a full month of entries.')).toBeVisible();
  });

  await addEntry(page, 'Expense', { name: 'Groceries', category: 'Food', amount: '84.20' });
  await addEntry(page, 'Income', { name: 'Salary', category: 'Salary', amount: '100', currency: 'EUR', rate: '4.25' });

  await showView(page, 'Entries');
  await expect(entryRow(page, 'Groceries')).toBeVisible();
  await expect(entryRow(page, 'Salary')).toContainText('EUR');

  await showView(page, 'Overview');
  const net = figure(page, 'Net so far').getByRole('heading');
  await expect.poll(async () => digitsOf(await net.innerText())).toBe('34080');
  await expect(figure(page, 'Income')).toContainText('425.00 PLN');
  await expect(figure(page, 'Expenses')).toContainText('84.20 PLN');

  const missingBalance = (month: Date) => page.getByText(`${monthTitle(month)} has no balance yet`, { exact: true });
  await showView(page, 'Balance');
  await expect(missingBalance(today)).toBeVisible();
  await page.getByRole('button', { name: 'Pick another month', exact: true }).click();
  const sheet = page.getByRole('dialog');
  if ((await sheet.getByText(String(previous.getUTCFullYear()), { exact: true }).count()) === 0) {
    await sheet.getByRole('button', { name: 'Previous year', exact: true }).click();
  }
  await expect(sheet.getByText(String(previous.getUTCFullYear()), { exact: true })).toBeVisible();
  await sheet.getByRole('button', { name: new RegExp(`^${previous.toLocaleString('en-US', { month: 'short', timeZone: 'UTC' })}`) }).click();
  await expect(missingBalance(previous)).toBeVisible();
  await page.getByRole('button', { name: 'This month', exact: true }).click();
  await expect(missingBalance(today)).toBeVisible();
  await showView(page, 'Overview');
  await expect.poll(async () => digitsOf(await net.innerText())).toBe('34080');

  const budgetId = budgetIdFromUrl(page);
  const device = await anotherDevice(page);
  const payday = lastDayOf(previous);

  await test.step('last month becomes the usual month: pace, worth a look, where money went and recent entries', async () => {
    await recordOn(device, budgetId, [
      { kind: 'income', name: 'Freelance job', category: 'Freelance', amount: 600, date: payday },
      { kind: 'expense', name: 'Supermarket', category: 'Food', amount: 620, date: new Date(previous.getTime() + 2 * 86_400_000) },
      { kind: 'expense', name: 'Rent', category: 'Home', amount: 2000, date: previous },
      { kind: 'expense', name: 'Market', category: 'Food', amount: 700, date: today },
      { kind: 'expense', name: 'Bus', category: 'Car', amount: 20, date: today },
    ]);
    await expect(figure(page, 'Expected month end')).toContainText('≈ 2,620.00 PLN');
    await expect(page.getByText('avg of last 1 month')).toBeVisible();
    await expect(page.getByText(/^Spent 31% of a usual month · \d+% of the month passed$/)).toBeVisible();
    await expect(figure(page, 'Net so far')).toContainText('−379.20 PLN');
    await expect(figure(page, 'Income')).toContainText('425.00 PLN');
    await expect(page.getByText(/^usual by now [\d,.]+ PLN$/)).toBeVisible();
    await expect(figure(page, 'Expenses')).toContainText('804.20 PLN');
    await expect(page.getByText(/^[+−][\d,.]+ PLN vs usual by now$/)).toBeVisible();

    const worth = region(page, 'Worth a look');
    await expect(worth).toContainText('Food already past a usual month');
    await expect(worth).toContainText('784.20 PLN vs 620.00 PLN average');
    const freelance = worth.getByText(`Freelance usually lands around the ${ordinal(payday.getUTCDate())}`);
    await expect(freelance).toHaveCount(payday.getUTCDate() >= today.getUTCDate() ? 1 : 0);

    const went = region(page, 'Where money went');
    await expect(went.getByRole('button')).toHaveText(['Food', 'Car']);
    await expect(went).toContainText('784.20 PLN');
    await expect(went).toContainText('20.00 PLN');

    const recent = region(page, 'Recent entries');
    await expect(recent.getByRole('option')).toHaveCount(5);
    for (const [index, name] of ['Bus', 'Market', 'Rent', 'Supermarket', 'Freelance job'].entries()) {
      await expect(recent.getByRole('option').nth(index)).toHaveAccessibleName(new RegExp(`^${name} .* · recorded today \\d\\d:\\d\\d`));
    }
  });

  await test.step('a category the overview points at opens its entries, and All entries opens the month', async () => {
    await region(page, 'Worth a look').getByText('Food already past a usual month').click();
    await expect(page).toHaveURL(new RegExp(`/budgets/${budgetId}/entries$`));
    await expect(page.getByText('Food', { exact: true }).getByRole('button', { name: 'Close' })).toBeVisible();
    await expect(page.getByRole('option')).toHaveCount(2);
    await expect(entryRow(page, 'Market')).toBeVisible();
    await expect(entryRow(page, 'Groceries')).toBeVisible();
    await closeFilterChip(page, 'Food');
    await showView(page, 'Overview');
    await region(page, 'Where money went').getByRole('button', { name: 'Car' }).click();
    await expect(page.getByText('Car', { exact: true }).getByRole('button', { name: 'Close' })).toBeVisible();
    await expect(page.getByRole('option')).toHaveCount(1);
    await closeFilterChip(page, 'Car');
    await showView(page, 'Overview');
    await region(page, 'Recent entries').getByRole('button', { name: `All ${monthTitle(today)} entries` }).click();
    await expect(page).toHaveURL(new RegExp(`/budgets/${budgetId}/entries$`));
  });

  const day = region(page, longDay(today));

  await test.step('entries list by day with their totals, and narrow by kind, search, category and sort', async () => {
    await expect(page.getByText('4 entries · income 425.00 · expenses 804.20 · net −379.20 PLN')).toBeVisible();
    await expect(day).toContainText('−379.20');
    await expect(day.getByRole('option')).toHaveCount(4);
    await expect(entryRow(day, 'Salary')).toHaveAccessibleName(/Salary · 100\.00 EUR @ 4\.2500 · recorded today .* \+425\.00$/);
    await expect(entryRow(day, 'Groceries')).toHaveAccessibleName(/Food · recorded today .* −84\.20$/);

    await pickOption(page, page.getByRole('combobox', { name: 'Kind' }), 'Income');
    await expect(page.getByText('1 entry · income 425.00 · expenses 0.00 · net +425.00 PLN')).toBeVisible();
    await expect(day.getByRole('option')).toHaveCount(1);
    await pickOption(page, page.getByRole('combobox', { name: 'Kind' }), 'All');

    const search = page.getByRole('textbox', { name: 'Search name or note' });
    await search.fill('mark');
    await expect(day.getByRole('option')).toHaveCount(1);
    await expect(entryRow(day, 'Market')).toBeVisible();
    await search.fill('zzz');
    await expect(page.getByText('No entries match.')).toBeVisible();
    await search.fill('');

    await entryRow(day, 'Bus').getByRole('button', { name: 'Car' }).click();
    await expect(day.getByRole('option')).toHaveCount(1);
    await monthButton(page, twoBack).click();
    await expect(page.getByText(`No entries in ${monthTitle(twoBack)}.`)).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Kind' })).toHaveCount(0);
    await closeFilterChip(page, 'Car');
    await page.getByRole('button', { name: 'This month' }).click();
    await expect(day.getByRole('option')).toHaveCount(4);

    await pickOption(page, page.getByRole('combobox', { name: 'Sort' }), 'Recorded');
    await expect(region(page, `Recorded ${longDay(today)}`).getByRole('option')).toHaveCount(4);
    await pickOption(page, page.getByRole('combobox', { name: 'Sort' }), 'Entry date');
  });

  await test.step('an entry opens in its panel, saves an edit and deletes', async () => {
    await entryRow(day, 'Groceries').click();
    await expect(panel(page).getByText('Expense', { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('textbox', { name: 'Name' })).toHaveValue('Groceries');
    await expect(panel(page).getByRole('spinbutton', { name: 'Rate to PLN' })).toHaveCount(0);
    await panel(page).getByRole('spinbutton', { name: 'Amount' }).fill('90');
    await savePanel(page);
    await expect(entryRow(day, 'Groceries')).toHaveAccessibleName(/−90\.00$/);
    await entryRow(day, 'Bus').click();
    await deleteFromPanel(page);
    await expect(entryRow(day, 'Bus')).toHaveCount(0);
    await expect(page.getByText('3 entries · income 425.00 · expenses 790.00 · net −365.00 PLN')).toBeVisible();
  });

  await test.step('a new expense starts in the default currency, waits for its fields, suggests the latest rate, takes NBP\'s and creates a category', async () => {
    await stubNbp(page, { EUR: 4.4 });
    await device.send('SetDefaultCurrency', { currency: 'EUR' });
    await syncNow(page);
    await page.getByRole('button', { name: 'Expense', exact: true }).click();
    await expect(panel(page).getByText('New expense', { exact: true })).toBeVisible();
    const save = panel(page).getByRole('button', { name: 'Save' });
    await expect(save).toBeDisabled();
    await expect(panel(page).getByRole('button', { name: 'Delete' })).toHaveCount(0);
    await panel(page).getByRole('textbox', { name: 'Name' }).fill('Hotel');
    await panel(page).getByRole('spinbutton', { name: 'Amount' }).fill('10');
    await expect(panel(page).getByRole('combobox', { name: 'Currency' })).toHaveText('EUR');
    await expect(panel(page).getByRole('spinbutton', { name: 'Rate to PLN' })).toHaveValue('4.25');
    await expect(panel(page).getByRole('textbox', { name: 'In PLN' })).toHaveValue('42.50 PLN');
    await panel(page).getByRole('button', { name: 'NBP' }).click();
    await expect(panel(page).getByRole('textbox', { name: 'In PLN' })).toHaveValue('44.00 PLN');
    await panel(page).getByRole('combobox', { name: 'Category' }).fill('Travel');
    await page.getByText('Create category "Travel"').click();
    await expect(save).toBeEnabled();
    await savePanel(page);
    await expect(entryRow(day, 'Hotel')).toHaveAccessibleName(/Travel · 10\.00 EUR @ 4\.4000 · .* −44\.00$/);
    await device.send('SetDefaultCurrency', { currency: 'PLN' });
    await syncNow(page);
  });

  await test.step('adding in a past month dates the entry on its last day', async () => {
    await monthButton(page, previous).click();
    await addEntry(page, 'Expense', { name: 'Late bill', category: 'Home', amount: '50' });
    await expect(entryRow(region(page, longDay(payday)), 'Late bill')).toBeVisible();
    await page.getByRole('button', { name: 'This month' }).click();
  });

  await test.step('each view has its address, the month row shows on Entries and Balance only, and settings list the new category', async () => {
    await expect(page).toHaveURL(new RegExp(`/budgets/${budgetId}/entries$`));
    await expect(page.getByRole('group', { name: 'Month', exact: true })).toBeVisible();
    await showView(page, 'Stats');
    await expect(page).toHaveURL(new RegExp(`/budgets/${budgetId}/stats$`));
    await expect(page.getByRole('group', { name: 'Month', exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Expense', exact: true })).toHaveCount(0);
    await showView(page, 'Balance');
    await expect(page).toHaveURL(new RegExp(`/budgets/${budgetId}/balance$`));
    await expect(page.getByRole('group', { name: 'Month', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Expense', exact: true })).toHaveCount(0);
    const settings = page.getByRole('button', { name: 'Budget settings' });
    await expect(settings).toHaveAttribute('aria-pressed', 'false');
    await settings.click();
    await expect(page).toHaveURL(new RegExp(`/budgets/${budgetId}/settings$`));
    await expect(settings).toHaveAttribute('aria-pressed', 'true');
    for (const view of ['Overview', 'Entries', 'Balance', 'Stats'] as const) {
      await expect(viewToggle(page, view)).not.toBeChecked();
    }
    await expect(region(page, 'Expense categories').getByRole('button', { name: 'Travel 1 entry' })).toBeVisible();
    await page.goto(`/budgets/${budgetId}/month`);
    await expect(viewToggle(page, 'Entries')).toBeChecked();
  });

  await test.step('on a phone the add button offers an expense or an income on Entries, and Stats has none', async () => {
    await page.setViewportSize({ width: 390, height: 844 });
    await openFabAction(page, 'Add income');
    await expect(panel(page).getByText('New income', { exact: true })).toBeVisible();
    await closePanel(page);
    await showView(page, 'Stats');
    await expect(page.getByRole('menu', { name: 'Add entry' })).toHaveCount(0);
    await page.setViewportSize({ width: 1280, height: 800 });
  });

  await test.step('the overview offers to start this month\'s balance, and then shows its net worth', async () => {
    await showView(page, 'Overview');
    const worth = region(page, 'Net worth');
    await expect(worth).toContainText('—');
    await expect(worth).toContainText(`${monthTitle(today)} has no balance yet`);
    await worth.getByRole('button', { name: `Start ${monthTitle(today)}` }).click();
    await expect(worth).toContainText(`Net worth · ${monthTitle(today)}`);
    await expect(worth).toContainText('0.00 PLN');
    await expect(worth.getByRole('button')).toHaveCount(0);
  });

  await device.dispose();
});
