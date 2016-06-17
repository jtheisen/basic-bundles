BasicBundles - the lightweight, pragmatic bundler
============

This is another ASP.NET bundling framework and it can serve as a
replacement for `Cassette` or the one in `Microsoft.Optimization`.

## Usage

You define a number of files and bundles in some static class that is
accessible from your views. For example,

    public static readonly WebResource JQuery
        = Configuration.AddScript("~/js/jquery.js");

defines a jQuery resource. In a view that needs it, you can then
require that resource by adding

    @{
        BasicBundlesConfiguration.JQuery.Require();
    }

In your layout file `_Layout.cshtml` you will let `BasicBundles`
then emit markup for all required resources by putting in

    @Html.Raw(WebResources.RenderStylesheets())

and

    @Html.Raw(WebResources.RenderScripts())

## Rationale

Why yet another bundling framework?
Isn't `WebGrease` and `Cassette` enough?

Bundling frameworks typically serve at least three purposes:

1. **Versioning**
   (/myscript?version=something-that-changes-when-myscript-changes)
2. **Bundling**
   (/my-many-js-files-in-one)
3. **Minification**
   (/my-minified-js-file)

Of the three, the last one is by far the most difficult one to implement,
which is why `Cassette` has a dependency on a parser framework - `Antlr` -
and `WebGrease` has a dependency on `AjaxMin`.

I feel that I really don't need minification, as

* In many projects, the vast majority of js and css comes
  pre-minified from third party packages and
* in the upcoming world of ASP.NET 5, minification will be done
  at compile time with tools that are better suited for the job.
  In that scenario you may argue that bundling would also be done
  at that level, but you *still need versioning*.

I wrote this little tool to have something that addresses the first
two goals while sacrificing the third.

It does a better job at that because:

### - It's extremely lightweight

The whole thing is about a thousand lines in one file, and half of
that is testing. 
You can use the NuGet package or simply copy the one file to your
project.

It has no dependencies, not even on `System.Web`, so you can use
it in ASP.NET 5 today.

### - It's more type safe

I don't understand what it is with web development and magic strings.

It's like every ASP.NET library developer tries to use C# as if it
was a scripting language.

BasicBundles lets you define all resources and bundles in C# and then
use those definitions in views in a type-safe manner.

### - It's serving bundled and bundled together

BasicBundles have no `web.config` app settings to make them run
in developer or release mode. It's easy to implement a choice between
unbundled and bundled resources by some request condition - a
`debug=true` query parameter, for example.  

### - It allows for dependency definitions

In neither `WebGrease` nor `Cassette`, there is any notion of a dependency.

If one resource depends on another, it's up to the view to require
them both.

In `BasicBundles` you can specify which resource or bundle requires
what other one as a dependency, and those will be injected also.
  
