# PLUGIN-FOUNDATION-01

First-batch contract implementation and independent verification samples. Not registered in the product toolbox.
No old solution conversion or plugin binary compatibility. Contract 3.0 rejects missing/older metadata.

## Projects
- `src/Plugin.Contracts`: UI-free net9.0 contracts, typed schema, frozen built-in values, edit session.
- `src/Plugin.Wpf`: minimal real WPF editor consumer; not the production window replacement.
- `src/Tool.Kernels`: point-distance kernel shared by existing production helper and new sample.
- `Samples`: point calculation, controlled waiting, script schema, model-lifetime substitute.
- `Host`: independent invocation/resource verification host, no workflow scheduler or production registration.
- `HalconSample`: real full-domain Gray8 Binary slice, links existing threshold/filter source files rather than duplicating algorithms.
- `Verification`: 27 local contract scenarios, including real HALCON pixel golden and STA WPF control events.

## Run
`pwsh -NoProfile -File samples/PluginFoundation/Verify.ps1`
Requires .NET9 runtime/targeting packs, .NET SDK with slnx support, Windows WPF for verification, and the pinned HALCON runtime/license for the real image slice.
`-NativeDirectory` may point to an independent read-only copy. No physical devices, production data or VM process is accessed.
Builds use private output under this repository's artifacts/validation/plugin-foundation-01/bin, not shared Release or tests/bin.
An independent tester must copy the delivered source tree/output and keep its own report/data/output roots; do not rebuild a writer's shared tree.

## Limits
- Foundation types cover bool/long/finite-double/string/copied-double-array/Gray8 image only; geometry, ROI and zero-copy native models are not a finished SDK.
- Dynamic script sample verifies declared outputs and rebinding, not script execution/sandbox security.
- Device wait/model samples are controlled substitutes, not real hardware or all native model lifetimes.
- The host validates single-session exclusion/shared resource keys/stop drain, not full workflow Branch/Join/Loop/Call scheduling.
- A06 generation gate is an independent example, not evidence that the product UI result pipeline has switched.
- A09 uses actual WPF control events; Visual Studio designer and interactive visual acceptance remain separate.
- Complete plugin load-context private-library conflict coverage and production SDK/tool cutover are later work, not claimed by these checks.

The source snapshot and binary manifests bind the handoff. Local passing tests do not substitute for independent acceptance.
