# SunEyeVision Plugin.Contracts — foundation 4.0

This first-batch SDK is UI-free and does not reference the production UI, workflow implementation or native engines.
A tool definition describes stable input/output identities, validates frozen settings and creates a non-reentrant session.
The session has one ExecuteAsync entry and explicit DisposeAsync lifetime. Configuration is separate from runtime inputs.
Images are borrowed read-only inputs; outputs must be created or retained through invocation resources.
TypeRef is the declaration authority: scalar, counted sequence, structural record, geometry and resource shapes.
Values are validated and frozen recursively. Numeric arrays are copied; records and other sequences use immutable
RecordValue/SequenceValue carriers. Required, nullable and missing remain separate.
Seven closed geometric data shapes use explicit coordinate-frame identity, domain and units, X right/Y down,
radians toward positive Y, full rectangle width/height and ellipse radii. Different frames require an explicit
mapping before invocation; matching geometry kind alone does not permit mixing image/model coordinates.
The foundation host tracks image ownership through declared nested fields and sequences.
Model/Contour resource execution, full BindingPlan adaptation and production representative integration remain pending.
This internal foundation is not a complete native image or device SDK, a sandbox or a production workflow replacement.

See samples/PluginFoundation/README.md for four representative samples, the independent harness and verification instructions.
No old solution conversion or legacy plugin binary compatibility is provided. All participants require contract 4.0.
