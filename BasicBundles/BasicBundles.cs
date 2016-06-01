using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IronStone.Web.BasicBundles
{
    using System.Collections;
    using AppFunc = Func<IDictionary<String, Object>, Task>;

    static class Extensions
    {
        static Uri DummyUri = new Uri("http://example.com", UriKind.Absolute);

        public static String MakeRelativePathFragment(String source, String target)
        {
            var result = new Uri(DummyUri, source)
                .MakeRelativeUri(new Uri(new Uri(DummyUri, target), "."))
                .ToString();

            return result.Substring(0, result.LastIndexOf('/'));
        }

        public static String ConcatUrls(String baseUri, String relativePathFragment)
        {
            if (baseUri.StartsWith("/") || baseUri.Contains(":")) return baseUri;

            var unsimplifiedResultUrl = $"{relativePathFragment}/{baseUri}";

            var resultUrl = SimplifyUrl(unsimplifiedResultUrl);

            return resultUrl;
        }

        public static String SimplifyUrl(String url)
        {
            var dummyUri = new Uri(DummyUri, String.Join("/",
                Enumerable.Range(0, url.Split(new[] { ".." }, StringSplitOptions.None).Length).Select(i => "x")));

            var absoluteResultUri = new Uri(dummyUri, url);
            var resultUri = dummyUri.MakeRelativeUri(absoluteResultUri);
            return resultUri.ToString();
        }

        public static IEnumerable<T> Bfs<T>(this IEnumerable<T> items,
            Func<T, IEnumerable<T>> childSelector)
        {
            var queue = new Queue<T>(items);
            while (queue.Any())
            {
                var next = queue.Dequeue();
                yield return next;
                foreach (var child in childSelector(next))
                    queue.Enqueue(child);
            }
        }

        public static IEnumerable<Resource> ExpandToResources(this IEnumerable<Requestable> items)
        {
            var queue = new Queue<Requestable>(items);
            while (queue.Any())
            {
                var next = queue.Dequeue();
                if (next is Resource)
                {
                    yield return next as Resource;
                }
                else if (next is Bundle)
                {
                    foreach (var child in (next as Bundle).Contents)
                        queue.Enqueue(child);
                }
                else
                {
                    throw new Exception("Unexpected type of Requestable.");
                }
            }
        }

        public static IEnumerable<Resource> ExpandToRequestables(this IEnumerable<Requestable> items)
        {
            var queue = new Queue<Requestable>(items);
            while (queue.Any())
            {
                var next = queue.Dequeue();
                if (next is Resource)
                {
                    yield return next as Resource;
                }
                else if (next is Bundle)
                {
                    foreach (var child in (next as Bundle).Contents)
                        queue.Enqueue(child);
                }
                else
                {
                    throw new Exception("Unexpected type of Requestable.");
                }
            }
        }

        public static IEnumerable<Resource> GetResourcesWithDependencies(this IEnumerable<Resource> resources)
        {
            return resources.Bfs(r => r.Dependencies).Distinct();
        }

        public static IEnumerable<T> DfsPostOrder<T>(this IEnumerable<T> items,
            Func<T, IEnumerable<T>> childrenSelector, Action<T> push, Func<T> pop)
        {
            foreach (var item in items)
            {
                yield return item;

                foreach (var descendant in childrenSelector(item).DfsPostOrder(childrenSelector, push, pop))
                {
                    yield return descendant;
                }

                push(item);

                pop();
            }
        }

        public static String ToScriptOrStylesheetName(this ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Js:
                    return "script";
                case ResourceType.Css:
                    return "stylesheet";
                default:
                    throw new ArgumentException();
            }
        }
    }

    class ResourceFlavorInfo
    {
        public String Path { get; set; }

        public String Content { get; set; }
    }

    class ResourceInfo
    {
        public Int32 Index { get; set; }

        public ResourceFlavorInfo StandardFlavor { get; set; }

        public ResourceFlavorInfo MinifiedFlavor { get; set; }

        public ResourceFlavorInfo GetFlavor(ResourceFlavor flavor)
        {
            switch (flavor)
            {
                case ResourceFlavor.Standard:
                    return StandardFlavor ?? MinifiedFlavor;
                case ResourceFlavor.Minified:
                    return MinifiedFlavor ?? StandardFlavor;
                default:
                    throw new ArgumentException();
            }
        }
    }

    enum ResourceType
    {
        Js,
        Css
    }

    /// <summary>
    /// Specifies whether the default or the minified versions should be preferred.
    /// </summary>
    public enum ResourceFlavor
    {
        /// <summary>
        /// The standard version is preferred.
        /// </summary>
        Standard,

        /// <summary>
        /// The minified version is preferred.
        /// </summary>
        Minified
    }

    /// <summary>
    /// Specifies whether bundling should actually happen.
    /// </summary>
    public enum RenderMode
    {
        /// <summary>
        /// Bundling is deactivated, files are requested individually.
        /// </summary>
        Individual,

        /// <summary>
        /// Bundles are served as defined.
        /// </summary>
        Bundled
    }

    /// <summary>
    /// Requirables can be individual resources, bundles or groups of requirables.
    /// </summary>
    public abstract class Requirable
    {
        /// <summary>
        /// Requires this requirable. All resources this requirable represents will
        /// have their tags emited on a call to <see cref="Consumption.WebResources.RenderScripts" />
        /// or <see cref="Consumption.WebResources.RenderStylesheets"/>.
        /// </summary>
        public void Require()
        {
            Consumption.WebResources.Require(this);
        }

        internal abstract IEnumerable<Requestable> GetRequestables();
    }


    /// <summary>
    /// A group of requirables requires all of its contents together. It can
    /// group scripts and stylesheets together.
    /// </summary>
    /// <seealso cref="IronStone.Web.BasicBundles.Requirable" />
    public class Group : Requirable
    {
        internal Group(Requirable[] contents)
        {
            Contents = contents;
        }

        internal Requirable[] Contents { get; }

        internal override IEnumerable<Requestable> GetRequestables()
        {
            foreach (var requirable in Contents)
            {
                foreach (var requestable in requirable.GetRequestables())
                {
                    yield return requestable;
                }
            }
        }
    }

    /// <summary>
    /// All that can be requested under a single location, such as
    /// individual files or bundles.
    /// </summary>
    /// <seealso cref="IronStone.Web.BasicBundles.Requirable" />
    public abstract class Requestable : Requirable
    {
        internal Requestable(ResourceType type, String path)
        {
            Type = type;
            Path = path;
        }

        internal ResourceType Type { get; private set; }
        internal String Path { get; private set; }

        internal abstract String GetFlavouredPath(ResourceFlavor flavor);

        /// <summary>
        /// Returns the path or pattern this requestable was created with.
        /// </summary>
        /// <returns>
        /// The path or pattern this requestable was created with.
        /// </returns>
        public override String ToString()
        {
            return Path;
        }

        internal override IEnumerable<Requestable> GetRequestables()
        {
            yield return this;
        }
    }

    /// <summary>
    /// A bundle groups multiple resources into one to be requestable
    /// under a new location.
    /// </summary>
    /// <seealso cref="IronStone.Web.BasicBundles.Requestable" />
    public class Bundle : Requestable
    {
        internal Bundle(ResourceType type, String path, Requestable[] contents)
            : base(type, path)
        {
            if (contents.Any(r => r.Type != type))
            {
                throw new ArgumentException(
                    $"The bundle with path {path} is a {type.ToScriptOrStylesheetName()}, " +
                    "yet some of it's contents are not.");
            }

            Contents = contents;
        }

        internal Requestable[] Contents { get; }

        internal override String GetFlavouredPath(ResourceFlavor flavor)
        {
            return Path;
        }
    }

    /// <summary>
    /// An individual resource such as a stylesheet or a script.
    /// </summary>
    /// <seealso cref="IronStone.Web.BasicBundles.Requestable" />
    public class Resource : Requestable
    {
        internal Resource(ResourceType type, String path, Resource[] dependencies)
            : base(type, path)
        {
            if (dependencies.Any(r => r.Type != type))
            {
                throw new ArgumentException(
                    $"The resource with path {path} is a {type.ToScriptOrStylesheetName()}, " +
                    "yet some of it's dependencies are not.");
            }

            Dependencies = dependencies;
        }

        internal ResourceInfo Info { get; set; }

        internal Resource[] Dependencies { get; private set; }

        internal override String GetFlavouredPath(ResourceFlavor flavor)
        {
            return Info.GetFlavor(flavor).Path;
        }
    }


    /// <summary>
    /// Contains all the state of BasicBundles and exposes methods to facilitate the
    /// serving of bundles.
    /// </summary>
    public class Repository
    {
        internal Repository(params Requestable[] resources)
        {
            allResourcesInOrder = GetAllResourcesInOrder(resources.OfType<Resource>()).ToArray();

            for (int i = 0; i < allResourcesInOrder.Length; ++i)
            {
                var resource = allResourcesInOrder[i];

                var resourceInfo = resourceInfos[resource] = resource.Info
                    = GetResourceInfo(i, resource.Path);

                hashes[resource] = MD5.Create().ComputeHash(
                    Encoding.UTF8.GetBytes(resourceInfo.StandardFlavor.Content));

                requestables[resource.Path] = resource;
            }

            foreach (var bundle in resources.OfType<Bundle>())
            {
                var contents = bundle.Contents
                    .ExpandToResources()
                    .ToArray();

                var writer = new StringWriter();

                foreach (var item in contents)
                {
                    var info = resourceInfos[item];

                    var flavorInfo = info.MinifiedFlavor ?? info.StandardFlavor;

                    writer.Write(Fixup(flavorInfo.Content, item.Type, item.Path, bundle.Path));
                }

                var hash = contents
                    .Select(r => hashes[r])
                    .Aggregate((l, r) => l.Zip(r, (x, y) => (Byte)(x ^ y)).ToArray());

                bundleContents[bundle] = writer.ToString();

                hashes[bundle] = hash;

                requestables[bundle.Path] = bundle;
            }
        }

        static ResourceInfo GetResourceInfo(Int32 index, String pattern)
        {
            if (pattern.Contains("(.min)"))
            {
                return new ResourceInfo()
                {
                    Index = index,
                    StandardFlavor = GetFlavorInfo(pattern.Replace("(.min)", "")),
                    MinifiedFlavor = GetFlavorInfo(pattern.Replace("(.min)", ".min"))
                };
            }
            else
            {
                return new ResourceInfo()
                {
                    Index = index,
                    StandardFlavor = GetFlavorInfo(pattern)
                };
            }
        }

        static ResourceFlavorInfo GetFlavorInfo(String path)
        {
            return new ResourceFlavorInfo()
            {
                Path = path,
                Content = Configuration.Settings.Load(path)
            };
        }


        internal String GetHash(Requestable resource)
        {
            return Convert.ToBase64String(hashes[resource]);
        }

        internal Int32 GetIndex(Resource resource)
        {
            return resourceInfos[resource].Index;
        }

        /// <summary>
        /// Looks up a requestable by by path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The requestable if found, null otherwise.</returns>
        public Requestable GetRequestableOrNull(String path)
        {
            Requestable result = null;

            requestables.TryGetValue(path, out result);

            return result;
        }

        /// <summary>
        /// Servers bundles requests in the Owin way.
        /// </summary>
        public AppFunc ServeOwin(AppFunc next)
        {
            return async context => {
                var requestPath = context["owin.RequestPath"] as String;

                var toAppRelative = Configuration.Settings.ToAppRelative;

                var path = toAppRelative(requestPath);

                Requestable requestable = null;

                if (requestables.TryGetValue(path, out requestable))
                {
                    var outputStream = context["owin.ResponseBody"] as Stream;
                    var headers = context["owin.ResponseHeaders"] as IDictionary<String, String[]>;

                    headers["Content-Type"] = new[] { GetContentType(requestable.Type) };

                    using (var writer = new StreamWriter(outputStream))
                    {
                        await FetchContentAsync(s => writer.WriteAsync(s), requestable);
                    }
                }
                else
                {
                    await next(context);
                }
            };
        }

        /// <summary>
        /// Fetches a bundle or resource asynchronously.
        /// </summary>
        /// <param name="writer">Handles multiple writes of strings which should be concatenated.</param>
        /// <param name="requestable">The bundle or resource.</param>
        public async Task FetchContentAsync(Func<String, Task> writer, Requestable requestable)
        {
            if (requestable is Resource)
            {
                await StreamContentAsync(writer, requestable as Resource, ResourceFlavor.Minified);
            }
            else if (requestable is Bundle)
            {
                await StreamContentAsync(writer, requestable as Bundle);
            }
            else
            {
                throw new Exception($"Unexpected type of requestable: {requestable.GetType()}");
            }
        }

        async Task StreamContentAsync(Func<String, Task> writer, Resource resource, ResourceFlavor flavor)
        {
            var content = resourceInfos[resource].GetFlavor(flavor).Content;

            await writer(content);
        }

        async Task StreamContentAsync(Func<String, Task> writer, Bundle bundle)
        {
            var content = bundleContents[bundle];

            await writer(content);
        }

        static IEnumerable<Resource> GetAllResourcesInOrder(IEnumerable<Resource> resources)
        {
            var backtrace = new Stack<Resource>();

            var visited = new HashSet<Resource>();

            var allResourcesInOrder = resources
                .DfsPostOrder(r => r.Dependencies, backtrace.Push, backtrace.Pop)
                .Where(r =>
                {
                    if (visited.Contains(r)) return false;

                    // It's currently impossible to accidentally create circles, but lets keep this in anyway.
                    if (backtrace.Contains(r))
                    {
                        var circle = backtrace.Skip(backtrace.ToList().IndexOf(r)).Concat(new[] { r });

                        throw new Exception("Circular dependency detected: " + String.Join("->", circle));
                    }

                    visited.Add(r);

                    return true;
                });

            return allResourcesInOrder;
        }

        static String Fixup(String content, ResourceType type, String oldUrl, String newUrl)
        {
            switch (type)
            {
                case ResourceType.Css:
                    return FixupCss(content, oldUrl, newUrl);
                default:
                    return content;
            }
        }

        static String FixupCss(String css, String oldUrl, String newUrl)
        {
            var relativeFragment = Extensions.MakeRelativePathFragment(newUrl, oldUrl);

            return ReplaceCssUrls(css, url => Extensions.ConcatUrls(url, relativeFragment).ToString());
        }

        static String ReplaceCssUrls(String css, Func<String, String> replaceUrl)
        {
            var regex = new Regex(@"url(?:\(['""]?)(.*?)(?:['""]?\))");

            var result = regex.Replace(css, m =>
            {
                return $"url('{replaceUrl(m.Groups[1].Value)}')";
            });

            return result;
        }

        static String GetContentType(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Js:
                    return "text/javascript";
                case ResourceType.Css:
                    return "text/css";
                default:
                    throw new ArgumentException();
            }
        }

        Dictionary<Resource, ResourceInfo> resourceInfos = new Dictionary<Resource, ResourceInfo>();

        Dictionary<Bundle, String> bundleContents = new Dictionary<Bundle, String>();

        Dictionary<Requestable, Byte[]> hashes = new Dictionary<Requestable, Byte[]>();

        Dictionary<String, Requestable> requestables = new Dictionary<String, Requestable>();

        Resource[] allResourcesInOrder;
    }

    /// <summary>
    /// Used to access ambient storage. Usually created with one of the factory methods
    /// in <see cref="RequestStore"/>.
    /// </summary>
    public interface IRequestStore
    {
        /// <summary>
        /// Sets the value denoted by <c>T</c> in the store.
        /// </summary>
        /// <typeparam name="T">Is type and key of the value.</typeparam>
        /// <param name="value">The value.</param>
        void Set<T>(T value);

        /// <summary>
        /// Gets the value denoted by <c>T</c> from the store.
        /// </summary>
        /// <typeparam name="T">Is type and key of the value.</typeparam>
        /// <returns>The value.</returns>
        T Get<T>();
    }

    /// <summary>
    /// Contains factory methods to build <see cref="IRequestStore"/>s.
    /// </summary>
    public static class RequestStore
    {
        /// <summary>
        /// Creates a store from the provided dictionary accessor. Useful for providing <c>HttpContext.Current.Items</c>.
        /// </summary>
        /// <param name="getDictionary">The dictionary accessor.</param>
        /// <returns>The store.</returns>
        public static IRequestStore Create(Func<IDictionary> getDictionary)
        {
            return new DictionaryRequestStore(getDictionary);
        }

        /// <summary>
        /// Creates a store from the provided dictionary accessor. Useful for providing <c>RouteData.Values</c>.
        /// </summary>
        /// <param name="getDictionary">The dictionary accessor.</param>
        /// <param name="keyPrefix">An optional key prefix used for all usages of the dictionary.</param>
        /// <returns>The store.</returns>
        public static IRequestStore Create(Func<IDictionary<String, Object>> getDictionary, String keyPrefix = null)
        {
            return new StringKeyedDictionaryRequestStore(getDictionary, keyPrefix);
        }
    }

    class DictionaryRequestStore : IRequestStore
    {
        public DictionaryRequestStore(Func<IDictionary> getDictionary)
        {
            this.getDictionary = getDictionary;
        }

        public void Set<T>(T value)
        {
            getDictionary()[typeof(T)] = value;
        }

        public T Get<T>()
        {
            return (T)getDictionary()[typeof(T)];
        }

        Func<IDictionary> getDictionary;
    }

    class StringKeyedDictionaryRequestStore : IRequestStore
    {
        public StringKeyedDictionaryRequestStore(Func<IDictionary<String, Object>> getDictionary, String keyPrefix = "")
        {
            this.getDictionary = getDictionary;
            this.keyPrefix = keyPrefix;
        }

        public void Set<T>(T value)
        {
            getDictionary()[keyPrefix + typeof(T).FullName] = value;
        }

        public T Get<T>()
        {
            return (T)getDictionary()[keyPrefix + typeof(T).FullName];
        }

        String keyPrefix;
        Func<IDictionary<String, Object>> getDictionary;
    }

    /// <summary>
    /// Provides all methods required to set BasicBundles up for use in views.
    /// </summary>
    public static class Configuration
    {
        internal class SettingsSet : IDisposable
        {
            internal Func<String, String> MapPath { get; set; }

            internal Func<String, String> Load { get; set; }

            internal IRequestStore RequestStore { get; set; }

            internal Func<String, String> ToAbsolute { get; set; }

            internal Func<String, String> ToAppRelative { get; set; }

            internal Func<RenderMode> GetMode { get; set; }

            internal Func<ResourceFlavor> GetFlavor { get; set; }

            internal Lazy<Repository> RepositoryHolder;

            internal HashSet<String> Paths = new HashSet<String>();

            internal List<Requestable> Resources = new List<Requestable>();

            public SettingsSet()
            {
                RepositoryHolder = new Lazy<Repository>(() => new Repository(Resources.ToArray()));
            }

            internal R InternalAdd<R>(R item)
                where R : Requestable
            {
                if (Paths.Contains(item.Path))
                {
                    throw new ArgumentException($"Configuration already contains the path {item.Path}.");
                }

                Paths.Add(item.Path);
                Resources.Add(item);
                return item;
            }

            void IDisposable.Dispose()
            {
                settingsStack.Pop();
            }
        }

        static Stack<SettingsSet> settingsStack = new Stack<SettingsSet>();

        internal static SettingsSet Settings { get { return settingsStack.Peek(); } }

        static ResourceFlavor GetStandardFlavorFromRenderMode(RenderMode mode)
        {
            switch (mode)
            {
                case RenderMode.Individual:
                    return ResourceFlavor.Standard;
                case RenderMode.Bundled:
                    return ResourceFlavor.Minified;
                default:
                    throw new ArgumentException();
            }
        }

        static Exception UnwrapTypeInitializerException(TypeInitializationException ex)
        {
            if (ex.InnerException is TypeInitializationException)
            {
                return UnwrapTypeInitializerException(ex.InnerException as TypeInitializationException);
            }
            else
            {
                return ex.InnerException;
            }
        }

        /// <summary>
        /// Installs a new configuration. This can be called multiple times in which case
        /// the old configuration gets temporarily hidden until the returned Disposable
        /// is disposed.
        /// </summary>
        /// <param name="load">Takes a path in the form resources are configured with and returns the content of the resource.</param>
        /// <param name="requestStore">Provides access to per-request storage.
        /// Look at the factory methods in the <see cref="RequestStore" /> class.</param>
        /// <param name="getMode">Specifies whether the current request should serve requested bundles as individual
        /// files or bundled. The default is as individual files.</param>
        /// <param name="getFlavor">Specifies whether the current request should be preferrably served with the minified
        /// variants or not. The default is unminified, although note that when bundles are served bundles they would always prefer
        /// minified contents regardless of this setting.</param>
        /// <param name="toAbsolute">Converts the paths in the form resources and bundles are configured with to the ones used for creating the style and script tags.
        /// If the application is hosted in a subfolder, this can be <c>VirtualPathUtility.ToAbsolute</c> on classic ASP.NET.</param>
        /// <param name="toAppRelative">Converts the paths in the form <c>toAbsolute</c> produces and converts them back to the one resources and bundles are configured with.
        /// If the application is hosted in a subfolder, this can be <c>VirtualPathUtility.ToAppRelative</c> on classic ASP.NET. /&gt;</param>
        /// <param name="bundleConfigurationType">If provided, the class will have its static constructor run in a way that
        /// improves developer experience in the presence of <see cref="TypeInitializationException" />s.</param>
        /// <returns>
        /// A disposable that can be disposed to revert the configuration.
        /// </returns>
        public static IDisposable Install(
            Func<String, String> load,
            IRequestStore requestStore,
            Type bundleConfigurationType = null,
            Func<RenderMode> getMode = null,
            Func<ResourceFlavor> getFlavor = null,
            Func<String, String> toAbsolute = null,
            Func<String, String> toAppRelative = null
            )
        {
            settingsStack.Push(new SettingsSet()
            {
                Load = load,
                RequestStore = requestStore,
                ToAbsolute = toAbsolute ?? (s => s),
                ToAppRelative = toAppRelative ?? (s => s),
                GetMode = getMode ?? (() => RenderMode.Individual),
                GetFlavor = getFlavor ?? (() => ResourceFlavor.Standard)
            });

            if (bundleConfigurationType != null)
            {
                try
                {
                    System.Runtime.CompilerServices.RuntimeHelpers
                        .RunClassConstructor(bundleConfigurationType.TypeHandle);
                }
                catch (TypeInitializationException ex)
                {
                    throw UnwrapTypeInitializerException(ex);
                }
            }

            return settingsStack.Peek();
        }

        private static Resource Add(ResourceType type, String path, params Resource[] dependencies)
        {
            if (Settings.RepositoryHolder.IsValueCreated)
            {
                throw new InvalidOperationException("A new resource is configured at a time where the configuration has already been used. This is not supported.");
            }

            return Settings.InternalAdd(new Resource(type, path, dependencies));
        }

        /// <summary>
        /// Defines a new script resource.
        /// </summary>
        /// <param name="path">An path to the resource.</param>
        /// <param name="dependencies">The dependencies.</param>
        /// <returns>The resource.</returns>
        public static Resource AddJs(String path, params Resource[] dependencies)
        {
            return Add(ResourceType.Js, path, dependencies);
        }

        /// <summary>
        /// Defines a new stylesheet resource.
        /// </summary>
        /// <param name="path">An path to the resource.</param>
        /// <param name="dependencies">The dependencies.</param>
        /// <returns>The resource.</returns>
        public static Resource AddCss(String path, params Resource[] dependencies)
        {
            return Add(ResourceType.Css, path, dependencies);
        }

        /// <summary>
        /// Defines a new bundle.
        /// </summary>
        /// <param name="path">An path to the resource.</param>
        /// <param name="contents">The contents of the bundle. This can be any requestable, including other bundles.</param>
        /// <returns>The bundle.</returns>
        public static Bundle AddBundle(String path, params Requestable[] contents)
        {
            if (contents.Length == 0) throw new ArgumentException();
            if (contents.Select(r => r.Type).Distinct().Count() > 1) throw new ArgumentException();

            return Settings.InternalAdd(new Bundle(contents[0].Type, path, contents));
        }

        /// <summary>
        /// Defines a new group.
        /// </summary>
        /// <param name="contents">The contents of the group. This can be any requirable, including bundles and other groups.
        /// It can also include stylesheets and scripts simultaneously.</param>
        /// <returns>The group.</returns>
        public static Requirable AddGroup(params Requirable[] contents)
        {
            return new Group(contents);
        }

        /// <summary>
        /// Gets the repository singleton, which is created on first call of this getter. After
        /// this point, no more configuration is allowed.
        /// </summary>
        /// <value>
        /// The repository.
        /// </value>
        public static Repository Repository { get { return Settings.RepositoryHolder.Value; } }
    }

    namespace Consumption
    {
        class Tracker
        {
            public Tracker()
            {
                this.repository = Configuration.Repository;
            }

            public void Require(Requirable requirable)
            {
                foreach (var requestable in requirable.GetRequestables())
                {
                    requestables.Add(requestable);
                }
            }

            public String Render(ResourceType type)
            {
                var settings = Configuration.Settings;

                var mode = settings.GetMode();

                var flavor = settings.GetFlavor();

                var template = GetTemplateForType(type);

                var selector = GetSelectorFromMode(mode);

                var html = String.Join("\n",
                    from r in selector(type)
                    select String.Format(template, GetUrlForWebResource(r, flavor, settings.ToAbsolute))
                    );

                return html;
            }

            Func<ResourceType, IEnumerable<Requestable>> GetSelectorFromMode(RenderMode mode)
            {
                switch (mode)
                {
                    case RenderMode.Individual:
                        return Expand;
                    case RenderMode.Bundled:
                        return Simplify;
                    default:
                        throw new ArgumentException();
                }
            }

            internal IEnumerable<Requestable> Simplify(ResourceType type)
            {
                // We could do more, but we don't have to.

                return requestables
                    .Where(r => r.Type == type)
                    .Distinct();
            }

            internal IEnumerable<Requestable> Expand(ResourceType type)
            {
                return GetOrderedResourcesWithDependencies(requestables.Where(r => r.Type == type).ExpandToResources());
            }

            Resource[] GetOrderedResourcesWithDependencies(IEnumerable<Resource> resources)
            {
                var resourcesWithDependencies = resources.GetResourcesWithDependencies()
                    .OrderBy(r => repository.GetIndex(r))
                    .ToArray();

                return resourcesWithDependencies;
            }

            static String GetTemplateForType(ResourceType type)
            {
                switch (type)
                {
                    case ResourceType.Js:
                        return "<script src=\"{0}\"></script>";
                    case ResourceType.Css:
                        return "<link href=\"{0}\" rel=\"stylesheet\" type=\"text/css\">";
                    default:
                        throw new ArgumentException();
                }
            }

            String GetUrlForWebResource(Requestable requestable, ResourceFlavor flavor, Func<String, String> toAbsolute)
            {

                return $"{toAbsolute(requestable.GetFlavouredPath(flavor))}?version={repository.GetHash(requestable)}";
            }

            Repository repository;

            List<Requestable> requestables = new List<Requestable>();
        }

        /// <summary>
        /// Provides all methods needed in views to require resources and render tags.
        /// </summary>
        public static class WebResources
        {
            internal static void Require(Requirable requirable)
            {
                GetTracker().Require(requirable);
            }

            private static String Render(ResourceType type)
            {
                return GetTracker().Render(type);
            }

            /// <summary>
            /// Renders the style tags.
            /// </summary>
            /// <returns></returns>
            public static String RenderStylesheets()
            {
                return Render(ResourceType.Css);
            }

            /// <summary>
            /// Renders the script tags.
            /// </summary>
            /// <returns></returns>
            public static String RenderScripts()
            {
                return Render(ResourceType.Js);
            }

            static Tracker GetTracker()
            {
                var store = Configuration.Settings.RequestStore;

                var tracker = store.Get<Tracker>();

                if (null == tracker)
                {
                    store.Set<Tracker>(tracker = new Tracker());
                }

                return tracker;
            }
        }
    }
}

