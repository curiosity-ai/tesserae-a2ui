// What a user does: type, press, tick, tab, open. Each check reads the page the way a user (or a browser automation) would.
export async function run({ browser, base, suite, L }) {
  const tab = (page, name) => page.getByRole('tab', { name, exact: true });

  await suite.test('typing in a bound TextField updates a bound Text and the data model', async () => {
    const page = await L.open(browser, base, 'Overview');
    try {
      const box = page.getByLabel('Your name');
      await box.fill('Grace');
      await page.getByText('Hello, Grace!').waitFor({ timeout: 3000 });
      await tab(page, 'Data model').click();
      const text = await page.locator('.tss-jsonviewer').nth(1).innerText();
      L.assert(text.includes('Grace'), 'data model does not show Grace: ' + text);
    } finally { await page.__ctx.close(); }
  });

  await suite.test('a Button action reaches the agent with its context resolved', async () => {
    const page = await L.open(browser, base, 'Overview');
    try {
      await page.getByLabel('Your name').fill('Linus');
      await page.getByRole('button', { name: 'Tell the agent' }).click();
      await tab(page, 'To agent').click();
      const card = page.locator('.a2ui-outbound').first();
      await card.waitFor({ timeout: 3000 });
      const text = await card.innerText();
      L.assert(text.includes('greeted') && text.includes('Linus') && text.includes('"who"') || text.includes('who'), 'action card: ' + text);
      L.assert(text.includes('sourceComponentId') && text.includes('go'), 'missing source component id: ' + text);
    } finally { await page.__ctx.close(); }
  });

  await suite.test('@checks: a failing check disables the Button and shows its message once the field is touched', async () => {
    const page = await L.open(browser, base, 'Actions and Functions');
    try {
      const submit = page.getByRole('button', { name: 'Submit' });
      L.assert(await submit.isDisabled(), 'submit should start disabled');
      L.assert(await page.getByText('Email is required').count() === 0 || !(await page.getByText('Email is required').first().isVisible()), 'an untouched empty field must not show an error');
      await page.getByLabel('Email').fill('nope');
      await page.getByText('That does not look like an email address').waitFor({ timeout: 3000 });
      await page.getByLabel('Email').fill('ada@example.com');
      await page.getByText('I agree to the terms').click();
      await page.waitForFunction(() => { const b = [...document.querySelectorAll('button')].find(x => x.textContent.trim() === 'Submit'); return b && !b.disabled; }, null, { timeout: 3000 });
      await submit.click();
      await tab(page, 'To agent').click();
      const text = await page.locator('.a2ui-outbound').first().innerText();
      L.assert(text.includes('submit') && text.includes('ada@example.com'), 'action: ' + text);
      L.assert(text.includes('Submitted ada@example.com'), 'userMessage not resolved: ' + text);
    } finally { await page.__ctx.close(); }
  });

  await suite.test('template: an agent update adds a row, relative paths read the item, a checkbox writes it back', async () => {
    const page = await L.open(browser, base, 'Data Binding');
    try {
      const stage = page.locator('.a2ui-surface');
      await stage.getByText('Write the spec').waitFor();
      await page.getByRole('button', { name: 'Agent: add a to-do' }).click();
      await stage.getByText('Item 3').waitFor({ timeout: 3000 });
      await stage.getByText('Render it').click();
      await tab(page, 'Data model').click();
      const text = await page.locator('.tss-jsonviewer').nth(1).innerText();
      L.assert(/done\W+true/.test(text.replace(/\s+/g, ' ')) || text.includes('true'), 'data model: ' + text);
      await page.getByRole('button', { name: 'Agent: rename to Grace' }).click();
      await stage.getByText('Hello, Grace').waitFor({ timeout: 3000 });
      const v = await page.getByLabel('Name (writes /user/name)').inputValue();
      L.assert(v === 'Grace', 'the bound field did not take the agent update: ' + v);
      await page.getByRole('button', { name: 'Agent: remove /user/name' }).click();
      await stage.getByText('Hello,', { exact: false }).first().waitFor();
      L.assert((await page.getByLabel('Name (writes /user/name)').inputValue()) === '', 'null should delete the key');
    } finally { await page.__ctx.close(); }
  });

  await suite.test('Slider and CheckBox write their paths', async () => {
    const page = await L.open(browser, base, 'Data Binding');
    try {
      await page.getByText('Notifications', { exact: true }).click();
      await page.getByText('notifications false').waitFor({ timeout: 3000 });
      const slider = page.locator('.a2ui-surface input[type=range]');
      await slider.evaluate(el => { el.value = '75'; el.dispatchEvent(new Event('input', { bubbles: true })); });
      await page.getByText('Volume is 75').waitFor({ timeout: 3000 });
    } finally { await page.__ctx.close(); }
  });

  await suite.test('Tabs show only the selected tab\'s child', async () => {
    const page = await L.open(browser, base, 'Recipe Card');
    try {
      const stage = page.locator('.a2ui-example-stage');
      await stage.getByText('Mediterranean Quinoa Bowl').waitFor();
      await stage.getByText('Ingredients', { exact: true }).click();
      await stage.getByText('quinoa', { exact: false }).first().waitFor({ timeout: 3000 });
    } finally { await page.__ctx.close(); }
  });

  await suite.test('Modal: the trigger opens the content, which closes again', async () => {
    const page = await L.open(browser, base, 'Modal Sample');
    try {
      const stage = page.locator('.a2ui-example-stage');
      L.assert(await page.getByText('This is the content inside the modal.').count() === 0, 'content must not be on the page before the trigger is pressed');
      await stage.getByRole('button', { name: 'Open Modal' }).click();
      await page.getByText('This is the content inside the modal.').waitFor({ timeout: 3000 });
      await page.keyboard.press('Escape');
      await page.getByText('This is the content inside the modal.').waitFor({ state: 'hidden', timeout: 3000 });
    } finally { await page.__ctx.close(); }
  });

  await suite.test('function formatting: currency, number, plural and date render as the catalog says', async () => {
    const page = await L.open(browser, base, 'Product Card');
    try {
      const text = await page.locator('.a2ui-example-stage').innerText();
      L.assert(/\(2,847 reviews\)/.test(text), 'pluralize + formatNumber: ' + text);
      L.assert(/\$199\.99/.test(text) && /\$249\.99/.test(text), 'formatCurrency: ' + text);
    } finally { await page.__ctx.close(); }
  });

  await suite.test('streaming: replaying delivers messages one at a time and ends in the same state', async () => {
    const page = await L.open(browser, base, 'Incremental Dashboard');
    try {
      const stage = page.locator('.a2ui-example-stage');
      const finalHtml = (await stage.innerText()).replace(/\s+/g, ' ');
      await page.getByRole('button', { name: 'Replay as a stream' }).click();
      await L.sleep(120);
      const early = (await stage.innerText()).replace(/\s+/g, ' ');
      L.assert(early !== finalHtml, 'the first message should not show the final UI');
      await page.waitForFunction(() => document.querySelectorAll('.tss-jsonviewer').length > 0, null, { timeout: 8000 });
      await L.sleep(2400);
      L.assert((await stage.innerText()).replace(/\s+/g, ' ') === finalHtml, 'the replay does not end where it started');
    } finally { await page.__ctx.close(); }
  });

  await suite.test('Playground: bad JSON is an error, Append updates a live surface', async () => {
    const page = await L.open(browser, base, 'Playground', { noWait: true });
    try {
      await page.getByLabel('A2UI messages').waitFor();
      const area = page.getByLabel('A2UI messages');
      await area.fill('{ this is not json');
      await page.getByRole('button', { name: 'Render' }).click();
      await tab(page, 'Errors').click();
      await page.locator('.a2ui-error').first().waitFor({ timeout: 3000 });
      await area.fill(JSON.stringify({ version: '1.0', createSurface: { surfaceId: 'p', catalogId: 'https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json', components: [{ id: 'root', component: 'Text', text: 'first' }] } }));
      await page.getByRole('button', { name: 'Render' }).click();
      await page.locator('.a2ui-surface').getByText('first').waitFor({ timeout: 3000 });
      await area.fill(JSON.stringify({ version: '1.0', updateComponents: { surfaceId: 'p', components: [{ id: 'root', component: 'Text', text: 'second' }] } }));
      await page.getByRole('button', { name: 'Append' }).click();
      await page.locator('.a2ui-surface').getByText('second').waitFor({ timeout: 3000 });
    } finally { await page.__ctx.close(); }
  });

  await suite.test('Error handling: broken messages are reported with protocol codes and the page survives', async () => {
    const page = await L.open(browser, base, 'Error Handling', { noWait: true });
    try {
      await page.getByRole('button', { name: 'Send the broken messages' }).waitFor();
      await page.getByRole('button', { name: 'Send the broken messages' }).click();
      await tab(page, 'Errors').click();
      await page.locator('.a2ui-error').first().waitFor({ timeout: 3000 });
      const text = (await page.locator('.a2ui-error').allInnerTexts()).join('\n');
      for (const code of ['VALIDATION_FAILED', 'INTEGRITY_ERROR', 'EXPRESSION_ERROR']) L.assert(text.includes(code), 'missing ' + code + ' in:\n' + text);
      L.assert(text.includes('theme'), 'a v1.0 createSurface with theme must be rejected');
      L.assert(text.includes('(a+)+$') || text.includes('backtrack'), 'the unsafe regex must be rejected');
      await page.locator('.a2ui-surface').getByText('This one is fine').waitFor();
      L.assert(await page.locator('.a2ui-surface [data-a2ui-problem]').count() >= 2, 'unknown and self-referencing components should draw a notice');
    } finally { await page.__ctx.close(); }
  });

  await suite.test('Custom catalog: a component from another catalog binds two-way and a custom function runs', async () => {
    const page = await L.open(browser, base, 'Custom Catalog');
    try {
      const stage = page.locator('.a2ui-surface');
      await stage.getByText('TESSERAE!').waitFor({ timeout: 3000 });
      await stage.getByText('You rated it 3 of 5').waitFor();
      await stage.locator('[data-a2ui-component="Rating"] [class*="star"], [data-a2ui-component="Rating"] i, [data-a2ui-component="Rating"] svg, [data-a2ui-component="Rating"] span').nth(4).click();
      await stage.getByText('You rated it 5 of 5').waitFor({ timeout: 3000 });
    } finally { await page.__ctx.close(); }
  });

  await suite.test('agent function calls: callAgentFunction goes out, callRendererFunction is answered, openUrl is refused', async () => {
    const page = await L.open(browser, base, 'Actions and Functions');
    try {
      await page.getByRole('button', { name: 'Ask the agent for a price' }).click();
      await tab(page, 'To agent').click();
      await page.locator('.a2ui-outbound', { hasText: 'callAgentFunction' }).first().waitFor({ timeout: 3000 });
      await page.getByRole('button', { name: 'Agent calls getScreenSize on the renderer' }).click();
      const resp = page.locator('.a2ui-outbound', { hasText: 'rendererFunctionResponse' }).first();
      await resp.waitFor({ timeout: 3000 });
      L.assert(/1400/.test(await resp.innerText()), 'screen size response: ' + await resp.innerText());
      await page.getByRole('button', { name: 'Agent calls formatCurrency (refused)' }).click();
      await page.locator('.a2ui-outbound', { hasText: 'rendererOnly' }).first().waitFor({ timeout: 3000 });
      await page.getByRole('button', { name: 'Agent calls openUrl (refused)' }).click();
      await page.locator('.a2ui-outbound', { hasText: "Function 'openUrl'" }).first().waitFor({ timeout: 3000 });
    } finally { await page.__ctx.close(); }
  });

  await suite.test('a host-owned catalog copy (OwnCatalog) can override and decorate without touching other hosts', async () => {
    const page = await L.open(browser, base, 'Custom Catalog');
    try {
      const stage = page.locator('.a2ui-surface');
      await stage.getByText('replaced Divider').waitFor({ timeout: 3000 });
      L.assert(await stage.locator('[data-a2ui-component="Divider"][data-decorated]').count() === 0, 'a Divider is not a Button');
      const button = stage.locator('[data-a2ui-component="Button"][data-decorated="true"]');
      L.assert(await button.count() === 1, 'the decorated Button should carry data-decorated');
      await button.click();
      await page.getByRole('tab', { name: 'To agent', exact: true }).click();
      await page.locator('.a2ui-outbound', { hasText: 'decorated' }).first().waitFor({ timeout: 3000 });   // the original's action still runs
      // another host on another page still has the basic Divider and Button
      const other = await L.open(browser, base, 'Showcase');
      try {
        L.assert(await other.getByText('replaced Divider').count() === 0, 'the override leaked into another host');
        L.assert(await other.locator('[data-decorated]').count() === 0, 'the decoration leaked into another host');
      } finally { await other.__ctx.close(); }
    } finally { await page.__ctx.close(); }
  });
}
