#!/bin/sh
set -e
root=/usr/share/nginx/html
envsubst < "$root/appsettings.template.json" > "$root/appsettings.json"
# gzip_static would keep serving the publish-time copy, which envsubst never touches.
rm -f "$root/appsettings.json.br"
gzip -9 -c "$root/appsettings.json" > "$root/appsettings.json.gz"
