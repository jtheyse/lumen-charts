"""Builds one program from every recipe in the skill's references, as written, so recipes that reuse one another's names or
call API that does not exist fail to compile. Run it, then `dotnet run -c Release` in this folder."""
import io, os, re

here = os.path.dirname(os.path.abspath(__file__))
references = os.path.join(here, '..', '..', 'integrations', 'claude-code', 'lumen-charts', 'references')

def blocks(name):
    text = io.open(os.path.join(references, name), encoding='utf-8').read()
    return re.findall(r"```csharp\n(.*?)```", text, re.S)

sports = blocks('sports.md')
race = blocks('recipes-race-face.md')
helper, sports = sports[0], sports[1:]           # sports.md opens with helpers the recipes share
usings = sorted({line for block in [helper] + race for line in block.splitlines() if line.startswith('using ')})
race = [b for b in race if not all(l.startswith('using ') or not l.strip() for l in b.splitlines())]

inputs = io.open(os.path.join(here, 'Inputs.txt'), encoding='utf-8').read()
recipes = ''.join(b + '\n' for b in sports + race)
# Each chart a recipe declares, and each a recipe's own ChartSpec function makes, such as one range of a chart the app slices.
makers = set(re.findall(r"\bChartSpec (\w+)\(", recipes))
specs = sorted(set(re.findall(r"\bvar (\w+) = new ChartSpec\b", recipes)) | {name for name, maker in re.findall(r"\bvar (\w+) = (\w+)\(", recipes) if maker in makers})

program = '\n'.join(usings) + '\n' + inputs + recipes
program += 'var specs = new (string, ChartSpec)[] {' + ','.join(f'("{n}", {n})' for n in specs) + '};\n'
program += ('foreach (var (name, spec) in specs) { var svg = ChartSvg.Render(spec); '
            'Directory.CreateDirectory("obj/out"); File.WriteAllText(Path.Combine("obj/out", name + ".svg"), svg); '
            'Console.WriteLine($"{name}: {svg.Length} chars"); }\n')
program += f'Console.WriteLine("{len(specs)} charts from {len(sports)} sports and {len(race)} race recipe blocks");\n'
program += '\n'.join(l for l in helper.splitlines() if not l.startswith('using ')) + '\n'

os.makedirs(os.path.join(here, 'obj'), exist_ok=True)
io.open(os.path.join(here, 'obj', 'Recipes.g.cs'), 'w', encoding='utf-8').write(program)
print(f'{len(sports)} sports and {len(race)} race recipe blocks, {len(specs)} charts')
