using System.Runtime.CompilerServices;

// The test project can see this one's internals.  Some of what is worth testing is deliberately not
// public: the sharing of one built shape among identical primitive calls, for one, whose whole point
// is that it changes nothing at all about what is drawn -- so no rendered image can show whether it
// happened, and the only honest check is to look.
[assembly: InternalsVisibleTo("Tests")]
