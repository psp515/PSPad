// Optional hardening example: creates (or re-passwords) a MongoDB user with readWrite on one database only,
// so the API need not sign in as root. Run it as the root user against the repo compose stack:
//
//   docker compose -f docker/compose.yaml cp docker/mongo-app-user.js mongo:/tmp/mongo-app-user.js
//   docker compose -f docker/compose.yaml exec \
//     -e MONGO_DATABASE=pspad -e MONGO_APP_USER=pspad-api -e MONGO_APP_PASSWORD='<new password>' \
//     mongo sh -c 'mongosh --quiet -u pspad -p "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin /tmp/mongo-app-user.js'
//
// Then point the API at it: mongodb://pspad-api:<new password>@mongo:27017/pspad?directConnection=true
// Running it again updates the password and roles. See docs/src/pages/self-host.astro.

const database = process.env.MONGO_DATABASE;
const user = process.env.MONGO_APP_USER;
const pwd = process.env.MONGO_APP_PASSWORD;

if (!database || !user || !pwd) {
  throw new Error('MONGO_DATABASE, MONGO_APP_USER and MONGO_APP_PASSWORD must all be set');
}

const maxTries = 60;
let tries = 0;
while (!db.hello().isWritablePrimary) {
  if (++tries >= maxTries) {
    throw new Error(`no writable primary after ${maxTries} tries`);
  }
  sleep(1000);
}

const target = db.getSiblingDB(database);
const roles = [{ role: 'readWrite', db: database }];

if (target.getUser(user)) {
  target.updateUser(user, { pwd, roles });
  print(`updated ${user} on ${database}`);
} else {
  target.createUser({ user, pwd, roles });
  print(`created ${user} on ${database}`);
}
