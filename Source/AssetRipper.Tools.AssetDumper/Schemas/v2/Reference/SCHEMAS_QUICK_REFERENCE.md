# AssetDump v2 Schemas - Quick Field Reference

This document provides a condensed field reference for all AssetDump v2 schemas. For complete documentation with examples and detailed descriptions, see `../COMPLETE_SCHEMA_REFERENCE.md` or individual schema reference files.

---

## Facts Layer

### script_metadata.schema.json
**Domain:** `script_metadata` | **PK:** `pk` (StableKey)

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "script_metadata" | ✓ | Domain identifier |
| pk | StableKey | ✓ | <collectionId>:<pathId> format |
| collectionId | CollectionID | ✓ | Collection containing MonoScript |
| pathId | integer | ✓ | Unity m_PathID for MonoScript |
| classId | integer | ✓ | Unity ClassID (typically 115) |
| className | string | ✓ | Short class name |
| fullName | string | ✓ | Fully qualified type name |
| assemblyName | string | ✓ | Assembly name (fixed via FixAssemblyName) |
| isPresent | boolean | ✓ | Script type found in assemblies |
| namespace | string | ✗ | Namespace |
| assemblyNameRaw | string | ✗ | Original assembly name |
| isGeneric | boolean | ✗ | Generic type definition |
| genericParameterCount | integer | ✗ | Number of generic parameters |
| executionOrder | integer | ✗ | Script execution order |
| scriptGuid | UnityGuid | ✗ | Script GUID |
| assemblyGuid | UnityGuid | ✗ | Assembly GUID |
| scriptFileId | integer | ✗ | MonoScript file identifier |
| propertiesHash | string | ✗ | m_PropertiesHash (8 or 32 hex) |
| scene | object | ✗ | Scene provenance {name, path, guid} |

---

### script_sources.schema.json
**Domain:** `script_sources` | **PK:** `pk` (UnityGuid)

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "script_sources" | ✓ | Domain identifier |
| pk | UnityGuid | ✓ | Script GUID from ScriptHashing |
| scriptPk | StableKey | ✓ | MonoScript PK reference |
| assemblyGuid | string | ✓ | Assembly GUID (32 hex) |
| sourcePath | string | ✓ | Relative path to decompiled file |
| sourceSize | integer | ✓ | File size in bytes |
| lineCount | integer | ✓ | Number of lines |
| sha256 | string | ✓ | SHA256 hash (64 hex) |
| language | enum | ✓ | CSharp/UnityShader/HLSL/UnityScript |
| decompiler | string | ✓ | Decompiler name (typically "ILSpy") |
| decompilationStatus | enum | ✓ | success/failed/empty/skipped |
| characterCount | integer | ✗ | Total characters |
| decompilerVersion | string | ✗ | Decompiler version |
| isEmpty | boolean | ✗ | EmptyScript placeholder |
| errorMessage | string | ✗ | Error if decompilation failed |
| isPresent | boolean | ✗ | Script type exists |
| isGeneric | boolean | ✗ | Generic type |
| hasAst | boolean | ✗ | AST file exists (future) |
| astPath | string | ✗ | Path to AST JSON (future) |

---

### types.schema.json
**Domain:** `types` | **PK:** `classKey`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "types" | ✓ | Domain identifier |
| classKey | integer | ✓ | Stable identifier (assigned by exporter) |
| classId | integer | ✓ | Unity ClassID (114=MonoBehaviour) |
| className | string | ✓ | Unity type name |
| typeId | integer | ✗ | SerializedType.TypeID |
| serializedTypeIndex | integer | ✗ | Index in SerializedFile.Types (-1 if N/A) |
| scriptTypeIndex | integer | ✗ | Script type index (-1 if N/A) |
| isStripped | boolean | ✗ | Type definition stripped |
| originalClassName | string | ✗ | Original Unity name |
| baseClassName | string | ✗ | Base class name |
| isAbstract | boolean | ✗ | Abstract class |
| isEditorOnly | boolean | ✗ | Editor-only class |
| isReleaseOnly | boolean | ✗ | Release-only class |
| monoScript | object | ✗ | MonoScript info (for ClassID 114) |
| notes | string | ✗ | Additional notes |

**monoScript Object (ClassID 114 only):**
- assemblyName: string
- namespace: string
- className: string
- scriptGuid: UnityGuid

---

