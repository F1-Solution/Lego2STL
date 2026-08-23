using System.Runtime.CompilerServices;

// The Rebrickable DTOs and the builder overload that takes them are internal: they are an
// implementation detail of talking to the API, not part of the tool's surface. The tests
// need them so the colour resolution rules can be exercised against synthetic catalogues
// that reproduce the real-world collisions without touching the network.
[assembly: InternalsVisibleTo("Lego2STL.Tests")]
