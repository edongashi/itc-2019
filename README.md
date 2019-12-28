# ITC2019 Solver

This is the source code of the solver used to compete in the [International Timetabling Competition 2019](https://www.itc2019.org).

## Building

[.NET Core](https://dotnet.microsoft.com/download) (2.1 or higher) is required to build the solver.

Common build targets are provided in the Makefile: `linux-x64`, `win-x64`, `osx-x64`.

To build standalone binaries, run either of the following commands:

```bash
$ make linux
$ make win
$ make osx

# Builds all targets
$ make all # or just make
```

Output is written to `bin/{platform}`.

## Running

The executable file `Timetabling.CLI` accepts the following arguments:

```
USAGE: Timetabling.CLI [--help] --instance <path> [--solution <path>] [--seed <seed>]

OPTIONS:

    --instance <path>     XML problem path.
    --solution <path>     Solution to reload.
    --seed <seed>         Seed number.
```

Shortcut scripts are provided in the repo root: `run-linux.sh`, `run-win.cmd`, `run-osx.sh`.

Example:

```bash
./run-linux.sh --instance /path/to/wbg-fal10.xml
```

The solver will routinely print stats and save solution backups.
Sending `Control+C` will stop the solver and save the best solution.

Solutions are saved relative to the working directory with format `solution_<instance>_<seed>.xml`.

## License

MIT License
