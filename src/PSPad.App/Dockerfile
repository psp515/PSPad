FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/PSPad.App/PSPad.App.csproj -c Release -o /app \
 && rm /app/wwwroot/appsettings.json.br \
 && mkdir /app/runtime \
 && mv /app/wwwroot/appsettings.json /app/wwwroot/appsettings.json.gz /app/runtime/

FROM nginxinc/nginx-unprivileged:1.31.7-alpine
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY --from=build --chown=nginx:nginx /app/runtime/ /usr/share/nginx/html/
COPY docker/nginx/pspad.conf /etc/nginx/conf.d/default.conf
COPY docker/nginx/security-headers.inc.template /etc/nginx/templates/security-headers.inc.template
COPY --chmod=755 docker/nginx/19-pspad-csp.envsh /docker-entrypoint.d/19-pspad-csp.envsh
COPY --chmod=755 src/PSPad.App/docker-entrypoint.sh /docker-entrypoint.d/pspad.sh
EXPOSE 8080
