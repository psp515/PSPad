import { randomUUID } from 'node:crypto';
import { expect, test } from '../fixtures/accounts';
import { anotherDevice, idOf } from '../helpers/api';
import { createBudgetOn } from '../helpers/budgets';
import { captureFromAnotherDevice, goToInbox } from '../helpers/inbox';
import { openInApp } from '../helpers/nav';
import { closePanel, panel } from '../helpers/panels';
import { answerAccountDeletionOnce, confirmAccountDeletion, deleteAccountDialog, keycloakLogIn } from '../helpers/settings';
import {
  bootSplash, bottomNav, collapseGroup, desktopViewport, expandGroup, openAccountDrawer, phoneViewport, pullToRefresh, shellMenu, topBar,
} from '../helpers/shell';
import { logInFromWelcome, signInDidNotFinish } from '../helpers/sign-in';
import { goToMyDay } from '../helpers/today';

test.use({ serviceWorkers: 'block' });

test('a fresh user signs in, finds their way around the shell and can delete the account', async ({ page, browser, account }) => {
  const liveWhileSignedOut: string[] = [];
  page.on('request', request => {
    if (request.url().endsWith('/api/live')) {
      liveWhileSignedOut.push(request.url());
    }
  });

  await test.step('Welcome greets a visitor and keeps where they were going', async () => {
    await page.goto('/inbox');
    await expect(page).toHaveURL(/\/welcome\?returnUrl=/);
    await expect(page.getByRole('heading', { name: 'Your day, planned.' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'An approach for GTD techniques' }))
      .toHaveAttribute('href', 'https://en.wikipedia.org/wiki/Getting_Things_Done');
    await expect(page.getByRole('link', { name: 'Docs' })).toHaveAttribute('href', 'https://psp515.com/pspad');
    await expect(page.getByRole('link', { name: 'Log in' })).toHaveAttribute('href', /returnUrl=.*inbox/);
    for (const preview of ['Renew the domain', 'Draft the release notes', 'Read a book', 'Water the plants']) {
      await expect(page.getByText(preview, { exact: true })).toBeVisible();
    }
    for (const gone of ['One Today screen', 'How it goes', 'Run it yourself', 'Install']) {
      await expect(page.getByText(gone)).toHaveCount(0);
    }
    await expect(page.getByRole('button', { name: /^Sync now/ })).toHaveCount(0);
    await expect(bootSplash(page)).toHaveCount(0);
    await expect(signInDidNotFinish(page)).toHaveCount(0);
  });

  await test.step('an identity provider that fails the sign-in returns the visitor to Welcome with a warning', async () => {
    await page.goto('/authentication/login-failed');
    await expect(page).toHaveURL(/\/welcome$/);
    await expect(signInDidNotFinish(page)).toBeVisible();
  });

  await test.step('the sign-in warning shows once per arrival on Welcome', async () => {
    await openInApp(page, '/welcome');
    await expect(page.getByRole('heading', { name: 'Your day, planned.' })).toBeVisible();
    await expect(signInDidNotFinish(page)).toHaveCount(0);
    await openInApp(page, '/authentication/login-failed');
    await expect(page).toHaveURL(/\/welcome$/);
    await expect(signInDidNotFinish(page)).toBeVisible();
    await openInApp(page, '/welcome');
    await expect(signInDidNotFinish(page)).toHaveCount(0);
  });

  await test.step('a sign-in the server cannot finish leaves the visitor on Welcome, signed out, warned once', async () => {
    await page.route('**/api/me', route => route.fulfill({ status: 503, headers: { 'Retry-After': '1' } }), { times: 1 });
    await logInFromWelcome(page, account, 'Your day, planned.');
    await expect(signInDidNotFinish(page)).toBeVisible();
    await expect(page.getByRole('link', { name: 'Log in' })).toBeVisible();
    expect(liveWhileSignedOut).toEqual([]);
    await openInApp(page, '/inbox');
    await expect(page).toHaveURL(/\/welcome\?returnUrl=/);
    await expect(page.getByRole('heading', { name: 'Your day, planned.' })).toBeVisible();
    await expect(signInDidNotFinish(page)).toHaveCount(0);
    await page.goto('/welcome');
    await expect(page.getByRole('link', { name: 'Log in' })).not.toHaveAttribute('href', /returnUrl/);
  });

  await test.step('logging in again holds the app until the first pull lands, then shows the account', async () => {
    let release = () => {};
    const released = new Promise<void>(resolve => { release = resolve; });
    await page.route('**/api/sync?since=0', async route => {
      await released;
      await route.continue();
    }, { times: 1 });
    const firstPull = page.waitForRequest(request => request.url().includes('/api/sync?since=0'));
    await page.getByRole('link', { name: 'Log in' }).click();
    await firstPull;
    await expect(page.getByRole('heading', { name: 'My Day' })).toHaveCount(0);
    await expect(bootSplash(page)).toBeVisible();
    release();
    await expect(page.getByRole('heading', { name: 'My Day' })).toBeVisible();
    await expect(bootSplash(page)).toHaveCount(0);
    for (const name of ['Personal', 'Work', 'Home']) {
      await expect(shellMenu(page).getByRole('link', { name, exact: true })).toBeVisible();
    }
  });

  await test.step('signing in from Welcome returns the visitor to where they were going', async () => {
    const visitor = await browser.newContext({ serviceWorkers: 'block' });
    const fresh = await visitor.newPage();
    await fresh.goto('/inbox');
    await expect(fresh).toHaveURL(/\/welcome\?returnUrl=/);
    await logInFromWelcome(fresh, account, 'Inbox');
    await expect(fresh).toHaveURL(/\/inbox$/);
    await visitor.close();
  });

  await test.step('an empty inbox and an empty day say so', async () => {
    await goToInbox(page);
    await expect(page.getByText('Nothing captured.')).toBeVisible();
    await goToMyDay(page);
    await expect(page.getByText('Nothing planned for today.')).toBeVisible();
    await expect(page.getByText('Schedule empty for today.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Overdue' })).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Starred' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Add task' })).toBeVisible();
  });

  const device = await anotherDevice(page);

  await test.step('the sidebar carries every section, counts the inbox and follows other devices', async () => {
    const menu = shellMenu(page);
    await expect(menu).toContainText(account.displayName);
    await expect(menu).toContainText(account.email);
    await expect(menu.getByRole('link')).toHaveText([
      'My Day', 'Inbox', 'Goals', 'Statistics', 'List snapshots', 'Budgets', 'Personal', 'Work', 'Home', 'Settings', 'App info',
    ]);
    await expect(menu.getByRole('searchbox')).toHaveCount(0);
    await expect(menu.getByText(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/)).toBeVisible();
    await expect(menu.getByText('PSPad · GPL v3')).toBeVisible();
    await expect(menu.getByRole('img', { name: 'Offline' })).toHaveCount(0);

    await captureFromAnotherDevice(page, `Call the plumber ${account.suffix}`);
    await expect(menu.getByRole('link', { name: 'Inbox 1', exact: true })).toBeVisible();
    await expect(menu.getByRole('link', { name: 'My Day', exact: true })).toBeVisible();

    const personal = `Personal ${account.suffix}`;
    const archived = `Old ${account.suffix}`;
    await createBudgetOn(device, personal);
    await device.send('ArchiveBudget', { budgetId: await createBudgetOn(device, archived) });
    await device.send('DeleteArea', { areaId: await idOf(device, 'areas', 'Home') });
    await expect(menu.getByRole('link', { name: personal, exact: true })).toBeVisible();
    await expect(menu.getByRole('link', { name: archived })).toHaveCount(0);
    await expect(menu.getByRole('link', { name: 'Home', exact: true })).toHaveCount(0);

    await collapseGroup(page, 'areas');
    await expect(menu.getByText('Areas · 2', { exact: true })).toBeVisible();
    await expect(menu.getByRole('link', { name: 'Work', exact: true })).toHaveCount(0);
    await expandGroup(page, 'areas');
    await expect(menu.getByRole('link', { name: 'Work', exact: true })).toBeVisible();

    await menu.getByText('New area', { exact: true }).click();
    await expect(panel(page).getByPlaceholder('Area name')).toBeVisible();
    await closePanel(page);
    await menu.getByText('New budget', { exact: true }).click();
    await expect(panel(page).getByPlaceholder('Budget name')).toBeVisible();
    await expect(page).toHaveURL(/^[^?]*\/\?/);
    await closePanel(page);
  });

  await test.step('App info names the version, the licence and the links, and an unknown address says so', async () => {
    await shellMenu(page).getByRole('link', { name: 'App info', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'App info', exact: true })).toBeVisible();
    await expect(page.getByText(/^Version \S+/)).toBeVisible();
    await expect(page.getByText('Free software, licensed under the GNU General Public License v3.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Source on GitHub' })).toHaveAttribute('href', 'https://github.com/psp515/PSPad');
    await expect(page.getByRole('link', { name: 'Documentation' })).toHaveAttribute('href', 'https://psp515.github.io');
    await page.goto('/no-such-page');
    await expect(page.getByRole('heading', { name: 'Not found', exact: true })).toBeVisible();
  });

  await test.step('on a phone the top bar names the screen, the bottom bar switches screens and the avatar opens the account', async () => {
    const workId = await idOf(device, 'areas', 'Work');
    const groceries = `Groceries ${account.suffix}`;
    const listId = randomUUID();
    await device.send('CreateTaskList', { listId, areaId: workId, name: groceries, kind: 0 });
    await goToMyDay(page);
    await page.setViewportSize(phoneViewport);

    const tabs = bottomNav(page).getByRole('link');
    await expect(tabs).toHaveText([/Inbox/, 'Areas', 'My Day', 'Goals', 'Budgets']);
    await expect(tabs.filter({ hasText: 'Inbox' }).getByRole('status')).toHaveText('1');
    await expect(bottomNav(page).getByRole('link', { name: 'My Day' })).toHaveAttribute('aria-current', 'page');
    await expect(topBar(page).getByRole('heading', { name: 'My Day' })).toBeVisible();
    await expect(topBar(page).getByRole('button', { name: /^Sync now\. Updated/ })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'My Day', level: 1 })).toBeHidden();
    await expect(shellMenu(page).getByRole('link', { name: 'Work', exact: true })).toHaveCount(0);
    const pulled = page.waitForRequest(request => request.url().includes('/api/sync'));
    await pullToRefresh(page);
    await pulled;

    await bottomNav(page).getByRole('link', { name: 'Areas' }).click();
    await expect(bottomNav(page).getByRole('link', { name: 'Areas' })).toHaveAttribute('aria-current', 'page');
    await page.goto(`/lists/${listId}`);
    await expect(topBar(page).getByRole('heading', { name: groceries })).toBeVisible();
    await expect(topBar(page)).toContainText('Work · no open tasks');
    await expect(bottomNav(page).getByRole('link', { name: 'Areas' })).toHaveAttribute('aria-current', 'page');
    await topBar(page).getByRole('link', { name: 'Back to area' }).click();
    await expect(topBar(page).getByRole('heading', { name: 'Work' })).toBeVisible();
    await expect(topBar(page).getByRole('link', { name: 'Back to area' })).toHaveCount(0);
    await page.goto(`/lists/${randomUUID()}`);
    await expect(page.getByText('This list is not there anymore.')).toBeVisible();
    await expect(topBar(page).getByRole('heading', { name: groceries })).toHaveCount(0);

    await bottomNav(page).getByRole('link', { name: 'Goals' }).click();
    await expect(topBar(page).getByRole('heading', { name: 'Goals' })).toBeVisible();
    await expect(bottomNav(page).getByRole('link', { name: 'Goals' })).toHaveAttribute('aria-current', 'page');

    await openAccountDrawer(page);
    const drawer = shellMenu(page);
    await expect(drawer).toContainText(account.displayName);
    await expect(drawer).toContainText(account.email);
    await expect(drawer.getByRole('link')).toHaveText(['Statistics', 'List snapshots', 'Settings', 'App info']);
    await expect(drawer.getByText('PSPad · GPL v3')).toBeVisible();
    await drawer.getByRole('link', { name: 'Settings', exact: true }).click();
    await expect(topBar(page).getByRole('heading', { name: 'Settings' })).toBeVisible();
    await expect(drawer.getByRole('link', { name: 'Statistics', exact: true })).not.toBeInViewport();
    await expect(bottomNav(page).locator('[aria-current="page"]')).toHaveCount(0);

    await openAccountDrawer(page);
    await page.setViewportSize(desktopViewport);
    await expect(shellMenu(page).getByRole('link', { name: 'Work', exact: true })).toBeInViewport();
    await expect(page.getByRole('heading', { name: 'Settings', level: 1 })).toBeVisible();
    await page.setViewportSize(phoneViewport);
    await expect(shellMenu(page).getByRole('link', { name: 'Statistics', exact: true })).not.toBeInViewport();
    await page.setViewportSize(desktopViewport);
  });

  await test.step('Settings shows the account, its time zone and a link to change the password', async () => {
    const card = page.getByRole('region', { name: 'Account', exact: true });
    await expect(card).toContainText(account.displayName);
    await expect(card).toContainText(account.email);
    await expect(page.getByLabel('Time zone')).toHaveValue('Etc/UTC');
    const password = page.getByRole('link', { name: 'Change password' });
    await expect(password).toHaveAttribute('href', /\/realms\/psplace\/account\/$/);
    await expect(password).toHaveAttribute('target', '_blank');
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

  await test.step.skip('BUG: deleting the account signs out before it leaves Settings, so Welcome keeps Settings as the return address (reported in PR #225)', async () => {
    await expect(page).toHaveURL(/\/welcome$/);
  });

  await test.step('the deleted account can no longer log in', async () => {
    await page.getByRole('link', { name: 'Log in' }).click();
    await keycloakLogIn(page, account);
    await expect(page.getByText('Invalid username or password.')).toBeVisible();
  });

  await device.dispose();
});
