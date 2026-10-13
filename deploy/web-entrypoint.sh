#!/bin/sh
# Writes the API address the browser should call into the Blazor app's config at container start.
set -e
: "${API_BASE_URL:=http://localhost:8080}"
printf '{ "ApiBaseUrl": "%s" }\n' "$API_BASE_URL" > /usr/share/nginx/html/appsettings.json
