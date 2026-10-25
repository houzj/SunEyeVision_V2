# SunEyeVision Plugin.Contracts — foundation 5.0

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
No old solution conversion or legacy plugin binary compatibility is provided. All participants require contract 5.0.

S1 version 5 introduces explicit pixel layout/stride, frozen physical frame and effective Domain,
Region read leases, and neutral pose/boundary declarations. Existing resource adapters must migrate;
there is no dimension-derived Domain fallback. Component role validation is a neutral candidate
consumer; production discovery, registration, optional-editor loading and deployment are not yet connected.
Disk document format 2 (controlled read of 1), binding-envelope migration, and plugin ABI are separate policies.
An explicit old binding mapping does not allow an old plugin binary to load.
