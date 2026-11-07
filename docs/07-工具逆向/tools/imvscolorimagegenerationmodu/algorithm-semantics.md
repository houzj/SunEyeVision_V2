# 彩图生成 VM algorithm semantics

- stableToolId: imvscolorimagegenerationmodu
- status: inventory-only; source-confirmed schema only

## Confirmed schema evidence
- Inputs: InputImage, InImage, InImageWidth, InImageHeight, InImagePixelFormat, InputImage1, InImage1, InImageWidth1, InImageHeight1, InImagePixelFormat1, InputImage2, InImage2, InImageWidth2, InImageHeight2, InImagePixelFormat2
- Outputs: ModuRunTime, ModuStatus, OutputImage, OutImage, OutImageWidth, OutImageHeight, OutImagePixelFormat
- Parameters: 
- UI controls: ImageSource, ImageSource1, ImageSource2, ImageFormat
- XML/PE/resource hashes: resources.json

## Execution and lifecycle (unknown)

| Stage | Conclusion |
|---|---|
| Process/ProcessAlg | Tool-specific body, ABI and return codes not recovered. |
| Result publication | Output names are XML declarations; writer order, null/empty behavior and verdict unknown. |
| Display | Display XML/resources indexed; runtime producer/template behavior unknown. |
| Save/restore | Serialization keys, migration and rebinding unknown. |
| Reader/release | Reader leases, temporary ownership, DeInit/Destroy order unknown. |

No algorithm behavior is inferred from field names or intended product semantics.
