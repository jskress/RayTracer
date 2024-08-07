# The Ray Tracer Challenge

This repo contains the result of me working through the book, ["The Ray Tracer Challenge:
A Test Driven Guide to Your First 3D Renderer"](https://www.amazon.com/Ray-Tracer-Challenge-Test-Driven-Renderer/dp/1680502719/ref=sr_1_1?crid=9PKWGDG8TT44&keywords=the+ray+tracer+challenge&qid=1697901294&sprefix=The+Ray%2Caps%2C149&sr=8-1)
by Jamis Buck.

This is complete up through the end of the book.  The rest will be stuff I want to add
(see the to-do lists below).

The following are improvements I want to make.  Most of the ones relating to patterns come
from the "Putting It Together" section of chapter 10, plus some things that POVRay has that
I want to add.  Once an item is checked, you can assume it's done.

### General To-Dos:

- [X] Add a "cheesy" file parser to get things going in that direction.
- [X] Add lots of named colors.
- [X] Add a named set of direction vectors.
- [X] Add a named set of indices of refraction.
- [ ] Motion blur.
- [ ] Antialiasing.
- [X] Support PNGs.
- [ ] Support other image formats?

### To-Dos for Lights:
- [ ] Area lights/soft shadows.
- [ ] Spotlights.

### To-Dos for Cameras:
- [ ] Focal blur.

### To-Dos for Surfaces:
- [ ] Texture mapping.
- [ ] Normal perturbation.
- [X] Torus.
- [ ] Other surfaces (Height fields, SORs, sweeps, etc.)

### To-Dos for Patterns:

- [X] Add a radial gradient pigment.
- [X] Update all pigments to use nested color sources.
- [X] Add a perturbing noise pigment.
- [X] Add a blending pigment.
- [ ] Add pattern with color map pigment ala POVRay.
- [X] Add a cyclical flag to the gradient pigments (start over vs bounce back).
- [ ] Update the gradients to do multiples, similar to how most graphics engines
      handle dash patterns.  POVRay has this, too.

### To-Dos for surface rendering:

- [X] Add a flag that will opt a surface out of shadow consideration; i.e., the surface
      does not cast a shadow.

### To-Dos for the render file language:

- [X] Move to the Lex port, when available.
- [X] Support loops for object graph construction.
- [X] Support variables for easier surface reuse, among other things.

More to come...
