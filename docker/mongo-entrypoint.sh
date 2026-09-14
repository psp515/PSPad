#!/bin/bash
set -e

KEYFILE=/keyfile/mongo-keyfile

if [ ! -f "$KEYFILE" ]; then
  openssl rand -base64 756 > "$KEYFILE"
fi

chmod 400 "$KEYFILE"
chown mongodb:mongodb "$KEYFILE"

exec docker-entrypoint.sh mongod --replSet rs0 --bind_ip_all --keyFile "$KEYFILE"
