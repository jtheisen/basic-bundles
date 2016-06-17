using IronStone.Web.BasicBundles;

namespace BasicBundles.SampleMvc5
{
    public class BundleConfig
    {
        public static class Css
        {
            public static readonly Resource bootstrap = Configuration.AddCss("~/Content/bootstrap(.min).css");
            public static readonly Resource bootstrapModal = Configuration.AddCss("~/Content/bootstrap-modal.css", bootstrap);
            public static readonly Resource jqueryUi = Configuration.AddCss("~/Content/jquery-ui(.min).css");

            public static Requestable DefaultRequirements = Configuration.AddBundle("~/bundles/styles",
                bootstrap, jqueryUi);
        }

        public static class Js
        {
            public static readonly Resource jquery = Configuration.AddJs("~/Scripts/jquery.js");
            public static readonly Resource jqueryUi = Configuration.AddJs("~/Scripts/jquery-ui(.min).js", jquery);
            public static readonly Resource bootstrap = Configuration.AddJs("~/Scripts/bootstrap(.min).js", jquery);

            public static Requestable DefaultRequirements = Configuration.AddBundle("~/bundles/scripts",
                jquery, bootstrap);
        }

        public static Requirable DefaultRequirements = Configuration.AddGroup(
            Css.DefaultRequirements, Js.DefaultRequirements
            );
    }
}
