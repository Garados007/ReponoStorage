FROM mcr.microsoft.com/dotnet/sdk:6.0 as builder
WORKDIR /src
COPY . .
RUN dotnet build --nologo -c RELEASE \
        ReponoStorage.csproj && \
    dotnet publish --nologo -c RELEASE -o /app \
        ReponoStorage.csproj

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /data
COPY --from=builder /app /app
CMD [ "dotnet", "/app/ReponoStorage.dll" ]
