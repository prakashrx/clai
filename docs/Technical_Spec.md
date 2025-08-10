# CLAI Technical Specification

## Minimal Terminal-First Design

### Core Concept
CLAI is a lightweight AI-enhanced terminal that runs inside your existing terminal emulator. No heavy GUI framework, just intelligent command-line interaction with minimal visual elements.

### Technology Stack
- **Language**: C# with .NET 9
- **Terminal Library**: Custom ConPTY wrapper (Clai.Terminal)
- **Main App UI**: Spectre.Console for rich CLI
- **Terminal Engine**: Windows ConPTY
- **AI Integration**: Provider-agnostic (OpenAI/Anthropic/Local)
- **Storage**: SQLite for context persistence

### Visual Design

```
┌─────────────────────────────────────────────────────────┐
│  > deploy the api                                       │
│  [AI thinking: Starting deployment process...]          │
│  $ git status                                          │
│  On branch main                                        │
│  Your branch is up to date with 'origin/main'.        │
│                                                        │
│  $ npm test                                            │
│  Tests: 42 passed, 0 failed                           │
│                                                        │
│  [AI thinking: Tests passed, proceeding to build...]   │
│  $ npm run build                                       │
│  Building production bundle...                         │
│  Build completed in 12.3s                              │
│                                                        │
│  > stop, let me check the config first                 │
│  [AI thinking: Pausing deployment, entering manual...]  │
│                                                        │
└─────────────────────────────────────────────────────────┘
[AI Ready] /home/project > _
```

### Key Components

#### 1. Terminal View
- Full terminal emulation via ConPTY
- VT100/ANSI sequence support
- Scrollback buffer
- Text selection and copy

#### 2. Unified Input Line
- Single input field at bottom
- Accepts both natural language and commands
- Auto-detection of intent (AI vs direct command)
- Command history (up/down arrows)

#### 3. Minimal Status Bar
```
[AI Ready] /current/directory > _
[AI Thinking...] /current/directory > _
[AI Observing (2s)] /current/directory > _
[Manual Mode] /current/directory > _
```

### Architecture

```
┌─────────────────────────────────────┐
│         CLAI Application           │
│  ┌─────────────────────────────┐   │
│  │   Spectre.Console UI        │   │
│  │  - Rich formatting          │   │
│  │  - Interactive prompts      │   │
│  │  - Progress indicators      │   │
│  └─────────────────────────────┘   │
└────────────────┬───────────────────┘
                 │
┌────────────────┴───────────────────┐
│          Core Engine               │
│  ┌─────────────┐ ┌─────────────┐  │
│  │ AI Service  │ │Context Mgr  │  │
│  └─────────────┘ └─────────────┘  │
│  ┌─────────────┐ ┌─────────────┐  │
│  │Clai.Terminal│ │ Observation │  │
│  │   Library   │ │   Service   │  │
│  └─────────────┘ └─────────────┘  │
└────────────────────────────────────┘
                 │
┌────────────────┴───────────────────┐
│      Clai.Terminal Library         │
│  ┌─────────────┐ ┌─────────────┐  │
│  │   Terminal  │ │    Screen   │  │
│  │   (ConPTY)  │ │   (Buffer)  │  │
│  └─────────────┘ └─────────────┘  │
│  ┌─────────────┐ ┌─────────────┐  │
│  │ AnsiParser  │ │TerminalCell│  │
│  │  (VT100)    │ │  (Display)  │  │
│  └─────────────┘ └─────────────┘  │
└────────────────────────────────────┘
```

### Clai.Terminal Library API

The terminal library provides a clean abstraction for terminal emulation with intelligent history management:

```csharp
// Core Classes
public class Terminal : IDisposable
{
    public Screen Screen { get; }
    public bool IsRunning { get; }
    
    // Start terminal with optional command
    void Start(string command = "cmd.exe");
    
    // Send input
    Task WriteAsync(string text);
    Task WriteLineAsync(string line);
    Task SendKeyAsync(ConsoleKeyInfo key);
    
    // Events
    event Action<TerminalUpdate> ScreenUpdated;
    event Action<int> ProcessExited;
}

public class Screen
{
    // Dimensions and position
    public int Width { get; }
    public int Height { get; }
    public int TotalLines { get; }      // History + current screen
    public int ViewportLine { get; }    // Current view position
    public bool IsAtBottom { get; }     // Viewing live output?
    
    // Get content at current viewport
    public TerminalCell[,] GetViewport();
    
    // Navigation
    public void ScrollUp(int lines = 1);
    public void ScrollDown(int lines = 1);
    public void ScrollToTop();
    public void ScrollToBottom();
    
    // Text extraction for AI
    public string GetText(int startLine, int lineCount);
    public string GetVisibleText();
    public string GetAllText();
}
```

