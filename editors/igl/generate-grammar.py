#!/usr/bin/env python3
"""
Writes the TextMate grammar that gives .igl files their highlighting in Rider, VS Code and anything
else that reads TextMate bundles:

    python3 editors/igl/generate-grammar.py

It writes editors/igl/syntaxes/igl.tmLanguage.json.  Nothing in that file is typed by hand: every word
list in it is read from the source the engine itself builds the same list from, so the grammar can be
regenerated whenever the language grows and will say exactly what the parser says.

    the keywords              the "_keywords:" list in Parser/LanguageParser.DSL.cs
    the named colors          the public Color fields of Graphics/Colors.cs
    the indices of refraction the public double constants of Core/IndicesOfRefraction.cs
    the direction vectors     the public Vector fields of Core/Directions.cs
    the built-in functions    the [Function("...")] attributes under Terms/
    the global constants      what Renderer/ImageRenderer.cs sets as globals

The one thing that is a judgement rather than a reading is how the keywords are grouped for color --
which ones are control flow, which name a block such as a shape or a material, which are patterns.
Those groups are the lists below.  A keyword in none of them is colored as a plain keyword, so a new
one needs nothing done here to be highlighted; it only needs adding to a group to be colored as one.

TestEditorGrammar holds the grammar to the engine, so a grammar that has fallen out of step fails the
tests rather than quietly highlighting the wrong words.
"""

import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "syntaxes", "igl.tmLanguage.json")

# -- How the keywords are grouped for color --------------------------------------------------------

CONTROL = ["if", "else", "for", "in", "switch", "case", "default", "return"]
IMPORTS = ["import", "include"]
DECLARATIONS = ["function", "primitive"]
WORD_OPERATORS = ["and", "or", "not"]
LANGUAGE_CONSTANTS = ["true", "false", "null"]

# The blocks that name a thing: shapes, the ways of combining them, and what a scene is made of.
TYPES = [
    "blob", "conic", "cube", "cylinder", "disc", "egg", "extrusion", "heightfield", "hyperboloid",
    "isosurface", "julia", "lathe", "lsystem", "paraboloid", "parallelogram", "parametric", "patch",
    "plane", "poly", "quadric", "ribbon", "saddle", "sdf", "sphere", "superellipsoid", "sweep", "text",
    "torus", "triangle", "tube", "svg", "icon",
    "union", "intersection", "difference", "group", "csg",
    "camera", "light", "context", "scene", "sky", "environment", "sun", "info", "render", "font",
    "material", "materials", "pigment", "interior", "medium", "image", "path", "spline", "profile",
]

# The patterns a pigment or a density can be made from.
PATTERNS = [
    "agate", "blend", "bozo", "brick", "checker", "crackle", "cylindrical", "dents", "gradient",
    "granite", "hexagon", "leopard", "marble", "mottled", "noise", "planar", "radial", "ripples",
    "spherical", "square", "squares", "stripes", "swells", "toroidal", "triangular", "wave", "waves",
    "wood", "wrinkles",
]


# -- Reading the lists from the source -------------------------------------------------------------

def read(*parts):
    with open(os.path.join(ROOT, *parts), encoding="utf-8") as source:
        return source.read()


def keywords():
    source = read("Parser", "LanguageParser.DSL.cs")
    start = source.index("_keywords:")
    end = source.index("_expressions:", start)
    return re.findall(r"'([A-Za-z]+)'", source[start:end])


def fields(pattern, *parts):
    return re.findall(pattern, read(*parts))


def functions():
    names = set()
    terms = os.path.join(ROOT, "Terms")
    for name in os.listdir(terms):
        if name.endswith(".cs"):
            names.update(re.findall(r'\[Function\("([^"]+)"', read("Terms", name)))
    return names


# -- Building the grammar --------------------------------------------------------------------------

