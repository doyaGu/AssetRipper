# Schema Reference Directory

This directory contains detailed field-level reference documentation for all AssetRipper AssetDump v2 schemas.

## Documentation Structure

- **Main Reference:** `../COMPLETE_SCHEMA_REFERENCE.md` - Complete overview of all 23 schemas
- **Individual References:** Detailed per-schema documentation files

## Reference Files

### Core Types
- [core.md](core.md) - Shared type definitions (CollectionID, AssetPK, UnityGuid, etc.)

### Facts Layer (10 schemas)
- [assets.md](assets.md) - Individual Unity object metadata ✓
- [bundles.md](bundles.md) - Bundle hierarchy and metadata ✓
- [collections.md](collections.md) - AssetCollection (SerializedFile) metadata ✓
- [scenes.md](scenes.md) - Scene aggregations and statistics ✓
- [script_metadata.md](script_metadata.md) - MonoScript metadata
- [script_sources.md](script_sources.md) - Decompiled source file links
- [types.md](types.md) - Type dictionary (classKey → ClassID/ClassName)
- [type_definitions.md](type_definitions.md) - .NET type definitions from assemblies
- [type_members.md](type_members.md) - Type member details (fields, properties, methods)
- [assemblies.md](assemblies.md) - Assembly metadata and DLL information

### Relations Layer (6 schemas)
- [asset_dependencies.md](asset_dependencies.md) - Asset-to-asset PPtr references
- [collection_dependencies.md](collection_dependencies.md) - Collection-level dependencies
- [bundle_hierarchy.md](bundle_hierarchy.md) - Bundle parent-child relationships
- [assembly_dependencies.md](assembly_dependencies.md) - Assembly dependency graph
- [script_type_mapping.md](script_type_mapping.md) - MonoScript to TypeDefinition mapping
- [type_inheritance.md](type_inheritance.md) - Type inheritance relationships

### Indexes Layer (2 schemas)
- [by_class.md](by_class.md) - Assets grouped by type for fast queries
- [by_collection.md](by_collection.md) - Collection summaries with type distribution

### Metrics Layer (3 schemas)
- [scene_stats.md](scene_stats.md) - Scene complexity metrics
- [asset_distribution.md](asset_distribution.md) - Asset type and size distribution
- [dependency_stats.md](dependency_stats.md) - Dependency graph analytics

## Quick Links

### For Developers
- **Getting Started:** `../COMPLETE_SCHEMA_REFERENCE.md`
- **Core Types:** [core.md](core.md)
- **Asset Data:** [assets.md](assets.md)
- **Dependencies:** [asset_dependencies.md](asset_dependencies.md)

### For Data Analysis
- **Scene Analysis:** [scenes.md](scenes.md) + [scene_stats.md](scene_stats.md)
- **Asset Distribution:** [asset_distribution.md](asset_distribution.md)
- **Dependency Analysis:** [dependency_stats.md](dependency_stats.md)

### For Script Analysis
- **Script Metadata:** [script_metadata.md](script_metadata.md)
- **Type Information:** [types.md](types.md) + [type_definitions.md](type_definitions.md)
- **Assembly Info:** [assemblies.md](assemblies.md)

## Documentation Status

✓ = Complete detailed reference
○ = Pending detailed reference (see main COMPLETE_SCHEMA_REFERENCE.md for full documentation)

**Status:** 5/24 detailed references complete

The main reference document (COMPLETE_SCHEMA_REFERENCE.md) contains comprehensive documentation for all schemas and is fully usable for implementation.
