# IGL for TextMate editors

Syntax highlighting for `.igl` scene files, as a TextMate bundle: JetBrains Rider and the other
IntelliJ-based IDEs, VS Code, and anything else that reads TextMate grammars.

## Installing it

**Rider.** Open *Settings → Editor → TextMate Bundles*, click **+**, and choose this `editors/igl`
folder.  If `.igl` has ever been given a file type of its own under *Settings → Editor → File Types*,
remove that, or it will take precedence over the bundle.  The colors are adjusted under
*Settings → Editor → Color Scheme → TextMate*.

**VS Code.** Link this folder into your extensions and reload the window:

```
ln -s "$PWD/editors/igl" ~/.vscode/extensions/igl
```

## What is in it

| | |
| --- | --- |
| `package.json` | Tells the editor that `.igl` files are IGL and where the grammar is. |
| `language-configuration.json` | The comment markers and the bracket pairs. |
| `syntaxes/igl.tmLanguage.json` | The grammar.  Generated: do not edit it by hand. |
| `generate-grammar.py` | Writes the grammar. |

## Regenerating the grammar

```
python3 editors/igl/generate-grammar.py
```

Every word list in the grammar is read from the source the engine builds the same list from: the
keywords from the grammar specification in `Parser/LanguageParser.DSL.cs`, and the named colors,
indices of refraction, directions, built-in functions and global constants from where the engine
defines them.  So after the language gains a keyword or the engine a color, run the generator.
`TestEditorGrammar` fails until you do, naming what the grammar is missing.

**An editor does not notice a new grammar by itself.**  Rider reads a bundle when it starts and when
its TextMate settings change, and never watches the folder.  So after regenerating the grammar, or
pulling one someone else regenerated, either restart Rider, or untick the IGL bundle under
*Settings → Editor → TextMate Bundles*, apply, tick it again and apply.  There is no need to remove it
and add it again: it still points at this folder, which now holds the new grammar.  In VS Code, reload
the window.

How the keywords are grouped for color is the one judgement in it: which are control flow, which
name a block such as a shape or a material, which are patterns.  Those groups are the lists at the top
of the generator.  A keyword in none of them is still highlighted, as a plain keyword; adding it to a
group is only needed to color it as one.
