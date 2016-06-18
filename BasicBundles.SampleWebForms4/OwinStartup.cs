using Microsoft.Owin;
using Owin;
using System.IO;
using System.Web;

[assembly: OwinStartup(typeof(BasicBundles.SampleWebForms4.OwinStartup))]

namespace BasicBundles.SampleWebForms4
{
    public class OwinStartup
    {
        public void Configuration(IAppBuilder app)
        {
            IronStone.Web.BasicBundles.Configuration.Install(
                load: vp => File.ReadAllText(HttpContext.Current.Server.MapPath(vp)),
                toAbsolute: VirtualPathUtility.ToAbsolute,
                toAppRelative: VirtualPathUtility.ToAppRelative,
                requestStore: IronStone.Web.BasicBundles.RequestStore.Create(() => HttpContext.Current.Items),
                getMode: () => IronStone.Web.BasicBundles.RenderMode.Individual,
                getFlavor: () => IronStone.Web.BasicBundles.ResourceFlavor.Minified,
                bundleConfigurationType: typeof(BundleConfig)
            );

            app.Use((context, next) =>
                IronStone.Web.BasicBundles.Configuration.Repository.ServeOwin(c => next())(context.Environment)
            );
        }
    }
}
