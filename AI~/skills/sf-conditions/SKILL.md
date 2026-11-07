---
name: sf-conditions
description: Use when adding SFramework Boolean conditions, AllMet/AnyMet checks, or polymorphic condition JSON with $Type.
---

# sf-conditions

Read [the package guide](references/package-readme.md) and [installed source](SOURCE.md).

Implement domain rules through SFCondition.Met or ISFCondition. Import SFramework.Modules.SFConditions.Runtime, despite the assembly being named SFramework.Conditions.Runtime.

SFCondition injects during construction, before JSON fields are populated. Register dependencies first and avoid reading configured fields from constructor-time injected hooks.

Use a case-sensitive concrete short class name in $Type when it uniquely identifies a condition, or the full type plus simple assembly name (such as "MyGame.Rules.HasFuel, MyGame.Runtime") for a collision. Converter-created conditions need a public parameterless constructor. Public writable fields and public read/write properties round-trip; JsonIgnore skips members and JsonProperty can name them.

Null arrays produce true for both AllMet and AnyMet; empty arrays produce true/false respectively. Null elements can throw. Preserve these distinctions when defining defaults, and consider the editorAlwaysTrue override explicitly.

Verify JSON round trips, missing/unknown discriminators, condition composition and player reflection/stripping against the installed implementation.
