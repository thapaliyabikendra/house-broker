FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish src/HouseBroker.Auth/HouseBroker.Auth.csproj -c Release -o /out/auth

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out/auth .
ENV ASPNETCORE_URLS=http://+:8080
ENV TZ=UTC
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
USER app
ENTRYPOINT ["dotnet", "HouseBroker.Auth.dll"]
