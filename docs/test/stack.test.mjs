import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { combinations, key, parse, quick } from '../src/data/stack/options.ts'
import { caddyfile, compose, env } from '../src/data/stack/assemble.ts'

const vps = (kc, mongo, expose) => ({ scenario: 'vps', kc, mongo, expose })
const services = (text) => [...text.matchAll(/^  ([a-z]+):$/gm)].map((m) => m[1])

test('nine combinations with unique keys', () => {
  const all = combinations()
  assert.equal(all.length, 9)
  assert.equal(new Set(all.map(key)).size, 9)
  assert.equal(key(quick), 'quick')
  assert.equal(key(vps('pg', 'atlas', 'dns')), 'vps-pg-atlas-dns')
})

test('parse falls back to defaults on unknown values', () => {
  assert.deepEqual(parse(new URLSearchParams('')), quick)
  assert.deepEqual(parse(new URLSearchParams('scenario=vps&kc=pg&mongo=atlas&expose=dns')), vps('pg', 'atlas', 'dns'))
  assert.deepEqual(parse(new URLSearchParams('scenario=vps&kc=oracle')), vps('h2', 'vps', 'tunnel'))
})

test('quick runs everything on localhost', () => {
  const text = compose(quick)
  assert.deepEqual(services(text), ['api', 'app', 'mongo', 'keycloak'])
  assert.match(text, /"5000:8080"/)
  assert.match(text, /"5001:8080"/)
  assert.match(text, /"8080:8080"/)
  assert.match(text, /start-dev/)
  assert.doesNotMatch(text, /\$\{/)
  assert.equal(env(quick), '')
  assert.equal(caddyfile(quick), '')
})

test('vps publishes nothing and runs keycloak in production mode', () => {
  for (const c of combinations().filter((c) => c.scenario === 'vps')) {
    const text = compose(c)
    assert.doesNotMatch(text, /"(5000|5001|8080):8080"/, key(c))
    assert.match(text, /\["start", "--import-realm"\]/, key(c))
    assert.match(text, /KC_HTTP_ENABLED: "true"/, key(c))
    assert.match(text, /KC_HOSTNAME_STRICT: "true"/, key(c))
    assert.match(text, /subnet: 10\.77\.0\.0\/24/, key(c))
    assert.match(env(c), /^PROXY_NETWORK=10\.77\.0\.0\/24$/m, key(c))
  }
})

test('atlas drops the mongo service and reads a connection string', () => {
  const c = vps('h2', 'atlas', 'tunnel')
  assert.ok(!services(compose(c)).includes('mongo'))
  assert.match(compose(c), /Mongo__ConnectionString: \$\{MONGO_CONNECTION_STRING\}/)
  assert.match(env(c), /^MONGO_CONNECTION_STRING=mongodb\+srv:\/\/pspad:CHANGE_ME@CLUSTER\.mongodb\.net\/pspad/m)
  assert.doesNotMatch(env(c), /MONGO_PASSWORD/)
  const onVps = vps('h2', 'vps', 'tunnel')
  assert.ok(services(compose(onVps)).includes('mongo'))
  assert.match(env(onVps), /^MONGO_PASSWORD=CHANGE_ME$/m)
})

test('postgres keeps keycloak users when chosen', () => {
  const c = vps('pg', 'vps', 'tunnel')
  assert.ok(services(compose(c)).includes('postgres'))
  assert.match(compose(c), /KC_DB: postgres/)
  assert.match(compose(c), /postgres-data:\/var\/lib\/postgresql$/m)
  assert.match(env(c), /^POSTGRES_PASSWORD=CHANGE_ME$/m)
  const h2 = vps('h2', 'vps', 'tunnel')
  assert.ok(!services(compose(h2)).includes('postgres'))
  assert.match(compose(h2), /keycloak-data:\/opt\/keycloak\/data/)
})

test('tunnel and dns choose the proxy and the client address header', () => {
  const tunnel = vps('h2', 'vps', 'tunnel')
  assert.ok(services(compose(tunnel)).includes('cloudflared'))
  assert.match(compose(tunnel), /Proxy__ClientIpHeader: CF-Connecting-IP/)
  assert.match(env(tunnel), /^CLOUDFLARE_TUNNEL_TOKEN=CHANGE_ME$/m)
  assert.equal(caddyfile(tunnel), '')
  const dns = vps('h2', 'vps', 'dns')
  assert.ok(services(compose(dns)).includes('caddy'))
  assert.ok(!services(compose(dns)).includes('cloudflared'))
  assert.match(compose(dns), /Proxy__ClientIpHeader: X-Forwarded-For/)
  assert.match(compose(dns), /"80:80"/)
  assert.match(compose(dns), /"443:443"/)
  assert.equal((caddyfile(dns).match(/reverse_proxy/g) ?? []).length, 3)
})

test('every variable a compose file reads is declared in its env', () => {
  for (const c of combinations().filter((c) => c.scenario === 'vps')) {
    const declared = new Set([...env(c).matchAll(/^([A-Z_]+)=/gm)].map((m) => m[1]))
    const used = new Set([...compose(c).matchAll(/\$\{([A-Z_]+)\}/g)].map((m) => m[1]))
    for (const name of used) assert.ok(declared.has(name), `${key(c)} uses ${name}`)
  }
})

test('image versions match the development stack and CI', () => {
  const dev = readFileSync(new URL('../../docker/compose.yaml', import.meta.url), 'utf8')
  const ci = readFileSync(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8')
  const text = compose(quick)
  for (const image of ['mongo:8.3.11', 'quay.io/keycloak/keycloak:26.8.1']) {
    assert.ok(dev.includes(`image: ${image}`), `dev has ${image}`)
    assert.ok(text.includes(`image: ${image}`), `stack has ${image}`)
  }
  for (const image of ['ghcr.io/psp515/pspad-backend', 'ghcr.io/psp515/pspad-frontend']) {
    assert.ok(ci.includes(`${image}:latest`), `ci pushes ${image}`)
    assert.ok(text.includes(`image: ${image}:latest`), `stack pulls ${image}`)
  }
})
