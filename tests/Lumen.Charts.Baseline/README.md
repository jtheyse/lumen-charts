# Rendering-hash baseline

Every release proves which renderings it moved. This harness renders a fixed set of charts — every kind in light and dark, with and without native titles, plus named rows for each feature — and writes one SHA-256 prefix per rendering. A release that should change nothing must reproduce the previous release's hashes exactly; a release that adds a feature may only add rows.

```powershell
dotnet run -c Release                      # refined finish → baseline.txt
dotnet run -c Release -- classic           # classic finish (0.23.0's look) → classic.txt
dotnet run -c Release -- mine.txt          # any argument ending .txt names the output
dotnet run -c Release -- svg-out <dir>     # also writes each rendering, for a contact sheet
```

`reference/refined.txt` and `reference/classic.txt` are the hashes of **v0.40.0**, 360 rows, recorded by its verification: v0.39.0's 351 rows reproduced exactly in both finishes, with 0.40.0's nine rows added (gradient columns in light and Midnight and gradient bars, sub-labels under columns and beside bars, the heart-rate-per-lap and best-efforts recipes at 340, a strip whose amounts are its shares, and an end label whose note is cut first). v0.39.0 recorded 351 rows: v0.38.0's 338 reproduced exactly, with thirteen added (strips, bars on tracks, undrawn titles). v0.38.0 recorded 338 rows: v0.37.0's 331 reproduced exactly, with seven added (set ticks, a unit, crowded end labels, the season arc and gap to the leader). v0.37.0 recorded 331 rows: v0.36.0's 325 reproduced exactly, with six added (averaged and windowed sampling, a pane without tick labels, the ride-channels recipe). v0.36.0 recorded 325 rows: v0.35.0's 318 reproduced exactly, with seven added (symmetric axes, signed labels, the fitness-and-form recipe). v0.35.0 recorded 318 rows: v0.34.0's 310 rows, 308 reproduced exactly in both finishes and two changed by design (`race/recommended-340-light` and `-midnight`, whose description now wraps to two lines), with 0.35.0's eight rows added (a finish-time histogram at 340 in light and Midnight and as a 1080 by 1350 card, bounds labels on Y, a reference in front of columns, and a long description, source and title at 340). Earlier releases changed rows by design: 0.31.0 the twenty graph rows, 0.30.0 the seven calendars drawn without `YZones`. Compare a new build with:

```bash
diff <(tr -d '\r' < reference/refined.txt) <(tr -d '\r' < baseline.txt)
```

When a release adds renderings, add their rows to `Program.cs` (named rows go in a block headed by the release that adds them) and, once the release is verified, replace the reference files with the new run.

The strongest check builds the previous release separately: `git worktree add <short path> v<previous>`, copy this folder into it, run it there, and compare that output with the current build's — rows that use API the old code lacks must be removed from the copy first. Keep the worktree path short; Windows refuses the long `obj` paths a deep one produces.

Hashes are recorded on Windows. Another platform's maths library can differ in the last bits of a sine or a logarithm, which moves an eighth decimal in a donut's arc, so compare hashes on Windows.
