# [Choice] .NET Core version: 3.1, 2.1
ARG VARIANT=3.1
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


RUN mkdir $HOME/dotnet_install && cd $HOME/dotnet_install
RUN curl -H 'Cache-Control: no-cache' -L https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh

# net 5 sdk links currently broken in script so specifying runtime only
# dotnet_install: Error: Could not find/download: `.NET Core SDK` with version = 5.0.0
# dotnet_install: Error: Refer to: https://aka.ms/dotnet-os-lifecycle for information on .NET Core support

#RUN sudo bash dotnet-install.sh --architecture x64 --runtime-id linux-x64 --version 5.0.0 --verbose
RUN sudo bash dotnet-install.sh --architecture x64 --runtime-id linux-x64 --version 5.0.0 --runtime dotnet --verbose