### type_definitions.schema.json
**Domain:** `type_definitions` | **PK:** `pk` (composite)

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "type_definitions" | ✓ | Domain identifier |
| pk | string | ✓ | ASSEMBLY::NAMESPACE::TYPENAME |
| assemblyGuid | string | ✓ | Assembly GUID (16-char hex) |
| assemblyName | string | ✓ | Assembly name |
| typeName | string | ✓ | Simple type name |
| fullName | string | ✓ | Fully qualified type name |
| isClass | boolean | ✓ | Type is a class |
| isStruct | boolean | ✓ | Type is a struct |
| isInterface | boolean | ✓ | Type is an interface |
| isEnum | boolean | ✓ | Type is an enum |
| isAbstract | boolean | ✓ | Abstract type |
| isSealed | boolean | ✓ | Sealed type |
| isGeneric | boolean | ✓ | Generic type |
| visibility | enum | ✓ | public/internal/private/protected/etc. |
| namespace | string | ✗ | Type namespace (empty for global) |
| genericParameterCount | integer | ✗ | Number of generic parameters |
| baseType | string | ✗ | Fully qualified base type name |
| isNested | boolean | ✗ | Nested type |
| declaringType | string | ✗ | Declaring type for nested types |
| interfaces | array | ✗ | Implemented interface names |
| fieldCount | integer | ✗ | Number of fields |
| methodCount | integer | ✗ | Number of methods |
| propertyCount | integer | ✗ | Number of properties |
| isMonoBehaviour | boolean | ✗ | Derives from MonoBehaviour |
| isScriptableObject | boolean | ✗ | Derives from ScriptableObject |
| isSerializable | boolean | ✗ | Serializable by Unity |
| scriptRef | object | ✗ | Associated MonoScript reference |

---

### type_members.schema.json
**Domain:** `type_members` | **PK:** `pk` (composite)

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "type_members" | ✓ | Domain identifier |
| pk | string | ✓ | ASSEMBLY::NAMESPACE::TYPENAME::MEMBERNAME |
| assemblyGuid | string | ✓ | Assembly GUID (16-char hex) |
| typeFullName | string | ✓ | Owner type full name |
| memberName | string | ✓ | Member name |
| memberKind | enum | ✓ | field/property/method/event/constructor/nestedType |
| memberType | string | ✓ | Member type (field/return type) |
| visibility | enum | ✓ | public/private/protected/internal/etc. |
| isStatic | boolean | ✓ | Static member |
| serialized | boolean | ✓ | Unity serializes this member |
| isVirtual | boolean | ✗ | Virtual (methods/properties) |
| isOverride | boolean | ✗ | Overrides base member |
| isSealed | boolean | ✗ | Sealed (prevents override) |
| attributes | array | ✗ | Applied C# attributes |
| documentationString | string | ✗ | XML documentation |
| obsoleteMessage | string | ✗ | Obsolete attribute message |
| nativeName | string | ✗ | Unity native name |
| isCompilerGenerated | boolean | ✗ | Compiler-generated |
| hasGetter | boolean | ✗ | Property has getter |
| hasSetter | boolean | ✗ | Property has setter |
| hasParameters | boolean | ✗ | Property has parameters (indexer) |
| isConst | boolean | ✗ | Field is const |
| isReadOnly | boolean | ✗ | Field is readonly |
| constantValue | any | ✗ | Constant value for const fields |
| parameterCount | integer | ✗ | Number of method parameters |
| parameters | array | ✗ | Method parameter details |
| serializeField | boolean | ✗ | Has [SerializeField] attribute |
| hideInInspector | boolean | ✗ | Has [HideInInspector] attribute |
| isAbstract | boolean | ✗ | Abstract (methods/properties) |
| isGeneric | boolean | ✗ | Generic method/type |
| genericParameterCount | integer | ✗ | Number of generic parameters |

---

### assemblies.schema.json
**Domain:** `assemblies` | **PK:** `pk` (32-char hex)

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "assemblies" | ✓ | Domain identifier |
| pk | string | ✓ | Assembly GUID (32-char uppercase hex) |
| name | string | ✓ | Assembly simple name |
| fullName | string | ✓ | Fully qualified name with version/culture/token |
| scriptingBackend | enum | ✓ | Mono/IL2CPP/Unknown |
| typeCount | integer | ✓ | Number of types in assembly |
| scriptCount | integer | ✓ | Number of MonoScripts referencing |
| isDynamic | boolean | ✓ | Dynamically generated assembly |
| isEditor | boolean | ✓ | Editor-only assembly |
| assemblyType | enum | ✓ | Predefined/UnityEngine/UnityExtension/User/System |
| version | string | ✗ | Assembly version (0.0.0.0 format) |
| targetFramework | string | ✗ | Target framework (netstandard2.1) |
| runtime | string | ✗ | Runtime version description |
| dllPath | string | ✗ | Relative path to exported DLL |
| dllSize | integer | ✗ | DLL file size in bytes |
| dllSha256 | string | ✗ | SHA256 hash (64 hex) |
| platform | string | ✗ | Target platform |
| mscorlibVersion | integer | ✗ | Mscorlib version (2 or 4) |
| references | array | ✗ | Referenced assembly names |
| exportType | enum | ✗ | Decompile/Save/Skip |
| isModified | boolean | ✗ | Modified by AssetRipper |

