import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'

const page = readFileSync(new URL('../src/pages/self-host.astro', import.meta.url), 'utf8')

test('the page tells the truth about redirect URIs', () => {
  assert.ok(page.includes('http://localhost:5001/*'))
  assert.ok(!page.includes('redirect URIs from'))
})

test('the page carries no stale guidance', () => {
  assert.ok(!page.includes('Docker Compose v2'))
  assert.ok(!page.includes('nbp.pl'))
})