#### Smart History Management

The terminal maintains **clean semantic history** - just like real terminals:

- **Regular commands** (ls, echo, git): Output saved to history
- **Full-screen apps** (vim, htop, top): Use alternate screen, no history pollution
- **Alternate screen detection**: ESC[?1049h/l sequences trigger mode switch

This ensures:
- Clean, chronological command history for scrollback
- No pollution from TUI application redraws
- AI gets meaningful context, not noise from htop refreshes

### Core Features Implementation

#### 1. Mode Switching
```csharp
// Natural detection - no special syntax needed
if (IsNaturalLanguage(input))
    await ProcessAICommand(input);
else
    await ExecuteDirectCommand(input);

// Or explicit with prefix
if (input.StartsWith("/"))
    await ExecuteDirectCommand(input[1..]);
```

#### 2. Time-Step Observation
```csharp
public class TerminalObserver
{
    private readonly Terminal terminal;
    private Timer _snapshotTimer;
    private string _lastSnapshot;
    
    public void StartObserving(int intervalMs)
    {
        _snapshotTimer = new Timer(TakeSnapshot, null, 0, intervalMs);
    }
    
    private void TakeSnapshot(object state)
    {
        // Get plain text representation of terminal
        var currentBuffer = terminal.Screen.GetPlainText();
        if (currentBuffer != _lastSnapshot)
        {
            AI.ProcessSnapshot(currentBuffer);
            _lastSnapshot = currentBuffer;
        }
    }
}
```

#### 3. Inline AI Feedback
```csharp
// AI thoughts appear as part of the terminal flow
await Terminal.WriteLine("[AI thinking: Running system diagnostics...]");
await Terminal.ExecuteCommand("systeminfo");
```

### Session & Context

#### Minimal Storage Schema
```sql
CREATE TABLE sessions (
    id TEXT PRIMARY KEY,
    name TEXT,
    mission TEXT,
    created_at TIMESTAMP,
    updated_at TIMESTAMP
);

CREATE TABLE context_events (
    session_id TEXT,
    timestamp TIMESTAMP,
    type TEXT, -- 'command', 'output', 'ai_thought', 'bookmark'
    content TEXT,
    metadata TEXT -- JSON
);
```

### Commands & Shortcuts

- **Ctrl+C**: Interrupt current operation
- **Ctrl+D**: Toggle AI/Manual mode
- **Ctrl+R**: Search command history
- **Up/Down**: Navigate history
- **Tab**: Command completion
- **Ctrl+S**: Save session checkpoint

### Minimal Session Management

Instead of GUI for sessions:
```
> list sessions
[AI: Here are your recent sessions:]
1. api-debugging (2 hours ago)
2. server-setup (yesterday)
3. react-migration (last week)

> continue 1
[AI: Loading api-debugging session...]
```

### Performance Considerations

- Minimal dependencies (just Spectre.Console for UI)
- ConPTY native performance
- Lazy-load AI only when needed
- Efficient snapshot diffing
- Minimal memory footprint
- Clean separation between terminal emulation and UI

### Installation & Usage

```bash
# Single executable
clai.exe

# Or install globally
dotnet tool install -g clai

# Run
clai
```

No complex setup, no heavy dependencies. Just run and start typing.

### Example Interaction

```
$ clai
CLAI - Command Line AI
Type naturally or enter commands. Ctrl+D to toggle modes.

> find all large log files and compress them
[AI thinking: Searching for large log files...]
$ find . -name "*.log" -size +100M
./app/debug.log
./system/error.log
./backup/archive.log

[AI thinking: Found 3 large files, compressing...]
$ gzip ./app/debug.log
$ gzip ./system/error.log
$ gzip ./backup/archive.log

[AI: Compressed 3 log files, saved 2.1GB]

> excellent, now check disk space
[AI thinking: Checking disk usage...]
$ df -h
Filesystem      Size  Used Avail Use% Mounted on
C:              238G  156G   82G  66% /

> _
```

Simple, fast, and intelligent. The terminal, evolved.