---

## Relations Layer

### asset_dependencies.schema.json
**Domain:** `asset_dependencies`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "asset_dependencies" | ✓ | Domain identifier |
| from | AssetPK | ✓ | Source asset (owns reference) |
| to | AssetPK | ✓ | Target asset (being referenced) |
| edge | object | ✓ | Edge metadata |
| status | enum | ✗ | Resolved/Missing/External/SelfReference/Null/InvalidFileID/TypeMismatch |
| targetType | string | ✗ | Expected target class name |
| notes | string | ✗ | Diagnostic information |

**edge Object:**
- kind: enum (pptr/external/internal/array_element/dictionary_key/dictionary_value) - Required
- field: string (field path) - Required
- fieldType: string (PPtr<T> type)
- fileId: integer (Unity FileID)
- arrayIndex: integer (array index if applicable)
- isNullable: boolean (field can be null)

---

### collection_dependencies.schema.json
**Domain:** `collection_dependencies`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "collection_dependencies" | ✓ | Domain identifier |
| sourceCollection | CollectionID | ✓ | Declaring collection |
| dependencyIndex | integer | ✓ | Index in dependency list (0=self) |
| targetCollection | CollectionID or null | ✓ | Target collection (null if unresolved) |
| resolved | boolean | ✗ | Successfully resolved |
| source | enum | ✗ | serialized/dynamic/builtin |
| fileIdentifier | object | ✗ | Original Unity FileIdentifier |

**fileIdentifier Object:**
- guid: UnityGuid
- type: integer (0-4)
- pathName: string

---

### bundle_hierarchy.schema.json
**Domain:** `bundle_hierarchy`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "bundle_hierarchy" | ✓ | Domain identifier |
| parentPk | string | ✓ | Parent bundle PK (8-char hex) |
| childPk | string | ✓ | Child bundle PK (8-char hex) |
| childIndex | integer | ✓ | Index in parent's child list |
| parentName | string | ✗ | Parent bundle name |
| childName | string | ✗ | Child bundle name |
| childBundleType | enum | ✗ | GameBundle/SerializedBundle/ProcessedBundle/etc. |
| childDepth | integer | ✗ | Depth in hierarchy (root=0) |

---

### assembly_dependencies.schema.json
**Domain:** `assembly_dependencies`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "assembly_dependencies" | ✓ | Domain identifier |
| sourceAssembly | string | ✓ | Source assembly GUID (32-char hex) |
| targetName | string | ✓ | Target assembly name |
| isResolved | boolean | ✓ | Successfully resolved |
| sourceModule | string | ✗ | Module declaring reference |
| targetAssembly | string or null | ✗ | Target assembly GUID (null if unresolved) |
| version | string | ✗ | Required version (Major.Minor.Build.Revision) |
| publicKeyToken | string | ✗ | Public key token (16-char hex) |
| culture | string | ✗ | Culture (neutral, en-US, etc.) |
| dependencyType | enum | ✗ | direct/framework/plugin/unknown |
| isFrameworkAssembly | boolean | ✗ | .NET framework/reference assembly |
| failureReason | string | ✗ | Resolution failure reason |

---

### script_type_mapping.schema.json
**Domain:** `script_type_mapping`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "script_type_mapping" | ✓ | Domain identifier |
| scriptPk | StableKey | ✓ | MonoScript PK |
| scriptGuid | UnityGuid | ✓ | Script GUID |
| typeFullName | string | ✓ | Fully qualified .NET type name |
| assemblyGuid | string | ✓ | Assembly GUID (32-char hex) |
| assemblyName | string | ✓ | Assembly name (fixed) |
| isValid | boolean | ✓ | TypeDefinition successfully resolved |
| namespace | string | ✗ | Type namespace |
| className | string | ✗ | Simple class name |
| failureReason | string | ✗ | Resolution failure reason (if isValid=false) |
| isGeneric | boolean | ✗ | Generic type |
| genericParameterCount | integer | ✗ | Generic parameter count |
| scriptIdentifier | string | ✗ | ScriptIdentifier.UniqueName for debugging |

---

