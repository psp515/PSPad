FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ src/
RUN case "$TARGETARCH" in arm64) rid=linux-arm64 ;; *) rid=linux-x64 ;; esac \
 && dotnet publish src/PSPad.Backup/PSPad.Backup.csproj -c Release -r "$rid" --self-contained \
      -p:PublishSingleFile=true -p:DebugType=none -o /out

FROM debian:bookworm-slim@sha256:7c7b2c966bc9ee8cedfeef67e0e279108992c77681fa595db4a9d65c06ccc587 AS age
ARG TARGETARCH
ARG AGE_VERSION=1.3.2
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /download
COPY src/PSPad.Backup/age.sha256 .
RUN curl -fsSLO "https://github.com/FiloSottile/age/releases/download/v${AGE_VERSION}/age-v${AGE_VERSION}-linux-${TARGETARCH}.tar.gz" \
 && grep -- "-linux-${TARGETARCH}" age.sha256 | sha256sum -c - \
 && tar -xzf "age-v${AGE_VERSION}-linux-${TARGETARCH}.tar.gz" \
 && mkdir -p /out \
 && install -m 0755 age/age age/age-keygen /out/

FROM mongo:8.3.11 AS mongo
COPY --from=age /out/ /usr/local/bin/
COPY --from=build /out/ /opt/backup/
RUN ln -s /opt/backup/backup /usr/local/bin/backup \
 && useradd --uid 10001 --user-group --no-create-home --shell /usr/sbin/nologin pspad-backup \
 && install -d -o 10001 -g 10001 -m 0700 /backups
ENV BACKUP_STACK=mongo
USER 10001
VOLUME /backups
HEALTHCHECK --interval=5m --timeout=10s CMD ["backup", "healthcheck"]
ENTRYPOINT ["backup"]
CMD ["run"]

FROM postgres:17.11 AS keycloak
COPY --from=age /out/ /usr/local/bin/
COPY --from=age /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
COPY --from=build /out/ /opt/backup/
RUN ln -s /opt/backup/backup /usr/local/bin/backup \
 && useradd --uid 10001 --user-group --no-create-home --shell /usr/sbin/nologin pspad-backup \
 && install -d -o 10001 -g 10001 -m 0700 /backups
ENV BACKUP_STACK=keycloak
USER 10001
VOLUME /backups
HEALTHCHECK --interval=5m --timeout=10s CMD ["backup", "healthcheck"]
ENTRYPOINT ["backup"]
CMD ["run"]
