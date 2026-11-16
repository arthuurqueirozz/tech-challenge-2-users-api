FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/FCG.Users.Api/FCG.Users.Api.csproj", "src/FCG.Users.Api/"]
COPY ["src/FCG.Users.Domain/FCG.Users.Domain.csproj", "src/FCG.Users.Domain/"]
COPY ["src/FCG.Users.Infrastructure/FCG.Users.Infrastructure.csproj", "src/FCG.Users.Infrastructure/"]
RUN dotnet restore "src/FCG.Users.Api/FCG.Users.Api.csproj"

COPY . .
RUN dotnet publish "src/FCG.Users.Api/FCG.Users.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ARG BUILD_SHA=development
ENV FCG_BUILD_SHA=${BUILD_SHA}
LABEL org.opencontainers.image.revision=${BUILD_SHA}
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "FCG.Users.Api.dll"]