### type_inheritance.schema.json
**Domain:** `type_inheritance`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "type_inheritance" | ✓ | Domain identifier |
| derivedType | string | ✓ | Fully qualified derived type name |
| derivedAssembly | string | ✓ | Assembly containing derived type |
| baseType | string | ✓ | Fully qualified base type name |
| baseAssembly | string | ✓ | Assembly containing base type |
| relationshipType | enum | ✓ | class_inheritance/interface_implementation |
| inheritanceDistance | integer | ✓ | Distance in chain (1=direct) |
| inheritanceDepth | integer | ✗ | Depth from root (0=root) |
| baseTypeArguments | array | ✗ | Type arguments if base is generic |
| descendantCount | integer | ✗ | Total descendants (including self) |

---

## Indexes Layer

### by_class.schema.json
**Domain:** `by_class`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "by_class" | ✓ | Domain identifier |
| classKey | integer | ✓ | Dense class key (min: 1) |
| assets | array | ✓ | Array of AssetPK objects |
| count | integer | ✓ | Number of assets (must equal assets.length) |
| className | string | ✗ | Unity type name (human-readable) |
| classId | integer | ✗ | Unity ClassID (min: -2) |

---

### by_collection.schema.json
**Domain:** `by_collection` (Array of objects)

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "by_collection" | ✓ | Domain identifier |
| collectionId | CollectionID | ✓ | Collection identifier |
| count | integer | ✓ | Total assets in collection |
| name | string | ✗ | Collection name |
| isScene | boolean | ✗ | Scene collection |
| bundleName | string | ✗ | Parent bundle name |
| typeDistribution | array | ✗ | Top 10 types by count |
| totalTypeCount | integer | ✗ | Total distinct types |

**typeDistribution Item:**
- classKey: integer (required)
- className: string
- classId: integer
- count: integer (required)

---

## Metrics Layer

### scene_stats.schema.json
**Domain:** `scene_stats`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "scene_stats" | ✓ | Domain identifier |
| sceneGuid | UnityGuid | ✓ | Scene GUID (primary key) |
| sceneName | string | ✓ | Scene name |
| counts | object | ✓ | Count object (all fields required) |
| scenePath | string | ✗ | Project-relative path |
| hierarchyAssetPk | AssetPK | ✗ | SceneHierarchyObject reference |
| hasSceneRoots | boolean | ✗ | Has SceneRoots asset |
| notes | string | ✗ | Optional notes |

**counts Object:**
- gameObjects: integer (required)
- components: integer (required)
- prefabInstances: integer (required)
- managers: integer (required)
- rootGameObjects: integer (required)
- strippedAssets: integer
- hiddenAssets: integer
- collections: integer

---

### asset_distribution.schema.json
**Domain:** `asset_distribution`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "asset_distribution" | ✓ | Domain identifier |
| summary | object | ✓ | Global summary statistics |
| byClass | array | ✓ | Per-class distribution (sorted by count desc) |
| byBundle | array | ✓ | Per-bundle distribution (sorted by totalAssets desc) |

**summary Object:**
- totalAssets: integer (required)
- totalBytes: integer (required)
- uniqueClasses: integer (required)
- totalCollections: integer (required)
- totalBundles: integer (required)
- assetsWithByteSize: integer (required)

**byClass Item:**
- classKey: integer (required)
- classId: integer (required)
- className: string (required)
- count: integer (required)
- countWithByteSize: integer (required)
- totalBytes, averageBytes, minBytes, maxBytes, medianBytes: integer

**byBundle Item:**
- bundleName: string (required)
- collections: integer (required)
- totalAssets: integer (required)
- assetsWithByteSize: integer (required)
- uniqueClasses: integer (required)
- totalBytes, averageBytes: integer
- byClass: array (top 20 per-class stats in bundle)

---

### dependency_stats.schema.json
**Domain:** `dependency_stats`

| Field | Type | Req | Description |
|-------|------|-----|-------------|
| domain | const: "dependency_stats" | ✓ | Domain identifier |
| edges | object | ✓ | Edge-level statistics |
| degree | object | ✓ | Degree distribution (outgoing/incoming) |
| health | object | ✓ | Dependency health indicators |
| byType | array | ✗ | Per-type dependency statistics (top 30) |

**edges Object:**
- total: integer (required)
- averagePerAsset: number (required)
- internalReferences: integer (required)
- externalReferences: integer (required)
- crossBundleReferences: integer (required)
- nullReferences, unresolvedReferences: integer

**degree Object:**
- outgoing: {average, min, max, median} (all required)
- incoming: {average, min, max, median} (all required)

**health Object:**
- totalAssets: integer (required)
- noOutgoingRefs: integer (required)
- noIncomingRefs: integer (required)
- completelyIsolated: integer (required)

**byType Item:**
- classId: integer (required)
- className: string (required)
- count: integer (required)
- averageOutDegree: number (required)
- averageInDegree: number (required)
- maxOutDegree, maxInDegree: integer

---

**End of Quick Reference**
