FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["McpServer/McpServer.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet publish "McpServer/McpServer.csproj" -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 6000
ENTRYPOINT ["dotnet", "McpServer.dll"]
