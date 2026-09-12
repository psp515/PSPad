FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/PSPad.App/PSPad.App.csproj -c Release -o /app

FROM nginx:alpine
RUN apk add --no-cache gettext
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY docker/nginx/pspad.conf /etc/nginx/conf.d/default.conf
COPY src/PSPad.App/docker-entrypoint.sh /docker-entrypoint.d/pspad.sh
RUN chmod +x /docker-entrypoint.d/pspad.sh
EXPOSE 8080
