import { randomUUID } from 'node:crypto';
import { expect, test } from '../fixtures/accounts';
import { createAccount, deleteAccount } from '../fixtures/keycloak';
import { anotherDevice } from '../helpers/api';
import { keycloakLogIn } from '../helpers/settings';
import { signIn } from '../helpers/sign-in';

test.use({ serviceWorkers: 'block' });

test('first sign-ins land on My Day while another user saves changes', async ({ page, browser, account }) => {
  await signIn(page, account);
  const device = await anotherDevice(page);
  await page.goto('about:blank');

  let saving = true;
  const save = async () => {
    while (saving) {
      await device.send('CreateArea', { areaId: randomUUID(), name: `Noise ${account.suffix}`, position: 9 })
        .catch(() => {});
      await new Promise(resolve => setTimeout(resolve, 350));
    }
  };
  const saves = Promise.all([save(), save(), save(), save()]).catch(() => {});

  const newcomers = await Promise.all([createAccount(), createAccount(), createAccount()]);
  const landings: string[] = [];
  try {
    await test.step('three newcomers sign in for the first time and each lands on My Day', async () => {
      for (let attempt = 0; attempt < 3; attempt++) {
        const newcomer = newcomers[attempt];
        const context = await browser.newContext({ serviceWorkers: 'block' });
        const fresh = await context.newPage();
        try {
          await fresh.goto('/');
          await fresh.getByRole('link', { name: 'Log in' }).click();
          await keycloakLogIn(fresh, newcomer);
          await expect(fresh.getByRole('heading', { name: 'My Day' }).or(fresh.getByRole('heading', { name: 'Your day, planned.' })))
            .toBeVisible({ timeout: 30_000 });
          landings.push(new URL(fresh.url()).pathname);
        } finally {
          await context.close();
        }
      }
      expect(landings.filter(path => path.startsWith('/welcome'))).toEqual([]);
    });
  } finally {
    saving = false;
    await saves;
    await device.dispose();
    await Promise.all(newcomers.map(deleteAccount));
  }
});
