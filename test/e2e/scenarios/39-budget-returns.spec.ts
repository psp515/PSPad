import { expect, test } from '../fixtures/accounts';
import {
  addEntry, createBudget, entryRow, figure, longDay, purchaseRow, recordReturn, region, returnRows, showView,
} from '../helpers/budgets';
import { utcToday } from '../helpers/dates';
import { closePanel, deleteFromPanel, panel, saveButton, savePanel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';

test('a purchase takes partial and full returns from its panel, nets them in the month, and takes them along when deleted', async ({ page, account }) => {
  const day = region(page, longDay(utcToday()));
  const shoes = () => purchaseRow(day, 'Running shoes');
  const expenses = figure(page, 'Expenses');

  await signIn(page, account);
  await createBudget(page, `Returns ${account.suffix}`);
  await addEntry(page, 'Expense', { name: 'Running shoes', category: 'Clothes', amount: '420' });
  await showView(page, 'Entries');

  await test.step('a purchase without returns offers to record one, pre-filled with what is left', async () => {
    await shoes().click();
    const form = panel(page);
    await expect(form.getByRole('heading', { name: 'Returns', exact: true })).toBeVisible();
    await expect(form.getByText(/^Returned .* of /)).toHaveCount(0);
    await expect(form.getByText('420.00 left to return', { exact: true })).toBeVisible();
    await form.getByRole('button', { name: 'Record return', exact: true }).click();
    await expect(panel(page).getByText('Record return', { exact: true })).toBeVisible();
    await expect(panel(page)).toContainText('Running shoes');
    await expect(panel(page)).toContainText('Clothes');
    await expect(figure(panel(page), 'Left to return')).toContainText('420.00');
    await expect(panel(page).getByRole('spinbutton', { name: 'Amount returned', exact: true })).toHaveValue(/^420(\.00)?$/);
    await expect(panel(page).getByText("Uses the purchase's rate, so a full return cancels it exactly.")).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Delete', exact: true })).toHaveCount(0);
  });

  await test.step('a partial return goes back to its purchase, lists there and shows as its own row', async () => {
    await panel(page).getByRole('spinbutton', { name: 'Amount returned', exact: true }).fill('150');
    await expect(panel(page).getByRole('textbox', { name: 'In PLN', exact: true })).toHaveValue('150.00 PLN');
    await savePanel(page);
    const form = panel(page);
    await expect(form.getByText('Returned 150.00 of 420.00', { exact: true })).toBeVisible();
    await expect(form.getByText('270.00 left to return', { exact: true })).toBeVisible();
    await expect(form.getByRole('option', { name: /\+150\.00$/ })).toBeVisible();
    await expect(form.getByText("Has returns: currency is locked and the amount can't go below 150.00.")).toBeVisible();
    await expect(form.getByRole('combobox', { name: 'Currency', exact: true })).toBeDisabled();
    await closePanel(page);

    await expect(shoes()).toContainText('Returned 150.00');
    await expect(shoes()).toHaveAccessibleName(/−420\.00$/);
    await expect(returnRows(day, 'Running shoes')).toHaveCount(1);
    await expect(returnRows(day, 'Running shoes')).toContainText('Return · Clothes');
    await expect(returnRows(day, 'Running shoes')).toHaveAccessibleName(/\+150\.00$/);
    await expect(page.getByText('2 entries · income 0.00 · expenses 270.00 · net −270.00 PLN')).toBeVisible();

    await showView(page, 'Overview');
    await expect(expenses).toContainText('270.00 PLN');
    await expect(page.getByText('after 150.00 PLN returned', { exact: true })).toBeVisible();
    await showView(page, 'Entries');
  });

  await test.step('no more than what is left can be returned, and the rest makes it a full return', async () => {
    await shoes().click();
    await panel(page).getByRole('button', { name: 'Record return', exact: true }).click();
    await expect(figure(panel(page), 'Left to return')).toContainText('270.00');
    await panel(page).getByRole('spinbutton', { name: 'Amount returned', exact: true }).fill('300');
    await expect(saveButton(page)).toBeDisabled();
    await recordReturn(page, '270');
    const form = panel(page);
    await expect(form.getByText('Returned 420.00 of 420.00', { exact: true })).toBeVisible();
    await expect(form.getByText('0.00 left to return', { exact: true })).toBeVisible();
    await expect(form.getByRole('button', { name: 'Record return', exact: true })).toBeDisabled();
    await closePanel(page);

    await expect(shoes()).toContainText('Returned');
    await expect(shoes()).not.toContainText('Returned 1');
    await expect(returnRows(day, 'Running shoes')).toHaveCount(2);
    await showView(page, 'Overview');
    await expect(expenses).toContainText('0.00 PLN');
    await expect(page.getByText('after 420.00 PLN returned', { exact: true })).toBeVisible();
    await showView(page, 'Entries');
  });

  await test.step('a return opens from its purchase and saves an edit', async () => {
    await shoes().click();
    await panel(page).getByRole('option', { name: /\+270\.00$/ }).click();
    await expect(page).toHaveURL(/\?return=/);
    await expect(panel(page).getByText('Return', { exact: true })).toBeVisible();
    await expect(figure(panel(page), 'Returned so far')).toContainText('420.00');
    const amount = panel(page).getByRole('spinbutton', { name: 'Amount returned', exact: true });
    await expect(amount).toHaveValue(/^270(\.00)?$/);
    await amount.fill('100');
    await savePanel(page);
    await expect(panel(page).getByText('Returned 250.00 of 420.00', { exact: true })).toBeVisible();
    await expect(panel(page).getByText('170.00 left to return', { exact: true })).toBeVisible();
  });

  await test.step('deleting the purchase names its returns and deletes them with it', async () => {
    await expect(panel(page).getByText('Deleting this expense also deletes its 2 returns.', { exact: true })).toBeVisible();
    await deleteFromPanel(page);
    await expect(panel(page)).toHaveCount(0);
    await expect(entryRow(page, 'Running shoes')).toHaveCount(0);
    await showView(page, 'Overview');
    await expect(expenses).toContainText('0.00 PLN');
    await expect(page.getByText(/^after .* returned$/)).toHaveCount(0);
  });
});
