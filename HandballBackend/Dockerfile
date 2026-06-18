FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
USER root
RUN apk --no-cache add git
RUN mkdir /resources
RUN chown $APP_UID /resources
ENV RESOURCES_DIRECTORY=/resources
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ./HandballBackend.csproj HandballBackend/
RUN dotnet restore "HandballBackend/HandballBackend.csproj"
WORKDIR "/src/HandballBackend"
COPY . .
RUN dotnet build "./HandballBackend.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HandballBackend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "HandballBackend.dll"]
