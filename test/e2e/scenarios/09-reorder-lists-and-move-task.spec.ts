import { expect, test } from '../fixtures/accounts';
import { createArea, listCard, listCardNames, openArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { chooseProperty, closePanel, openFabAction, panel, propertyRow } from '../helpers/panels';
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

  const cardTitles = () => listCardNames(page);
  await expect.poll(cardTitles).toEqual([first, second]);

  await openFabAction(page, 'Edit area');
  const row = (name: string) => panel(page).getByRole('group', { name, exact: true });
  await dragRow(row(second).getByLabel(`Drag ${second}`), row(first));
  const listRows = panel(page).getByRole('group').filter({ has: page.getByRole('img', { name: /^Drag / }) });
  await expect.poll(() => listRows.evaluateAll(groups => groups.map(group => group.getAttribute('aria-label'))))
    .toEqual([second, first]);
  await closePanel(page);

  await page.reload();
  await expect(page.getByRole('heading', { name: area, exact: true })).toBeVisible();
  await expect.poll(cardTitles).toEqual([second, first]);

  await openList(page, first);
  await addTask(page, task);
  await openTask(page, task);
  await chooseProperty(page, 'List', second);
  await expect(propertyRow(page, 'List')).toContainText(second);
  await closeTask(page);
  await expect(taskRow(page, task)).toHaveCount(0);

  await openArea(page, area);
  await expect(listCard(page, first).getByText('no open tasks', { exact: true })).toBeAttached();
  await expect(listCard(page, second).getByText('1 open task', { exact: true })).toBeAttached();
  await expect(listCard(page, second)).toContainText(task);
});
