FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PadelBooking.Api.csproj", "."]
COPY ["src/PadelBooking.Domain/PadelBooking.Domain.csproj", "src/PadelBooking.Domain/"]
COPY ["src/PadelBooking.Application/PadelBooking.Application.csproj", "src/PadelBooking.Application/"]
COPY ["src/PadelBooking.Infrastructure/PadelBooking.Infrastructure.csproj", "src/PadelBooking.Infrastructure/"]
RUN dotnet restore "PadelBooking.Api.csproj"

COPY . .
RUN dotnet publish "PadelBooking.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/wwwroot/uploads/courts \
    && chown -R $APP_UID:$APP_UID /app/wwwroot

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "PadelBooking.Api.dll"]
