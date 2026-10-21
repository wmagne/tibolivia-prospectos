# Imagen del Sistema de Prospección TI Bolivia (ASP.NET Core 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/TiBolivia.Prospeccion/TiBolivia.Prospeccion.csproj src/TiBolivia.Prospeccion/
RUN dotnet restore src/TiBolivia.Prospeccion/TiBolivia.Prospeccion.csproj
COPY src/ src/
RUN dotnet publish src/TiBolivia.Prospeccion/TiBolivia.Prospeccion.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=America/La_Paz \
    DataProtection__Ruta=/claves
VOLUME /claves
EXPOSE 8080
ENTRYPOINT ["dotnet", "TiBolivia.Prospeccion.dll"]
