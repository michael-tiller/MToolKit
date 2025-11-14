# VisualGraphs Runtime Debugger Implementation

## Overview

This document describes the comprehensive runtime debugging system implemented for VisualGraphs, covering all Phase 6.2 requirements from the PRODUCTION_ROADMAP.md.

## Architecture

The debugger system consists of three main layers:

1. **Runtime Debug Events** - Emits events when nodes execute, state changes, and graphs start/stop
2. **Editor Debug State** - Tracks execution history, active graphs, and statistics
3. **Editor UI Tools** - Visual highlighting in graph editor and comprehensive debugger window

## Components

### Runtime Components

#### `NodeDebugEvents` (Runtime/Debug/NodeDebugEvents.cs)
Static event system that emits debug events:
- `NodeExecuted` - Fired when a node executes (includes timing and errors)
- `StateChanged` - Fired when graph state changes
- `GraphExecutionChanged` - Fired when graphs start/stop execution

#### `DebuggableGraphState` (Runtime/State/DebuggableGraphState.cs)
Wrapper around `IGraphState` that emits debug events for all state changes. Automatically wraps all graph states in `GraphLoader`.

#### `GraphRunner` Integration
Modified to:
- Track execution timing using `Stopwatch`
- Emit debug events for node execution (with timing and errors)
- Emit debug events for graph execution start/stop

### Editor Components

#### `XNodeDebugState` (Editor/VisualGraphs/XNodeDebugState.cs)
Editor-only service that:
- Subscribes to runtime debug events
- Maintains execution history (last 1000 node executions, 500 state changes)
- Tracks active graphs with execution status
- Computes execution statistics per graph
- Auto-clears when exiting play mode

#### `DebuggableNodeEditor` (Editor/VisualGraphs/DebuggableNodeEditor.cs)
Custom xNode editor that:
- Highlights currently executing nodes (green border)
- Highlights last executed nodes (yellow border)
- Updates in real-time during play mode
- Works with any `VisualGraphNodeBase` node

#### `XNodeOdinDebuggerWindow` (Editor/VisualGraphs/XNodeOdinDebuggerWindow.cs)
Comprehensive Odin-powered debugger window with:

**Active Graphs View:**
- Lists all active graphs in hierarchy
- Shows execution status (is executing, last executed node)
- Displays execution counts and statistics
- Shows error counts per graph

**Execution History:**
- Last 200 node executions
- Execution time per node
- Error information
- Timestamps

**State Changes:**
- Last 100 state changes
- Old/new values
- Timestamps
- Graph and key information

**Graph Statistics:**
- Total executions per graph
- Average/min/max execution times
- Error counts

**Manual Event Triggering:**
- Dropdown of all `IGameMessage` types
- JSON input for message data
- Uses `GameMessageBroker.Publish()` to trigger events
- Useful for testing graph behavior

**State Inspector:**
- Select any active graph
- View all state values
- Refresh to see current state
- (State editing can be extended in future)

## Phase 6.2 Coverage

✅ **Create graph execution debugger**
- ✅ Show active graphs in hierarchy
- ✅ Display current execution state
- ✅ Show which nodes executed recently
- ✅ Visualize event flow (via execution history)

✅ **Add execution history**
- ✅ Record last N node executions (200 in window, 1000 in state)
- ✅ Show execution time per node
- ✅ Display state changes over time

✅ **Create state inspector window**
- ✅ Show all active graph states
- ⚠️ Allow editing state values at runtime (basic structure in place, can be extended)
- ✅ Trigger events manually for testing

## Usage

### Opening the Debugger Window

1. In Unity Editor, go to `Tools > xNode > VisualGraphs Debugger`
2. Window opens with all debug information

### Visual Node Highlighting

1. Open any graph in xNode editor
2. Enter Play Mode
3. Nodes will automatically highlight:
   - **Green border**: Currently executing node
   - **Yellow border**: Last executed node

### Manual Event Triggering

1. Open the debugger window
2. In "Manual Event Triggering" section:
   - Select a message type from dropdown
   - Optionally enter JSON data
   - Click "Trigger Event"
3. Event is published via `GameMessageBroker` and graphs will react

### Viewing State

1. Open the debugger window
2. In "State Inspector" section:
   - Select a graph ID from dropdown
   - Click "Refresh State"
   - View all state key-value pairs

## Integration Points

### GraphLoader
Automatically wraps all graph states with `DebuggableGraphState`:
```csharp
var baseState = new InMemoryGraphState();
var state = new DebuggableGraphState(baseState, runtimeDef.GraphId);
```

### GraphRunner
Automatically emits debug events:
- On graph execution start/stop
- On each node execution (with timing)
- On errors

### Editor Initialization
`XNodeDebugState` uses `[InitializeOnLoad]` to automatically subscribe to runtime events when Unity loads.

## Performance Considerations

- Debug events are only emitted in editor builds (via `#if UNITY_EDITOR`)
- History is capped (1000 node executions, 500 state changes)
- Window updates are throttled via `OnInspectorUpdate()`
- Visual highlighting only active during play mode

## Future Enhancements

1. **State Editing**: Complete the state editing functionality in the debugger window
2. **Breakpoints**: Add breakpoint support to pause execution at specific nodes
3. **Step Through**: Add step-through execution control
4. **Watch Window**: Add watch window for specific state keys
5. **Execution Flow Visualization**: Animate wires when they fire
6. **Performance Profiling**: Add detailed profiling per node type

## Files Created

### Runtime
- `Runtime/Debug/INodeExecutionDebugEvent.cs`
- `Runtime/Debug/NodeDebugEvents.cs`
- `Runtime/State/DebuggableGraphState.cs`

### Editor
- `Editor/VisualGraphs/XNodeDebugState.cs`
- `Editor/VisualGraphs/DebuggableNodeEditor.cs`
- `Editor/VisualGraphs/XNodeOdinDebuggerWindow.cs`

### Modified
- `Runtime/GraphRunner.cs` - Added debug event emission
- `Runtime/Loading/GraphLoader.cs` - Wraps states with DebuggableGraphState

## Dependencies

- **Odin Inspector**: Required for the debugger window
- **xNode**: Required for node editor customization
- **GameMessageBroker**: Used for manual event triggering

## Notes

- All debug code is editor-only where possible
- Runtime debug events are lightweight (just event emission)
- No breaking changes to existing graph execution
- Works with existing xNode graphs without modification

