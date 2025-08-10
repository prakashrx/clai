# ConPTY Integration Design

## Overview

This document outlines the architecture for integrating Windows ConPTY (Console Pseudo Terminal) into CLAI, enabling real terminal emulation with full keystroke capture, process execution, and AI observation capabilities.

## Core Requirements

1. **Real-time keystroke capture** - Every key press, including ESC, Tab, Ctrl sequences
2. **Bidirectional process communication** - Send input to and receive output from real processes
3. **AI observation** - AI monitors both input and output streams in real-time
4. **Mode switching** - Seamless transition between AI and manual terminal modes
5. **Complete event tracking** - Full audit trail of all terminal interactions

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    User Keyboard Input                       │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                   Raw Input Handler                          │
│              (Character-by-character capture)                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                    Input Router                              │
│    Decides: AI Processing vs Direct Terminal Passthrough     │
└────────┬───────────────────────────────────┬────────────────┘
         │                                   │
         │ AI Mode                           │ Manual Mode
         ▼                                   ▼
┌─────────────────────┐           ┌─────────────────────┐
│   AI Input Buffer   │           │  Direct to ConPTY   │
│  (Line editing, AI  │           │   (Raw passthrough) │
│   can observe)      │           └──────────┬──────────┘
└──────────┬──────────┘                      │
           │                                  │
           ▼                                  ▼
┌─────────────────────────────────────────────────────────────┐
│                      ConPTY Service                          │
│            (PseudoConsole + Process Management)              │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                 Unified Event Stream                         │
│          (Chronological log of ALL events)                  │
└────────────────────────┬────────────────────────────────────┘
                         │
                   ┌─────┴─────┐
                   ▼           ▼
        ┌─────────────────┐ ┌─────────────────┐
        │  Terminal Buffer│ │  AI Observer    │
        │   (Display)     │ │  (Analysis)     │
        └────────┬────────┘ └─────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────┐
