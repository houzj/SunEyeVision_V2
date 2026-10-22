# SunEyeVision Plugin.Contracts — foundation 3.0

This first-batch SDK is UI-free and does not reference the production UI, workflow implementation or native engines.
A tool definition describes stable input/output identities, validates frozen settings and creates a non-reentrant session.
The session has one ExecuteAsync entry and explicit DisposeAsync lifetime. Configuration is separate from runtime inputs.
Images are borrowed read-only inputs; outputs must be created or retained through invocation resources.
Supported foundation values: bool, long, finite double, string, copied double[] and registered Gray8 image views.
This is not a complete geometry/ROI/native image or device SDK, a sandbox or a production workflow replacement.

See samples/PluginFoundation/README.md for four representative samples, the independent harness and verification instructions.
No old solution conversion or legacy plugin binary compatibility is provided. All participants require contract 3.0.
