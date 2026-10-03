# Recipe check

The Claude Code skill's recipes are code that people paste. This check builds one program from every `csharp` block in `integrations/claude-code/lumen-charts/references/sports.md` and `recipes-race-face.md`, **together and as written**, with warnings as errors. Recipes that reuse one another's variable names, call API that does not exist, or no longer compile all fail here. It then renders every `var … = new ChartSpec` the recipes declare.

```bash
python check.py            # writes obj/Recipes.g.cs from the recipes
dotnet run -c Release      # compiles and renders; SVGs go to obj/out
```

`Inputs.txt` holds the invented data the recipes take as inputs (`days`, `hrv`, `lapLog` and so on). When a new recipe names an input that does not exist yet, add it there. The project references `src/Lumen.Charts`, so it checks the working tree. For a release, also install the packed package into a fresh app as the release ritual says. It stays outside the solution.
