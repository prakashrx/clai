# Reference Code

This directory contains reference implementations and libraries that we're studying for CLAI development.

## Contents

- **aider** - AI pair programming in terminal
- **libvt100** - VT100/ANSI terminal emulation library
- **PocketFlow** - Example of terminal-based workflow
- **spectre.console** - Rich console formatting library (we use this)
- **terminal** - Windows Terminal source code (ConPTY reference)

## Git Management

These are external repositories included for reference. To keep the main repo lightweight:

1. The `.gitignore` excludes nested `.git` directories
2. We commit only the source files we need for reference
3. Original repos can be found at:
   - aider: https://github.com/paul-gauthier/aider
   - libvt100: (check original source)
   - spectre.console: https://github.com/spectreconsole/spectre.console
   - Windows Terminal: https://github.com/microsoft/terminal
For now, we're committing just the files we need for reference.