using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Tabs;

namespace Dnn.Libraries.DynamicRoutes
{
    /// <summary>
    /// HTTP Module that handles recursive slug-based routing for DNN.
    ///
    /// DNN pages named with brackets (e.g. [community], [company]) are
    /// dynamic segments. The module auto-detects them from the page tree;
    /// no explicit registration needed. Any URL value is accepted without
    /// validation - the matched values are stored in HttpContext.Items
    /// for downstream code to consume freely.
    ///
    /// HttpContext.Items after /keizerswaard/bond/ardit:
    ///   Items["community"] = "keizerswaard"
    ///   Items["company"]   = "bond"
    ///   Items["RouteKeys"] = string[] { "community", "company" }
    ///   Items["RouteActive"]  = true
    /// </summary>
    public class DynamicRoutes : IHttpModule
    {
        private const bool EnableLogging = false;

        // ── HttpContext.Items keys ───────────────────────────────────
        private const string ItemOriginalPath    = "RouteOriginalPath";
        private const string ItemRouteKeys       = "RouteKeys";
        internal const string ItemRouteActive    = "RouteActive";
        private const string ItemRawOriginalPath = "_OriginalPath";
        private const string Page404             = "404 Error Page";

        // ── Helpers ──────────────────────────────────────────────────

        private static bool IsDynamic(string pageName) =>
            pageName != null && pageName.Length > 2
            && pageName[0] == '[' && pageName[pageName.Length - 1] == ']';

        private static string ParamName(string pageName) =>
            pageName.Substring(1, pageName.Length - 2);

        private static readonly string[] DnnVirtualPrefixes = new[]
        {
            "api", "login", "register", "logoff", "tabid"
        };

