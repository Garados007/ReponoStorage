FROM mcr.microsoft.com/dotnet/sdk:6.0 as builder
WORKDIR /src
COPY . .
RUN dotnet build --nologo -c RELEASE \
        Storage.csproj && \
    dotnet publish --nologo -c RELEASE -o /app \
        Storage.csproj

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=builder /app /app
CMD [ "dotnet", "/app/Storage.dll" ]
