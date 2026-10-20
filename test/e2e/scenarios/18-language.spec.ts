import { expect, test } from '../fixtures/accounts';
import { goToSettings, setLanguage } from '../helpers/settings';
import { signIn } from '../helpers/sign-in';

test('the language is chosen once and follows the user to another device', async ({ page, browser, account }) => {
  await signIn(page, account);
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

  const deviceContext = await browser.newContext();
  const device = await deviceContext.newPage();
  await signIn(device, account, 'Mój dzień');
  await expect(device.locator('.pspad-nav').getByRole('link', { name: 'Ustawienia' })).toBeVisible();
  await deviceContext.close();

  await goToSettings(page, 'Ustawienia');
  await setLanguage(page, 'English');
  await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
  await expect(page.locator('.pspad-nav').getByRole('link', { name: 'My Day' })).toBeVisible();
});
