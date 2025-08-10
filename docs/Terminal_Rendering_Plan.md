# Terminal Rendering Plan for CLAI

## Current State
We have a working terminal emulator with:
- **ConPTY integration** - Spawns cmd/PowerShell processes
- **ANSI parsing** - Correctly parses escape sequences 
- **Screen buffer** - Maintains terminal state with colors and positioning
- **Multiple implementations** - Mix of Screen.cs and NativeScrollingScreen.cs

## The Problem
We're mixing approaches and have rendering issues:
1. **Cursor position mismatch** - Terminal cursor doesn't align with displayed text
2. **Can't see typing** - Input doesn't appear where expected
3. **Overlapping output** - ConPTY output conflicts with our console output
4. **Two different Screen classes** - Screen.cs (fixed buffer) and NativeScrollingScreen.cs (dynamic)

## Root Cause
We've been passing raw ConPTY output directly to Console.Write(), which includes ANSI escape sequences that position cursor absolutely. This conflicts with our console's existing content and cursor position.

## The Solution

### Architecture
```
ConPTY (hidden) → ANSI Parser → Screen Buffer → Smart Renderer → Console Display
```

### Two Rendering Modes

#### 1. Normal Mode (Commands with Output)
For regular commands like `dir`, `git status`, etc:
- Output flows naturally in conversation
- Preserves colors and formatting
- AI thoughts interleave seamlessly

```
C:\Users\prakash> dir
[output with colors preserved]
[AI: Found 5 files]
C:\Users\prakash> 
```

#### 2. Interactive Mode (Full-Screen Apps)
For vim, htop, etc that use alternate screen buffer:
- Render full 24x80 terminal buffer
- Handle absolute cursor positioning
- Clear and restore when exiting

### Implementation Steps

#### Step 1: Clean Up (Remove Experiments)
- Delete NativeScrollingScreen.cs (keep original Screen.cs)
- Delete ConsoleRenderer.cs
- Delete SimpleRenderer.cs  
- Delete InteractiveDemo.cs
- Remove any Console.Write(raw_ansi) calls

#### Step 2: Fix Core Components
**Terminal.cs:**
- Remove direct Console.Write() of ConPTY output
- Only feed data to Screen for parsing

**Screen.cs:**
- Already works correctly with fixed 24x80 buffer
- Handles alternate screen for vim/htop
- Keep as-is

**TerminalRenderer.cs:**
- Fix to render FROM Screen buffer, not raw output
- Two modes based on screen.IsAlternateScreen
- Preserve colors when rendering

#### Step 3: Implement Proper Rendering

**Normal Mode Rendering:**
```csharp
class TerminalRenderer {
    private int lastRenderedLine = 0;
    
    void RenderNormalMode(Screen screen) {
        // Get new content since last render
        var scrollback = screen.Scrollback;
        for (int i = lastRenderedLine; i < scrollback.Count; i++) {
            RenderLineWithColors(scrollback[i]);
        }
        lastRenderedLine = scrollback.Count;
    }
    
    void RenderLineWithColors(TerminalCell[] line) {
        foreach (var cell in line) {
            Console.ForegroundColor = cell.ForegroundColor;
            Console.BackgroundColor = cell.BackgroundColor;
            Console.Write(cell.Char);
        }
        Console.WriteLine();
        Console.ResetColor();
    }
}
```

**Interactive Mode Rendering:**
```csharp
void RenderInteractiveMode(Screen screen) {
    // Save position
    var startY = Console.CursorTop;
    
    // Render full buffer at current position
    var buffer = screen.GetScreen();
    for (int y = 0; y < screen.Height; y++) {
        Console.SetCursorPosition(0, startY + y);
        for (int x = 0; x < screen.Width; x++) {
            var cell = buffer[y, x];
            // Apply colors
            Console.ForegroundColor = cell.ForegroundColor;
            Console.Write(cell.Char);
        }
    }
    
    // Position cursor
    Console.SetCursorPosition(screen.CursorX, startY + screen.CursorY);
}
```

## Expected Outcome

### For Normal Commands:
```
=== Clai.Terminal Demo ===
> Running dir command...

C:\Users\prakash> dir
 Volume in drive C is Windows
 Directory of C:\Users\prakash

08/11/2025  09:00 AM    <DIR>          Documents
08/11/2025  09:00 AM    <DIR>          Downloads
               0 File(s)              0 bytes
               2 Dir(s)  100,000,000,000 bytes free

C:\Users\prakash> exit

Process exited with code: 0
```

### For Interactive Apps:
- Full-screen rendering when vim/htop starts
- Proper cursor positioning for editing
- Clean exit back to normal mode

## Benefits
1. **No more overlapping** - ConPTY output properly isolated
2. **Cursor works** - Input appears where expected
3. **Colors preserved** - Full terminal experience
4. **AI integration ready** - Clean separation allows AI to observe and comment
5. **Scrolling works** - Natural flow in console

## Next Steps
1. Remove experimental code
2. Fix TerminalRenderer to use this approach
3. Test with various commands and interactive apps
4. Integrate with CLAI's AI conversation flow