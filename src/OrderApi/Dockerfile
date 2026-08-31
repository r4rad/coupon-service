# Order API image. Built by the CD Package/Deploy path (section 18 / CS-29).
# Build context must be the repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json CouponService.slnx ./
COPY src/OrderApi/ src/OrderApi/
COPY data/pizzas.json data/pizzas.json
RUN dotnet restore src/OrderApi/OrderApi.csproj
RUN dotnet publish src/OrderApi/OrderApi.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    -p:OpenApiGenerateDocuments=false \
    -p:OpenApiGenerateDocumentsOnBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderApi.dll"]
