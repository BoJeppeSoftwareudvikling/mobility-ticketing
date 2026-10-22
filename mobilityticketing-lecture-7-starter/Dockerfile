FROM mcr.microsoft.com/dotnet/sdk:8.0.426-bookworm-slim@sha256:f7e28a0a3f6f9235929ec19274fd0ddf8534e9159f99fac602ac73304f1ce99f AS build
WORKDIR /src
COPY . .
RUN dotnet restore MobilityTicketing.Lecture07.sln --locked-mode
RUN dotnet build MobilityTicketing.Lecture07.sln -c Release --no-restore
RUN dotnet publish src/MobilityTicketing.Lecture07 -c Release --no-build --no-restore -o /out
FROM build AS tests
ENTRYPOINT ["dotnet", "test", "tests/MobilityTicketing.Lecture07.Tests", "-c", "Release", "--no-build", "--no-restore", "--logger", "trx", "--results-directory", "/evidence/tests"]
FROM mcr.microsoft.com/dotnet/runtime:8.0.32-bookworm-slim@sha256:60d11c2be381e7f90b8bd984472dd2432933858aba55087d5936980cf2071d5a AS app
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "MobilityTicketing.Lecture07.dll"]