namespace IronStone.Web.BasicBundles.Testing
{
    using Tracker = IronStone.Web.BasicBundles.Consumption.Tracker;

    /// <summary>
    /// Contains unit tests.
    /// </summary>
    public static class Tests
    {
        /// <summary>
        /// Runs the tests.
        /// </summary>
        public static void RunTests()
        {
            using (Configuration.Install(
                load: path => "",
                requestStore: RequestStore.Create(() => dictionary),
                toAbsolute: s => s
                ))
            {
                TestMakeRelativePathFragment();
                TestConcatUrls();
                TestDependencies();
                TestUrlFixups1();
                TestUrlFixups2();
            }
        }

        static void TestMakeRelativePathFragment()
        {
            AssertEqual(Extensions.MakeRelativePathFragment("~/foo", "~/bar"), ".");
            AssertEqual(Extensions.MakeRelativePathFragment("~/foo", "~/baz/bar"), "baz");
            AssertEqual(Extensions.MakeRelativePathFragment("~/baz/foo", "~/bar"), "..");

            AssertEqual(Extensions.MakeRelativePathFragment("foo", "bar"), ".");
            AssertEqual(Extensions.MakeRelativePathFragment("foo", "baz/bar"), "baz");
            AssertEqual(Extensions.MakeRelativePathFragment("baz/foo", "bar"), "..");
        }

