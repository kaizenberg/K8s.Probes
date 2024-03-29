FROM mcr.microsoft.com/dotnet/core/runtime:3.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["Kubernetes.Probes.Worker/Kubernetes.Probes.Worker.csproj", "Kubernetes.Probes.Worker/"]
COPY ["Kubernetes.Probes.Core/Kubernetes.Probes.Core.csproj", "Kubernetes.Probes.Core/"]
RUN dotnet restore "Kubernetes.Probes.Worker/Kubernetes.Probes.Worker.csproj"
COPY . .
WORKDIR "/src/Kubernetes.Probes.Worker"
RUN dotnet build "Kubernetes.Probes.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Kubernetes.Probes.Worker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kubernetes.Probes.Worker.dll"]