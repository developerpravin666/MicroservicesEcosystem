FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Leverage Docker layer caching: copy just the csproj/props files first, restore,
# then copy everything else — this way a source-only change doesn't invalidate the
# (slow) NuGet restore layer.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Gateway/ src/Gateway/
COPY src/Identity/ src/Identity/
COPY src/Services/ src/Services/

RUN dotnet restore "src/Gateway/ApiGateway/ApiGateway.csproj"

RUN dotnet publish "src/Gateway/ApiGateway/ApiGateway.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
