import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { combinations, key } from '../src/data/stack/options.ts'
import { caddyfile, compose, env } from '../src/data/stack/assemble.ts'

const root = mkdtempSync(path.join(tmpdir(), 'pspad-stack-'))
let failed = 0

for (const choices of combinations()) {
  const dir = path.join(root, key(choices))
  mkdirSync(path.join(dir, 'keycloak'), { recursive: true })
  writeFileSync(path.join(dir, 'compose.yaml'), compose(choices))
  writeFileSync(path.join(dir, 'mongo-entrypoint.sh'), '')
  const envText = env(choices)
  if (envText) writeFileSync(path.join(dir, '.env'), envText)
  const caddy = caddyfile(choices)
  if (caddy) writeFileSync(path.join(dir, 'Caddyfile'), caddy)
  const args = ['compose', '-f', 'compose.yaml', ...(envText ? ['--env-file', '.env'] : []), 'config', '--quiet']
  try {
    execFileSync('docker', args, { cwd: dir, stdio: ['ignore', 'ignore', 'pipe'] })
    console.log(`ok   ${key(choices)}`)
  } catch (error) {
    failed++
    console.error(`FAIL ${key(choices)}\n${error.stderr}`)
  }
}

rmSync(root, { recursive: true, force: true })
if (failed > 0) process.exit(1)
