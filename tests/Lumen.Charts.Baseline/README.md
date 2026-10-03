# Rendering-hash baseline

Every release proves which renderings it moved. This harness renders a fixed set of charts — every kind in light and dark, with and without native titles, plus named rows for each feature — and writes one SHA-256 prefix per rendering. A release that should change nothing must reproduce the previous release's hashes exactly; a release that adds a feature may only add rows.

```powershell
dotnet run -c Release                      # refined finish → baseline.txt
dotnet run -c Release -- classic           # classic finish (0.23.0's look) → classic.txt
dotnet run -c Release -- mine.txt          # any argument ending .txt names the output
dotnet run -c Release -- svg-out <dir>     # also writes each rendering, for a contact sheet
```

`reference/refined.txt` and `reference/classic.txt` are the hashes of **v0.31.0**, 275 rows, recorded by its verification: v0.30.0's 269 rows with 0.31.0's six graph rows added (the gallery's pipeline at 900 pixels and fitted to 337, and a crowded graph, in each layout). The 249 rows that are not graphs reproduced exactly in both finishes; the twenty graph rows changed by design in both, since 0.31.0 keeps edges and edge labels clear of node labels and stands a circle in from the sides by half its widest label. (0.30.0 had changed the seven calendars drawn without `YZones` the same way, by design.) Compare a new build with:

```bash
diff <(tr -d '\r' < reference/refined.txt) <(tr -d '\r' < baseline.txt)
```

When a release adds renderings, add their rows to `Program.cs` (named rows go in a block headed by the release that adds them) and, once the release is verified, replace the reference files with the new run.

The strongest check builds the previous release separately: `git worktree add <short path> v<previous>`, copy this folder into it, run it there, and compare that output with the current build's — rows that use API the old code lacks must be removed from the copy first. Keep the worktree path short; Windows refuses the long `obj` paths a deep one produces.

Hashes are recorded on Windows. Another platform's maths library can differ in the last bits of a sine or a logarithm, which moves an eighth decimal in a donut's arc, so compare hashes on Windows.
