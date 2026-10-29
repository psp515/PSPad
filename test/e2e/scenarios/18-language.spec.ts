import { expect, test } from '../fixtures/accounts';
import { anotherDevice } from '../helpers/api';
import { createBudgetOn, recordOn } from '../helpers/budgets';
import { utcToday } from '../helpers/dates';
import { goToSettings, setLanguage } from '../helpers/settings';
import { signIn } from '../helpers/sign-in';

test('the language is chosen once and follows the user to another device', async ({ page, browser, account }) => {
  await signIn(page, account);
  const api = await anotherDevice(page);
  await goToSettings(page);
  await expect(page.locator('.pspad-language')).toContainText('English');

  await setLanguage(page, 'Polski');
  await expect(page.getByRole('heading', { name: 'Ustawienia', exact: true })).toBeVisible();
  await expect(page.locator('.pspad-nav').getByRole('link', { name: 'Mój dzień' })).toBeVisible();
  await expect(page.locator('.pspad-language')).toContainText('Polski');

  await page.reload();
  await expect(page.getByRole('heading', { name: 'Ustawienia', exact: true })).toBeVisible();
  await page.locator('.pspad-nav').getByRole('link', { name: 'Mój dzień' }).click();
  await expect(page.getByRole('heading', { name: 'Mój dzień' })).toBeVisible();
  await expect(page.getByText('Na dziś nic nie zaplanowano.')).toBeVisible();
  await page.locator('.pspad-nav').getByRole('link', { name: 'Skrzynka' }).click();
  await expect(page.getByRole('heading', { name: 'Skrzynka', exact: true })).toBeVisible();
  await expect(page.getByText('Nic jeszcze nie dodano.')).toBeVisible();

  await test.step('Budgets speak Polish, with Polish amounts and plurals', async () => {
    await page.getByRole('navigation').getByRole('link', { name: 'Budżety', exact: true }).click();
    await expect(page.getByText('Nie masz jeszcze budżetów.')).toBeVisible();
    const budgetId = await createBudgetOn(api, `Osobisty ${account.suffix}`);
    await recordOn(api, budgetId, [
      { kind: 'income', name: 'Pensja', category: 'Salary', amount: 1000, date: utcToday() },
      { kind: 'expense', name: 'Czynsz', category: 'Home', amount: 250, date: utcToday() },
    ]);
    const card = page.getByRole('group', { name: `Osobisty ${account.suffix}`, exact: true });
    await expect(card).toContainText('250,00 PLN');
    await expect(card).toContainText('Przychody');
    await expect(page.getByText('1 budżet · zarchiwizowane: 0 · sumy w PLN', { exact: true }).filter({ visible: true })).toBeVisible();
  });

  const deviceContext = await browser.newContext();
  const device = await deviceContext.newPage();
  await signIn(device, account, 'Mój dzień');
  await expect(device.locator('.pspad-nav').getByRole('link', { name: 'Ustawienia' })).toBeVisible();
  await deviceContext.close();

  await goToSettings(page, 'Ustawienia');
  await setLanguage(page, 'English');
  await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
  await expect(page.locator('.pspad-nav').getByRole('link', { name: 'My Day' })).toBeVisible();
  await api.dispose();
});
