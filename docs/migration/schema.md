# Migration Schema

The `WrapGod.Migration` project provides the schema model for describing migration rules between library versions. A **migration schema** is a machine-readable document (JSON) that specifies how user code should be updated when upgrading from one library version to another.

## Schema Format

```json
{
  "schema": "wrapgod-migration/1.0",
  "library": "MudBlazor",
  "from": "6.0.0",
  "to": "7.0.0",
  "generatedFrom": "manifest-diff",
  "lastEdited": "2026-04-01T00:00:00Z",
  "rules": [...]
}
```

| Field | Required | Description |
|---|---|---|
| `schema` | ✓ | Schema version identifier — always `wrapgod-migration/1.0`. |
| `library` | ✓ | NuGet package or library name. |
| `from` | ✓ | Source (old) library version. |
| `to` | ✓ | Target (new) library version. |
| `generatedFrom` | | How the schema was produced: `manifest-diff` or `manual`. |
| `lastEdited` | | ISO-8601 timestamp of the last manual edit. |
| `rules` | ✓ | Array of migration rule objects. |

## Rule Kinds

Each rule has an `id`, a `kind` discriminator, and optional `confidence` / `note` fields. The `kind` determines the shape of the remaining properties.

### `renameType`
A type was renamed (namespace unchanged).

```json
{
  "id": "rename-Button",
  "kind": "renameType",
  "confidence": "auto",
  "oldName": "MudBlazor.Button",
  "newName": "MudBlazor.MudButton"
}
```

### `renameMember`
A method, property, field, or event was renamed on its declaring type.

```json
{
  "id": "rename-Color-prop",
  "kind": "renameMember",
  "confidence": "verified",
  "typeName": "MudBlazor.MudButton",
  "oldMemberName": "Color",
  "newMemberName": "ButtonColor"
}
```

### `renameNamespace`
A type was moved to a different namespace.

```json
{
  "id": "rename-ns-components",
  "kind": "renameNamespace",
  "confidence": "auto",
  "oldNamespace": "MudBlazor.Components",
  "newNamespace": "MudBlazor"
}
```

### `changeParameter`
A method parameter was renamed, retyped, or had its default value changed.

```json
{
  "id": "change-param-size",
  "kind": "changeParameter",
  "confidence": "manual",
  "typeName": "MudBlazor.MudButton",
  "methodName": "SetSize",
  "oldParameterName": "size",
  "newParameterName": "buttonSize",
  "oldParameterType": "int",
  "newParameterType": "MudBlazor.Size"
}
```

### `removeMember`
A member was removed with no direct replacement.

```json
{
  "id": "remove-legacy-ctor",
  "kind": "removeMember",
  "confidence": "manual",
  "note": "Use the primary constructor instead.",
  "typeName": "MudBlazor.MudButton",
  "memberName": ".ctor"
}
```

### `addRequiredParameter`
A required parameter was added to an existing method.

```json
{
  "id": "add-required-theme",
  "kind": "addRequiredParameter",
  "confidence": "manual",
  "typeName": "MudBlazor.MudThemeProvider",
  "methodName": "Apply",
  "parameterName": "theme",
  "parameterType": "MudBlazor.MudTheme",
  "position": 0
}
```

### `changeTypeReference`
A type reference was changed across the API.

```json
{
  "id": "change-ilist-to-ireadonly",
  "kind": "changeTypeReference",
  "confidence": "auto",
  "oldType": "System.Collections.Generic.IList`1",
  "newType": "System.Collections.Generic.IReadOnlyList`1"
}
```

### `splitMethod`
A single method was split into multiple methods.

```json
{
  "id": "split-render",
  "kind": "splitMethod",
  "confidence": "manual",
  "typeName": "MudBlazor.MudCard",
  "oldMethodName": "Render",
  "newMethodNames": ["RenderHeader", "RenderBody", "RenderFooter"]
}
```

### `extractParameterObject`
Several parameters were extracted into a dedicated parameter-object type.

```json
{
  "id": "extract-dialog-params",
  "kind": "extractParameterObject",
  "confidence": "manual",
  "typeName": "MudBlazor.MudDialog",
  "methodName": "ShowAsync",
  "parameterObjectType": "MudBlazor.DialogParameters",
  "extractedParameters": ["title", "content"]
}
```

### `propertyToMethod`
A property was converted to a method (or vice-versa).

```json
{
  "id": "prop-to-method-disabled",
  "kind": "propertyToMethod",
  "confidence": "auto",
  "typeName": "MudBlazor.MudButton",
  "oldPropertyName": "Disabled",
  "newMethodName": "SetDisabled"
}
```

### `moveMember`
A member was moved from one type to another.

```json
{
  "id": "move-get-color-helper",
  "kind": "moveMember",
  "confidence": "verified",
  "oldTypeName": "MudBlazor.Utilities",
  "newTypeName": "MudBlazor.MudHelpers",
  "memberName": "GetColor"
}
```

## Rule Confidence

| Value | Meaning |
|---|---|
| `auto` | The fix can be applied fully automatically without human review. |
| `verified` | The fix has been manually reviewed and confirmed correct. |
| `manual` | The fix requires a human to apply it; automated application is not safe. |

## Serialization

Use `MigrationSchemaSerializer` to read and write migration schemas:

```csharp
using WrapGod.Migration;

// Serialize to JSON
var schema = new MigrationSchema
{
    Library = "MudBlazor",
    From = "6.0.0",
    To = "7.0.0",
    Rules = [new RenameTypeRule { Id = "r1", OldName = "Button", NewName = "MudButton" }],
};
string json = MigrationSchemaSerializer.Serialize(schema);

// Deserialize from JSON
MigrationSchema? loaded = MigrationSchemaSerializer.Deserialize(json);
```

The serializer uses camelCase property names, indented output, and supports `//` line comments in JSON input.

## JSON Schema Validation

The JSON Schema file is located at [`schemas/wrapgod-migration.v1.schema.json`](../../../schemas/wrapgod-migration.v1.schema.json).
