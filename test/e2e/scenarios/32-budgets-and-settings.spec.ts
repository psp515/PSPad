import { randomUUID } from 'node:crypto';
import { expect, test } from '../fixtures/accounts';
import { anotherDevice } from '../helpers/api';
import {
  confirmIfAsked, createBudgetOn, entryRow, figure, lastDayOf, monthOf, pickOption, recordOn, region, showView, startBalanceOn,
} from '../helpers/budgets';
import { isoMonth, monthButton, monthTitle, utcToday } from '../helpers/dates';
import { goTo } from '../helpers/nav';
import { closePanel, dismissMenu, panel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';

test('budget cards sum this month, settings edit categories, and an archived budget reads only', async ({ page, account }) => {
  const personal = `Personal ${account.suffix}`;
  const quiet = `Quiet ${account.suffix}`;
  const old = `Old ${account.suffix}`;
  const today = utcToday();
  const previous = monthOf(today, -1);
  const subtitle = (text: string) => page.getByText(text, { exact: true }).filter({ visible: true });

  await signIn(page, account);
  const device = await anotherDevice(page);
  const personalId = await createBudgetOn(device, personal);
  await recordOn(device, personalId, [
    { kind: 'income', name: 'Salary', category: 'Salary', amount: 1000, date: today },
    { kind: 'expense', name: 'Rent', category: 'Home', amount: 250, date: today },
    { kind: 'expense', name: 'Rolls', category: 'Food', amount: 3, date: today },
    { kind: 'expense', name: 'Old bread', category: 'Food', amount: 99, date: lastDayOf(previous) },
  ]);
  await startBalanceOn(device, personalId, previous, [
    { name: 'Wallet', type: 'Cash', amount: 100 },
    { name: 'Konto', type: 'Savings', amount: 10, currency: 'EUR', rate: 4.3 },
  ]);
  await startBalanceOn(device, personalId, monthOf(today, 1), [{ name: 'Wallet', type: 'Cash', amount: 999 }]);
  const quietId = await createBudgetOn(device, quiet);
  await startBalanceOn(device, quietId, today, [{ name: 'Jar', type: 'Cash', amount: 5 }]);
  await device.send('DeleteBalanceMonth', { budgetId: quietId, month: isoMonth(today) });
  const oldId = await createBudgetOn(device, old);
  await device.send('ArchiveBudget', { budgetId: oldId });

  await test.step('each card sums this month in PLN and shows its latest balance, and archived budgets wait behind a switch', async () => {
    await goTo(page, 'Budgets');
    await expect(subtitle('2 budgets · 1 archived · totals in PLN')).toBeVisible();
    const card = page.getByRole('group', { name: personal, exact: true });
    for (const text of ['Income', '1,000.00 PLN', 'Expenses', '253.00 PLN', '+747.00 PLN', `Net worth · ${monthTitle(previous)}`, '143.00 PLN']) {
      await expect(card).toContainText(text);
    }
    await expect(card.getByRole('link', { name: /^Start / })).toHaveCount(0);
    const empty = page.getByRole('group', { name: quiet, exact: true });
    await expect(empty).toContainText(`No entries in ${monthTitle(today)}`);
    await expect(empty.getByRole('link', { name: `Start ${monthTitle(today)}` })).toHaveAttribute('href', `/budgets/${quietId}/balance`);
    await expect(page.getByRole('group', { name: old, exact: true })).toHaveCount(0);
    await page.getByRole('switch', { name: 'Show archived' }).click();
    const archived = page.getByRole('group', { name: old, exact: true });
    await expect(archived).toContainText('Archived');
    await expect(archived).toContainText('No balance yet');
    await expect(archived.getByRole('link', { name: /^Start / })).toHaveCount(0);
    await archived.getByRole('link', { name: old }).click();
    await expect(page.getByRole('heading', { name: old, exact: true })).toBeVisible();
    const worth = region(page, 'Net worth');
    await expect(worth).toContainText('—');
    await expect(worth).toContainText('No balance yet');
    await expect(worth.getByRole('button')).toHaveCount(0);
    await page.getByRole('link', { name: 'Back to budgets' }).click();
    await expect(page.getByRole('heading', { name: 'Budgets', exact: true })).toBeVisible();
  });

  const expenses = region(page, 'Expense categories');
  const incomes = region(page, 'Income categories');
  const actions = async (list: typeof expenses, category: string) => {
    await list.getByRole('button', { name: `Actions for ${category}` }).click();
    return page.getByRole('menuitem');
  };

  await test.step('settings list both kinds of categories with their use, and add, rename, merge and remove them', async () => {
    await goTo(page, 'Budgets');
    await page.getByRole('link', { name: personal, exact: true }).first().click();
    await page.getByRole('button', { name: 'Budget settings' }).click();
    await expect(expenses.getByRole('button', { name: 'Eating Out', exact: true })).toBeVisible();
    await expect(incomes.getByRole('button', { name: 'Freelance', exact: true })).toBeVisible();
    await expect(expenses.getByRole('button', { name: 'Food 2 entries' })).toBeVisible();
    await expect(expenses.getByRole('button', { name: 'Home 1 entry' })).toBeVisible();
    await expect(expenses.getByRole('button', { name: 'Car', exact: true })).toBeVisible();

    const foodMenu = await actions(expenses, 'Food');
    await expect(foodMenu).toHaveText(['Rename', 'Merge', 'Remove']);
    await expect(page.getByRole('menuitem', { name: 'Remove' })).toBeDisabled();
    await dismissMenu(page);
    await actions(expenses, 'Car');
    await expect(page.getByRole('menuitem', { name: 'Remove' })).toBeEnabled();
    await dismissMenu(page);

    await expenses.getByRole('button', { name: 'Add' }).click();
    const draft = expenses.getByPlaceholder('New category');
    await draft.fill('Hobby');
    await draft.press('Enter');
    await expect(expenses.getByRole('button', { name: 'Hobby', exact: true })).toBeVisible();

    await actions(expenses, 'Food');
    await page.getByRole('menuitem', { name: 'Rename' }).click();
    await expenses.getByRole('textbox').fill('Groceries');
    await expenses.getByRole('textbox').press('Enter');
    await expect(expenses.getByRole('button', { name: 'Groceries 2 entries' })).toBeVisible();

    await actions(expenses, 'Home');
    await page.getByRole('menuitem', { name: 'Rename' }).click();
    await expenses.getByRole('textbox').fill('groceries');
    await expenses.getByRole('textbox').press('Enter');
    await expect(page.getByText('That category already exists. Merge them instead.')).toBeVisible();
    await expect(expenses.getByRole('textbox')).toHaveValue('groceries');
    await expenses.getByRole('textbox').press('Escape');
    await expect(expenses.getByRole('button', { name: 'Home 1 entry' })).toBeVisible();

    await actions(expenses, 'Bike');
    await page.getByRole('menuitem', { name: 'Merge' }).click();
    await pickOption(page, expenses.getByRole('combobox', { name: 'Merge Bike into' }), 'Car');
    await expect(expenses.getByRole('button', { name: 'Bike', exact: true })).toHaveCount(0);

    await actions(incomes, 'Other');
    await page.getByRole('menuitem', { name: 'Remove' }).click();
    await expect(incomes.getByRole('button', { name: 'Other', exact: true })).toHaveCount(0);

    await expenses.getByRole('button', { name: 'Groceries 2 entries' }).click();
    await expect(page.getByText('Groceries', { exact: true }).getByRole('button', { name: 'Close' })).toBeVisible();
    await expect(page.getByRole('option')).toHaveCount(1);
    await expect(entryRow(page, 'Rolls')).toHaveAccessibleName(/^Rolls Groceries · /);
  });

  const renamed = `${personal} home`;

  await test.step('Rename in settings renames the budget', async () => {
    await page.getByRole('button', { name: 'Budget settings' }).click();
    await page.getByRole('button', { name: 'Rename', exact: true }).click();
    const name = panel(page).getByPlaceholder('Budget name');
    await expect(name).toHaveValue(personal);
    await name.fill(renamed);
    await name.press('Tab');
    await closePanel(page);
    await expect(page.getByRole('heading', { name: renamed, exact: true })).toBeVisible();
  });

  await test.step('an archived budget reads only: no adding, editing, starting or importing, and it waits behind Show archived', async () => {
    await page.getByRole('button', { name: 'Archive budget' }).click();
    await confirmIfAsked(page);
    await expect(page.getByText('This budget is archived. It is read-only.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Restore budget' })).toBeVisible();
    await expect(expenses.getByRole('button', { name: 'Add' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /^Actions for / })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Upload expenses and incomes' })).toHaveCount(0);

    await showView(page, 'Overview');
    await expect(page.getByRole('button', { name: 'Expense', exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Income', exact: true })).toHaveCount(0);

    await showView(page, 'Entries');
    await entryRow(page, 'Rolls').click();
    await expect(panel(page).getByRole('textbox', { name: 'Name' })).toHaveValue('Rolls');
    await expect(panel(page).getByRole('button', { name: 'Save' })).toHaveCount(0);
    await expect(panel(page).getByRole('button', { name: 'Delete' })).toHaveCount(0);
    await closePanel(page);

    await showView(page, 'Balance');
    await expect(page.getByText(`${monthTitle(today)} has no balance yet`)).toBeVisible();
    await expect(page.getByRole('button', { name: /^Start / })).toHaveCount(0);
    await monthButton(page, previous).click();
    await expect(page.getByRole('button', { name: /^Konto / })).toBeVisible();
    await expect(page.getByRole('menu', { name: 'Month actions' })).toHaveCount(0);

    await showView(page, 'Stats');
    await expect(page.getByText('This budget is archived. It is read-only.')).toBeVisible();
    await expect(figure(page, 'Avg expenses / month')).toContainText('99.00 PLN');

    await goTo(page, 'Budgets');
    await expect(page.getByRole('group', { name: renamed, exact: true })).toHaveCount(0);
    await page.getByRole('switch', { name: 'Show archived' }).click();
    await page.getByRole('group', { name: renamed, exact: true }).getByRole('link', { name: renamed }).click();
    await page.getByRole('button', { name: 'Budget settings' }).click();
    await page.getByRole('button', { name: 'Restore budget' }).click();
    await expect(page.getByText('This budget is archived. It is read-only.')).toHaveCount(0);
    await showView(page, 'Overview');
    await expect(page.getByRole('button', { name: 'Expense', exact: true })).toBeVisible();
  });

  await test.step('an unknown budget says so', async () => {
    await page.goto(`/budgets/${randomUUID()}`);
    await expect(page.getByText('That budget does not exist.')).toBeVisible();
  });

  await device.dispose();
});
