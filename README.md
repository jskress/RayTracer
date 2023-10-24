# The Ray Tracer Challenge

This repo contains the result of me working through the book, ["The Ray Tracer Challenge:
A Test Driven Guide to Your First 3D Renderer"](https://www.amazon.com/Ray-Tracer-Challenge-Test-Driven-Renderer/dp/1680502719/ref=sr_1_1?crid=9PKWGDG8TT44&keywords=the+ray+tracer+challenge&qid=1697901294&sprefix=The+Ray%2Caps%2C149&sr=8-1)
by Jamis Buck.

This is complete up through chapter 10, "Patterns".

The following are improvements I want to make.  Most of the ones relating to patterns come
from the "Putting It Together" section of chapter 10, plus some things that POVRay has that
I want to add.  Once an item is checked, you can assume it's done.

### General To-Dos:

- [X] Add a "cheesy" file parser to get things going in that direction.
- [X] Add lots of named colors.
- [X] Add a named set of direction vectors.

### To-Dos for Patterns:

- [ ] Add a radial gradient color source.
- [X] Update all color sources to use nested color sources.
- [ ] Add a perturbing noise function to all color sources.
- [ ] Add a blending color source.
- [ ] Add a color map color source ala POVRay.
- [ ] Add a cyclical flag to the gradient color sources (start over vs bounce back).
- [ ] Update the linear gradient to do multiples, similar to how most graphics engines
      handle dash patterns.  POVRay has this, too.

### To-Dos for surface rendering:

- [ ] Add a flag that will opt a surface out of shadow consideration; i.e., the surface
      does not cast a shadow.

More to come...
