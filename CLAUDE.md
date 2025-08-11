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

## Status - V2 Implementation (Event Streaming Architecture)

### ✅ Completed
- **ConPTY Integration**: Full Windows Pseudo Console support with persistent shell
- **Event Streaming**: IAsyncEnumerable + Channel<T> for proper async event flow
- **ANSI Parser with State Machine**: 
  - Proper OSC sequence handling (terminates on BEL, not letters)
  - CSI sequences (cursor, colors, clearing)
  - Distinct states for different escape types
- **Event Types**: Rich event model (TextWritten, CursorMoved, Clear, Prompt, etc.)
- **Process Management**: Persistent cmd.exe with proper stream handling
- **Demo Application**: Event streaming proof-of-concept

### 🎯 Current Architecture Decisions

#### Event-Driven, No Screen Buffer Required
After analysis, we've determined that a traditional Screen buffer class is **not needed**:
- **VirtualTerminal is sufficient**: Each command runs in its own VirtualTerminal viewport
- **Events ARE the history**: The event stream itself provides complete history
- **AI observes VirtualTerminals**: Each VirtualTerminal is an isolated, observable unit
- **CLAI conversation = sequence of VirtualTerminals**: Natural chronological history

#### VirtualTerminal Design
- Each command execution creates a VirtualTerminal
- Handles both simple commands AND interactive programs (vim, htop)
- Interactive programs update within their viewport (contained chaos)
- Coordinate translation keeps output isolated
- Event stream provides natural boundaries (command start → output → completion)

### 🚧 To Implement Next
1. **VirtualTerminal Class**: 
   - Consume events from PseudoConsole
   - Translate coordinates to viewport
   - Handle isolation for each command
   - Support interactive programs within viewport

2. **Integration with Main App**:
   - Wire up VirtualTerminals to Spectre.Console display
   - Show command outputs in isolated boxes
   - Maintain conversation history (list of VirtualTerminals)

3. **AI Observation**:
   - AI observes VirtualTerminal event streams
   - Can query "what's in terminal N?"
   - Can review conversation history

### 📋 Future Enhancements
- **Multiple Terminal Sessions**: Like tmux - multiple independent shells
- **Session Persistence**: Save/restore conversation with all VirtualTerminals
- **Rich Copy/Paste**: Select text from any VirtualTerminal in history
- **Smart Summarization**: Compress interactive program sessions for AI context

### 🔍 Key Insights
- **No Screen class needed**: VirtualTerminals + events provide everything
- **Simpler than traditional terminals**: Each command naturally isolated
- **Full observability**: AI sees everything through event streams
- **Interactive programs work**: They just update within their VirtualTerminal viewport
- **History is automatic**: The conversation IS the history