# An identifier may use Greek letters as well as the usual ones, so a word's edges have to know about
# them too: a plain \b would call the edge between "x" and "α" a boundary.
ID_START = "A-Za-z_α-ωΑ-Ω"
ID_PART = "A-Za-z0-9_α-ωΑ-Ω"
IDENTIFIER = f"[{ID_START}][{ID_PART}]*"
BEFORE = f"(?<![{ID_PART}])"
AFTER = f"(?![{ID_PART}])"


def words(names):
    """A pattern matching any one of the given words as a whole word.  The longest are tried first,
    which the edges make unnecessary for correctness but which keeps the output the same every time."""
    ordered = sorted(set(names), key=lambda word: (-len(word), word))
    return BEFORE + "(?:" + "|".join(ordered) + ")" + AFTER


def grammar(keyword_list, colors, indices, directions, function_names, globals_):
    all_keywords = set(keyword_list)
    grouped = CONTROL + IMPORTS + DECLARATIONS + WORD_OPERATORS + LANGUAGE_CONSTANTS + TYPES + PATTERNS
    strangers = sorted(set(grouped) - all_keywords)

    if strangers:
        raise SystemExit(f"these are grouped for color but are not keywords of the language: {strangers}")

    other = sorted(all_keywords - set(grouped)) + ["object"]

    return {
        "$schema": "https://raw.githubusercontent.com/martinring/tmlanguage/master/tmlanguage.json",
        "name": "IGL",
        "scopeName": "source.igl",
        "fileTypes": ["igl"],
        "patterns": [{"include": "#" + name} for name in (
            "comments", "strings", "import", "declaration", "object", "definition", "calls", "numbers",
            "constants", "keywords", "operators", "punctuation")],
        "repository": {
            "comments": {"patterns": [
                {"name": "comment.block.igl", "begin": r"/\*", "end": r"\*/"},
                {"name": "comment.line.double-slash.igl", "match": r"//.*$"}]},
            "escapes": {"name": "constant.character.escape.igl", "match": r"\\."},
            "strings": {"patterns": [
                {"name": "string.quoted.triple.igl", "begin": '"""', "end": '"""',
                 "patterns": [{"include": "#escapes"}]},
                {"name": "string.quoted.double.igl", "begin": '"', "end": '"',
                 "patterns": [{"include": "#escapes"}]},
                {"name": "string.quoted.single.igl", "begin": "'", "end": "'",
                 "patterns": [{"include": "#escapes"}]}]},
            "import": {"patterns": [
                {"match": BEFORE + "(include)" + AFTER,
                 "captures": {"1": {"name": "keyword.control.import.igl"}}},
                {"begin": BEFORE + "(import)" + AFTER,
                 "beginCaptures": {"1": {"name": "keyword.control.import.igl"}},
                 "end": r"(\})",
                 "endCaptures": {"1": {"name": "punctuation.section.braces.end.igl"}},
                 "patterns": [
                     {"include": "#comments"},
                     {"include": "#strings"},
                     {"match": r"\{", "name": "punctuation.section.braces.begin.igl"},
                     {"match": ",", "name": "punctuation.separator.comma.igl"},
                     {"match": IDENTIFIER, "name": "entity.name.type.module.igl"}]}]},
            "declaration": {"patterns": [
                {"match": BEFORE + r"(function|primitive)\s+(" + IDENTIFIER + ")",
                 "captures": {"1": {"name": "storage.type.igl"}, "2": {"name": "entity.name.function.igl"}}},
                {"match": r"(->)\s*(" + IDENTIFIER + ")",
                 "captures": {"1": {"name": "keyword.operator.arrow.igl"}, "2": {"name": "support.type.igl"}}}]},
            "object": {"patterns": [
                {"match": BEFORE + r"(object)\s+(" + IDENTIFIER + r")(?=\()",
                 "captures": {"1": {"name": "keyword.other.igl"}, "2": {"name": "entity.name.function.igl"}}},
                {"match": BEFORE + r"(object)\s+(" + IDENTIFIER + ")",
                 "captures": {"1": {"name": "keyword.other.igl"}, "2": {"name": "variable.other.object.igl"}}}]},
            # A lone "=" is only ever an assignment: comparison is written "==".
            "definition": {"match": BEFORE + "(" + IDENTIFIER + r")\s*(=)(?!=)",
                           "captures": {"1": {"name": "variable.other.definition.igl"},
                                        "2": {"name": "keyword.operator.assignment.igl"}}},
            "calls": {"patterns": [
                {"match": words(function_names) + r"(?=\()", "name": "support.function.builtin.igl"},
                {"match": BEFORE + IDENTIFIER + r"(?=\()", "name": "entity.name.function.call.igl"}]},
            "numbers": {"match": f"(?<![{ID_PART}.])" + r"(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?",
                        "name": "constant.numeric.igl"},
            "constants": {"patterns": [
                {"match": words(LANGUAGE_CONSTANTS), "name": "constant.language.igl"},
                {"match": words(globals_), "name": "support.constant.igl"},
                {"match": words(colors), "name": "support.constant.color.igl"},
                {"match": words(indices), "name": "support.constant.ior.igl"},
                {"match": words(directions), "name": "support.constant.direction.igl"}]},
            "keywords": {"patterns": [
                {"match": words(CONTROL), "name": "keyword.control.igl"},
                {"match": words(WORD_OPERATORS), "name": "keyword.operator.word.igl"},
                {"match": words(DECLARATIONS), "name": "storage.type.igl"},
                {"match": words(TYPES), "name": "support.type.igl"},
                {"match": words(PATTERNS), "name": "support.type.pattern.igl"},
                {"match": words(other), "name": "keyword.other.igl"}]},
            "operators": {"patterns": [
                {"match": "->", "name": "keyword.operator.arrow.igl"},
                {"match": "==|!=|<=|>=|<|>|≤|≥|≠", "name": "keyword.operator.comparison.igl"},
                {"match": r"&&|\|\||!|∧|∨|¬", "name": "keyword.operator.logical.igl"},
                {"match": "=", "name": "keyword.operator.assignment.igl"},
                {"match": r"\?|:", "name": "keyword.operator.ternary.igl"},
                {"match": r"[+\-*/%^$]|[−–×⨯÷∕⁄⋅·∙•∗⋆√∛²³⁰¹⁴⁵⁶⁷⁸⁹°]", "name": "keyword.operator.arithmetic.igl"}]},
            "punctuation": {"patterns": [
                {"match": ",", "name": "punctuation.separator.comma.igl"},
                {"match": r"[{}]", "name": "punctuation.section.braces.igl"},
                {"match": r"[\[\]]", "name": "punctuation.section.brackets.igl"},
                {"match": r"[()]", "name": "punctuation.section.parens.igl"}]},
        },
    }


def main():
    keyword_list = keywords()
    colors = fields(r"public static readonly Color (\w+)", "Graphics", "Colors.cs")
    indices = fields(r"public const double (\w+)", "Core", "IndicesOfRefraction.cs")
    directions = fields(r"public static readonly Vector (\w+)", "Core", "Directions.cs")
    function_names = functions()
    globals_ = fields(r'_globals\.SetValue\("([^"]+)"', "Renderer", "ImageRenderer.cs")

    with open(OUT, "w", encoding="utf-8") as out:
        json.dump(grammar(keyword_list, colors, indices, directions, function_names, globals_),
                  out, ensure_ascii=False, indent=2)
        out.write("\n")

    print(f"wrote {os.path.relpath(OUT, ROOT)}: {len(set(keyword_list))} keywords, {len(set(colors))} colors, "
          f"{len(set(indices))} indices of refraction, {len(set(directions))} directions, "
          f"{len(function_names)} functions, {len(set(globals_))} global constants")


if __name__ == "__main__":
    main()
