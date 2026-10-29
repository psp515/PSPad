import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { confirm, panel } from '../helpers/panels';
import {
  addField, addReferenceItem, dragRow, dropOn, editFieldValue, fieldLabels, fieldRow, openReferenceItem, referenceRow, reopenReferenceItem,
  setFieldKind,
  removeField,
} from '../helpers/references';
import { signIn } from '../helpers/sign-in';

test('a reference item renders each field kind and keeps its field order', async ({ page, account }) => {
  const item = `Router ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, `Tech ${account.suffix}`);
  await createList(page, `Devices ${account.suffix}`, 'Reference');
  await openList(page, `Devices ${account.suffix}`);
  await addReferenceItem(page, item);
  await openReferenceItem(page, item);

  await addField(page, 'Model', 'RT-AX88U');
  await addField(page, 'Admin', 'https://192.168.1.1/admin');
  await addField(page, 'Config', '/etc/router/config.yaml');
  await addField(page, 'Ports', '8');
  await addField(page, 'Docs', 'https://example.com/router-docs');
  await expect(async () => {
    await reopenReferenceItem(page, item);
    expect(await fieldLabels(page)).toEqual(['Model', 'Admin', 'Config', 'Ports', 'Docs']);
  }).toPass({ timeout: 30_000 });

  await setFieldKind(page, 'Model', 'Text');
  await setFieldKind(page, 'Admin', 'Link');
  await setFieldKind(page, 'Config', 'Path');
  await setFieldKind(page, 'Ports', 'Quantity');
  await reopenReferenceItem(page, item);

  await expect(fieldRow(page, 'Model').locator('.pspad-field-text')).toHaveText('RT-AX88U');
  await expect(fieldRow(page, 'Admin').locator('a.pspad-field-link')).toHaveAttribute('href', 'https://192.168.1.1/admin');
  await expect(fieldRow(page, 'Config').locator('code.pspad-field-path')).toHaveText('/etc/router/config.yaml');
  await expect(fieldRow(page, 'Ports').locator('.pspad-field-quantity')).toHaveText('8');
  await expect(fieldRow(page, 'Docs').locator('a.pspad-field-link')).toHaveAttribute('href', 'https://example.com/router-docs');

  await test.step('a copy or a link leaves the row closed, an unsafe link stays text, and the editor wants a label, offers Remove and takes a drop', async () => {
    const save = panel(page).getByRole('button', { name: 'Save', exact: true });
    await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
    await fieldRow(page, 'Config').getByRole('button', { name: 'Copy' }).click();
    await expect(page.getByText('Copied')).toBeVisible();
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('/etc/router/config.yaml');
    await expect(save).toHaveCount(0);
    await page.evaluate(() => { navigator.clipboard.writeText = () => Promise.reject(new Error('denied')); });
    await fieldRow(page, 'Config').getByRole('button', { name: 'Copy' }).click();
    await expect(page.getByText('Could not copy — select the path instead.')).toBeVisible();
    await page.context().route('https://example.com/**', route => route.fulfill({ body: 'docs' }));
    const docs = page.waitForEvent('popup');
    await panel(page).getByRole('link', { name: 'https://example.com/router-docs' }).click();
    await (await docs).close();
    await page.context().unroute('https://example.com/**');
    await expect(save).toHaveCount(0);

    await panel(page).getByText('RT-AX88U', { exact: true }).click();
    await expect(panel(page).getByRole('button', { name: 'Remove', exact: true })).toBeVisible();
    const value = panel(page).getByRole('textbox', { name: 'Value', exact: true });
    await dropOn(value, { 'text/uri-list': 'https://example.com/rt-ax88u' });
    await expect(value).toHaveValue('https://example.com/rt-ax88u');
    await dropOn(value, {}, 'manual.pdf');
    await expect(page.getByText("Browsers can't read a file's full path.", { exact: false })).toBeVisible();
    await panel(page).getByRole('textbox', { name: 'Label', exact: true }).fill('');
    await panel(page).getByRole('textbox', { name: 'Label', exact: true }).press('Tab');
    await expect(save).toBeDisabled();
    await reopenReferenceItem(page, item);
    await expect(save).toHaveCount(0);
    await expect(panel(page).getByText('RT-AX88U', { exact: true })).toBeVisible();

    await addField(page, 'Trap', 'javascript:alert(1)');
    await setFieldKind(page, 'Trap', 'Link');
    await expect(panel(page).getByText('javascript:alert(1)', { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('link', { name: 'javascript:alert(1)' })).toHaveCount(0);
    await removeField(page, 'Trap');
  });

  await test.step('Cancel in a label\'s editor closes it and keeps the label', async () => {
    const save = panel(page).getByRole('button', { name: 'Save', exact: true });
    await panel(page).getByText('RT-AX88U', { exact: true }).click();
    await panel(page).getByRole('textbox', { name: 'Label', exact: true }).fill('Router model');
    await panel(page).getByRole('button', { name: 'Cancel', exact: true }).click();
    await expect(save).toHaveCount(0);
    expect(await fieldLabels(page)).toEqual(['Model', 'Admin', 'Config', 'Ports', 'Docs']);
  });

  await test.step('the add row moves from label to value on Enter, adds only on Enter with a label, and a dropped link or text fills its value', async () => {
    const label = panel(page).getByRole('textbox', { name: 'New label', exact: true });
    const value = panel(page).getByRole('textbox', { name: 'New label value', exact: true });
    const fields = panel(page).getByRole('button', { name: /^Remove / });
    await expect(fields).toHaveCount(5);
    await label.fill('Serial');
    await label.press('Enter');
    await expect(value).toBeFocused();
    await expect(fields).toHaveCount(5);
    await value.fill('SN-1');
    await value.blur();
    await expect(fields).toHaveCount(5);
    await label.fill('');
    await value.press('Enter');
    await expect(fields).toHaveCount(5);
    await label.fill('Serial');
    await value.press('Enter');
    await expect(panel(page).getByRole('button', { name: 'Remove Serial', exact: true })).toBeVisible();
    await expect(label).toHaveValue('');
    await expect(value).toHaveValue('');
    await removeField(page, 'Serial');

    await dropOn(value, { 'text/uri-list': '# saved\nhttps://example.com/manual\nhttps://example.com/other' });
    await expect(value).toHaveValue('https://example.com/manual');
    await dropOn(value, { 'text/plain': 'OneDrive/router.pdf' });
    await expect(value).toHaveValue('OneDrive/router.pdf');
    await value.fill('');
    await dropOn(value, {}, 'router.pdf');
    await expect(page.getByText("Browsers can't read a file's full path.", { exact: false }).last()).toBeVisible();
    await expect(value).toHaveValue('');
  });

  await dragRow(fieldRow(page, 'Docs').getByLabel('Drag Docs'), fieldRow(page, 'Model'));
  await expect.poll(() => fieldLabels(page)).toEqual(['Docs', 'Model', 'Admin', 'Config', 'Ports']);

  await editFieldValue(page, 'Ports', '12');
  await removeField(page, 'Config');

  await page.reload();
  await expect(panel(page).getByPlaceholder('Item name')).toHaveValue(item);
  await expect.poll(() => fieldLabels(page)).toEqual(['Docs', 'Model', 'Admin', 'Ports']);
  await expect(fieldRow(page, 'Admin').locator('a.pspad-field-link')).toBeVisible();
  await expect(fieldRow(page, 'Ports').locator('.pspad-field-quantity')).toHaveText('12');

  await panel(page).locator('.pspad-item-delete').click();
  await confirm(page, 'Delete');
  await expect(referenceRow(page, item)).toHaveCount(0);
  await expect(page.getByText('No items yet.')).toBeVisible();
});
