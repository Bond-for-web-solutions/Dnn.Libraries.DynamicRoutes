# Dnn.Libraries.DynamicRoutes

DotNetNuke 9.x library that adds **Next.js-style dynamic segment routing**
to the DNN page tree. Any DNN page whose name is bracketed (e.g.
`[community]`, `[company]`) becomes a dynamic URL segment whose value is
captured into `HttpContext.Items` for downstream skins, modules, and Web
API endpoints to consume.

- **Package name (manifest):** `DynamicRoutes`
- **Package type:** `Library`
- **Assembly / namespace:** `Dnn.Libraries.DynamicRoutes`
- **DNN core dependency:** `08.00.00+`
- **Target framework:** `.NET Framework 4.8`

---

## How it works

`Dnn.Libraries.DynamicRoutes` ships **two** `IHttpModule`s that wrap DNN's
`UrlRewrite` module:

1. `DynamicRoutes` runs **before** `UrlRewrite`. On `BeginRequest` it
   walks the DNN page tree, matching each URL segment either to a real
   page or to a bracketed `[param]` page; on match it stores the segment
   value in `HttpContext.Items[<param>]` and rewrites the path to
   `Default.aspx?TabId=<id>`.
2. `DynamicRoutesFix` runs **after** `UrlRewrite`. DNN's `UrlRewrite`
   tends to issue a 301 because its canonical friendly URL (with the
   brackets stripped) does not match the browser URL. This second module
   detects that 301 on a route flagged by `DynamicRoutes` and cancels
   it, leaving the rewrite in place and letting the request finish
   normally.

The two modules are coupled through `HttpContext.Items`; the install
manifest registers them around `UrlRewrite` automatically.

### Decision rules

`DynamicRoutes` **skips** a request when:

- the URL contains a file extension (`.js`, `.png`, `.aspx`, ...);
- the first segment is a physical directory in the web root
  (auto-detected via `Directory.Exists`);
- any segment is in the reserved-prefix list (`api`, `login`, `register`,
  `logoff`, `tabid`);
- any segment is `ctl` (case-insensitive) - a DNN control URL;
- every segment resolves to an existing DNN page (DNN already knows what
  to do).

Otherwise, it walks the page tree, matching literal child pages or
bracketed `[param]` children, and rewrites to the resolved `TabId`.
Unmatched trailing segments are passed through as friendly-URL
`PathInfo`.

### Example

With dynamic pages `[community]` and `[company]` in the page tree, the
URL `/keizerswaard/bond/dashboard` resolves like this:

| Segment        | Match                   | Result                                |
| -------------- | ----------------------- | ------------------------------------- |
| `keizerswaard` | `[community]` (dynamic) | `Items["community"] = "keizerswaard"` |
| `bond`         | `[company]` (dynamic)   | `Items["company"]   = "bond"`         |
| `dashboard`    | literal child page      | -                                     |

| URL                          | Result                                            |
| ---------------------------- | ------------------------------------------------- |
| `/keizerswaard`              | Rewrites to `/[community]`                        |
| `/keizerswaard/dashboard`    | Rewrites to `/[community]/dashboard`              |
| `/keizerswaard/bond`         | Rewrites to `/[community]/[company]`              |
| `/dashboard`                 | Standalone -> rewrites to `/[community]/dashboard`|
| `/admin`                     | Skipped (reserved prefix)                         |
| `/some-real-page`            | Skipped (existing DNN page)                       |

---

## Project layout

| Path / file                              | Purpose                                                                          |
| ---------------------------------------- | -------------------------------------------------------------------------------- |
| `DynamicRoutes.cs`                       | First `IHttpModule` - captures slugs and rewrites the path                       |
| `DynamicRoutesFix.cs`                    | Second `IHttpModule` - cancels DNN's 301 after `UrlRewrite`                      |
| `Dnn.Libraries.DynamicRoutes.csproj`     | .NET Framework 4.8 project file                                                  |
| `Dnn.Libraries.DynamicRoutes.sln`        | Solution file                                                                    |
| `manifest.dnn`                           | DNN install manifest (registers assembly + both HTTP modules around `UrlRewrite`)|
| `BuildScripts/`                          | MSBuild props/targets that produce the install zip                               |
| `test/`                                  | NUnit tests (`DynamicRoutesHelperTests`, `DynamicRoutesModuleTests`)             |
| `install/`                               | Build output: `Dnn.Libraries.DynamicRoutes_<version>_Install.zip`                |

The manifest registers the two HTTP modules with fully-qualified type
names:

```text
Dnn.Libraries.DynamicRoutes.DynamicRoutes,    Dnn.Libraries.DynamicRoutes
Dnn.Libraries.DynamicRoutes.DynamicRoutesFix, Dnn.Libraries.DynamicRoutes
```

---

## Build

This project is built with MSBuild (the targets in `BuildScripts/` produce
the install zip on Release builds).

### VS Code

`Ctrl+Shift+B` runs the default build task (Release configuration).

### Visual Studio

