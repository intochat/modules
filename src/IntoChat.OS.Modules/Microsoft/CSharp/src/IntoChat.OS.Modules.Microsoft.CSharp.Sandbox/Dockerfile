# The sandbox host: a runtime-only image. Behaviors arrive compiled; nothing compiles here.
# It carries the brain client, the DigitalBrain and DigitalBrain.UI core packages and every contracts assembly, so a brain can check the image speaks its
# contracts before loading anything. Build from the repository root:
#   docker build -f src/IntoChat.OS.Modules/Microsoft/CSharp/src/IntoChat.OS.Modules.Microsoft.CSharp.Sandbox/Dockerfile -t csharp-sandbox .
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.3 AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    CodeGraphRefresh=false
WORKDIR /src
COPY . .
RUN mkdir -p /nuget && if [ -d /src/.nuget-local ]; then cp /src/.nuget-local/*.nupkg /nuget/ 2>/dev/null || true; fi \
    && dotnet publish src/IntoChat.OS.Modules/Microsoft/CSharp/src/IntoChat.OS.Modules.Microsoft.CSharp.Sandbox/IntoChat.OS.Modules.Microsoft.CSharp.Sandbox.csproj -c Release -o /app \
    && for contracts in $(find src -name '*.Contracts.csproj' -not -path '*/tests/*') $(grep -rl --include='*.csproj' '<IntoChatOSContractsHost>true</IntoChatOSContractsHost>' src); do dotnet build "$contracts" -c Release -p:CopyLocalLockFileAssemblies=true -o /app/contracts || exit 1; done \
    && cp -n /app/contracts/*.dll /app/ && rm -rf /app/contracts     && test -f /app/DigitalBrain.dll && test -f /app/DigitalBrain.UI.dll

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.3
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_NOLOGO=1
EXPOSE 8080
ENTRYPOINT ["dotnet", "IntoChat.OS.Modules.Microsoft.CSharp.Sandbox.dll"]
