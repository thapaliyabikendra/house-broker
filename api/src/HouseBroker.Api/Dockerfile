FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish src/HouseBroker.Api/HouseBroker.Api.csproj -c Release -o /out/api

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out/api .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "HouseBroker.Api.dll"]
