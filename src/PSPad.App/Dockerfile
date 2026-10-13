FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/PSPad.App/PSPad.App.csproj -c Release -o /app \
 && rm /app/wwwroot/appsettings.json.br \
 && mkdir /app/runtime \
 && mv /app/wwwroot/appsettings.json /app/wwwroot/appsettings.json.gz /app/runtime/

FROM nginxinc/nginx-unprivileged:1.31.6-alpine@sha256:884e0b7f42f303641b0f4e78755aea647521dca0b89ca3be7992ad78b5c07434
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY --from=build --chown=nginx:nginx /app/runtime/ /usr/share/nginx/html/
COPY docker/nginx/pspad.conf /etc/nginx/conf.d/default.conf
COPY docker/nginx/security-headers.inc.template /etc/nginx/templates/security-headers.inc.template
COPY --chmod=755 docker/nginx/19-pspad-csp.envsh /docker-entrypoint.d/19-pspad-csp.envsh
COPY --chmod=755 src/PSPad.App/docker-entrypoint.sh /docker-entrypoint.d/pspad.sh
EXPOSE 8080
