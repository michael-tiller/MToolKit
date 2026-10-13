# Graph variables text schema

The common variable literal implementation is `Variables/GraphVariableTextSyntax.cs`.
Dirigible event and quest parsers use it directly.

```text
[variables]
caption: string = "Hello\n\"traveler\""
count: int = 2
multiplier: float = 1.25
enabled: bool = true
position: vector2 = (1, 2)
offset: vector3 = (1, 2, 3)
tint: color = #FF8040FF
```

Keys are case-sensitive, unique, nonempty, and contain no whitespace, `:` or `=`.
Type names are case-insensitive on import and lowercase on export. Strings are quoted;
supported escapes are `\"`, `\\`, `\n`, `\r`, `\t`, `\b`, `\f`, and `\uXXXX`.
Integers are signed 32-bit values. Floats and vector components use invariant culture
and must be finite. Vector parentheses and the exact number of components are required.
Colors require eight hex digits: RGBA, with 8-bit precision per channel. Export converts
Unity colors to that precision. Other numeric defaults retain float round-trip precision.

Import accepts any declaration order. Export sorts keys using ordinal comparison.
The stability contract is `export(import(text)) == canonicalize(text)` and
`import(export(asset)) == asset declarations`, modulo color's documented 8-bit precision.
Malformed declarations and duplicate keys report `file(line):` errors and abort parsing
before the importer mutates assets. A present empty block owns an empty set; removing
the block removes the owned set.

Event graphs own one `DeclaredVariables` sub-asset. Quest-level graphs own one set
shared by every objective graph. Reimport updates that same set in place, preserving
its asset identity; objective graphs do not own duplicate copies. Text declarations
win over divergent asset declarations. Runtime values and saved state remain separate
from authoring defaults.

## Asset-canonical event graphs

Event text represents entry nodes and their supported linear action chains. A scan of
every node and connection rejects lossy text representation: logic nodes, orphan nodes,
shared chains, branches, cycles, and unsupported nodes use this subset instead:

```text
[events]
id: existing_graph
name: Existing Graph
asset_canonical: true

[variables]
count: int = 2
```

The real `asset_canonical` field permits a variables-only import over an existing event
definition and graph. It preserves node objects, connections, subscriptions, graph
metadata, and event definition metadata. Without that field, import refuses to overwrite
unsupported topology. Export automatically selects this subset when full linear text
would lose graph data. General logic topology text syntax remains deferred.

## Reserved condition literals

Condition/DynamicValue syntax belongs to phase 9.5 and is not implemented by this schema.
