import { randomUUID } from 'node:crypto';
import { expect, test } from '../fixtures/accounts';
import { anotherDevice, apiFor, bearerToken, idOf } from '../helpers/api';
import { addDays, utcToday } from '../helpers/dates';
import { goTo, openInApp } from '../helpers/nav';
import { answerAccountDeletionOnce, confirmAccountDeletion, deleteAccountDialog, goToSettings, keycloakLogIn } from '../helpers/settings';
import { signIn } from '../helpers/sign-in';
import { publishSnapshotOn } from '../helpers/snapshots';
import { goOffline, goOnline } from '../helpers/sync';

test.use({ serviceWorkers: 'block' });

test('the account shows in Settings and deleting it leaves nothing behind', async ({ page, account }) => {
  await signIn(page, account);
  const device = await anotherDevice(page);
  await goToSettings(page);

  await test.step('Settings shows the account, its time zone and a link to change the password', async () => {
    const card = page.getByRole('region', { name: 'Account', exact: true });
    await expect(card).toContainText(account.displayName);
    await expect(card).toContainText(account.email);
    await expect(page.getByLabel('Time zone')).toHaveValue('Etc/UTC');
    const password = page.getByRole('link', { name: 'Change password' });
    await expect(password).toHaveAttribute('href', /\/realms\/psplace\/account\/$/);
    await expect(password).toHaveAttribute('target', '_blank');
  });

  const keptStatistics = () => page.evaluate(() => Object.keys(localStorage).filter(key => key.startsWith('pspad.statistics.')));
  const pantry = `Pantry ${account.suffix}`;
  let snapshotLink = '';

  await test.step('a snapshot the account opened reads offline, and the statistics it opened stay on this device', async () => {
    const listId = randomUUID();
    await device.send('CreateTaskList', { listId, areaId: await idOf(device, 'areas', 'Work'), name: pantry, kind: 0 });
    const api = await apiFor(await bearerToken(page));
    const { token } = await publishSnapshotOn(api, listId, addDays(utcToday(), 1));
    await api.dispose();
    snapshotLink = `/public/snapshot/${token}`;
    await page.goto(snapshotLink);
    await expect(page.getByRole('heading', { name: pantry })).toBeVisible();
    await goTo(page, 'Statistics');
    await expect.poll(keptStatistics).not.toEqual([]);
    await goTo(page, 'Settings');
    await expect(page.getByRole('heading', { name: 'Settings', level: 1 })).toBeVisible();
  });

  await test.step('deleting the account asks for the email and a fresh sign-in', async () => {
    const dialog = await deleteAccountDialog(page);
    const remove = dialog.getByRole('button', { name: 'Delete account' });
    await expect(dialog).toContainText(`Type ${account.email} to confirm.`);
    await expect(remove).toBeDisabled();
    await dialog.getByLabel('Email').fill('someone-else@example.com');
    await expect(remove).toBeDisabled();
    await dialog.getByLabel('Email').fill(account.email.toUpperCase());
    await expect(remove).toBeEnabled();

    await answerAccountDeletionOnce(page, 401, 'Bearer error="insufficient_user_authentication", max_age=300');
    await remove.click();
    await expect(dialog).toContainText('Deleting your account needs a fresh sign-in.');
    await expect(page.getByRole('heading', { name: 'Settings', level: 1 })).toBeVisible();
    await dialog.getByRole('button', { name: 'Sign in again' }).click();
    await keycloakLogIn(page, account);
    await expect(page.getByRole('heading', { name: 'Settings', level: 1 })).toBeVisible({ timeout: 30_000 });
    await expect(page).toHaveURL(/\/settings$/);
  });

  await test.step('coming back from the fresh sign-in reopens the delete dialog', async () => {
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByLabel('Email')).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Delete account' })).toBeDisabled();
  });

  await test.step('a failed deletion keeps the dialog open, and a deletion Keycloak could not finish says an administrator is needed', async () => {
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('button', { name: 'Delete account' })).toBeDisabled();
    await dialog.getByLabel('Email').fill(account.email);
    await answerAccountDeletionOnce(page, 500);
    await dialog.getByRole('button', { name: 'Delete account' }).click();
    await expect(dialog).toContainText('Something went wrong.');
    await expect(dialog.getByRole('button', { name: 'Delete account' })).toBeEnabled();

    await page.route('**/api/account', async route => {
      const response = await route.fetch();
      await route.fulfill({ response, json: { keycloakRemoved: false } });
    }, { times: 1 });
    await confirmAccountDeletion(page);
    await expect(dialog).toContainText('Your data is gone. Removing your sign-in still needs an administrator.');
    await dialog.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByRole('heading', { name: 'Your day, planned.' })).toBeVisible();
    await expect(page).toHaveURL(/\/welcome/);
  });

  await test.step('after deleting the account Welcome carries no return address', async () => {
    await expect(page).toHaveURL(/\/welcome$/);
    await expect(page.getByRole('link', { name: 'Log in' })).not.toHaveAttribute('href', /returnUrl/);
  });

  await test.step('the deleted account leaves nothing behind on this device: its snapshot no longer reads offline, and no statistics stay', async () => {
    await goOffline(page);
    await openInApp(page, snapshotLink);
    await expect(page.getByText('Connect to the internet to open this snapshot.')).toBeVisible();
    await expect(page.getByRole('heading', { name: pantry })).toHaveCount(0);
    await goOnline(page);
    expect(await keptStatistics()).toEqual([]);
    await page.goto('/welcome');
  });

  await test.step('the deleted account can no longer log in', async () => {
    await page.getByRole('link', { name: 'Log in' }).click();
    await keycloakLogIn(page, account);
    await expect(page.getByText('Invalid username or password.')).toBeVisible();
  });

  await device.dispose();
});
