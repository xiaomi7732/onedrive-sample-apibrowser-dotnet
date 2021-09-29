using System;
using System.Diagnostics;
using Azure.Identity;
using Microsoft.Graph;

namespace OneDriveApiBrowser
{
    public class AuthenticationHelper
    {
        // The Client ID is used by the application to uniquely identify itself to the v2.0 authentication endpoint.
        static readonly string clientId = FormBrowser.MsaClientId;
        private static readonly string[] Scopes = { "Files.ReadWrite.All" };

        public static DateTimeOffset Expiration;

        private static GraphServiceClient graphClient = null;

        // Get an access token for the given context and resourceId. An attempt is first made to 
        // acquire the token silently. If that fails, then we try to acquire the token by prompting the user.
        public static GraphServiceClient GetAuthenticatedClient()
        {
            if (graphClient == null)
            {
                try
                {
                    string tenant = "common";
                    InteractiveBrowserCredentialOptions options = new InteractiveBrowserCredentialOptions
                    {
                        TenantId = tenant,
                        ClientId = clientId,
                        AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                        RedirectUri = new Uri("http://localhost"),
                    };
                    InteractiveBrowserCredential credential = new InteractiveBrowserCredential(options);
                    graphClient = new GraphServiceClient(credential, Scopes);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Could not create a graph client: " + ex.Message);
                }
            }

            return graphClient;
        }

        /// <summary>
        /// Signs the user out of the service.
        /// </summary>
        public static void SignOut()
        {
            //foreach (var user in IdentityClientApp.Users)
            //{
            //    user.SignOut();
            //}
            graphClient = null;
        }
    }
}
