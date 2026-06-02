using System;
using System.Web;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Tabs;

namespace Dnn.Libraries.DynamicRoutes
{
    /// <summary>
    /// Companion module that runs AFTER DNN's UrlRewrite in BeginRequest.
    ///
    /// Problem: DNN's UrlRewrite 301-redirects URLs whose canonical form
    /// doesn't match the browser's URL. For slug routes like /bond/home
    /// (rewritten to /[community]/home by DynamicRoutes), DNN strips the
    /// brackets → canonical = /community/home → mismatch → 301.
    ///
    /// Fix: if DynamicRoutes already resolved a slug route (RouteActive
    /// is set) and DNN issued a 301, cancel the redirect, undo
    /// CompleteRequest, re-apply the RewritePath, and let the pipeline
    /// continue normally.
    ///
    /// Register in web.config AFTER UrlRewrite:
    ///   &lt;add name="DynamicRoutesFix" ... /&gt;
    /// </summary>
    public class DynamicRoutesFix : IHttpModule
    {
        private static readonly System.Reflection.FieldInfo RequestCompletedField =
            typeof(HttpApplication).GetField("_requestCompleted",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public void Init(HttpApplication context)
        {
            context.BeginRequest += OnBeginRequest;
        }

        private void OnBeginRequest(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;

            if (app.Context.Items[DynamicRoutes.ItemRouteActive] == null)
                return;

            // Only act when DNN issued a redirect (301 / 302)
            var status = app.Context.Response.StatusCode;
            if (status != 301 && status != 302)
                return;

            // Cancel DNN's redirect
            app.Context.Response.ClearHeaders();
            app.Context.Response.ClearContent();
            app.Context.Response.StatusCode = 200;

            // Re-apply the TabId-based rewrite (DNN's redirect cleared it)
            var tabId = app.Context.Items["_DeferredTabId"];
            if (tabId != null)
            {
                var extraPath = app.Context.Items["_DeferredExtraPath"] as string ?? "";
                app.Context.RewritePath("/Default.aspx", extraPath, "TabId=" + tabId);
            }

            // Undo CompleteRequest so the pipeline continues normally
            // (PostAuth, handler execution, etc.)
            if (RequestCompletedField != null)
                RequestCompletedField.SetValue(app, false);
        }

        public void Dispose() { }
    }
}
