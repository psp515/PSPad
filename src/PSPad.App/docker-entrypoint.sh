#!/bin/sh
set -e
envsubst < /usr/share/nginx/html/appsettings.template.json > /usr/share/nginx/html/appsettings.json
