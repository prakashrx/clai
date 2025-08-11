# Virtual Terminal Architecture

## Overview

After implementing our first version, we discovered fundamental issues with trying to extract "command output" from a persistent shell session. The solution is to use a **Virtual Terminal** approach where each command execution creates its own isolated viewport that properly handles all terminal capabilities.

## Problems with First Implementation

1. **Output Isolation**: Cannot cleanly separate command output from shell prompts
2. **Blocking Execution**: Waiting for command completion blocks streaming
3. **Fixed Delays**: Arbitrary timeouts don't work for varying command durations
4. **Lost Interactivity**: Can't properly handle interactive programs (vim, htop)
5. **Color Loss**: Difficult to preserve ANSI colors through the pipeline

## The Virtual Terminal Solution

### Core Concept

Instead of trying to extract output, we create a virtual terminal viewport for each command:

```
CLAI Display:
┌─────────────────────────────────────┐
│ > run htop                          │  ← User command
│ [AI: Executing htop]                │  ← AI annotation
│ ┌─────────────────────────────────┐ │  ← Virtual Terminal starts
│ │ htop output (fully interactive)  │ │     (coordinate translation)
│ │ CPU [||||    ] 45%               │ │     
│ │ MEM [||||||||| ] 89%             │ │
│ └─────────────────────────────────┘ │  ← Virtual Terminal ends
│ > next command                      │  ← Ready for next input
└─────────────────────────────────────┘
```

### How It Works

1. **Event Streaming**: Terminal outputs a stream of granular events
2. **Coordinate Translation**: Virtual terminal translates all coordinates relative to its viewport
3. **Isolation**: Each command's output is contained in its own virtual space
4. **Full Support**: Interactive programs work perfectly (vim, htop, etc.)

## Event Stream Architecture

### Event Flow

```
ConPTY (raw bytes)
    ↓
AnsiParser (tokenizes ANSI sequences)
    ↓
Screen (maintains terminal state)
    ↓
EventStream (emits granular events)
    ↓
VirtualTerminal (translates coordinates)
    ↓
Renderer (displays to user)
```

### Event Types

```csharp
// Base event with timestamp
abstract class TerminalEvent 
{
    DateTime Timestamp { get; }
    TerminalEventSource Source { get; } // ConPTY, System, User
}

// Content events
class TextWrittenEvent : TerminalEvent
{
    string Text { get; }
    TerminalCell[] Cells { get; }  // With color attributes
    int X, Y { get; }               // Position
}

// Cursor events
class CursorMovedEvent : TerminalEvent
{
    int X, Y { get; }
    bool Visible { get; }
}

// Screen manipulation events
class ClearScreenEvent : TerminalEvent { }
class ClearLineEvent : TerminalEvent 
{
    ClearMode Mode { get; } // ToEnd, ToStart, Entire
}
class ScrollEvent : TerminalEvent
{
    int Lines { get; }
    ScrollDirection Direction { get; }
}

// Region events
class SetScrollRegionEvent : TerminalEvent
{
    int Top, Bottom { get; }
}

// Attribute events
class ColorChangeEvent : TerminalEvent
{
    ConsoleColor? Foreground { get; }
    ConsoleColor? Background { get; }
}

// Special events
class BellEvent : TerminalEvent { }
class TitleChangeEvent : TerminalEvent
{
    string Title { get; }
}

// Semantic events (derived from heuristics)
class PromptDetectedEvent : TerminalEvent
{
    string PromptText { get; }
}

class CommandCompletedEvent : TerminalEvent
{
    TimeSpan Duration { get; }
}
```

## Virtual Terminal Implementation

### VirtualTerminal Class

