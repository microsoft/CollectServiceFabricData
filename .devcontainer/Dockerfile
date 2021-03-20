# [Choice] .NET Core version: 3.1, 2.1
ARG VARIANT=5.0
FROM mcr.microsoft.com/vscode/devcontainers/dotnetcore:${VARIANT}

# [Option] Install Node.js
ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "source /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi

# [Option] Install Azure CLI
ARG INSTALL_AZURE_CLI="false"
COPY library-scripts/azcli-debian.sh /tmp/library-scripts/
RUN if [ "$INSTALL_AZURE_CLI" = "true" ]; then bash /tmp/library-scripts/azcli-debian.sh; fi \
    && apt-get clean -y && rm -rf /var/lib/apt/lists/* /tmp/library-scripts

# [Optional] Uncomment this section to install additional OS packages.
# RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
#     && apt-get -y install --no-install-recommends <your-package-list-here>

# [Optional] Uncomment this line to install global node packages.
# RUN su vscode -c "source /usr/local/share/nvm/nvm.sh && npm install -g <your-package-here>" 2>&1

# install .net 5
# dotnet-install.sh  [--architecture <ARCHITECTURE>] [--azure-feed]
#    [--channel <CHANNEL>] [--dry-run] [--feed-credential]
#    [--install-dir <DIRECTORY>] [--jsonfile <JSONFILE>]
#    [--no-cdn] [--no-path] [--runtime <RUNTIME>] [--runtime-id <RID>]
#    [--skip-non-versioned-files] [--uncached-feed] [--verbose]
#    [--version <VERSION>]
# https://dot.net/v1/dotnet-install.sh

# https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu#1804-
RUN wget https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN sudo dpkg -i packages-microsoft-prod.deb

# sdk
# RUN sudo apt-get update; \
#   sudo apt-get install -y apt-transport-https && \
#   sudo apt-get update && \
#   sudo apt-get install -y dotnet-sdk-5.0

# runtime asp
# RUN sudo apt-get update; \
#  sudo apt-get install -y apt-transport-https && \
#  sudo apt-get update && \
#  sudo apt-get install -y aspnetcore-runtime-5.0

# runtime net5
#RUN sudo apt-get install -y dotnet-runtime-5.0
