# CIE lamp spectra

The three tables in this folder are the International Commission on Illumination's (CIE) relative spectral power
distributions of typical lamps, from CIE 015:2018, *Colorimetry*, 4th edition
([doi:10.25039/TR.015.2018](https://doi.org/10.25039/TR.015.2018)), as the CIE publishes them in its open access
datasets.  They are what the renderer's named lamps are read from (`Core/Lamps.cs`), embedded in the build.

| File | What it holds | Dataset |
| --- | --- | --- |
| `CIE_illum_FLs.csv` | Fluorescent lamps: FL1 to FL12, FL3.1 to FL3.15 | [doi:10.25039/CIE.DS.ukaymjdn](https://doi.org/10.25039/CIE.DS.ukaymjdn) |
| `CIE_illum_HPs.csv` | High pressure discharge lamps: HP1 (standard high pressure sodium), HP2 (colour enhanced high pressure sodium), HP3 to HP5 (metal halide) | [doi:10.25039/CIE.DS.f6rvvnev](https://doi.org/10.25039/CIE.DS.f6rvvnev) |
| `CIE_illum_LEDs.csv` | LED lamps: LED-B1 to LED-B5 (phosphor-type), LED-BH1 (hybrid), LED-RGB1, LED-V1 and LED-V2 (violet-pumped) | [doi:10.25039/CIE.DS.vgssnyfg](https://doi.org/10.25039/CIE.DS.vgssnyfg) |

Each is 380 nm to 780 nm in 5 nm steps, a wavelength and then one column per lamp, and each is **unchanged** from
the file the CIE publishes: their md5 checksums match those on the datasets' pages.

**License.**  The CIE publishes these datasets under the
[Creative Commons Attribution-ShareAlike 4.0 International](https://creativecommons.org/licenses/by-sa/4.0/)
license (CC BY-SA 4.0), and they are offered here under the same license, credited to the CIE.  The license covers
these three files only; nothing else in this repository is drawn from them.

The two gas lamps the CIE does not tabulate -- low pressure sodium and mercury -- are not from here: they are built
in `Core/Lamps.cs` from the US National Institute of Standards and Technology's *Handbook of Basic Atomic
Spectroscopic Data* (strong lines of sodium and of mercury), a work of the US government.
