FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["LlmClient/LlmClient.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet publish "LlmClient/LlmClient.csproj" -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "LlmClient.dll"]
