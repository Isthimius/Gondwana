FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["global.json", "."]
COPY ["Tooling/Gondwana.Mcp/Gondwana.Mcp.csproj", "Tooling/Gondwana.Mcp/"]

RUN dotnet restore "Tooling/Gondwana.Mcp/Gondwana.Mcp.csproj"

COPY . .

RUN dotnet publish "Tooling/Gondwana.Mcp/Gondwana.Mcp.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Gondwana.Mcp.dll"]