        private static bool IsSystemPrefix(string segment)
        {
            foreach (var prefix in DnnVirtualPrefixes)
                if (segment.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;

            var dir = System.IO.Path.Combine(HttpRuntime.AppDomainAppPath, segment);
            return System.IO.Directory.Exists(dir);
        }

        // ── Module lifecycle ─────────────────────────────────────────

        public void Init(HttpApplication context)
        {
            context.BeginRequest += OnBeginRequest;
        }

        private void OnBeginRequest(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var request = app.Context.Request;
            var path = request.Url.AbsolutePath;

            if (app.Context.Items[ItemRawOriginalPath] == null)
                app.Context.Items[ItemRawOriginalPath] = path;

            try
            {
                ProcessRoute(app, request, path);
            }
            catch (Exception ex)
            {
                Log("EXCEPTION for " + path + ": " + ex);
            }
        }

        private static void Log(string msg)
        {
            #pragma warning disable CS0162
            if (!EnableLogging) return;
            try
            {
                var logFile = System.IO.Path.Combine(
                    HttpRuntime.AppDomainAppPath, "App_Data", "DynamicRoutes.log");
                System.IO.File.AppendAllText(logFile,
                    DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine);
            }
            catch { }
            #pragma warning restore CS0162
        }

        private void ProcessRoute(HttpApplication app, HttpRequest request, string path)
        {
            if (path.Contains("."))
                return;
            var trimmed = path.Trim('/');
            if (string.IsNullOrEmpty(trimmed))
                return;

            var segments = trimmed.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return;

            Log(path + " -> BEGIN routing (" + segments.Length + " segment(s): "
                + string.Join("/", segments) + ")");

            // 1. Skip physical/virtual directories
            if (IsSystemPrefix(segments[0]))
            {
                Log(path + " -> skipped (physical directory '" + segments[0] + "')");
                return;
            }

            // 1b. Skip DNN control URLs (e.g. /page/ctl/Edit/mid/418)
            if (segments.Any(s => s.Equals("ctl", StringComparison.OrdinalIgnoreCase)))
            {
                Log(path + " -> skipped (ctl URL)");
                return;
            }

            // 2. Resolve portal
            int portalId = ResolvePortalId(request);
            if (portalId < 0)
                return;

            // 3. Get all tabs and build parent → children lookup (filters deleted once)
            var allTabs = TabController.Instance.GetTabsByPortal(portalId).AsList();
            var childrenOf = allTabs
                .Where(t => !t.IsDeleted)
                .ToLookup(t => t.ParentId);
            var errorTab = allTabs.FirstOrDefault(t =>
                !t.IsDeleted && t.TabName.Equals(Page404, StringComparison.OrdinalIgnoreCase));

            // 4. If every URL segment matches a real DNN page, let DNN handle it.
            {
                int walkParent = -1;
                bool allLiteral = true;
                for (int i = 0; i < segments.Length; i++)
                {
                    var pageMatch = childrenOf[walkParent]
                        .FirstOrDefault(t =>
                            t.TabName.Equals(segments[i], StringComparison.OrdinalIgnoreCase));
                    if (pageMatch != null)
                    {
                        walkParent = pageMatch.TabID;
                    }
                    else
                    {
                        allLiteral = false;
                        break;
                    }
                }
                if (allLiteral)
                {
                    Log(path + " -> all segments match real DNN pages, skipping");
                    return;
                }
            }

            // 4c. Standalone pages: first segment matches a child of a dynamic root page.
            foreach (var dynRoot in childrenOf[-1].Where(t => IsDynamic(t.TabName)))
            {
                var childMatch = childrenOf[dynRoot.TabID]
                    .FirstOrDefault(t =>
                        t.TabName.Equals(segments[0], StringComparison.OrdinalIgnoreCase));
                if (childMatch == null)
                    continue;

                if (segments.Length == 1)
                {
                    Log(path + " -> standalone page '" + segments[0] + "' (TabId=" + childMatch.TabID + ")");
                    app.Context.Items[ItemRouteActive] = true;
                    app.Context.Items[ItemOriginalPath] = path;
                    app.Context.Items["_DeferredTabId"] = childMatch.TabID;
                    app.Context.RewritePath("/Default.aspx", "", "TabId=" + childMatch.TabID);
                    return;
                }

                Log(path + " -> standalone page '" + segments[0] + "' with extra segments, rewriting to 404");
                RewriteTo404(app, errorTab);
                return;
            }

            // 5. Slug routing: walk URL segments against the DNN page tree.
            //    Any value is accepted for [param] pages - no validation.
            var resolvedPath = "";
            var extraPathInfo = "";
            var routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int currentParent = -1;
            bool matchedDynamic = false;   // matched at least one [param] segment

            for (int segIndex = 0; segIndex < segments.Length; segIndex++)
            {
                var segment = segments[segIndex];
                var children = childrenOf[currentParent];

                // a) Literal match - non-dynamic child page
                var literalMatch = children.FirstOrDefault(t =>
                    t.TabName.Equals(segment, StringComparison.OrdinalIgnoreCase)
                    && !IsDynamic(t.TabName));

                if (literalMatch != null)
                {
                    resolvedPath += "/" + literalMatch.TabName;
                    currentParent = literalMatch.TabID;
                    Log(path + " seg[" + segIndex + "] '" + segment + "' -> literal page");
                    continue;
                }

                // b) Dynamic match - accept ANY value, no validation
                var dynamicChild = children.FirstOrDefault(t => IsDynamic(t.TabName));
                if (dynamicChild != null)
                {
                    var dynParamName = ParamName(dynamicChild.TabName);

                    resolvedPath += "/" + dynamicChild.TabName;
                    routeValues[dynParamName] = segment;
                    currentParent = dynamicChild.TabID;
                    matchedDynamic = true;
                    Log(path + " seg[" + segIndex + "] '" + segment
                        + "' -> dynamic '" + dynamicChild.TabName + "'");
                    continue;
                }

                // c) No match
                if (matchedDynamic)
                {
                    // Active slug route - append remaining as friendly-URL params
                    for (int r = segIndex; r < segments.Length; r++)
                    {
                        resolvedPath += "/" + segments[r];
                        extraPathInfo += "/" + segments[r];
                    }
                    Log(path + " seg[" + segIndex + "] '" + segment
                        + "' -> no child page, appending remaining as friendly-URL params");
                    break;
                }

                if (segIndex > 0)
                {
                    // Real page(s) followed by an unresolvable segment -> 404
                    Log(path + " seg[" + segIndex + "] '" + segment
                        + "' -> real page(s) + unresolvable segment, rewriting to 404");
                    RewriteTo404(app, errorTab);
                    return;
                }

                Log(path + " seg[" + segIndex + "] '" + segment + "' -> no match, returning");
                return;
            }

            // Only rewrite when at least one dynamic segment was captured.
            // A purely-literal path is handled by DNN (see step 4).
            if (!matchedDynamic)
                return;

            // 6. Store all route values in HttpContext.Items - no validation
            foreach (var kvp in routeValues)
                app.Context.Items[kvp.Key] = kvp.Value;

            app.Context.Items[ItemRouteKeys] = routeValues.Keys.ToArray();
            app.Context.Items[ItemRouteActive] = true;

            Log(path + " -> REWRITE /Default.aspx?TabId=" + currentParent
                + " (" + resolvedPath + ") values=["
                + string.Join(", ", routeValues.Select(kv => kv.Key + "=" + kv.Value))
                + "]");

            app.Context.Items[ItemOriginalPath] = path;
            app.Context.Items["_DeferredTabId"] = currentParent;
            app.Context.Items["_DeferredExtraPath"] = extraPathInfo;
            app.Context.RewritePath("/Default.aspx", extraPathInfo, "TabId=" + currentParent);
        }

        private static int ResolvePortalId(HttpRequest request)
        {
            var host = request.Url.Host;

            #pragma warning disable CS0618
            var aliases = PortalAliasController.Instance.GetPortalAliases();
            if (aliases == null)
                return 0;

            foreach (var key in aliases.Keys)
            {
                var alias = aliases[key.ToString()];
                if (alias != null)
                {
                    var aliasHost = alias.HTTPAlias.Split('/')[0];
                    if (aliasHost.Equals(host, StringComparison.OrdinalIgnoreCase))
                        return alias.PortalID;
                }
            }
            #pragma warning restore CS0618

            return 0;
        }

        private static void RewriteTo404(HttpApplication app, TabInfo errorTab)
        {
            if (errorTab != null)
            {
                app.Context.Items[ItemRouteActive] = true;
                app.Context.RewritePath("/" + errorTab.TabName);
                app.Context.Response.StatusCode = 404;
            }
        }

        public void Dispose() { }
    }
}