# CLAI - Command Line AI Project

## Project Overview
Building an AI-native terminal emulator that seamlessly blends natural language interaction with traditional command-line functionality. Think of it as a terminal where you can talk naturally and the AI understands whether you want to execute a command or accomplish a goal.

## Key Design Decisions
1. **Minimal Dependencies** - No heavy GUI frameworks (no WPF/Avalonia). Runs inside existing terminal.
2. **Spectre.Console for UI** - Rich console formatting, prompts, and tables for the main app
3. **ConPTY for Windows** - Using Windows Pseudo Console for proper terminal emulation
4. **.NET 9 with modern C#** - Latest language features, minimal dependencies
5. **Clean, Modular Code** - Emphasis on elegant, simple, modern C# patterns. No over-engineering.

## Core Features
- **Natural language by default**: Type "check disk space" → AI runs `df -h`
- **Seamless mode switching**: AI mode default, easy switch to manual
- **Real-time observation**: AI sees terminal output via periodic snapshots (not byte-by-byte parsing)
- **Inline AI thinking**: `[AI thinking: Running system check...]` appears in terminal flow
- **Context persistence**: Sessions save/restore with full context and mission tracking
- **Interruption support**: User can stop AI and take control anytime

## Architecture
```
CLAI Main App (Spectre.Console)
    ├── Rich CLI UI (Prompts + Tables + Progress)
    └── Core Engine
        ├── AI Service (Natural language → Commands)
        ├── Context Manager (Mission/Goal/Task hierarchy)
        ├── Clai.Terminal Library (ConPTY wrapper)
        └── Observation Service (Periodic snapshots)
```

## Interaction Model
```
> find large log files and compress them
[AI thinking: Searching for large log files...]
$ find . -name "*.log" -size +100M
[output shown]
[AI thinking: Compressing 3 files...]
$ gzip file1.log
$ gzip file2.log
> stop, just show me the list first
[AI thinking: Stopping compression, showing file list...]
```

## Current Status - Major Architecture Revision (2025-08-11)

### 🔄 Why We're Starting Fresh
After implementing the first version with terminal integration, we discovered fundamental issues:
1. **Output Isolation Problem**: Cannot cleanly separate command output from shell prompts
2. **Blocking Architecture**: Must wait for command completion before showing output
3. **No Real Streaming**: Output appears all at once instead of progressively
4. **Interactive Programs**: Can't properly handle vim, htop, or other TUI applications

### 🎯 New Approach: Virtual Terminal Architecture
We're rebuilding with proper event-streaming architecture:
- **Event Streaming**: Granular events flow from terminal to consumers
- **Virtual Terminal**: Each command gets its own viewport with coordinate translation
- **Real-time Display**: Output streams as it arrives
- **Full Compatibility**: vim, htop, and all terminal programs work perfectly

See `/docs/Virtual_Terminal_Architecture.md` for detailed design.

## Next Steps (V2 Implementation)
1. Move first implementation to `/reference/first-impl/`
2. Create new `/src` with Virtual Terminal architecture
3. Implement proper event streaming with IAsyncEnumerable
4. Build VirtualTerminal class for coordinate translation
5. Integrate streaming with CLAI command processor

## Code Style Principles
- **Simple over clever** - Readable code beats clever one-liners
- **Modern C# features** - Primary constructors, pattern matching, nullable reference types
- **Small focused classes** - Single responsibility, easy to test
- **Minimal abstractions** - Only abstract when there's a clear need
- **Descriptive names** - Code should read like documentation
- **No premature optimization** - Clean first, optimize when measured

## Key Files
- `/docs/Vision.md` - Full project vision
- `/docs/Technical_Spec.md` - Detailed technical specification
- `/reference/terminal/` - Windows Terminal reference code
- Always build and make sure there are no errors/warnings. I will run it myself

## Status

### ✅ Completed (Clai.Terminal Library)
- **ConPTY Integration**: Full Windows Pseudo Console support
- **Process Management**: Start/stop processes, handle I/O  
- **ANSI/VT100 Parser**: Colors, cursor movement, clearing, ESC[?25h/l recognition
- **Screen Buffer**: 2D cell array with attributes
- **Basic Scrollback**: Lines saved when scrolling up (currently called `Scrollback`, should rename to `History`)
- **Color Support**: Full 16 colors + extended 256-color format (ESC[38;5;n)
- **Cursor Tracking**: Position, save/restore (ESC[s/u, ESC7/8)
- **Demo Application**: Interactive terminal with PowerShell testing
- **Debug Output**: Debug.WriteLine for input/output analysis

### 🚧 To Implement Next
- **Viewport Navigation**: Clean API for scrolling through history
  - `ScrollUp()/ScrollDown()` methods
  - `GetViewport()` to render from any position
  - Page Up/Down key handling
- **Plain Text Extraction**: For AI observation
  - `GetVisibleText()` - current screen
  - `GetHistoryText()` - scrollback content  
  - `GetAllText()` - everything
- **Alternate Screen Buffer**: ESC[?1049h/l for vim/htop
  - Separate buffer for full-screen apps
  - Prevent history pollution from TUI redraws

### 📋 Future Plans
- **Event Stream**: Unified chronological event log (as per ConPTY_Integration_Design.md)
- **AI Observer**: Snapshot and context extraction service
- **Mode Switching**: AI vs Manual mode routing in main app
- **Session Persistence**: Save/restore terminal state
- **Main CLAI Integration**: Connect terminal library to Spectre.Console UI

### 🔍 Notes & Observations
- **NOT using Terminal.Gui** - Main app uses Spectre.Console, terminal library is standalone
- **Scrollbars**: Windows Terminal feature, not ConPTY - we'll need status bar indicators
- **PowerShell colors working**: ESC[93m (yellow), ESC[38;5;9m (red) properly handled
- **Cursor "flickering"**: Actually PSReadLine redrawing entire line with syntax highlighting - expected behavior
- **Clean history important**: Regular commands go to history, full-screen apps use alternate buffer
- Do not run. just build