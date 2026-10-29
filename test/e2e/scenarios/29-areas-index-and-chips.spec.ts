import { expect, test } from '../fixtures/accounts';
import { areaChips, createArea, deleteCurrentArea, openArea } from '../helpers/areas';
import { goTo } from '../helpers/nav';
import { closePanel, panel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';

test('Areas opens the remembered area, and on a phone every area is a chip', async ({ page, account }) => {
  const garden = `Garden ${account.suffix}`;
  const phone = { width: 390, height: 844 };
  const desktop = { width: 1280, height: 800 };

  await signIn(page, account);
  await createArea(page, garden);

  await test.step('on a phone every area is a chip in order, the open one marked current, and New area opens the panel', async () => {
    await page.setViewportSize(phone);
    const chips = areaChips(page).getByRole('link');
    await expect(chips).toHaveText(['Personal', 'Work', 'Home', garden]);
    await expect(chips.filter({ hasText: garden })).toHaveAttribute('aria-current', 'page');
    await areaChips(page).getByText('New area').click();
    await expect(panel(page).getByText('New area', { exact: true })).toBeVisible();
    await closePanel(page);
    await chips.filter({ hasText: 'Home' }).click();
    await expect(chips.filter({ hasText: 'Home' })).toHaveAttribute('aria-current', 'page');
    await expect(chips.filter({ hasText: garden })).not.toHaveAttribute('aria-current', 'page');
    await page.setViewportSize(desktop);
    await expect(page.getByRole('heading', { name: 'Home', exact: true })).toBeVisible();
  });

  await test.step('Areas opens the remembered area, and the first one once the remembered area is deleted', async () => {
    await goTo(page, 'My Day');
    await page.goto('/areas');
    await expect(page.getByRole('heading', { name: 'Home', exact: true })).toBeVisible();
    await deleteCurrentArea(page);
    await expect(page.getByRole('heading', { name: 'My Day', exact: true })).toBeVisible();
    await page.setViewportSize(phone);
    await page.goto('/areas');
    const chips = areaChips(page).getByRole('link');
    await expect(chips).toHaveText(['Personal', 'Work', garden]);
    await expect(chips.filter({ hasText: 'Personal' })).toHaveAttribute('aria-current', 'page');
    await page.setViewportSize(desktop);
  });

  await test.step('with every area deleted Areas offers a new one', async () => {
    for (const name of ['Personal', 'Work', garden]) {
      await openArea(page, name);
      await deleteCurrentArea(page);
      await expect(page.getByRole('heading', { name: 'My Day', exact: true })).toBeVisible();
    }
    await page.goto('/areas');
    await page.getByText('No areas yet.').click();
    await expect(panel(page).getByText('New area', { exact: true })).toBeVisible();
    await panel(page).getByPlaceholder('Area name').fill(garden);
    await panel(page).getByRole('button', { name: 'Add area' }).click();
    await expect(page.getByRole('heading', { name: garden, exact: true })).toBeVisible();
  });
});
