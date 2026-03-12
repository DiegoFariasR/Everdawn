// Explicit global usings for Unity source compilation.
// The .csproj ImplicitUsings generates these into obj/ at build time,
// but Unity compiles .cs files directly and never reads obj/.
// This file ensures Unity sees the same namespaces the SDK build assumes.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
