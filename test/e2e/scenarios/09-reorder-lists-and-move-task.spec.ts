import { expect, test } from '../fixtures/accounts';
import { createArea, openArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { closePanel, openFabAction, panel, pickFromPropertyRow } from '../helpers/panels';
import { dragRow } from '../helpers/references';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, openTask, taskRow } from '../helpers/tasks';

test('lists keep their order and a task moves between lists', async ({ page, account }) => {
  const area = `Projects ${account.suffix}`;
  const first = `Alpha ${account.suffix}`;
  const second = `Beta ${account.suffix}`;
  const task = `Write the brief ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, first);
  await createList(page, second);

  const cardTitles = () => page.locator('.pspad-card-header a').allInnerTexts();
  await expect.poll(cardTitles).toEqual([first, second]);

  await openFabAction(page, 'Edit area');
  const rows = panel(page).locator('.pspad-area-list-row');
  await dragRow(rows.filter({ hasText: second }).getByLabel(`Drag ${second}`), rows.filter({ hasText: first }));
  await expect.poll(() => panel(page).locator('.pspad-area-list-name').allInnerTexts()).toEqual([second, first]);
  await closePanel(page);

  await page.reload();
  await expect(page.getByRole('heading', { name: area, exact: true })).toBeVisible();
  await expect.poll(cardTitles).toEqual([second, first]);

  await openList(page, first);
  await addTask(page, task);
  await openTask(page, task);
  await pickFromPropertyRow(page, 'pspad-task-list', second);
  await expect(panel(page).locator('.pspad-task-list .pspad-property-value')).toContainText(second);
  await closeTask(page);
  await expect(taskRow(page, task)).toHaveCount(0);

  await openArea(page, area);
  const card = (name: string) =>
    page.locator('.mud-paper').filter({ has: page.locator('.pspad-card-header a', { hasText: name }) }).first();
  await expect(card(first).locator('.pspad-open-count')).toContainText('0');
  await expect(card(second).locator('.pspad-open-count')).toContainText('1');
  await expect(card(second)).toContainText(task);
});
