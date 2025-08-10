using System.Text;
using Clai.Terminal;

namespace Clai.Terminal.Demo;

/// <summary>
/// Optimized terminal renderer that minimizes flicker and improves performance
/// </summary>
public class TerminalRenderer
{
    private readonly Screen screen;
    private TerminalCell[,]? lastBuffer;
    private ConsoleColor lastFg = ConsoleColor.Gray;
    private ConsoleColor lastBg = ConsoleColor.Black;
    private readonly StringBuilder outputBuffer = new(8192);
    private DateTime lastRenderTime = DateTime.MinValue;
    private readonly TimeSpan minRenderInterval = TimeSpan.FromMilliseconds(16); // ~60 FPS max
    private bool pendingUpdate;
    
    public TerminalRenderer(Screen screen)
    {
        this.screen = screen;
    }
    
    /// <summary>
    /// Request a screen update (batches rapid updates)
    /// </summary>
    public void RequestUpdate()
    {
        pendingUpdate = true;
        
        // Check if enough time has passed since last render
        var now = DateTime.UtcNow;
        if (now - lastRenderTime >= minRenderInterval)
        {
            Render();
        }
        else
        {
            // Schedule a render after the minimum interval
            Task.Run(async () =>
            {
                await Task.Delay(minRenderInterval - (now - lastRenderTime));
                if (pendingUpdate)
                {
                    Render();
                }
            });
        }
    }
    
    /// <summary>
    /// Force immediate render (for initial display)
    /// </summary>
    public void RenderImmediate()
    {
        Render();
    }
    
    private void Render()
    {
        lock (outputBuffer)
        {
            if (!pendingUpdate && lastBuffer != null)
                return;
                
            pendingUpdate = false;
            lastRenderTime = DateTime.UtcNow;
            
            var buffer = screen.GetScreen();
            outputBuffer.Clear();
            
            // First time render or full refresh needed
            if (lastBuffer == null)
            {
                Console.Clear();
                RenderFullScreen(buffer);
                lastBuffer = (TerminalCell[,])buffer.Clone();
                return;
            }
            
            // Differential rendering - only update changed cells
            bool cursorMoved = false;
            
            for (int y = 0; y < screen.Height; y++)
            {
                bool lineChanged = false;
                int firstChange = -1;
                int lastChange = -1;
                
                // Find the range of changes in this line
                for (int x = 0; x < screen.Width; x++)
                {
                    if (!CellEquals(buffer[y, x], lastBuffer[y, x]))
                    {
                        if (firstChange == -1)
                            firstChange = x;
                        lastChange = x;
                        lineChanged = true;
                    }
                }
                
                if (lineChanged)
                {
                    // Move cursor to start of changed region
                    Console.SetCursorPosition(firstChange, y);
                    cursorMoved = true;
                    
                    // Render only the changed portion
                    for (int x = firstChange; x <= lastChange; x++)
                    {
                        RenderCell(buffer[y, x]);
                        lastBuffer[y, x] = buffer[y, x];
                    }
                }
            }
            
            // Update cursor position if needed
            Console.SetCursorPosition(screen.CursorX, screen.CursorY);
            
            // Flush any buffered output
            if (outputBuffer.Length > 0)
            {
                Console.Write(outputBuffer.ToString());
                outputBuffer.Clear();
            }
            
            Console.ResetColor();
        }
    }
    
    private void RenderFullScreen(TerminalCell[,] buffer)
    {
        for (int y = 0; y < screen.Height; y++)
        {
            Console.SetCursorPosition(0, y);
            for (int x = 0; x < screen.Width; x++)
            {
                RenderCell(buffer[y, x]);
            }
        }
        
        Console.SetCursorPosition(screen.CursorX, screen.CursorY);
        Console.ResetColor();
    }
    
    private void RenderCell(TerminalCell cell)
    {
        var fg = TerminalCell.AnsiToConsoleColor(cell.ForegroundColor);
        var bg = TerminalCell.AnsiToConsoleColor(cell.BackgroundColor);
        
        if (cell.Inverse)
        {
            (fg, bg) = (bg, fg);
        }
        
        // Apply bold (make it bright)
        if (cell.Bold && (int)fg < 8)
        {
            fg = (ConsoleColor)((int)fg + 8);
        }
        
        // Only change colors if different
        if (fg != lastFg)
        {
            Console.ForegroundColor = fg;
            lastFg = fg;
        }
        
        if (bg != lastBg)
        {
            Console.BackgroundColor = bg;
            lastBg = bg;
        }
        
        Console.Write(cell.Char);
    }
    
    private bool CellEquals(TerminalCell a, TerminalCell b)
    {
        return a.Char == b.Char &&
               a.ForegroundColor == b.ForegroundColor &&
               a.BackgroundColor == b.BackgroundColor &&
               a.Bold == b.Bold &&
               a.Inverse == b.Inverse &&
               a.Underline == b.Underline;
    }
}