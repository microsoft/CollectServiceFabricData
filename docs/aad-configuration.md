# Azure Active Directory Configuration

Any application accessing Azure resources on behalf of a user requires an Azure Active Directory (AAD) application registration. CollectSFData accesses Azure Data Explorer (Kusto) or Azure resource information if using Log Analytics. For CollectSFData to function, an AAD app registration is required. CollectSFData parameter 'azureClientId' is set to the app registration 'Application (client) ID' guid value. Use steps below to setup Azure app registration for use with CollectSFData.

## Create Azure Active Directory Application Registration

The following describes how to create an AAD app registration for use with CollectSFData for access to Azure resources and Kusto. Only required settings are configured for a default Azure subscription. Additional settings may be required for different environments.

**NOTE: these steps may require AAD administrative permissions to complete depending on the configuration of Azure environment and configuration being set below.**

1. Open Azure portal https://portal.azure.com and navigate to 'Azure Active Directory' blade https://portal.azure.com/#blade/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/Overview  

    ![](media/azure-aad-overview.png)

1. Select 'App registrations', 'New registration' https://portal.azure.com/#blade/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/RegisteredApps.  

    ![](media/azure-app-registrations.png)

1. Enter a descriptive name for new registration and 'Register'. In this example 'collectsfdata service fabric data collection' is used.  

    ![](media/azure-register-application.png)  

1. After app is registered, select 'Authentication', 'Add a platform'.  

    ![](media/azure-app-authentication.png)

1. Select 'Mobile and desktop applications'

    ![](media/azure-app-configure-platforms.png)

1. Select 'https://login.microsoftonline.com/common/oauth2/nativeclient' for the 'Redirect URIs' and 'Configure'.  

    ![](media/azure-configure-desktop-devices.png)

1. Authentication configuration should look same / similar to below.

    ![](media/azure-app-authentication-configuration.png)

1. Select 'API permissions' to modify permissions. By default, 'User.Read' permissions are already added.

    ![](media/azure-api-permissions.png)

1. Select 'Add a permission', then 'Azure Data Explorer' Microsoft Api.

    ![](media/azure-select-kusto-api.png)

1. Select 'user_impersonation' under 'delegated permissions', then 'Add permissions'.

    ![](media/azure-select-kusto-permissions.png)

1. After Kusto permission is added, API permissions should be configured as below.

    ![](media/azure-api-permissions-configured.png)

## (Optional) Add certificate as a client secret to app registration for non-interactive authentication

For environments where CollectSFData utility executes non-interactively, for example if utility is called from a service, a client secret can be used to prevent interactive authentication prompt. Best practice is for a certificate to be used as a secret. If needed, use the steps below to add a certificate to app registration for use with non-interactive logon.

## Add app registration to Kusto cluster and database


## Configuring CollectSFData to use App Registration

After app registration has been created, copy the 'Application (client) ID' guid value. Set CollectSFData parameter 'azureClientId' to the guid value and optionally 'azureClientSecret' to certificate base64 value. These values can be done via command line or json configuration file. See [configuration](./configuration.md).

## Application consent

By default, app registrations are configured to prompt one time either per application or per user to request consent. This can be changed in 'API permissions' of the app registration. This operation requires admin permissions in AAD.

![](media/azure-app-grant-admin-consent.png)

With the default setting to prompt, the following will be displayed upon the first successful logon. Selecting 'Consent on behalf of your organization' will prevent this prompt from displaying for any other users.  

![](media/azure-app-registration-permissions-consent.png)

## Troubleshooting

If app registration creation or api permission configuration fails, it is most likely due to insufficient permissions. Additional information should be available in the portal as to reason of failure.  