│                   Display Renderer                           │
│                 (Visual representation)                      │
└─────────────────────────────────────────────────────────────┘
```

## The Unified Event Stream

The heart of our design is a **Unified Event Stream** that captures EVERYTHING in chronological order:

### Event Types

1. **Input Events**
   - Keystroke (key, timestamp, modifiers)
   - Paste operation (content, timestamp)
   - Special key (ESC, Tab, Ctrl+C, etc.)
   - Mode switch (AI ↔ Manual)

2. **Output Events**
   - Process output (text, VT sequences)
   - Error output (stderr)
   - Process state changes (started, exited, crashed)

3. **System Events**
   - Command executed (command line, timestamp)
   - Process spawned (PID, command)
   - Terminal resized (new dimensions)
   - User interrupted (Ctrl+C pressed)
   - Input cancelled (ESC in AI mode)
   - Autocomplete triggered
   - Suggestion accepted/rejected

4. **AI Events**
   - AI thinking started/completed
   - Command generated
   - Observation snapshot taken
   - Context updated

### Event Stream Structure

Each event in the stream contains:
- **Timestamp** - Precise timing for replay and analysis
- **Type** - Category of event
- **Source** - User, Process, AI, System
- **Content** - The actual data
- **Context** - Current mode, working directory, etc.
- **Metadata** - Additional information (e.g., exit codes, signal types)

### Example Event Flow

```
[10:23:45.123] USER_INPUT: "l" (AI mode, buffering)
[10:23:45.234] USER_INPUT: "s" (AI mode, buffering)
[10:23:45.567] USER_INPUT: <Enter> (AI mode, submit)
[10:23:45.568] AI_THINKING: Started processing "ls"
[10:23:45.789] AI_COMMAND: Generated "Get-ChildItem"
[10:23:45.790] PROCESS_START: pwsh.exe -Command "Get-ChildItem"
[10:23:45.823] PROCESS_OUTPUT: "Directory: C:\Users\..."
[10:23:45.845] PROCESS_OUTPUT: "Mode    LastWriteTime..."
[10:23:45.867] PROCESS_EXIT: Code 0
[10:23:45.868] AI_OBSERVATION: Snapshot taken, 15 files listed
[10:23:47.234] USER_INPUT: <Ctrl+C> (interrupt)
[10:23:47.235] SYSTEM_EVENT: Process interrupted
```

## Component Responsibilities

### Raw Input Handler
- Captures every keystroke using low-level console APIs
- Fires events for each key press with full context
- Handles special key combinations (Ctrl+, Alt+, etc.)
- Supports both Windows and Unix key sequences

### Input Router
- Maintains current mode state (AI vs Manual)
- Routes input to appropriate handler
- Manages mode switching logic
- Tracks input context for the event stream

### ConPTY Service
- Creates and manages the pseudo console
- Handles process lifecycle (start, stop, signal)
- Manages input/output pipes
- Translates between Windows console API and VT sequences

### Unified Event Stream
- Maintains chronological log of all events
- Provides snapshots for AI observation
- Supports scrollback and search
- Enables session replay and debugging
- Persists to disk for session continuity

### AI Observer
- Subscribes to the event stream
- Takes periodic snapshots of terminal state
- Identifies patterns and context changes
- Maintains awareness of:
  - Current command being typed
  - Recent output
  - Error states
  - User corrections/cancellations

### Terminal Buffer (Screen)
- Maintains visual state of the terminal
- Processes VT sequences for display
- Handles cursor positioning
- **Smart history management**:
  - Clean semantic history for regular commands
  - Alternate screen buffer for full-screen apps (vim, htop)
  - No pollution from TUI application redraws
- Viewport-based navigation for scrollback
- Plain text extraction for AI observation
- Separate from event stream (display vs history)

## Mode Behaviors

### AI Mode
1. User types → Events logged → Characters buffered
2. Special keys (Tab, ESC) → Events logged → AI processes
3. Enter pressed → AI analyzes buffer → Generates command
4. Command sent to ConPTY → Output captured → AI observes

### Manual Mode
1. User types → Events logged → Direct to ConPTY
2. No buffering, immediate passthrough
3. AI still observes but doesn't intervene
4. Full terminal application support (vim, ssh, etc.)

## Special Scenarios

### User Cancellation (ESC)
- In AI mode: Clears current buffer, logs cancellation event
- In Manual mode: Sends ESC to process
- AI learns from cancellations to improve suggestions

### Interrupt (Ctrl+C)
- Always sent to ConPTY process
- Logged as interrupt event
- AI notes the interruption for context

### Autocomplete (Tab)
- In AI mode: Triggers AI suggestions
- In Manual mode: Sends Tab to process
- Completion choices logged for learning

### History Navigation (Up/Down arrows)
- In AI mode: Navigate command history
- In Manual mode: Send to process
- History access patterns logged

## Benefits of This Design

1. **Complete Observability** - AI sees everything: input, output, cancellations, corrections
2. **Learning Capability** - AI can learn from user behavior patterns
3. **Debugging Support** - Full event log for troubleshooting
4. **Session Replay** - Can recreate exact terminal session
5. **Context Awareness** - AI understands the full interaction context
6. **Audit Trail** - Complete record of all terminal activity

## Implementation Priorities

### Phase 1: Foundation
- ConPTY integration
- Basic process execution
- Output capture

### Phase 2: Event Stream
- Event type definitions
- Chronological logging
- Basic persistence

### Phase 3: Input System
- Raw keystroke capture
- Mode routing
- Special key handling

### Phase 4: AI Integration
- Observer pattern
- Snapshot generation
- Context extraction

### Phase 5: Polish
- Display rendering
- Scrollback
- Session management

## Key Design Decisions

1. **Event Stream First** - Everything goes through the event stream, ensuring nothing is lost
2. **Separation of Concerns** - Display buffer is separate from event history
3. **Mode Agnostic Logging** - All events logged regardless of mode
4. **Immutable Events** - Once logged, events are never modified
5. **Async by Default** - Non-blocking event processing

## Success Criteria

- Can run any console application (git, npm, ssh, vim)
- AI can observe and understand full interaction context
- User can seamlessly switch between AI and manual modes
- Complete session history available for review
- Sub-100ms latency for keystroke processing
- No lost keystrokes or output