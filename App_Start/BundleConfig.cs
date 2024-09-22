using System.Web;
using System.Web.Optimization;

namespace ZB_FEPMS
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            BundleTable.EnableOptimizations = false;

            //bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
            //            "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/custom").Include(
            "~/Content/Scripts/custom.js"));

            bundles.Add(new ScriptBundle("~/bundles/my-scripts-bundle").Include(
            "~/Content/Scripts/jquery-3.1.1.js",
            "~/Content/Scripts/pwdwidget.js",
            "~/Content/Scripts/jquery.inputmask.js",
            "~/Content/Scripts/jquery.inputmask.date.extensions.js",
            "~/Content/Scripts/jquery.unobtrusive-ajax.js",
            "~/Content/Scripts/bootstrap.js",
            "~/Content/Scripts/respond.js",
            "~/Content/Scripts/bootstrap-progressbar.js",
            "~/Content/Scripts/date.js",
            "~/Content/Scripts/moment.js",
            "~/Content/Scripts/daterangepicker.js",
            "~/Content/Scripts/bootstrap-datepicker.js",
            "~/Content/Scripts/jquery.counterup.min.js",
            "~/Content/Scripts/bootstrap-multiselect.min.js"
            ));

            bundles.Add(new StyleBundle("~/bundles/my-styles-bundle").Include(
                      "~/Content/Styles/bootstrap.css",
                      "~/Content/Styles/font-awesome.css",
                      "~/Content/Styles/nprogress.css",
                      "~/Content/Styles/nprogress.css",
                      "~/Content/Styles/bootstrap-progressbar-3.3.4.css",
                      "~/Content/Styles/jqvmap.css",
                      "~/Content/Styles/datepicker3.css",
                      "~/Content/Styles/daterangepicker.css",
                      "~/Content/Styles/pwdwidget.css",
                      "~/Content/Styles/animate.css",
                      "~/Content/Styles/custom.css",
                      "~/Content/Styles/animations.css",
                      "~/Content/Styles/feedback-left.css",
                      "~/Content/Styles/bootstrap-multiselect.min.css",
                      "~/Content/Styles/Site.css"));
        }
    }
}