        static void TestConcatUrls()
        {
            AssertEqual(Extensions.ConcatUrls("foo", "."), "foo");
            AssertEqual(Extensions.ConcatUrls("foo/bar", "."), "foo/bar");
            AssertEqual(Extensions.ConcatUrls("foo/bar", "baz"), "baz/foo/bar");
            AssertEqual(Extensions.ConcatUrls("foo/bar", ".."), "../foo/bar");

            AssertEqual(Extensions.ConcatUrls("../foo", "."), "../foo");
            AssertEqual(Extensions.ConcatUrls("../foo/bar", "."), "../foo/bar");
            AssertEqual(Extensions.ConcatUrls("../foo/bar", "baz"), "foo/bar");
            AssertEqual(Extensions.ConcatUrls("../foo/bar", ".."), "../../foo/bar");

            AssertEqual(Extensions.ConcatUrls("/foo", "."), "/foo");
            AssertEqual(Extensions.ConcatUrls("/foo", ".."), "/foo");
            AssertEqual(Extensions.ConcatUrls("/foo", "bar"), "/foo");

            AssertEqual(Extensions.ConcatUrls("http://example.com/foo", "."), "http://example.com/foo");
            AssertEqual(Extensions.ConcatUrls("http://example.com/foo", ".."), "http://example.com/foo");
            AssertEqual(Extensions.ConcatUrls("http://example.com/foo", "bar"), "http://example.com/foo");
        }

