# End-to-end tests

Playwright scenarios that drive the real application in Chromium against the
Docker Compose stack. Design: `specs/e2e-testing-design.md`.

## Run

From the repository root, with Docker running and nothing of yours on ports
5000, 5001 or 18080:

```bash
docker compose -f docker/compose.yaml -f docker/compose.e2e.yaml --env-file docker/.env.example -p pspad-e2e up -d --build --wait
cd test/e2e
npm ci
npx playwright install chromium
npm test
```

`npm run test:ui` opens Playwright's UI mode; `npm run report` opens the last
HTML report (CI only writes one). `npm run stack:up` and `npm run stack:down`
wrap the two compose commands; `stack:down` also drops the stack's volumes.

The stack is `docker/compose.yaml` with `docker/compose.e2e.yaml` layered on
top: Keycloak listens on 18080 so the suite can run beside a developer's own
Keycloak on 8080. The app and API stay on 5001 and 5000 because the realm's
redirect URI names 5001.

## Configuration

| Variable | Default |
|---|---|
| `E2E_APP_URL` | `http://localhost:5001` |
| `E2E_API_URL` | `http://localhost:5000` |
| `E2E_KEYCLOAK_URL` | `http://localhost:18080` |
| `E2E_KEYCLOAK_ADMIN_USER` | `admin` |
| `E2E_KEYCLOAK_ADMIN_PASSWORD` | `admin` |
| `E2E_WORKERS` | `4` |

Every test creates its own Keycloak user through the admin API and deletes it
afterwards, so the realm needs no seeded users.

## Without Chromium's system libraries

When `npx playwright install chromium` cannot be followed by
`npx playwright install-deps` (no root), run the suite from the official
Playwright image on the host network, with the stack up as above:

```bash
cd test/e2e
docker run --rm --network host -v "$PWD:/work" -w /work -e HOME=/tmp mcr.microsoft.com/playwright:v1.64.1-noble npx playwright test
```

The image's Playwright version must match `package.json`.
