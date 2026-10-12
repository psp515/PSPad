FROM debian:bookworm-slim@sha256:7c7b2c966bc9ee8cedfeef67e0e279108992c77681fa595db4a9d65c06ccc587 AS tools
ARG TARGETARCH
ARG RCLONE_VERSION=1.75.1
ARG AGE_VERSION=1.3.2
ARG SUPERCRONIC_VERSION=0.2.48
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl unzip \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /download
COPY checksums.txt .
RUN set -eu; \
    curl -fsSLO "https://github.com/rclone/rclone/releases/download/v${RCLONE_VERSION}/rclone-v${RCLONE_VERSION}-linux-${TARGETARCH}.zip"; \
    curl -fsSLO "https://github.com/FiloSottile/age/releases/download/v${AGE_VERSION}/age-v${AGE_VERSION}-linux-${TARGETARCH}.tar.gz"; \
    curl -fsSLO "https://github.com/aptible/supercronic/releases/download/v${SUPERCRONIC_VERSION}/supercronic-linux-${TARGETARCH}"; \
    grep -- "-linux-${TARGETARCH}" checksums.txt | sha256sum -c -; \
    mkdir -p /out; \
    unzip -q "rclone-v${RCLONE_VERSION}-linux-${TARGETARCH}.zip"; \
    install -m 0755 "rclone-v${RCLONE_VERSION}-linux-${TARGETARCH}/rclone" /out/rclone; \
    tar -xzf "age-v${AGE_VERSION}-linux-${TARGETARCH}.tar.gz"; \
    install -m 0755 age/age age/age-keygen /out/; \
    install -m 0755 "supercronic-linux-${TARGETARCH}" /out/supercronic

FROM mongo:8.3.11 AS mongo
COPY --from=tools /out/ /usr/local/bin/
COPY backup /usr/local/bin/backup
COPY lib/common.sh lib/mongo.sh /usr/local/lib/backup/
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates \
 && rm -rf /var/lib/apt/lists/* \
 && useradd --uid 10001 --user-group --no-create-home --shell /usr/sbin/nologin pspad-backup \
 && install -d -o 10001 -g 10001 -m 0700 /backups
ENV BACKUP_STACK=mongo HOME=/tmp RCLONE_CONFIG=/tmp/rclone.conf
USER 10001
VOLUME /backups
HEALTHCHECK --interval=5m --timeout=10s CMD ["backup", "healthcheck"]
ENTRYPOINT ["backup"]
CMD ["run"]

FROM postgres:17.11 AS keycloak
COPY --from=tools /out/ /usr/local/bin/
COPY backup /usr/local/bin/backup
COPY lib/common.sh lib/keycloak.sh /usr/local/lib/backup/
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates \
 && rm -rf /var/lib/apt/lists/* \
 && useradd --uid 10001 --user-group --no-create-home --shell /usr/sbin/nologin pspad-backup \
 && install -d -o 10001 -g 10001 -m 0700 /backups
ENV BACKUP_STACK=keycloak HOME=/tmp RCLONE_CONFIG=/tmp/rclone.conf
USER 10001
VOLUME /backups
HEALTHCHECK --interval=5m --timeout=10s CMD ["backup", "healthcheck"]
ENTRYPOINT ["backup"]
CMD ["run"]