```csharp
public class VirtualTerminal
{
    private readonly int offsetX, offsetY;  // Position in parent display
    private readonly int width, height;     // Viewport dimensions
    private TerminalCell[,] buffer;        // Local buffer
    private int cursorX, cursorY;          // Local cursor position
    
    public async IAsyncEnumerable<RenderedLine> ProcessEventStream(
        IAsyncEnumerable<TerminalEvent> events)
    {
        await foreach (var evt in events)
        {
            switch (evt)
            {
                case TextWrittenEvent text:
                    WriteText(text.Text, text.Cells);
                    yield return RenderLine(cursorY);
                    break;
                    
                case CursorMovedEvent cursor:
                    // Translate to local coordinates
                    SetCursor(cursor.X, cursor.Y);
                    break;
                    
                case ClearScreenEvent:
                    // Clear only our viewport
                    ClearViewport();
                    yield return RenderClear();
                    break;
                    
                case ScrollEvent scroll:
                    // Scroll within our viewport
                    ScrollViewport(scroll.Lines, scroll.Direction);
                    yield return RenderScroll();
                    break;
            }
        }
    }
}
```

### Coordinate Translation

All coordinates from the real terminal are translated to the virtual terminal's viewport:

- Real: `MoveCursor(0, 0)` → Virtual: `MoveCursor(offsetX, offsetY)`
- Real: `Clear()` → Virtual: Clear only viewport area
- Real: `ScrollUp()` → Virtual: Scroll only within viewport

## Command Execution Flow

### Streaming Command Execution

```csharp
public async Task ExecuteCommand(string command)
{
    // Show what we're doing
    await output.WriteLineAsync($"[AI: Executing {command}]");
    
    // Create virtual terminal at current position
    var virtualTerminal = new VirtualTerminal(
        offsetY: Console.CursorTop,
        width: Console.WindowWidth,
        height: 24  // Standard terminal height
    );
    
    // Start streaming events from the real terminal
    await foreach (var evt in terminal.StreamEventsAsync(command))
    {
        // Let virtual terminal process the event
        await virtualTerminal.ProcessEventAsync(evt);
        
        // Check for completion
        if (evt is CommandCompletedEvent)
            break;
    }
}
```

## Advantages

1. **True Streaming**: Events flow as they arrive, no buffering
2. **Full Compatibility**: Any terminal program works (vim, htop, ssh)
3. **Clean Isolation**: Each command's output is contained
4. **Preserved Colors**: Full ANSI color support through the pipeline
5. **Interactive Support**: Programs that update in-place work perfectly
6. **No Prompt Pollution**: Shell prompts don't contaminate output

## Detection Strategies

### Command Completion Detection

Multiple strategies working together:

1. **Prompt Pattern**: Detect common prompt patterns (>, $, #)
2. **Idle Detection**: No output for configurable period
3. **Cursor Position**: Cursor returns to expected position
4. **Process Exit**: For directly executed commands

### Prompt Filtering

Prompts are detected but not displayed:
- Initial prompt before command echo: Hidden
- Command echo line: Hidden
- Output: Displayed
- Final prompt: Hidden, triggers completion event

## Implementation Plan

1. **Create Event System**: Define all event types and streaming infrastructure
2. **Update Terminal/Screen**: Emit granular events for every change
3. **Build VirtualTerminal**: Implement coordinate translation and rendering
4. **Integrate with CLAI**: Wire up streaming to command processor
5. **Add Color Rendering**: Map terminal colors to Spectre.Console markup

## Technical Considerations

### Streaming with IAsyncEnumerable

```csharp
public async IAsyncEnumerable<TerminalEvent> StreamEventsAsync(
    string command,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    await terminal.WriteLineAsync(command);
    
    await foreach (var evt in eventQueue.ReadAllAsync(ct))
    {
        yield return evt;
        
        if (evt is CommandCompletedEvent)
            yield break;
    }
}
```

### Backpressure Handling

Using `Channel<T>` for the event queue provides natural backpressure:
- If consumer is slow, producer naturally slows
- Bounded channel prevents memory issues
- Async enumeration handles flow control

## Future Enhancements

1. **Session Recording**: Stream events can be recorded and replayed
2. **Multiple Viewports**: Split screen for parallel command execution
3. **Smart Scrollback**: Each command's output in its own scrollback buffer
4. **AI Observation**: AI can observe event stream for better understanding
5. **Rich Interactions**: Click on output, copy with colors, etc.