Open `Dnn.Libraries.DynamicRoutes.sln` and build the solution
(`Ctrl+Shift+B`). Release builds package the install zip automatically.

### Command line

Use the MSBuild that ships with Visual Studio (any edition). Example:

```powershell
& "C:\Program Files\Microsoft Visual Studio\<edition>\MSBuild\Current\Bin\MSBuild.exe" `
    Dnn.Libraries.DynamicRoutes.sln -p:Configuration=Release
```

Release output:

- `bin/Release/Dnn.Libraries.DynamicRoutes.dll`
- `install/Dnn.Libraries.DynamicRoutes_01.00.00_Install.zip`
- `install/Dnn.Libraries.DynamicRoutes_01.00.00_Source.zip`

See [`BuildScripts/README.md`](BuildScripts/README.md) for the
`DnnBinRoot` setting and how the packaging targets work.

---

## Install

1. **Settings -> Extensions -> Install Extension** in DNN.
2. Upload `install/Dnn.Libraries.DynamicRoutes_<version>_Install.zip`.
3. The manifest installs the assembly into `bin/` and inserts the two
   HTTP modules around `UrlRewrite` in `web.config`.

Verify the resulting `web.config` order is preserved:

```xml
<modules>
  <!-- ... -->
  <add name="DynamicRoutes"
       type="Dnn.Libraries.DynamicRoutes.DynamicRoutes, Dnn.Libraries.DynamicRoutes"
       preCondition="managedHandler" />
  <add name="UrlRewrite" ... />        <!-- DNN built-in -->
  <add name="DynamicRoutesFix"
       type="Dnn.Libraries.DynamicRoutes.DynamicRoutesFix, Dnn.Libraries.DynamicRoutes"
       preCondition="managedHandler" />
</modules>
```

> Order is **load-bearing**. If `web.config` is rewritten by another
> install or by hand, re-check this section.

---

## Consuming the route values

Route values are written to `HttpContext.Items`, keyed by the param name
(without brackets):

```csharp
var community = HttpContext.Current.Items["community"] as string;
var company   = HttpContext.Current.Items["company"]   as string;
var keys      = HttpContext.Current.Items["RouteKeys"] as string[];
var isRouted  = HttpContext.Current.Items["RouteActive"] is bool b && b;
var original  = HttpContext.Current.Items["RouteOriginalPath"] as string;
```

| Key                  | Type       | Description                                   |
| -------------------- | ---------- | --------------------------------------------- |
| `community`          | `string`   | Captured `[community]` slug                   |
| `company`            | `string`   | Captured `[company]` slug                     |
| `RouteKeys`          | `string[]` | All matched param names                       |
| `RouteActive`        | `bool`     | `true` when this module applied a rewrite     |
| `RouteOriginalPath`  | `string`   | Original URL path before rewriting            |
| `_DeferredTabId`     | `int`      | TabId the URL was rewritten to (internal)     |
| `_DeferredExtraPath` | `string`   | Trailing path passed as `PathInfo` (internal) |
| `_OriginalPath`      | `string`   | First-seen request path (internal)            |

`Dnn.Libraries.CommunityAuth.CommunityContext.Current` reads `community`
and `company` from this dictionary, validates them, and exposes the
typed result. Prefer it in application code over reading the raw items.

---

## Adding a new dynamic segment

1. Create a DNN page named `[param]` (brackets included).
2. The module detects it on the next request - no code change required.
3. Read the value: `HttpContext.Current.Items["param"] as string`.

---

## Debug logging

Toggle the constant at the top of `DynamicRoutes.cs`:

```csharp
private const bool EnableLogging = true;
```

Each request is appended to `App_Data/DynamicRoutes.log` with a
timestamp. Turn this off for production builds.

---

## Tests

`test/Dnn.Libraries.DynamicRoutes.Tests.csproj` contains coverage for the
helper / detection logic. Run from VS Code or Visual Studio's Test
Explorer.

---

## Troubleshooting

| Symptom                              | Cause                                              | Fix                                                                |
| ------------------------------------ | -------------------------------------------------- | ------------------------------------------------------------------ |
| `404` on `/{slug}`                   | No `[param]` page at the appropriate level         | Create one in **Pages**                                            |
| `404` on `/{slug}/child`             | Child page doesn't exist                           | Create the child under the `[param]` page                          |
| Browser 301 strips the brackets      | `DynamicRoutesFix` missing / out of order          | Re-install or fix the `web.config` `<modules>` order               |
| Items missing in a downstream module | Modules registered in wrong order / wrong assembly | Confirm the entries in `web.config` and that the DLL is in `bin/`  |
| Changes not picked up                | IIS cached the old DLL                             | `iisreset` or recycle the app pool                                 |
| Redirect loop                        | Browser cached the old 301                         | Hard-reload or test in a private window                            |

---

## Deep-dive docs

For the request-flow diagram, the full `HttpContext.Items` contract, and
the handover-prompt voice (for an AI assistant taking over the project),
see `c:\DNN\dzp\workspace\docs\Dnn.Libraries.DynamicRoutes.md` in the
DZP portal workspace.
