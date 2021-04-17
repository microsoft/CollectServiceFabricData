using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Specialized;
using System.Web;

namespace BlazorApp.Helpers
{
    public static class ExtensionMethods
    {
        // get entire querystring name/value collection
        public static NameValueCollection QueryString(this NavigationManager navigationManager)
        {
            return HttpUtility.ParseQueryString(new Uri(navigationManager.Uri).Query);
        }

        // get single querystring value with specified key
        public static string QueryString(this NavigationManager navigationManager, string key)
        {
            return navigationManager.QueryString()[key];
        }
    }
}