Todo:

* Test ASP.NET 5
* break all remaining dependencies

Done:

* Doc comments for public interfaces
* Support minification.
* Actually support bundling. :-)
* Make sure throws are done as soon as possible for
  * missing files (already done)
  * wrong dependency/content types
* Check experience of setup failures

Later:

* Write ASP.NET 4 and ASP.NET 5 usage examples.
* Real simplification
* Test cycles.

Won't do:

* Better type safety to distinguish scripts from styles

  Although: There's really no benefit, as every mistake otherwise
  possible would be caught directly on running the application.
  
* Do something clever about the issue that some scripts should be
  added at the bottom and some should be added at the top.
  
  Hmmm: We don't really know what's best here. The pragmantic thing
  is to leave this for now.
  
* Should we allow for inline-content also?

  No for now too.

* Make sure the outer namespace isn't polluted

  Doesn't matter, the outer one is used only for configuration.