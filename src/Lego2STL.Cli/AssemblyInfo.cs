using System.Runtime.CompilerServices;

// The commands and their option declarations are internal: they are how this program is put
// together, not a surface anything else calls. The interface suite needs them so that the
// window can be checked against the options the command line really registers, rather than
// against a list someone has to remember to keep in step.
[assembly: InternalsVisibleTo("Lego2STL.UiTests")]