        static void TestUrlFixups1()
        {
            var fooContent = @"foo-element {
    prop1: url('../img/image1.jpg')
    prop2: url(../img/image1.jpg)
    prop3: url(""../img/image1.jpg"")
}
";
            var expectedBundleContent = @"foo-element {
    prop1: url('../img/image1.jpg')
    prop2: url('../img/image1.jpg')
    prop3: url('../img/image1.jpg')
}
";

            using (Configuration.Install(
                load: path => fooContent,
                requestStore: RequestStore.Create(() => dictionary),
                toAbsolute: s => s
                ))
            {
                var foo = Configuration.AddCss("~/css/foo");

                var bundle = Configuration.AddBundle("~/css/styles.css", foo);

                AssertRequestableContent(foo, fooContent);
                AssertRequestableContent(bundle, expectedBundleContent);
            }
        }

        static void TestUrlFixups2()
        {
            var fooContent = @"foo-element {
    prop1: url('img/image1.jpg')
    prop2: url('image1.jpg')
    prop3: url('../image1.jpg')
    prop4: url('/image1.jpg')
    prop5: url('http://example.com/image1.jpg')
}
";
            var expectedBundleContent = @"foo-element {
    prop1: url('../img/image1.jpg')
    prop2: url('../image1.jpg')
    prop3: url('../../image1.jpg')
    prop4: url('/image1.jpg')
    prop5: url('http://example.com/image1.jpg')
}
foo-element {
    prop1: url('img/image1.jpg')
    prop2: url('image1.jpg')
    prop3: url('../image1.jpg')
    prop4: url('/image1.jpg')
    prop5: url('http://example.com/image1.jpg')
}
foo-element {
    prop1: url('sub/img/image1.jpg')
    prop2: url('sub/image1.jpg')
    prop3: url('image1.jpg')
    prop4: url('/image1.jpg')
    prop5: url('http://example.com/image1.jpg')
}
";

            using (Configuration.Install(
                load: path => fooContent,
                requestStore: RequestStore.Create(() => dictionary),
                toAbsolute: s => s
                ))
            {
                var foo1 = Configuration.AddCss("~/foo");
                var foo2 = Configuration.AddCss("~/css/foo");
                var foo3 = Configuration.AddCss("~/css/sub/foo");

                var bundle = Configuration.AddBundle("~/css/styles.css", foo1, foo2, foo3);

                var repository = new Repository(
                    foo1, foo2, foo3, bundle);

                AssertRequestableContent(bundle, expectedBundleContent);
            }
        }

        static void AssertRequestableContent(Requestable requestable, String expected)
        {
            StringBuilder builder = new StringBuilder();
            Configuration.Repository.FetchContentAsync(s => { builder.Append(s); return Task.FromResult(0); }, requestable).Wait();
            var result = builder.ToString();
            if (result != expected)
            {
                throw new Exception($"Unexpected contents, had:\n\n{result}\n\nExpected:\n\n{expected}\n");
            }
        }

        static void TestDependencies()
        {
            var foodep12 = Configuration.AddJs("~/foo12");
            var foodep1 = Configuration.AddJs("~/foo1", foodep12);
            var foodep2 = Configuration.AddJs("~/foo2", foodep12);
            var foo = Configuration.AddJs("~/foo", foodep1, foodep2);

            var bardep12 = Configuration.AddJs("~/bar12");
            var bardep1 = Configuration.AddJs("~/bar1", bardep12);
            var bardep2 = Configuration.AddJs("~/bar2", bardep12);
            var bar = Configuration.AddJs("~/bar", bardep1, bardep2);

            var bazdep12 = Configuration.AddJs("~/baz12");
            var bazdep1 = Configuration.AddJs("~/baz1", bazdep12);
            var bazdep2 = Configuration.AddJs("~/baz2", bazdep12);
            var baz = Configuration.AddJs("~/baz", bazdep1, bazdep2);

            var bundle1 = Configuration.AddBundle("~/bundle1", foo, bar);
            var bundle2 = Configuration.AddBundle("~/bundle2", bar, baz);

            // simple require of a whole tree
            {
                var tracker = new Tracker();

                tracker.Require(foo);

                AssertEqual(
                    tracker.Expand(ResourceType.Js),
                    foodep12, foodep1, foodep2, foo
                    );

                AssertEqual(
                    tracker.Simplify(ResourceType.Js),
                    foo
                    );
            }

            // simple require of part of the tree
            {
                var tracker = new Tracker();

                tracker.Require(foodep1);

                AssertEqual(
                    tracker.Expand(ResourceType.Js),
                    foodep12, foodep1
                    );
            }

            // duplicate require
            {
                var tracker = new Tracker();

                tracker.Require(foo);
                tracker.Require(foo);

                AssertEqual(
                    tracker.Expand(ResourceType.Js),
                    foodep12, foodep1, foodep2, foo
                    );
            }

            // gratuitous require
            {
                var tracker = new Tracker();

                tracker.Require(foodep12);
                tracker.Require(foo);

                AssertEqual(
                    tracker.Expand(ResourceType.Js),
                    foodep12, foodep1, foodep2, foo
                    );
            }

            // wrong order require
            {
                var tracker = new Tracker();

                tracker.Require(foodep1);
                tracker.Require(foodep12);

                AssertEqual(
                    tracker.Expand(ResourceType.Js),
                    foodep12, foodep1
                    );
            }

            // bundle require
            {
                var tracker = new Tracker();

                tracker.Require(bundle1);

                AssertEqual(
                    tracker.Expand(ResourceType.Js),
                    foodep12, foodep1, foodep2, foo,
                    bardep12, bardep1, bardep2, bar
                    );

                AssertEqual(
                    tracker.Simplify(ResourceType.Js),
                    bundle1
                );
            }

            // duplicate bundle require
            {
                var tracker = new Tracker();

                tracker.Require(bundle1);
                tracker.Require(bundle1);

                AssertEqual(
                    tracker.Expand(ResourceType.Js),
                    foodep12, foodep1, foodep2, foo,
                    bardep12, bardep1, bardep2, bar
                    );

                AssertEqual(
                    tracker.Simplify(ResourceType.Js),
                    bundle1
                );
            }

            // bundle require plus gratuitous requires
            {
                var tracker = new Tracker();

                tracker.Require(bar);
                tracker.Require(bundle1);
                tracker.Require(foodep1);

                AssertEqual(
                    tracker.Expand(ResourceType.Js),
                    foodep12, foodep1, foodep2, foo,
                    bardep12, bardep1, bardep2, bar
                    );

                // FIXME: better if we really simplify
                //AssertEqual(
                //    tracker.Simplify(WebResourceType.Script),
                //    bundle1
                //);
            }

            // overlapping bundle require
            {
                var tracker = new Tracker();

                tracker.Require(bundle1);
                tracker.Require(bundle2);

                AssertEqual(
                    tracker.Expand(ResourceType.Js),
                    foodep12, foodep1, foodep2, foo,
                    bardep12, bardep1, bardep2, bar,
                    bazdep12, bazdep1, bazdep2, baz
                    );

                AssertEqual(
                    tracker.Simplify(ResourceType.Js),
                    bundle1, bundle2
                );
            }

        }

        static void AssertEqual(String actual, String expected)
        {
            if (actual != expected)
            {
                var message = $"Expected '{expected}', got '{actual}'.";

                throw new Exception(message);
            }
        }

        static void AssertEqual(IEnumerable<Requestable> actual, params Requestable[] expected)
        {
            if (!actual.SequenceEqual(expected))
            {
                var message = $"Expected {ToString(expected)}, got {ToString(actual)}.";

                throw new Exception(message);
            }
        }

        static String ToString(IEnumerable<Requestable> resources)
        {
            return $"[ {String.Join(" ", resources.Select(r => r.Path))} ]";
        }

        static Dictionary<String, Object> dictionary = new Dictionary<String, Object>();
    }
}
