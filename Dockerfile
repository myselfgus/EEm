# Est�gio de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["EemServer/EemServer.csproj", "EemServer/"]
COPY ["EemCore/EemCore.csproj", "EemCore/"]
RUN dotnet restore "EemServer/EemServer.csproj"
COPY . .
WORKDIR "/src/EemServer"
RUN dotnet build "EemServer.csproj" -c Release -o /app/build

# Est�gio de publica��o
FROM build AS publish
RUN dotnet publish "EemServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Est�gio final
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EemServer.dll"]