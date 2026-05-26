FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SmartPlanner/SmartPlanner.csproj", "SmartPlanner/"]
RUN dotnet restore "SmartPlanner/SmartPlanner.csproj"
COPY . .
WORKDIR "/src/SmartPlanner"
RUN dotnet build "SmartPlanner.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SmartPlanner.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartPlanner.dll"]
